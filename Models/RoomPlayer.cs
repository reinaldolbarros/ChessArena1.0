namespace ChessMAUI.Models;

public class RoomPlayer
{
    public string Name     { get; init; } = "";
    public bool   IsHuman  { get; init; }
    public int    Strength { get; init; } = 5;
    public string Avatar   { get; init; } = "🤖";
}
