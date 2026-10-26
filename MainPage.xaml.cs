using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using WinSnake.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinSnake;

/// <summary>
/// The game screen. Rendering is done with one rectangle per board cell, which
/// keeps the visual tree small and lets a tick only repaint cells that changed.
/// </summary>
public sealed partial class MainPage : Page
{
    private const int CellSize = 32;
    private const int CellInset = 1;
    private const int Columns = SnakeGame.DefaultColumns;
    private const int Rows = SnakeGame.DefaultRows;
    private const int InitialTickMs = 160;
    private const int MinimumTickMs = 65;
    private const int TickStepMs = 7;

    private static readonly CellKind Unset = (CellKind)(-1);

    private readonly SnakeGame _game = new();
    private readonly DispatcherTimer _timer = new();
    private readonly Rectangle[,] _cellVisuals = new Rectangle[Columns, Rows];
    private readonly CellKind[,] _renderedKinds = new CellKind[Columns, Rows];

    private readonly Brush _cellDarkBrush;
    private readonly Brush _cellLightBrush;
    private readonly Brush _snakeBodyBrush;
    private readonly Brush _snakeHeadBrush;
    private readonly Brush _foodBrush;

    private int _highScore;
    private bool _isNewRecord;
    private GameStatus _shownOverlayStatus = (GameStatus)(-1);

    public MainPage()
    {
        InitializeComponent();

        _cellDarkBrush = (Brush)Resources["CellDarkBrush"];
        _cellLightBrush = (Brush)Resources["CellLightBrush"];
        _snakeBodyBrush = (Brush)Resources["SnakeBodyBrush"];
        _snakeHeadBrush = (Brush)Resources["SnakeHeadBrush"];
        _foodBrush = (Brush)Resources["FoodBrush"];

        _timer.Interval = TimeSpan.FromMilliseconds(InitialTickMs);
        _timer.Tick += OnGameTick;

        BuildBoard();
        LoadHighScore();
        RenderBoard();
        UpdateHud();
        UpdateOverlay();
    }

