using System.Text.Json;
using ChessMAUI.Models;

namespace ChessMAUI.Services;

/// <summary>
/// Busca puzzles reais do banco do Lichess (lichess.org/api/puzzle/next).
/// Mantém cache local e rastreia IDs já vistos para garantir puzzles inéditos.
/// </summary>
public class LichessPuzzleService
{
    private const string ApiUrl    = "https://lichess.org/api/puzzle/next";
    private const string KeySeen   = "lichess_seen_v1";
    private const int    CacheTarget = 20;

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly Queue<Puzzle>    _prefetched = new();
    private readonly HashSet<string>  _seenIds;
    private          Puzzle?          _current;
    private          bool             _prefetching;

    public LichessPuzzleService()
    {
        var saved = Preferences.Default.Get(KeySeen, "");
        _seenIds = saved.Length > 0
            ? [.. saved.Split(',', StringSplitOptions.RemoveEmptyEntries)]
            : [];
    }

    // -----------------------------------------------------------------------
    // API pública
    // -----------------------------------------------------------------------

    /// <summary>Inicia pré-busca em background ao inicializar o app.</summary>
    public void BeginPrefetch()
    {
        if (_prefetching) return;
        _prefetching = true;
        _ = Task.Run(PrefetchLoopAsync);
    }

    /// <summary>Retorna o puzzle atual (mesmo em chamadas repetidas). Null se offline e sem cache.</summary>
    public Puzzle? GetCurrent()
    {
        if (_current != null) return _current;
        Advance();
        return _current;
    }

    /// <summary>Avança para o próximo puzzle inédito. Chamado após IncrementDailyCount.</summary>
    public void Advance()
    {
        while (_prefetched.Count > 0)
        {
            var candidate = _prefetched.Dequeue();
            if (!_seenIds.Contains(candidate.Id))
            {
                _current = candidate;
                MarkSeen(candidate.Id);
                RefillIfLow();
                return;
            }
        }
        _current = null; // offline ou sem cache — PuzzleService usa fallback local
        RefillIfLow();
    }

    public bool HasPuzzleReady => _current != null || _prefetched.Count > 0;

    // -----------------------------------------------------------------------
    // Internos
    // -----------------------------------------------------------------------

    private void RefillIfLow()
    {
        if (_prefetched.Count < CacheTarget / 2 && !_prefetching)
            BeginPrefetch();
    }

    private async Task PrefetchLoopAsync()
    {
        try
        {
            while (_prefetched.Count < CacheTarget)
            {
                var p = await FetchOneAsync();
                if (p != null && !_seenIds.Contains(p.Id))
                    _prefetched.Enqueue(p);
                await Task.Delay(700);
            }
        }
        finally { _prefetching = false; }
    }

    private static async Task<Puzzle?> FetchOneAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(ApiUrl);
            return ParseJson(json);
        }
        catch { return null; }
    }

    private void MarkSeen(string id)
    {
        _seenIds.Add(id);
        if (_seenIds.Count > 3000)
        {
            // Mantém os últimos 1500 IDs para não inflar as preferências
            var arr = _seenIds.ToArray();
            _seenIds.Clear();
            foreach (var s in arr[^1500..]) _seenIds.Add(s);
        }
        Preferences.Default.Set(KeySeen, string.Join(",", _seenIds));
    }

    // -----------------------------------------------------------------------
    // Parsing JSON do Lichess
    // -----------------------------------------------------------------------

    private static Puzzle? ParseJson(string json)
    {
        try
        {
            using var doc    = JsonDocument.Parse(json);
            var root         = doc.RootElement;
            var puzzleEl     = root.GetProperty("puzzle");

            string id        = puzzleEl.GetProperty("id").GetString() ?? "";
            string fen       = puzzleEl.GetProperty("fen").GetString() ?? "";
            int    rating    = puzzleEl.TryGetProperty("rating", out var rp) ? rp.GetInt32() : 1200;

            var solEl        = puzzleEl.GetProperty("solution");
            var solution     = new string[solEl.GetArrayLength()];
            for (int i = 0; i < solution.Length; i++)
                solution[i] = solEl[i].GetString() ?? "";

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(fen) || solution.Length == 0)
                return null;

            // No formato Lichess, o FEN tem como lado-a-mover o OPONENTE (quem cometeu o erro).
            // O jogador que resolve o puzzle é o lado OPOSTO.
            string? fenTurn  = fen.Split(' ').ElementAtOrDefault(1);
            PieceColor player = fenTurn == "w" ? PieceColor.Black : PieceColor.White;

            var themesEl     = puzzleEl.TryGetProperty("themes", out var te) ? te : default;
            string category  = ExtractCategory(themesEl);
            string desc      = CategoryDescription(category);
            int    diff      = rating < 1300 ? 1 : rating < 1700 ? 2 : 3;

            return new Puzzle("L" + id, fen, solution, player, category, diff, desc);
        }
        catch { return null; }
    }

    private static string ExtractCategory(JsonElement themes)
    {
        if (themes.ValueKind != JsonValueKind.Array) return "Tática";

        var list = new List<string>();
        foreach (var t in themes.EnumerateArray())
            list.Add(t.GetString() ?? "");

        if (list.Any(t => t.StartsWith("mate")))     return "Xeque-mate";
        if (list.Contains("fork"))                    return "Garfo";
        if (list.Contains("pin"))                     return "Forca";
        if (list.Contains("skewer"))                  return "Espeto";
        if (list.Contains("promotion"))               return "Promoção";
        if (list.Contains("discoveredAttack"))        return "Ataque Descoberto";
        if (list.Contains("doubleCheck"))             return "Duplo Xeque";
        if (list.Contains("sacrifice"))               return "Sacrifício";
        if (list.Contains("hangingPiece"))            return "Captura Simples";
        if (list.Contains("deflection"))              return "Desvio";
        if (list.Contains("decoy"))                   return "Atração";
        if (list.Contains("endgame"))                 return "Final";
        return "Tática";
    }

    private static string CategoryDescription(string category) => category switch
    {
        "Xeque-mate"         => "Encontre o xeque-mate!",
        "Garfo"              => "Uma peça ataca dois alvos ao mesmo tempo",
        "Forca"              => "Imobilize a peça adversária na linha de fogo",
        "Espeto"             => "Ataque duas peças na mesma linha",
        "Promoção"           => "Avance o peão até a promoção",
        "Ataque Descoberto"  => "Mova uma peça e revele um ataque oculto",
        "Duplo Xeque"        => "Duas peças dão xeque ao mesmo tempo",
        "Sacrifício"         => "Sacrifique material para ganhar vantagem decisiva",
        "Captura Simples"    => "Capture a peça desprotegida",
        "Desvio"             => "Force o adversário a abandonar uma defesa crucial",
        "Atração"            => "Atraia o rei adversário para uma armadilha",
        "Final"              => "Técnica precisa no final de jogo",
        _                    => "Encontre o melhor lance"
    };
}
