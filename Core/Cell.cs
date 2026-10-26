namespace WinSnake.Core;

/// <summary>A board coordinate. The origin is the top-left corner.</summary>
public readonly record struct Cell(int X, int Y)
{
    public Cell Offset(Direction direction) => direction switch
    {
        Direction.Up => new Cell(X, Y - 1),
        Direction.Down => new Cell(X, Y + 1),
        Direction.Left => new Cell(X - 1, Y),
        Direction.Right => new Cell(X + 1, Y),
        _ => this,
    };
}
