namespace ChessMAUI.Models;

public enum OnlineMatchPhase { Idle, Searching, Confirmed, Cancelled }

public class OnlineMatchState
{
    public string           MatchId         { get; set; } = "";
    public string           OpponentName    { get; set; } = "";
    public int              OpponentRating  { get; set; }
    public int              AgreedMinutes   { get; set; } = 5;
    public bool             PlayerIsWhite   { get; set; }
    public OnlineMatchPhase Phase           { get; set; } = OnlineMatchPhase.Idle;
}
