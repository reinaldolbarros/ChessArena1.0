using ChessMAUI.Models;

namespace ChessMAUI.Services;

/// <summary>
/// Gerencia a sala de espera (matchmaking).
/// Hoje usa bots para preencher as vagas; futuramente substituir
/// o FillBotsAsync por chamadas SignalR/WebSocket para jogadores reais.
/// </summary>
public class MatchmakingService
{
    private static readonly string[] BotAvatars =
        ["🤖","🦾","⚙️","🔩","💻","🎯","🧠","🦿","🕹️","👾"];

    // ── Nomes de bots (pool grande para torneios de 64) ──────────────────────
    private static readonly string[] BotPool =
    [
        "Magnus Bot",    "Fischer AI",    "Kasparov X",    "Tal Ghost",
        "Deep Blue Jr",  "Capablanca AI", "Morphy Bot",    "Karpov Engine",
        "Anand AI",      "Kramnik Bot",   "Petrosian AI",  "Botvinnik Bot",
        "Smyslov AI",    "Spassky X",     "Alekhine Ghost","Nakamura Bot",
        "Caruana AI",    "Giri X",        "Nepo Bot",      "Mamedyarov AI",
        "Aronian X",     "Topalov Ghost", "Leko Bot",      "Ivanchuk AI",
        "Shirov Bot",    "Adams AI",      "Morozevich X",  "Polgar AI",
        "Kamsky Bot",    "Short AI",      "Gelfand X",     "Bareev Ghost",
        "Khalifman Bot", "Dreev AI",      "Svidler X",     "Grischuk AI",
        "Lputian Bot",   "Rublevsky AI",  "Timofeev X",    "Sjugirov Bot",
        "Sakaev AI",     "Korobov X",     "Vitiugov Bot",  "Lysyj AI",
        "Jakovenko X",   "Eljanov Bot",   "Fedoseev AI",   "Cheparinov X",
        "Nisipeanu Bot", "Motylev AI",    "Tomashevsky X", "Efimenko Bot",
        "Malakhov AI",   "Inarkiev X",    "Riazantsev Bot","Potkin AI",
        "Dubov Bot",     "Vidit AI",      "Praggnanandhaa","Abdusattorov",
        "Firouzja Bot",  "Esipenko AI",   "Keymer Bot",    "Niemann AI"
    ];

    // ── Estado da sala ────────────────────────────────────────────────────────
    public int              TotalSlots      { get; private set; }
    public decimal          BuyIn           { get; private set; }
    public int              TimeMinutes     { get; private set; }
    public TournamentType   RoomType        { get; private set; } = TournamentType.Standard;
    public decimal          SatelliteTarget { get; private set; } = 0;
    public decimal          BountyPerPlayer { get; private set; } = 0;
    public bool             IsPrivate       { get; private set; } = false;
    public string           AccessCode      { get; private set; } = "";
    public List<RoomPlayer> Players         { get; } = [];
    public bool             IsReady         => Players.Count == TotalSlots;

    // ── Eventos (disparados de threads de background — despache na UI) ────────
    /// <summary>Disparado cada vez que um jogador entra na sala.</summary>
    public event Action<RoomPlayer>? PlayerJoined;
    /// <summary>Disparado quando a sala está cheia e o torneio vai começar.</summary>
    public event Action?             RoomFull;

    // ─────────────────────────────────────────────────────────────────────────
    // Inicializa sala e registra o jogador humano
    // ─────────────────────────────────────────────────────────────────────────
    public void CreateRoom(int size, decimal buyIn, int timeMinutes, string humanName,
                           string humanAvatar = "♟",
                           TournamentType type = TournamentType.Standard,
                           decimal satelliteTarget = 0,
                           decimal bountyPerPlayer = 0,
                           bool isPrivate = false, string accessCode = "")
    {
        TotalSlots      = size;
        BuyIn           = buyIn;
        TimeMinutes     = timeMinutes;
        RoomType        = type;
        SatelliteTarget = satelliteTarget;
        BountyPerPlayer = bountyPerPlayer;
        IsPrivate       = isPrivate;
        AccessCode      = accessCode;
        Players.Clear();

        var human = new RoomPlayer
        {
            Name = humanName, IsHuman = true, Strength = 6,
            Avatar = humanAvatar
        };
        Players.Add(human);
        PlayerJoined?.Invoke(human);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Preenche a sala com bots simulando chegada assíncrona de jogadores reais.
    // Para escalar para online: substituir por listener de eventos SignalR.
    // ─────────────────────────────────────────────────────────────────────────
    public async Task FillBotsAsync(CancellationToken ct)
    {
        int needed   = TotalSlots - Players.Count;
        var botNames = BotPool.OrderBy(_ => Random.Shared.Next())
                              .Take(needed)
                              .ToArray();

        foreach (var name in botNames)
        {
            if (ct.IsCancellationRequested) return;

            // Delay aleatório simulando latência de rede (300ms–2s por jogador)
            int delay = Random.Shared.Next(300, 2000);
            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) { return; }

            // Força bots mais fortes em torneios de alto valor
            int minStr = BuyIn switch { >= 2500 => 8, >= 1000 => 7, >= 500 => 6, _ => 3 };
            int str    = Random.Shared.Next(minStr, 10);
            var bot = new RoomPlayer
            {
                Name     = name,
                IsHuman  = false,
                Strength = str,
                Avatar   = BotAvatars[Random.Shared.Next(BotAvatars.Length)]
            };

            Players.Add(bot);
            PlayerJoined?.Invoke(bot);
        }

        if (!ct.IsCancellationRequested && IsReady)
            RoomFull?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Preenche vagas restantes imediatamente (usado pelo criador de sala privada)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task FillBotsImmediateAsync()
    {
        int needed   = TotalSlots - Players.Count;
        var botNames = BotPool.OrderBy(_ => Random.Shared.Next())
                              .Take(needed).ToArray();

        foreach (var name in botNames)
        {
            await Task.Delay(180);
            int str = Random.Shared.Next(3, 10);
            var bot = new RoomPlayer
            {
                Name     = name,
                IsHuman  = false,
                Strength = str,
                Avatar   = BotAvatars[Random.Shared.Next(BotAvatars.Length)]
            };
            Players.Add(bot);
            PlayerJoined?.Invoke(bot);
        }

        if (IsReady) RoomFull?.Invoke();
    }
}
