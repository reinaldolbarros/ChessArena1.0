using ChessMAUI.Models;
using ChessMAUI.Services;

namespace ChessMAUI;

/// <summary>Estado global compartilhado entre páginas (singleton).</summary>
public class AppState
{
    public static AppState Current { get; } = new();

    public AuthService         Auth         { get; } = new();
    public ProfileService      Profile      { get; } = new();
    public DailyService        Daily        { get; } = new();
    public StarService         Stars        { get; } = new();
    public AdminService        Admin        { get; } = new();
    public SubscriptionService Subscription { get; } = new();
    public AdService           Ads          { get; } = new();
    public RankingService      Ranking      { get; } = new();
    public StockfishService    Stockfish    { get; } = new();

    // Liga/Casual — páginas sem rota ativa, aguardando base de jogadores.
    // Mantidos para compilação; remover junto com as páginas quando forem deletadas.
    public TournamentService        TournSvc      { get; } = new();
    public MatchmakingService       Matchmaking   { get; } = new();
    public RoomLobbyService         RoomLobby     { get; } = new();
    public TournamentHistoryService History       { get; } = new();
    public TitleService             Titles        { get; } = new();
    public SeasonService            Season        { get; } = new();
    public LeagueService            League        { get; } = new();
    public CasualRankingService     CasualRanking { get; } = new();

    // Torneio ativo
    public Tournament? ActiveTournament { get; set; }

    // Resultado da última partida
    public bool LastMatchHumanWon { get; set; }

    // BracketPage → GamePage
    public bool PendingTournamentGame { get; set; }

    // GamePage → BracketPage
    public bool MatchResultReady { get; set; }

    // Contexto da partida
    public string TournamentOpponentName   { get; set; } = "";
    public int    TournamentAIDepth        { get; set; } = 2;
    public int    TournamentTimeMinutes    { get; set; } = 5;

    public bool IsInTournamentMatch => PendingTournamentGame;

    // Jogo com amigo (pass-and-play)
    public bool   PendingFriendGame  { get; set; }
    public string FriendPlayer1Name  { get; set; } = "";
    public string FriendOpponentName { get; set; } = "";
    public int    FriendTimeMinutes  { get; set; }
    public int    FriendGameCount    { get; set; } // par = jogador1 com brancas, ímpar = invertido

    // Modo administrador para testes
    public bool IsAdminMode { get; set; } = false;

    // Modo carreira
    public CareerService Career { get; } = new();

    // CareerPage → GamePage
    public bool   PendingCareerGame  { get; set; }
    public bool   IsCareerGame       { get; set; }
    public string CareerOpponentName { get; set; } = "";
    public int    CareerAIDepth      { get; set; } = 1;
    public int    CareerTimeMinutes  { get; set; } = 10;
    public int    CareerSkillLevel   { get; set; } = 20;
    public BotPersonality CareerPersonality { get; set; } = BotPersonality.Balanced;
    public bool   LastMatchWasDraw   { get; set; }

    // Revisão de partida
    public Models.GameReviewData? PendingReviewData { get; set; }

    // Jogo aleatório online
    public OnlineMatchService OnlineMatch       { get; } = new();
    public bool               PendingOnlineGame { get; set; }
    public bool               IsOnlineGame      { get; set; }
    public string             OnlineOpponentName  { get; set; } = "";
    public int                OnlineTimeMinutes   { get; set; } = 5;
    public bool               OnlinePlayerIsWhite { get; set; }
}
