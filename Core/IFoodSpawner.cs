namespace WinSnake.Core;

/// <summary>Selects the next food cell from the cells not occupied by the snake.</summary>
public interface IFoodSpawner
{
    Cell Choose(IReadOnlyList<Cell> freeCells);
}

public sealed class RandomFoodSpawner : IFoodSpawner
{
    public Cell Choose(IReadOnlyList<Cell> freeCells) => freeCells[Random.Shared.Next(freeCells.Count)];
}
