namespace ChessMAUI.Models;

public class TournamentPlayer
{
    public string Name          { get; set; } = "";
    public bool   IsHuman       { get; set; }
    public int    Strength      { get; set; } = 5;  // 1-10 (força real para simulação)
    public bool   IsEliminated  { get; set; }
    public int    FinalPosition { get; set; } = 0;
    public string Avatar        { get; set; } = "🤖"; // emoji de avatar
}
