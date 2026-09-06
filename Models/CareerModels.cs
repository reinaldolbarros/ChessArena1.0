namespace ChessMAUI.Models;

public enum CareerLevel
{
    Local = 0, Zonal = 1, CopaMundo = 2, GrandSwiss = 3,
    GrandPrix = 4, Candidatos = 5, Mundial = 6
}

public enum CareerFormat { Swiss, Elimination, BestOfN }

public enum CareerRoundResult { NotPlayed, Win, Draw, Loss }

public enum CareerStageOutcome { None, Advanced, AdvancedDirect, Eliminated }

public class CareerPlayer
{
    public string       Name       { get; set; } = "";
    public int          Difficulty { get; set; }
    public double       Points     { get; set; }
    public bool         IsHuman    { get; set; }
    public List<string> Faced      { get; set; } = [];
}

public class CareerRound
{
    public int               Number     { get; set; }
    public string            Opponent   { get; set; } = "";
    public int               Difficulty { get; set; }
    public CareerRoundResult Result     { get; set; } = CareerRoundResult.NotPlayed;
}

public class CareerTournamentState
{
    public CareerLevel        Level            { get; set; }
    public CareerFormat       Format           { get; set; }
    public int                TotalRounds      { get; set; }
    public int                CurrentRound     { get; set; }
    public int                AdvancementSpots { get; set; } = 1;
    public bool               IsCompleted      { get; set; }
    public CareerStageOutcome Outcome          { get; set; }
    public List<CareerPlayer> Players          { get; set; } = [];
    public List<CareerRound>  Rounds           { get; set; } = [];

    // BestOfN (Mundial)
    public int HumanWins   { get; set; }
    public int HumanLosses { get; set; }
    public int WinsNeeded  { get; set; } = 3;

    public CareerPlayer  Human    => Players.First(p => p.IsHuman);
    public CareerPlayer? Opponent => Players.FirstOrDefault(p => !p.IsHuman);

    public string LevelName => Level switch
    {
        CareerLevel.Local      => "Torneio Local",
        CareerLevel.Zonal      => "Torneio Zonal",
        CareerLevel.CopaMundo  => "Copa do Mundo FIDE",
        CareerLevel.GrandSwiss => "Grand Swiss",
        CareerLevel.GrandPrix  => "Grand Prix FIDE",
        CareerLevel.Candidatos => "Torneio de Candidatos",
        CareerLevel.Mundial    => "Campeonato Mundial",
        _                      => "Torneio"
    };

    public string LevelSubtitle => Level switch
    {
        CareerLevel.Local      => "Suíço · 7 rodadas · Top 2 avançam",
        CareerLevel.Zonal      => "Suíço · 7 rodadas · Top 2 avançam",
        CareerLevel.CopaMundo  => "Eliminação direta · 3 partidas",
        CareerLevel.GrandSwiss => "Suíço · 7 rodadas · Top 2 avançam",
        CareerLevel.GrandPrix  => "Suíço · 7 rodadas · Apenas o 1º avança",
        CareerLevel.Candidatos => "Round-robin · 7 rodadas · Apenas o 1º avança",
        CareerLevel.Mundial    => "Melhor de 5 · IA Mestre",
        _                      => ""
    };

    public List<CareerPlayer> Standings => [.. Players
        .OrderByDescending(p => p.Points)
        .ThenByDescending(p => p.Difficulty)];
}

public class CareerProgress
{
    public CareerLevel            CurrentLevel      { get; set; }
    public int                    ZonalRetries      { get; set; }
    public CareerTournamentState? ActiveTournament  { get; set; }
    public bool                   IsCareerCompleted { get; set; }
    public int                    CycleYear         { get; set; }
    public int                    TitlesWon         { get; set; }

    public int EffectiveCycleYear => CycleYear > 0 ? CycleYear : 2024;
}
