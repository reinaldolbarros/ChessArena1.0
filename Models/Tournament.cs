namespace ChessMAUI.Models;

public enum TournamentStatus { Active, HumanEliminated, HumanWon }

public class Tournament
{
    public int              Size            { get; set; }
    public decimal          BuyIn           { get; set; }
    public decimal          PrizePool       { get; set; }
    public int              TotalRounds     { get; set; }
    public int              CurrentRound    { get; set; } = 1;
    public TournamentStatus Status          { get; set; } = TournamentStatus.Active;
    public TournamentType   Type            { get; set; } = TournamentType.Standard;
    public decimal          SatelliteTarget { get; set; } = 0;
    public decimal          BountyPerPlayer { get; set; } = 0;

    public List<TournamentPlayer> Players { get; set; } = [];
    public List<TournamentMatch>  Matches { get; set; } = [];

    // Posição → prêmio em dinheiro virtual
    public Dictionary<int, decimal> PrizeTable { get; set; } = [];

    // Torneio da Liga — premiação em pontos de temporada
    public bool                  IsLiga            { get; set; }
    public bool                  LeagueIsSemanal   { get; set; }
    public Dictionary<int, int>  LeaguePointsTable { get; set; } = [];

    // Heads-Up: melhor de 3
    public bool IsHeadsUp            { get; set; }
    public int  HumanSeriesWins      { get; set; }
    public int  OpponentSeriesWins   { get; set; }
    public int  HeadsUpGame          => HumanSeriesWins + OpponentSeriesWins + 1;
    public string HeadsUpScore       => $"Jogo {HumanSeriesWins + OpponentSeriesWins + 1} de 3  ·  Placar: {HumanSeriesWins}–{OpponentSeriesWins}";
    public bool HeadsUpSeriesDecided => HumanSeriesWins >= 2 || OpponentSeriesWins >= 2;

    public TournamentPlayer? HumanPlayer =>
        Players.FirstOrDefault(p => p.IsHuman);

    // Para HeadsUp, pega o último match pendente (pode haver mais de um na mesma rodada)
    public TournamentMatch? CurrentHumanMatch =>
        Matches.LastOrDefault(m => m.Round == CurrentRound
                                && m.IsHumanMatch
                                && m.Status == MatchStatus.Pending);

    public List<TournamentMatch> CurrentRoundMatches =>
        Matches.Where(m => m.Round == CurrentRound).ToList();

    public string RoundName =>
        (TotalRounds - CurrentRound) switch
        {
            0 => "Final",
            1 => "Semifinal",
            2 => "Quartas de Final",
            3 => "Oitavas de Final",
            _ => $"Rodada de {(int)Math.Pow(2, TotalRounds - CurrentRound + 1)}"
        };

    public int PlayersRemaining =>
        Players.Count(p => !p.IsEliminated);
}
