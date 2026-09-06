namespace ChessMAUI.Models;

public enum RoomStatus { Open, Starting, InProgress }

public enum TournamentType
{
    Standard,    // Mata-mata clássico
    HeadsUp,     // 1v1, melhor de 3
    Bounty,      // Recompensa por eliminar adversário
    Satellite,   // Prêmio = vaga em torneio maior
    Turbo,       // Tempo reduzido (≤ 2 min)
    HyperTurbo,  // Tempo ultra-reduzido (1 min, 15s/jogada)
}

public class TournamentRoom
{
    public string         Id          { get; init; } = Guid.NewGuid().ToString()[..6].ToUpper();
    public int            Size        { get; init; }
    public decimal        BuyIn       { get; init; }
    public int            TimeMinutes { get; init; }
    public TournamentType Type        { get; init; } = TournamentType.Standard;
    public int            Joined      { get; set; } = 0;
    public RoomStatus     Status      { get; set; } = RoomStatus.Open;

    // Campos específicos por tipo
    public decimal BountyPerPlayer  { get; init; } = 0;   // Bounty
    public decimal SatelliteTarget  { get; init; } = 0;   // Satellite (buy-in do torneio alvo)

    // Torneios criados por jogador
    public bool   IsPlayerCreated { get; init; } = false;
    public string CreatorName     { get; init; } = "";
    public bool   IsPrivate       { get; init; } = false;
    public string AccessCode      { get; init; } = "";

    public decimal PrizePool    => BuyIn * Size;
    public string  TimeLabel    => TimeMinutes > 0 ? $"{TimeMinutes} min" : "1 min";
    public bool    IsHighStakes => BuyIn >= 500;

    /// <summary>Nome descritivo para torneios de alto valor.</summary>
    public string HighStakesName => BuyIn switch
    {
        >= 2500 => "Elite Cup",
        >= 1000 => "Master Series",
        >= 500  => "Grand Prix",
        _       => ""
    };

    public string TypeBadge => Type switch
    {
        TournamentType.HeadsUp    => "⚔",
        TournamentType.Bounty     => "🎯",
        TournamentType.Satellite  => "🎟",
        TournamentType.Turbo      => "⚡",
        TournamentType.HyperTurbo => "🔥",
        _                         => ""
    };

    public string TypeLabel => Type switch
    {
        TournamentType.HeadsUp    => "Duelo",
        TournamentType.Bounty     => "Bounty",
        TournamentType.Satellite  => "Passaporte",
        TournamentType.Turbo      => "Turbo",
        TournamentType.HyperTurbo => "Hyper",
        _                         => "Clássico"
    };

    public string StatusLabel => Status switch
    {
        RoomStatus.Open       => $"{Joined}/{Size} na sala",
        RoomStatus.Starting   => "Iniciando...",
        RoomStatus.InProgress => "Em andamento",
        _ => ""
    };

    public Color StatusColor => Status switch
    {
        RoomStatus.Open       => Color.FromArgb("#4CAF50"),
        RoomStatus.Starting   => Color.FromArgb("#FFD700"),
        RoomStatus.InProgress => Color.FromArgb("#888888"),
        _ => Colors.White
    };

    public bool CanJoin => Status == RoomStatus.Open && Joined < Size;
}
