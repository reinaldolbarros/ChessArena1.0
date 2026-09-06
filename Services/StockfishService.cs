using System.Diagnostics;

namespace ChessMAUI.Services;

/// <summary>
/// Wraps a Stockfish process via the UCI protocol.
/// Skill Level 0-20 controls strength; 0 = weakest, 20 = full strength.
/// Call StartAsync once; reuse across games. Falls back to null when unavailable.
/// </summary>
public sealed class StockfishService : IAsyncDisposable
{
    private Process?      _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly SemaphoreSlim _sem = new(1, 1);

    public bool IsAvailable => _process is { HasExited: false };

    // ── Initialisation ───────────────────────────────────────────────────────
    public async Task<bool> StartAsync(string binaryPath)
    {
        if (!File.Exists(binaryPath)) return false;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[SF] StartAsync: {binaryPath}");
            var psi = new ProcessStartInfo
            {
                FileName               = binaryPath,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            _process = Process.Start(psi);
            if (_process == null)
            {
                System.Diagnostics.Debug.WriteLine("[SF] Process.Start returned null");
                return false;
            }
            System.Diagnostics.Debug.WriteLine($"[SF] Process started PID={_process.Id}");

            _stdin           = _process.StandardInput;
            _stdin.AutoFlush = true;
            _stdout          = _process.StandardOutput;

            // UCI handshake
            await _stdin.WriteLineAsync("uci");
            if (!await WaitForLineAsync("uciok", TimeSpan.FromSeconds(5)))
            {
                System.Diagnostics.Debug.WriteLine("[SF] Timeout waiting for uciok");
                return false;
            }
            System.Diagnostics.Debug.WriteLine("[SF] uciok received");

            // Hash pequeno demais faz buscas mais longas (Médio/Difícil) sofrerem colisões na
            // tabela de transposição, deixando o motor instável e "girando" peças sem propósito
            // no mesmo tempo de busca. 64 MB é leve pra qualquer aparelho e resolve isso sem
            // deixar a jogada mais lenta. 2 threads aproveitam mais núcleos no mesmo tempo real.
            await _stdin.WriteLineAsync("setoption name Threads value 2");
            await _stdin.WriteLineAsync("setoption name Hash value 64");
            await _stdin.WriteLineAsync("isready");
            if (!await WaitForLineAsync("readyok", TimeSpan.FromSeconds(3)))
            {
                System.Diagnostics.Debug.WriteLine("[SF] Timeout waiting for readyok");
                return false;
            }
            System.Diagnostics.Debug.WriteLine("[SF] readyok — Stockfish ready");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SF] StartAsync exception: {ex.Message}");
            return false;
        }
    }

    // ── Best move ────────────────────────────────────────────────────────────
    /// <param name="uciMoves">All moves played so far (both sides) in UCI format.</param>
    /// <param name="moveTimeMs">Milliseconds to think per move.</param>
    /// <param name="skillLevel">0 (weakest) to 20 (strongest).</param>
    /// <returns>O lance e, em <c>IsForcedMate</c>, se esse lance inicia um mate forçado
    /// (a favor de quem está jogando) — o chamador nunca deve trocar esse lance por outro.</returns>
    public async Task<(string? Move, bool IsForcedMate)> GetBestMoveAsync(
        IReadOnlyList<string> uciMoves,
        int                   moveTimeMs,
        int                   skillLevel,
        CancellationToken     ct)
    {
        if (!IsAvailable) return (null, false);

        await _sem.WaitAsync(ct);
        try
        {
            // Skill Level (Stockfish-specific; overrides UCI_LimitStrength)
            await _stdin!.WriteLineAsync($"setoption name Skill Level value {skillLevel}");
            await _stdin.WriteLineAsync("setoption name MultiPV value 1");

            // Position
            string posCmd = uciMoves.Count > 0
                ? $"position startpos moves {string.Join(' ', uciMoves)}"
                : "position startpos";
            await _stdin.WriteLineAsync(posCmd);

            // Search
            await _stdin.WriteLineAsync($"go movetime {moveTimeMs}");

            // Read until "bestmove" (with safety timeout = movetime + 4 s)
            using var safetyCtS = CancellationTokenSource.CreateLinkedTokenSource(ct);
            safetyCtS.CancelAfter(moveTimeMs + 4_000);

            bool isForcedMate = false;

            while (!safetyCtS.IsCancellationRequested)
            {
                var line = await _stdout!.ReadLineAsync(safetyCtS.Token);
                if (line == null) break;

                if (line.StartsWith("info") && line.Contains(" score mate "))
                {
                    int idx = line.IndexOf(" score mate ") + 12;
                    int end = line.IndexOf(' ', idx);
                    var s   = end < 0 ? line[idx..] : line[idx..end];
                    // mate > 0: quem está pensando agora (a IA) tem mate forçado a favor —
                    // mate < 0: é a IA que vai ser mateada — nunca deve trocar o lance nesse caso também,
                    // pois qualquer alternativa "mais fraca" só pioraria a defesa.
                    if (int.TryParse(s, out int mate) && mate != 0) isForcedMate = true;
                }

                if (!line.StartsWith("bestmove")) continue;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var move  = parts.Length > 1 ? parts[1] : null;
                return move is null or "(none)" ? (null, false) : (move, isForcedMate);
            }

            // Cancelled before bestmove — send stop and drain
            await StopAndDrainAsync();
            return (null, false);
        }
        catch (OperationCanceledException)
        {
            await StopAndDrainAsync();
            return (null, false);
        }
        finally
        {
            _sem.Release();
        }
    }

    // ── Top-N moves (variedade de abertura) ────────────────────────────────────
    /// <summary>
    /// Retorna até <paramref name="multiPv"/> candidatos, sempre em força máxima (Skill Level 20),
    /// ordenados do melhor para o pior. Usado só para dar variedade nas primeiras jogadas — nunca
    /// para enfraquecer a IA, já que todos os candidatos vêm da própria busca em força total.
    /// </summary>
    public async Task<List<(string Move, int ScoreCp, bool IsMate)>> GetTopMovesAsync(
        IReadOnlyList<string> uciMoves,
        int                   moveTimeMs,
        int                   multiPv,
        CancellationToken     ct)
    {
        if (!IsAvailable) return [];

        await _sem.WaitAsync(ct);
        try
        {
            await _stdin!.WriteLineAsync("setoption name Skill Level value 20");
            await _stdin.WriteLineAsync($"setoption name MultiPV value {multiPv}");

            string posCmd = uciMoves.Count > 0
                ? $"position startpos moves {string.Join(' ', uciMoves)}"
                : "position startpos";
            await _stdin.WriteLineAsync(posCmd);

            await _stdin.WriteLineAsync($"go movetime {moveTimeMs}");

            using var safetyCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            safetyCts.CancelAfter(moveTimeMs + 4_000);

            // Uma entrada por rank de MultiPV; atualizada a cada iteração mais funda da busca.
            var byRank = new SortedDictionary<int, (int ScoreCp, bool IsMate, string Move)>();

            while (!safetyCts.IsCancellationRequested)
            {
                var line = await _stdout!.ReadLineAsync(safetyCts.Token);
                if (line == null) break;

                if (line.StartsWith("bestmove")) break;
                if (!line.StartsWith("info") || !line.Contains(" multipv ") || !line.Contains(" pv ")) continue;

                int rank = ExtractIntAfter(line, " multipv ");
                string? move = ExtractMoveAfter(line, " pv ");
                if (rank <= 0 || move == null) continue;

                bool isMate  = line.Contains(" score mate ");
                int  scoreCp = isMate
                    ? ExtractIntAfter(line, " score mate ") * 100_000 // mate sempre "vence" qualquer cp
                    : ExtractIntAfter(line, " score cp ");

                byRank[rank] = (scoreCp, isMate, move);
            }

            try { await _stdin!.WriteLineAsync("setoption name MultiPV value 1"); } catch { }

            return [.. byRank.Values.Select(v => (v.Move, v.ScoreCp, v.IsMate))];
        }
        catch (OperationCanceledException)
        {
            await StopAndDrainAsync();
            return [];
        }
        finally
        {
            _sem.Release();
        }
    }

    private static int ExtractIntAfter(string line, string marker)
    {
        int idx = line.IndexOf(marker);
        if (idx < 0) return 0;
        idx += marker.Length;
        int end = line.IndexOf(' ', idx);
        var s = end < 0 ? line[idx..] : line[idx..end];
        return int.TryParse(s, out int v) ? v : 0;
    }

    private static string? ExtractMoveAfter(string line, string marker)
    {
        int idx = line.IndexOf(marker);
        if (idx < 0) return null;
        idx += marker.Length;
        int end = line.IndexOf(' ', idx);
        return end < 0 ? line[idx..] : line[idx..end];
    }

    // Envia "stop" e drena o stream até ver o "bestmove" de resposta (o UCI sempre manda um,
    // mesmo pra uma busca abortada). Sem isso, essa linha fica no buffer e contamina a leitura
    // da PRÓXIMA pergunta feita ao motor — todo o resto da partida fica dessincronizado.
    private async Task StopAndDrainAsync()
    {
        try
        {
            await _stdin!.WriteLineAsync("stop");
            using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while (!drainCts.IsCancellationRequested)
            {
                var line = await _stdout!.ReadLineAsync(drainCts.Token);
                if (line == null || line.StartsWith("bestmove")) break;
            }
        }
        catch { /* processo pode ter morrido — nada a fazer */ }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private async Task<bool> WaitForLineAsync(string keyword, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var line = await _stdout!.ReadLineAsync(cts.Token);
                if (line == null) return false;
                if (line.Contains(keyword)) return true;
            }
        }
        catch (OperationCanceledException) { }
        return false;
    }

    // ── Post-game position analysis ─────────────────────────────────────────
    /// <summary>
    /// Evaluates the position reached after <paramref name="uciMoves"/>.
    /// Pass <paramref name="moveTimeMs"/> &gt; 0 to use movetime (bounded), otherwise uses depth.
    /// Returns score from the side-to-move's perspective and the engine's best move.
    /// </summary>
    public async Task<(int ScoreCp, string? BestMoveUci)> AnalyzePositionAsync(
        IReadOnlyList<string> uciMoves, int depth, CancellationToken ct, int moveTimeMs = 0)
    {
        if (!IsAvailable) return (0, null);

        await _sem.WaitAsync(ct);
        try
        {
            await _stdin!.WriteLineAsync("setoption name Skill Level value 20");
            await _stdin.WriteLineAsync("setoption name MultiPV value 1");

            string posCmd = uciMoves.Count > 0
                ? $"position startpos moves {string.Join(' ', uciMoves)}"
                : "position startpos";
            await _stdin.WriteLineAsync(posCmd);

            string goCmd = moveTimeMs > 0 ? $"go movetime {moveTimeMs}" : $"go depth {depth}";
            await _stdin.WriteLineAsync(goCmd);

            int     scoreCp  = 0;
            bool    isMate   = false;
            int     mateIn   = 0;
            string? bestMove = null;

            int safetyMs = moveTimeMs > 0 ? moveTimeMs + 3_000 : 15_000;
            using var safetyCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            safetyCts.CancelAfter(TimeSpan.FromMilliseconds(safetyMs));

            while (!safetyCts.IsCancellationRequested)
            {
                var line = await _stdout!.ReadLineAsync(safetyCts.Token);
                if (line == null) break;

                if (line.StartsWith("info") && line.Contains(" score "))
                {
                    int idx = line.IndexOf(" score cp ");
                    if (idx >= 0)
                    {
                        idx += 10;
                        int end = line.IndexOf(' ', idx);
                        var s = end < 0 ? line[idx..] : line[idx..end];
                        if (int.TryParse(s, out int cp)) { scoreCp = cp; isMate = false; }
                    }
                    else
                    {
                        idx = line.IndexOf(" score mate ");
                        if (idx >= 0)
                        {
                            idx += 12;
                            int end = line.IndexOf(' ', idx);
                            var s = end < 0 ? line[idx..] : line[idx..end];
                            if (int.TryParse(s, out int mate)) { mateIn = mate; isMate = true; }
                        }
                    }
                }
                else if (line.StartsWith("bestmove"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    bestMove = parts.Length > 1 && parts[1] != "(none)" ? parts[1] : null;
                    break;
                }
            }

            if (isMate) scoreCp = mateIn > 0 ? 30_000 : -30_000;
            return (scoreCp, bestMove);
        }
        catch (OperationCanceledException)
        {
            await StopAndDrainAsync();
            return (0, null);
        }
        finally
        {
            _sem.Release();
        }
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────
    public async ValueTask DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            try { await _stdin!.WriteLineAsync("quit"); } catch { }
            await Task.Delay(200);
            try { if (!_process.HasExited) _process.Kill(); } catch { }
        }
        _process?.Dispose();
        _sem.Dispose();
    }
}
