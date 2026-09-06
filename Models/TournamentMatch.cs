namespace ChessMAUI.Models;

public enum MatchStatus { Pending, Completed }

public class TournamentMatch
{
    public int              Round       { get; set; }
    public TournamentPlayer Player1     { get; set; } = null!;
    public TournamentPlayer Player2     { get; set; } = null!;
    public TournamentPlayer? Winner     { get; set; }
    public TournamentPlayer? Loser      { get; set; }
    public MatchStatus      Status      { get; set; } = MatchStatus.Pending;
    public bool IsHumanMatch => Player1.IsHuman || Player2.IsHuman;
    public TournamentPlayer? HumanPlayer => Player1.IsHuman ? Player1 : Player2.IsHuman ? Player2 : null;
    public TournamentPlayer? Opponent(TournamentPlayer p) => Player1 == p ? Player2 : Player1;
}
