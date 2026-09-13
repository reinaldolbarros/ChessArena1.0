using ChessMAUI.Models;
using Supabase.Realtime.PostgresChanges;

namespace ChessMAUI.Services;

/// <summary>
/// Motor de partida online real (usado tanto por "Jogar com Amigo" quanto, futuramente, por
/// "Jogar Online"): carrega/assina a linha da partida em "games" e chama as funções do
/// servidor já criadas em supabase_schema_online.sql — o cliente nunca decide sozinho quem
/// venceu ou qual é o rating; isso é sempre validado no servidor.
/// </summary>
public class OnlineGameService
{
    private Supabase.Client? Db => SupabaseService.Instance.IsReady ? SupabaseService.Instance.Client : null;

    private string? _gameId;

    /// <summary>Disparado sempre que a linha da partida muda (lance do adversário, oferta de
    /// empate, fim de jogo) — inclui as mudanças que o próprio cliente causou também.</summary>
    public event Action<SupabaseGame>? GameUpdated;

    public async Task<SupabaseGame?> LoadGameAsync(string gameId)
    {
        if (Db == null) return null;
        try
        {
            return await Db.From<SupabaseGame>().Where(g => g.Id == gameId).Single();
        }
        catch { return null; }
    }

    /// <summary>Carrega a partida, decide cor/adversário e preenche o AppState — usado tanto
    /// por "Jogar com Amigo" quanto por "Jogar Online" pra entrar na mesma tela de jogo real,
    /// evitando que cada fluxo duplique (e esqueça algum campo) essa lógica na mão.</summary>
    public async Task<bool> EnterGameAsync(string gameId, string? knownOpponentName)
    {
        var game = await LoadGameAsync(gameId);
        if (game == null) return false;

        string myId          = AppState.Current.Auth.UserId;
        bool   playerIsWhite = game.WhiteId == myId;
        string opponentId    = playerIsWhite ? game.BlackId : game.WhiteId;
        string opponentName  = knownOpponentName ?? await LookupProfileNameAsync(opponentId);

        var state = AppState.Current;
        state.PendingOnlineGameId = gameId;
        state.IsOnlineGame        = true;
        state.PendingOnlineGame   = true;
        state.OnlineOpponentName  = opponentName;
        state.OnlineTimeMinutes   = game.TimeMinutes;
        state.OnlinePlayerIsWhite = playerIsWhite;
        return true;
    }

    private static async Task<string> LookupProfileNameAsync(string userId)
    {
        try
        {
            var row = await SupabaseService.Instance.Client
                .From<SupabaseProfile>()
                .Where(p => p.Id == userId)
                .Single();
            return string.IsNullOrWhiteSpace(row?.Name) ? "Adversário" : row!.Name;
        }
        catch { return "Adversário"; }
    }

    /// <summary>Assina mudanças na partida via Realtime. Chame uma vez, depois de LoadGameAsync.</summary>
    public void Subscribe(string gameId)
    {
        _gameId = gameId;
        try
        {
            Db?.From<SupabaseGame>().On(PostgresChangesOptions.ListenType.Updates, (_, change) =>
            {
                var game = change.Model<SupabaseGame>();
                if (game != null && game.Id == _gameId)
                    GameUpdated?.Invoke(game);
            });
        }
        catch { }
    }

    /// <summary>Anexa exatamente 1 lance novo (UCI, ex. "e2e4") ao histórico — o gatilho
    /// validate_game_move no servidor recalcula turno/relógio e rejeita qualquer outra coisa.</summary>
    public async Task<bool> SubmitMoveAsync(List<string> movesBeforeThisMove, string uciMove)
    {
        if (Db == null || string.IsNullOrEmpty(_gameId)) return false;
        try
        {
            var newMoves = new List<string>(movesBeforeThisMove) { uciMove };
            await Db.From<SupabaseGame>()
                .Where(g => g.Id == _gameId)
                .Set(g => g.Moves, newMoves)
                .Update();
            return true;
        }
        catch { return false; }
    }

    /// <summary>Chamado quando o ChessEngine local detecta fim de jogo (xeque-mate, afogamento,
    /// regra de empate) — só libera Elo quando os DOIS lados reivindicarem o mesmo resultado.</summary>
    public Task<bool> ClaimResultAsync(string result) =>
        CallRpc("submit_result_claim", new Dictionary<string, object> { ["p_game_id"] = _gameId!, ["p_claim"] = result });

    public Task<bool> ResignAsync() =>
        CallRpc("resign_game", new Dictionary<string, object> { ["p_game_id"] = _gameId! });

    public Task<bool> ClaimTimeoutAsync() =>
        CallRpc("claim_timeout", new Dictionary<string, object> { ["p_game_id"] = _gameId! });

    public Task<bool> OfferDrawAsync() =>
        CallRpc("offer_draw", new Dictionary<string, object> { ["p_game_id"] = _gameId! });

    public Task<bool> RespondDrawAsync(bool accept) =>
        CallRpc("respond_draw", new Dictionary<string, object> { ["p_game_id"] = _gameId!, ["p_accept"] = accept });

    private async Task<bool> CallRpc(string function, Dictionary<string, object> args)
    {
        if (Db == null || string.IsNullOrEmpty(_gameId)) return false;
        try { await Db.Rpc(function, args); return true; }
        catch { return false; }
    }
}
