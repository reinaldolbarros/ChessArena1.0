using ChessMAUI.Models;

namespace ChessMAUI.Services;

/// <summary>
/// Matchmaking 1v1 com margem adaptativa de rating.
/// Começa em ±100, expande +100 a cada 5s até ±400.
/// Phase 1: mock local. Phase 2: substituir por Firebase/SignalR.
/// </summary>
public class OnlineMatchService
{
    private const int InitialMargin = 100;
    private const int MarginStep    = 100;
    private const int MaxMargin     = 400;
    private const int ExpandEveryMs = 5_000;

    private static readonly string[] MockNames =
    [
        "Carlos Silva", "Ana Lima",       "Pedro Santos",  "Julia Rocha",
        "Rafael Costa", "Fernanda Alves", "Bruno Martins", "Camila Nunes",
        "Diego Souza",  "Larissa Melo"
    ];

    public OnlineMatchState State          { get; } = new();
    public int              CurrentMargin  { get; private set; }

    public event Action?       MatchReady;
    public event Action?       SearchCancelled;
    public event Action<int>?  MarginChanged;   // dispara quando a margem expande

    private CancellationTokenSource? _cts;

    public async Task StartSearchingAsync(int minutes, int playerRating)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        State.Phase         = OnlineMatchPhase.Searching;
        State.AgreedMinutes = minutes;
        State.MatchId       = Guid.NewGuid().ToString()[..8];
        CurrentMargin       = InitialMargin;

        try
        {
            for (int margin = InitialMargin; ; margin += MarginStep)
            {
                CurrentMargin = Math.Min(margin, MaxMargin);
                MarginChanged?.Invoke(CurrentMargin);

                bool isLastWindow = CurrentMargin >= MaxMargin;

                // Mock: 65 % de chance de encontrar nesta janela; última janela sempre encontra
                bool foundHere = isLastWindow || Random.Shared.Next(100) < 65;

                if (foundHere)
                {
                    int delay = Random.Shared.Next(400, ExpandEveryMs);
                    await Task.Delay(delay, _cts.Token);
                    ConfirmMatch(playerRating, CurrentMargin);
                    return;
                }

                await Task.Delay(ExpandEveryMs, _cts.Token);

                if (isLastWindow) break;
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Cancel()
    {
        _cts?.Cancel();
        State.Phase = OnlineMatchPhase.Cancelled;
        SearchCancelled?.Invoke();
        Reset();
    }

    public void Reset()
    {
        _cts?.Cancel();
        _cts                 = null;
        CurrentMargin        = 0;
        State.Phase          = OnlineMatchPhase.Idle;
        State.OpponentName   = "";
        State.OpponentRating = 0;
        State.MatchId        = "";
    }

    // ── Privados ─────────────────────────────────────────────────────────────

    private void ConfirmMatch(int playerRating, int margin)
    {
        int offset = Random.Shared.Next(-margin, margin + 1);
        State.OpponentRating = Math.Max(400, playerRating + offset);
        State.OpponentName   = MockNames[Random.Shared.Next(MockNames.Length)];
        State.PlayerIsWhite  = Random.Shared.Next(2) == 0;
        State.Phase          = OnlineMatchPhase.Confirmed;
        MatchReady?.Invoke();
    }
}
