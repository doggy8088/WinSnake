using WinSnake.Core;

namespace WinSnake.Core.Tests;

public class SnakeGameTests
{
    /// <summary>Deterministic spawner: hands out queued cells, then the first free cell.</summary>
    private sealed class ScriptedFoodSpawner(params Cell[] cells) : IFoodSpawner
    {
        private readonly Queue<Cell> _cells = new(cells);

        public Cell Choose(IReadOnlyList<Cell> freeCells)
        {
            Cell cell = _cells.Count > 0 ? _cells.Dequeue() : freeCells[0];
            Assert.Contains(cell, freeCells);
            return cell;
        }
    }

    private sealed class BadFoodSpawner(Cell cell) : IFoodSpawner
    {
        public Cell Choose(IReadOnlyList<Cell> freeCells) => cell;
    }

    private static SnakeGame CreateGame(params Cell[] foods) => new(foodSpawner: new ScriptedFoodSpawner(foods));

    [Fact]
    public void NewGame_IsReadyWithCenteredSnake()
    {
        SnakeGame game = CreateGame(new Cell(0, 0));

        Assert.Equal(GameStatus.Ready, game.Status);
        Assert.Equal(Direction.Right, game.Direction);
        Assert.Equal(3, game.Snake.Count);
        Assert.Equal(new Cell(10, 10), game.Head);
        Assert.Equal(new Cell(9, 10), game.Snake[1]);
        Assert.Equal(new Cell(8, 10), game.Snake[2]);
        Assert.Equal(new Cell(0, 0), game.Food);
        Assert.Equal(0, game.Score);
        Assert.Equal(1, game.Level);
        Assert.False(game.HasWon);
    }

    [Fact]
    public void Start_BeginsRunning()
    {
        SnakeGame game = CreateGame(new Cell(0, 0), new Cell(1, 0));

        game.Start();

        Assert.Equal(GameStatus.Running, game.Status);
        Assert.Equal(new Cell(1, 0), game.Food);
    }

    [Fact]
    public void Tick_MovesSnakeForwardAndKeepsLength()
    {
        SnakeGame game = CreateGame(new Cell(0, 0), new Cell(1, 0));
        game.Start();

        TickResult result = game.Tick();

        Assert.Equal(TickResult.Moved, result);
        Assert.Equal(new Cell(11, 10), game.Head);
        Assert.Equal(3, game.Snake.Count);
        Assert.Equal(new Cell(9, 10), game.Snake[2]);
    }

    [Fact]
    public void Turn_IsAppliedOnTheNextTick()
    {
        SnakeGame game = CreateGame(new Cell(0, 0), new Cell(1, 0));
        game.Start();

        Assert.True(game.TryTurn(Direction.Up));
        Assert.Equal(new Cell(10, 10), game.Head);

        game.Tick();

        Assert.Equal(new Cell(10, 9), game.Head);
        Assert.Equal(Direction.Up, game.Direction);
    }

    [Fact]
    public void OppositeTurn_IsRejected()
    {
        SnakeGame game = CreateGame(new Cell(0, 0), new Cell(1, 0));
        game.Start();

        Assert.False(game.TryTurn(Direction.Left));
        game.Tick();

        Assert.Equal(new Cell(11, 10), game.Head);
    }

    [Fact]
    public void QueuedTurns_AreAppliedOnePerTick()
    {
        SnakeGame game = CreateGame(new Cell(0, 0), new Cell(1, 0));
        game.Start();

        Assert.True(game.TryTurn(Direction.Up));
        Assert.True(game.TryTurn(Direction.Left));

        game.Tick();
        Assert.Equal(new Cell(10, 9), game.Head);

        game.Tick();
        Assert.Equal(new Cell(9, 9), game.Head);
    }

