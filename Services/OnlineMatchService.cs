using ChessMAUI.Models;
using Supabase.Realtime.PostgresChanges;

namespace ChessMAUI.Services;

/// <summary>
/// Matchmaking 1v1 real: chama a função de servidor find_match() (já valida rating/fila),
/// e assina Realtime em "games" pro caso do adversário ser quem encontra o par primeiro —
/// nenhuma escolha de adversário acontece no cliente.
/// </summary>
public class OnlineMatchService
{
    private const int    InitialMargin = 100;
    private const int    MarginStep    = 100;
    private const int    MaxMargin     = 400;
    private const int    ExpandEveryMs = 5_000;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    public OnlineMatchState State          { get; } = new();
    public int              CurrentMargin  { get; private set; }

    public event Action?       MatchReady;
    public event Action?       SearchCancelled;
    public event Action<int>?  MarginChanged;   // cosmético — reflete o tempo decorrido local

    private IDispatcherTimer? _pollTimer;
    private IDispatcherTimer? _marginTimer;
    private DateTime          _searchStartedAt;
    private int               _minutes;
    private bool              _resolved;

    public async Task StartSearchingAsync(int minutes, int playerRating)
    {
        StopTimers();
        _resolved           = false;
        _minutes            = minutes;
        _searchStartedAt    = DateTime.UtcNow;
        CurrentMargin       = InitialMargin;

        State.Phase         = OnlineMatchPhase.Searching;
        State.AgreedMinutes = minutes;
        State.MatchId       = Guid.NewGuid().ToString()[..8];

        SubscribeToIncomingGame();

        _marginTimer = Application.Current!.Dispatcher.CreateTimer();
        _marginTimer.Interval = TimeSpan.FromMilliseconds(ExpandEveryMs);
        _marginTimer.Tick += (_, _) =>
        {
            CurrentMargin = Math.Min(CurrentMargin + MarginStep, MaxMargin);
            MarginChanged?.Invoke(CurrentMargin);
        };
        _marginTimer.Start();

        _pollTimer = Application.Current!.Dispatcher.CreateTimer();
        _pollTimer.Interval = PollInterval;
        _pollTimer.Tick += async (_, _) => await PollOnce();
        _pollTimer.Start();

        await PollOnce();
    }

    private async Task PollOnce()
    {
        if (State.Phase != OnlineMatchPhase.Searching) return;
        var svc = SupabaseService.Instance;
        if (!svc.IsReady) return;

        try
        {
            var response = await svc.Client.Rpc("find_match",
                new Dictionary<string, object> { ["p_time_minutes"] = _minutes });
            var gameId = response?.Content?.Trim('"');
            if (!string.IsNullOrEmpty(gameId) && gameId != "null")
                await ResolveMatch(gameId);
        }
        catch { /* mantém tentando na próxima batida */ }
    }

    /// <summary>Cobre o caso em que o OUTRO jogador chamou find_match e casou comigo primeiro —
    /// quem encontra o par é sempre quem cria a linha em "games".</summary>
    private void SubscribeToIncomingGame()
    {
        try
        {
            string myId = AppState.Current.Auth.UserId;
            SupabaseService.Instance.Client
                .From<SupabaseGame>()
                .On(PostgresChangesOptions.ListenType.Inserts, async (_, change) =>
                {
                    var game = change.Model<SupabaseGame>();
                    if (game == null) return;
                    if (game.WhiteId != myId && game.BlackId != myId) return;

                    await MainThread.InvokeOnMainThreadAsync(() => ResolveMatch(game.Id));
                });
        }
        catch { }
    }

    private async Task ResolveMatch(string gameId)
    {
        if (_resolved || State.Phase != OnlineMatchPhase.Searching) return;
        _resolved = true;
        StopTimers();

        await FillOpponentInfoAsync(gameId);
        State.GameId = gameId;
        State.Phase  = OnlineMatchPhase.Confirmed;
        MatchReady?.Invoke();
    }

    private async Task FillOpponentInfoAsync(string gameId)
    {
        try
        {
            var svc  = SupabaseService.Instance;
            var game = await svc.Client.From<SupabaseGame>().Where(g => g.Id == gameId).Single();
            if (game == null) return;

            string myId       = AppState.Current.Auth.UserId;
            bool   isWhite    = game.WhiteId == myId;
            string opponentId = isWhite ? game.BlackId : game.WhiteId;

            var profile = await svc.Client.From<SupabaseProfile>().Where(p => p.Id == opponentId).Single();

            State.PlayerIsWhite  = isWhite;
            State.OpponentName   = string.IsNullOrWhiteSpace(profile?.Name) ? "Adversário" : profile!.Name;
            State.OpponentRating = profile?.Elo ?? 1200;
        }
        catch { State.OpponentName = "Adversário"; }
    }

    public async void Cancel()
    {
        StopTimers();
        State.Phase = OnlineMatchPhase.Cancelled;
        try
        {
            var svc = SupabaseService.Instance;
            if (svc.IsReady)
                await svc.Client.Rpc("cancel_search", new Dictionary<string, object>());
        }
        catch { }
        SearchCancelled?.Invoke();
        Reset();
    }

    public void Reset()
    {
        StopTimers();
        CurrentMargin        = 0;
        State.Phase          = OnlineMatchPhase.Idle;
        State.GameId         = "";
        State.OpponentName   = "";
        State.OpponentRating = 0;
        State.MatchId        = "";
    }

    private void StopTimers()
    {
        _pollTimer?.Stop();
        _pollTimer = null;
        _marginTimer?.Stop();
        _marginTimer = null;
    }
}