    /// <summary>Pauses a running round, e.g. when the window loses focus.</summary>
    public void PauseForFocusLoss()
    {
        if (_game.Status == GameStatus.Running)
        {
            PauseGame();
        }
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        FocusBoard();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private void BuildBoard()
    {
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                var visual = new Rectangle
                {
                    Width = CellSize - (CellInset * 2),
                    Height = CellSize - (CellInset * 2),
                    RadiusX = 6,
                    RadiusY = 6,
                    Fill = (x + y) % 2 == 0 ? _cellDarkBrush : _cellLightBrush,
                };

                Canvas.SetLeft(visual, (x * CellSize) + CellInset);
                Canvas.SetTop(visual, (y * CellSize) + CellInset);
                BoardCanvas.Children.Add(visual);

                _cellVisuals[x, y] = visual;
                _renderedKinds[x, y] = Unset;
            }
        }
    }

    /// <summary>Repaints only the cells whose content changed since the previous frame.</summary>
    private void RenderBoard()
    {
        var body = new HashSet<Cell>(_game.Snake.Skip(1));
        Cell head = _game.Head;
        Cell food = _game.Food;

        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                var cell = new Cell(x, y);
                CellKind kind = cell == head ? CellKind.SnakeHead
                    : body.Contains(cell) ? CellKind.SnakeBody
                    : cell == food ? CellKind.Food
                    : (x + y) % 2 == 0 ? CellKind.BaseDark : CellKind.BaseLight;

                if (_renderedKinds[x, y] == kind)
                {
                    continue;
                }

                Rectangle visual = _cellVisuals[x, y];
                visual.Fill = BrushFor(kind);
                visual.RadiusX = kind switch
                {
                    CellKind.SnakeHead => 11,
                    CellKind.Food => 15,
                    CellKind.SnakeBody => 8,
                    _ => 6,
                };
                visual.RadiusY = visual.RadiusX;
                _renderedKinds[x, y] = kind;
            }
        }
    }

    private Brush BrushFor(CellKind kind) => kind switch
    {
        CellKind.SnakeHead => _snakeHeadBrush,
        CellKind.SnakeBody => _snakeBodyBrush,
        CellKind.Food => _foodBrush,
        CellKind.BaseLight => _cellLightBrush,
        _ => _cellDarkBrush,
    };

    private void OnGameTick(object? sender, object e)
    {
        TickResult result = _game.Tick();

        switch (result)
        {
            case TickResult.HitWall:
            case TickResult.HitSelf:
            case TickResult.BoardCleared:
                _timer.Stop();
                RecordHighScore();
                break;
            case TickResult.AteFood:
                _timer.Interval = TickInterval;
                break;
        }

        RenderBoard();
        UpdateHud();
        UpdateOverlay();
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool handled = true;

        switch (e.Key)
        {
            case VirtualKey.Up:
            case VirtualKey.W:
                _game.TryTurn(Direction.Up);
                break;
            case VirtualKey.Down:
            case VirtualKey.S:
                _game.TryTurn(Direction.Down);
                break;
            case VirtualKey.Left:
            case VirtualKey.A:
                _game.TryTurn(Direction.Left);
                break;
            case VirtualKey.Right:
            case VirtualKey.D:
                _game.TryTurn(Direction.Right);
                break;
            case VirtualKey.Space:
            case VirtualKey.Enter:
                PerformPrimaryAction();
                break;
            case VirtualKey.P:
            case VirtualKey.Escape:
                if (_game.Status == GameStatus.Running)
                {
                    PauseGame();
                }
                else if (_game.Status == GameStatus.Paused)
                {
                    ResumeGame();
                }

                break;
            case VirtualKey.R:
                StartNewGame();
                break;
            default:
                handled = false;
                break;
        }

        e.Handled = handled;
    }

    private void OnDirectionButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse(tag, out Direction direction))
        {
            _game.TryTurn(direction);
        }

        FocusBoard();
    }

    private void OnPrimaryActionClick(object sender, RoutedEventArgs e)
    {
        PerformPrimaryAction();
        FocusBoard();
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        if (_game.Status == GameStatus.Paused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }

        FocusBoard();
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        StartNewGame();
    }

    private void PerformPrimaryAction()
    {
        switch (_game.Status)
        {
            case GameStatus.Ready:
            case GameStatus.GameOver:
                StartNewGame();
                break;
            case GameStatus.Running:
                PauseGame();
                break;
            case GameStatus.Paused:
                ResumeGame();
                break;
        }
    }

    private void StartNewGame()
    {
        _game.Start();
        _isNewRecord = false;
        _timer.Interval = TickInterval;
        _timer.Start();
        RenderBoard();
        UpdateHud();
        UpdateOverlay();
        FocusBoard();
    }

    private void PauseGame()
    {
        _game.Pause();
        _timer.Stop();
        UpdateOverlay();
    }

    private void ResumeGame()
    {
        _game.Resume();
        _timer.Start();
        UpdateOverlay();
        FocusBoard();
    }

    private TimeSpan TickInterval => TimeSpan.FromMilliseconds(
        Math.Max(MinimumTickMs, InitialTickMs - (_game.FoodEaten * TickStepMs)));

    private void FocusBoard()
    {
        RootLayout.Focus(FocusState.Programmatic);
    }

    private void UpdateHud()
    {
        ScoreText.Text = _game.Score.ToString();
        HighScoreText.Text = Math.Max(_highScore, _game.Score).ToString();
        LevelText.Text = _game.Level.ToString();
    }

    private void UpdateOverlay()
    {
        GameStatus status = _game.Status;
        if (_shownOverlayStatus == status)
        {
            return;
        }

        _shownOverlayStatus = status;

        switch (status)
        {
            case GameStatus.Ready:
                OverlayLayer.Visibility = Visibility.Visible;
                OverlayBadgeText.Text = "準備開始";
                OverlayTitle.Text = "貪食蛇";
                OverlayMessage.Text = "用方向鍵或 WASD 操控蛇身，吃下越多果實分數越高。撞到牆壁或自己的身體就結束了。";
                PrimaryActionButton.Content = "開始遊戲";
                OverlayHint.Text = "按 Enter 或空白鍵開始";
                break;
            case GameStatus.Running:
                OverlayLayer.Visibility = Visibility.Collapsed;
                break;
            case GameStatus.Paused:
                OverlayLayer.Visibility = Visibility.Visible;
                OverlayBadgeText.Text = "遊戲暫停";
                OverlayTitle.Text = "已暫停";
                OverlayMessage.Text = "喘口氣，準備好再繼續。";
                PrimaryActionButton.Content = "繼續遊戲";
                OverlayHint.Text = "按空白鍵或 P 繼續";
                break;
            case GameStatus.GameOver:
                OverlayLayer.Visibility = Visibility.Visible;
                OverlayBadgeText.Text = _isNewRecord ? "新紀錄" : _game.HasWon ? "完美通關" : "遊戲結束";
                OverlayTitle.Text = _game.HasWon ? "太強了！" : "遊戲結束";
                OverlayMessage.Text = _game.HasWon
                    ? $"你填滿了整個盤面，最終得分 {_game.Score} 分。"
                    : $"本局得分 {_game.Score} 分 · 長度 {_game.Snake.Count} · 等級 {_game.Level}";
                PrimaryActionButton.Content = "再玩一次";
                OverlayHint.Text = "按 R 或 Enter 重新開始";
                break;
        }

        PauseButton.Content = status == GameStatus.Paused ? "繼續" : "暫停";
        PauseButton.IsEnabled = status is GameStatus.Running or GameStatus.Paused;
        RestartButton.IsEnabled = status != GameStatus.Ready;
    }

    private void LoadHighScore()
    {
        _highScore = HighScoreStore.Load();
        UpdateHud();
    }

    private void RecordHighScore()
    {
        if (_game.Score <= _highScore)
        {
            return;
        }

        _highScore = _game.Score;
        _isNewRecord = true;
        HighScoreStore.Save(_highScore);
    }

    private enum CellKind
    {
        BaseDark,
        BaseLight,
        SnakeBody,
        SnakeHead,
        Food,
    }
}