    [Fact]
    public void WallCollision_EndsTheRound()
    {
        SnakeGame game = new(5, 5, new ScriptedFoodSpawner(new Cell(0, 0), new Cell(1, 0)));
        game.Start();

        Assert.Equal(TickResult.Moved, game.Tick()); // 3,2
        Assert.Equal(TickResult.Moved, game.Tick()); // 4,2
        Assert.Equal(TickResult.HitWall, game.Tick());

        Assert.Equal(GameStatus.GameOver, game.Status);
        Assert.False(game.HasWon);
    }

    [Fact]
    public void Pause_StopsTheSimulationUntilResumed()
    {
        SnakeGame game = CreateGame(new Cell(0, 0), new Cell(1, 0));
        game.Start();
        game.Pause();

        Assert.Equal(GameStatus.Paused, game.Status);
        Assert.False(game.TryTurn(Direction.Up));
        Assert.Equal(TickResult.None, game.Tick());
        Assert.Equal(new Cell(10, 10), game.Head);

        game.Resume();
        Assert.Equal(GameStatus.Running, game.Status);
        Assert.Equal(TickResult.Moved, game.Tick());
    }

    [Fact]
    public void Eating_GrowsSnakeAndScores()
    {
        SnakeGame game = CreateGame(new Cell(0, 0), new Cell(11, 10), new Cell(12, 10));
        game.Start();

        Assert.Equal(TickResult.AteFood, game.Tick());
        Assert.Equal(new Cell(11, 10), game.Head);
        Assert.Equal(4, game.Snake.Count);
        Assert.Equal(10, game.Score);
        Assert.Equal(new Cell(12, 10), game.Food);

        Assert.Equal(TickResult.AteFood, game.Tick());
        Assert.Equal(5, game.Snake.Count);
        Assert.Equal(20, game.Score);
        Assert.Equal(2, game.FoodEaten);
        Assert.Equal(1, game.Level);
    }

    [Fact]
    public void RunningIntoTail_EndsTheRound()
    {
        SnakeGame game = CreateGame(new Cell(0, 0), new Cell(11, 10), new Cell(12, 10));
        game.Start();
        game.Tick(); // eat at 11,10
        game.Tick(); // eat at 12,10 -> length 5

        game.TryTurn(Direction.Down);
        Assert.Equal(TickResult.Moved, game.Tick());

        game.TryTurn(Direction.Left);
        Assert.Equal(TickResult.Moved, game.Tick());

        game.TryTurn(Direction.Up);
        Assert.Equal(TickResult.HitSelf, game.Tick());
        Assert.Equal(GameStatus.GameOver, game.Status);
    }

    [Fact]
    public void Reset_ReturnsToReadyStateAndClearsScore()
    {
        SnakeGame game = CreateGame(new Cell(0, 0), new Cell(11, 10), new Cell(12, 10));
        game.Start();
        game.Tick();

        game.Reset();

        Assert.Equal(GameStatus.Ready, game.Status);
        Assert.Equal(0, game.Score);
        Assert.Equal(0, game.FoodEaten);
        Assert.Equal(3, game.Snake.Count);
        Assert.Equal(new Cell(10, 10), game.Head);
    }

    [Fact]
    public void Food_IsAlwaysPlacedOnAFreeCell()
    {
        var spawner = new ScriptedFoodSpawner(new Cell(11, 10), new Cell(12, 10));
        SnakeGame game = new(foodSpawner: spawner);
        game.Start();

        for (int i = 0; i < 2; i++)
        {
            game.Tick();
            Assert.DoesNotContain(game.Snake, cell => cell == game.Food);
            Assert.InRange(game.Food.X, 0, game.Columns - 1);
            Assert.InRange(game.Food.Y, 0, game.Rows - 1);
        }
    }

    [Fact]
    public void InvalidBoardSize_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeGame(4, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeGame(20, 4));
    }

    [Fact]
    public void SpawnerReturningAnOccupiedCell_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new SnakeGame(foodSpawner: new BadFoodSpawner(new Cell(10, 10))));
    }
}
