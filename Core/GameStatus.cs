namespace WinSnake.Core;

public enum GameStatus
{
    Ready,
    Running,
    Paused,
    GameOver,
}

public enum TickResult
{
    None,
    Moved,
    AteFood,
    HitWall,
    HitSelf,
    BoardCleared,
}
