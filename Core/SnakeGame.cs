namespace WinSnake.Core;

/// <summary>
/// The complete, UI-independent snake game state machine. Callers advance the
/// game with <see cref="Tick"/> (usually from a timer) and steer it with
/// <see cref="TryTurn"/>. The class performs no drawing and knows nothing
/// about WinUI, which keeps the rules unit-testable.
/// </summary>
public sealed class SnakeGame
{
    public const int DefaultColumns = 20;
    public const int DefaultRows = 20;
    public const int InitialLength = 3;
    public const int ScorePerFood = 10;

    private const int MaxQueuedTurns = 2;

    private readonly List<Cell> _snake = [];
    private readonly Queue<Direction> _pendingTurns = new();
    private readonly IFoodSpawner _foodSpawner;

    private Direction _direction = Direction.Right;

    public SnakeGame(int columns = DefaultColumns, int rows = DefaultRows, IFoodSpawner? foodSpawner = null)
    {
        if (columns < 5)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "The board must be at least 5 columns wide.");
        }

        if (rows < 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "The board must be at least 5 rows tall.");
        }

        Columns = columns;
        Rows = rows;
        _foodSpawner = foodSpawner ?? new RandomFoodSpawner();

        Reset();
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>Snake segments ordered from head to tail.</summary>
    public IReadOnlyList<Cell> Snake => _snake;

    public Cell Head => _snake[0];

    public Cell Food { get; private set; }

    public Direction Direction => _direction;

    public GameStatus Status { get; private set; }

    public int Score { get; private set; }

    public int FoodEaten { get; private set; }

    /// <summary>Current difficulty level; increases every five foods eaten.</summary>
    public int Level => 1 + (FoodEaten / 5);

    /// <summary>True when the snake filled the entire board.</summary>
    public bool HasWon { get; private set; }

    /// <summary>Returns the board to its initial <see cref="GameStatus.Ready"/> state.</summary>
    public void Reset()
    {
        _snake.Clear();
        _pendingTurns.Clear();
        _direction = Direction.Right;
        Score = 0;
        FoodEaten = 0;
        HasWon = false;
        Status = GameStatus.Ready;

        int y = Rows / 2;
        int headX = Columns / 2;
        for (int i = 0; i < InitialLength; i++)
        {
            _snake.Add(new Cell(headX - i, y));
        }

        Food = PickFood();
    }

    /// <summary>Resets the board and immediately begins a new round.</summary>
    public void Start()
    {
        Reset();
        Status = GameStatus.Running;
    }

    public void Pause()
    {
        if (Status == GameStatus.Running)
        {
            Status = GameStatus.Paused;
        }
    }

    public void Resume()
    {
        if (Status == GameStatus.Paused)
        {
            Status = GameStatus.Running;
        }
    }

    /// <summary>Queues a direction change; ignored while not running or when it would reverse the snake.</summary>
    public bool TryTurn(Direction direction)
    {
        if (Status != GameStatus.Running || _pendingTurns.Count >= MaxQueuedTurns)
        {
            return false;
        }

        Direction effective = _pendingTurns.Count > 0 ? _pendingTurns.Last() : _direction;
        if (direction == effective || direction == effective.Opposite())
        {
            return false;
        }

        _pendingTurns.Enqueue(direction);
        return true;
    }

    /// <summary>Advances the simulation by one cell.</summary>
    public TickResult Tick()
    {
        if (Status != GameStatus.Running)
        {
            return TickResult.None;
        }

        if (_pendingTurns.Count > 0)
        {
            _direction = _pendingTurns.Dequeue();
        }

        Cell next = Head.Offset(_direction);

        if (!InBounds(next))
        {
            Status = GameStatus.GameOver;
            return TickResult.HitWall;
        }

        bool willEat = next == Food;

        // The tail vacates its cell on this tick unless the snake grows, so it is
        // not an obstacle in that case.
        int obstacleCount = willEat ? _snake.Count : _snake.Count - 1;
        for (int i = 0; i < obstacleCount; i++)
        {
            if (_snake[i] == next)
            {
                Status = GameStatus.GameOver;
                return TickResult.HitSelf;
            }
        }

        _snake.Insert(0, next);

        if (!willEat)
        {
            _snake.RemoveAt(_snake.Count - 1);
            return TickResult.Moved;
        }

        Score += ScorePerFood;
        FoodEaten++;

        if (!TryPickFood())
        {
            // Every cell is occupied: the player cleared the board.
            HasWon = true;
            Status = GameStatus.GameOver;
            return TickResult.BoardCleared;
        }

        return TickResult.AteFood;
    }

    private bool InBounds(Cell cell) => cell.X >= 0 && cell.X < Columns && cell.Y >= 0 && cell.Y < Rows;

    private bool TryPickFood()
    {
        List<Cell> freeCells = new(Columns * Rows - _snake.Count);

        var occupied = new HashSet<Cell>(_snake);
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                var cell = new Cell(x, y);
                if (!occupied.Contains(cell))
                {
                    freeCells.Add(cell);
                }
            }
        }

        if (freeCells.Count == 0)
        {
            return false;
        }

        Food = ChooseValidCell(freeCells);
        return true;
    }

    private Cell PickFood()
    {
        if (!TryPickFood())
        {
            throw new InvalidOperationException("There is no free cell available for food.");
        }

        return Food;
    }

    private Cell ChooseValidCell(List<Cell> freeCells)
    {
        Cell cell = _foodSpawner.Choose(freeCells);
        if (!InBounds(cell) || !freeCells.Contains(cell))
        {
            throw new InvalidOperationException($"The food spawner returned an invalid cell: ({cell.X}, {cell.Y}).");
        }

        return cell;
    }
}
