using ChessMAUI.Models;

namespace ChessMAUI.Services;

public class AIService
{
    private readonly int   _depth;
    private readonly int   _noise; // centipawn jitter for variety

    public AIService(int depth = 2)
    {
        // With quiescence search, each depth level is roughly worth 2 raw plies.
        _depth = depth switch
        {
            1 => 2,  // Fácil   — depth 2 + QS
            3 => 3,  // Médio   — depth 3 + QS
            _ => 6   // Difícil — depth 6 + QS, time-limited
        };
        _noise = depth switch
        {
            1 => 25, // Fácil: 25 cp jitter → escolhe entre os 2-3 melhores
            3 => 8,  // Médio: 8 cp jitter  → variação leve
            _ => 0
        };
    }

    // ── Material (centipeões) ────────────────────────────────────────────────
    private static readonly int[] Material = [100, 320, 330, 500, 900, 20_000];

    // ── Piece-Square Tables ──────────────────────────────────────────────────
    private static readonly int[,] PawnPst = {
        {  0,  0,  0,  0,  0,  0,  0,  0 },
        { 50, 50, 50, 50, 50, 50, 50, 50 },
        { 10, 10, 20, 30, 30, 20, 10, 10 },
        {  5,  5, 10, 25, 25, 10,  5,  5 },
        {  0,  0,  0, 20, 20,  0,  0,  0 },
        {  5, -5,-10,  0,  0,-10, -5,  5 },
        {  5, 10, 10,-20,-20, 10, 10,  5 },
        {  0,  0,  0,  0,  0,  0,  0,  0 }
    };
    private static readonly int[,] KnightPst = {
        {-50,-40,-30,-30,-30,-30,-40,-50},
        {-40,-20,  0,  0,  0,  0,-20,-40},
        {-30,  0, 10, 15, 15, 10,  0,-30},
        {-30,  5, 15, 20, 20, 15,  5,-30},
        {-30,  0, 15, 20, 20, 15,  0,-30},
        {-30,  5, 10, 15, 15, 10,  5,-30},
        {-40,-20,  0,  5,  5,  0,-20,-40},
        {-50,-40,-30,-30,-30,-30,-40,-50}
    };
    private static readonly int[,] BishopPst = {
        {-20,-10,-10,-10,-10,-10,-10,-20},
        {-10,  0,  0,  0,  0,  0,  0,-10},
        {-10,  0,  5, 10, 10,  5,  0,-10},
        {-10,  5,  5, 10, 10,  5,  5,-10},
        {-10,  0, 10, 10, 10, 10,  0,-10},
        {-10, 10, 10, 10, 10, 10, 10,-10},
        {-10,  5,  0,  0,  0,  0,  5,-10},
        {-20,-10,-10,-10,-10,-10,-10,-20}
    };
    private static readonly int[,] RookPst = {
        {  0,  0,  0,  0,  0,  0,  0,  0},
        {  5, 10, 10, 10, 10, 10, 10,  5},
        { -5,  0,  0,  0,  0,  0,  0, -5},
        { -5,  0,  0,  0,  0,  0,  0, -5},
        { -5,  0,  0,  0,  0,  0,  0, -5},
        { -5,  0,  0,  0,  0,  0,  0, -5},
        { -5,  0,  0,  0,  0,  0,  0, -5},
        {  0,  0,  0,  5,  5,  0,  0,  0}
    };
    private static readonly int[,] QueenPst = {
        {-20,-10,-10, -5, -5,-10,-10,-20},
        {-10,  0,  0,  0,  0,  0,  0,-10},
        {-10,  0,  5,  5,  5,  5,  0,-10},
        { -5,  0,  5,  5,  5,  5,  0, -5},
        {  0,  0,  5,  5,  5,  5,  0, -5},
        {-10,  5,  5,  5,  5,  5,  0,-10},
        {-10,  0,  5,  0,  0,  0,  0,-10},
        {-20,-10,-10, -5, -5,-10,-10,-20}
    };
    private static readonly int[,] KingMidPst = {
        {-30,-40,-40,-50,-50,-40,-40,-30},
        {-30,-40,-40,-50,-50,-40,-40,-30},
        {-30,-40,-40,-50,-50,-40,-40,-30},
        {-30,-40,-40,-50,-50,-40,-40,-30},
        {-20,-30,-30,-40,-40,-30,-30,-20},
        {-10,-20,-20,-20,-20,-20,-20,-10},
        { 20, 20,  0,  0,  0,  0, 20, 20},
        { 20, 30, 10,  0,  0, 10, 30, 20}
    };
    private static readonly int[,] KingEndPst = {
        {-50,-40,-30,-20,-20,-30,-40,-50},
        {-30,-20,-10,  0,  0,-10,-20,-30},
        {-30,-10, 20, 30, 30, 20,-10,-30},
        {-30,-10, 30, 40, 40, 30,-10,-30},
        {-30,-10, 30, 40, 40, 30,-10,-30},
        {-30,-10, 20, 30, 30, 20,-10,-30},
        {-30,-30,  0,  0,  0,  0,-30,-30},
        {-50,-30,-30,-30,-30,-30,-30,-50}
    };

    // ── Gameplay entry point ─────────────────────────────────────────────────
    public Task<ChessMove?> GetBestMoveAsync(ChessBoard board, CancellationToken ct = default)
        => Task.Run(() => FindBest(board, ct), ct);

    private ChessMove? FindBest(ChessBoard board, CancellationToken ct)
    {
        var moves = ChessEngine.GetAllLegalMoves(board, board.CurrentTurn);
        if (moves.Count == 0) return null;

        CancellationTokenSource? timeCts = null;
        CancellationToken searchToken    = ct;
        if (_depth >= 4)
        {
            timeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeCts.CancelAfter(TimeSpan.FromSeconds(8.0));
            searchToken = timeCts.Token;
        }

        try   { return SearchBest(board, searchToken); }
        finally { timeCts?.Dispose(); }
    }

    private ChessMove? SearchBest(ChessBoard board, CancellationToken ct)
    {
        var moves = ChessEngine.GetAllLegalMoves(board, board.CurrentTurn);
        OrderMoves(board, moves);

        ChessMove? best    = null;
        int        bestVal = int.MinValue + 1;

        foreach (var move in moves)
        {
            if (ct.IsCancellationRequested) break;
            var clone = board.Clone();
            ChessEngine.ApplyMove(clone, move);
            // Small jitter gives variety without skipping the search entirely
            int score = -Negamax(clone, _depth - 1, int.MinValue + 1, int.MaxValue - 1, ct)
                        + (_noise > 0 ? Random.Shared.Next(_noise + 1) : 0);
            if (score > bestVal) { bestVal = score; best = move; }
        }

        return best ?? moves.FirstOrDefault();
    }

    // ── Negamax + Alpha-Beta ─────────────────────────────────────────────────
    private static int Negamax(ChessBoard board, int depth, int alpha, int beta, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return 0;

        var state = ChessEngine.GetGameState(board);
        if (state == GameState.Checkmate)                   return -(20_000 + depth * 100);
        if (state is GameState.Stalemate or GameState.Draw) return 0;
        if (depth == 0)                                     return Quiesce(board, alpha, beta, ct);

        var moves = ChessEngine.GetAllLegalMoves(board, board.CurrentTurn);
        OrderMoves(board, moves);

        foreach (var move in moves)
        {
            if (ct.IsCancellationRequested) break;
            var clone = board.Clone();
            ChessEngine.ApplyMove(clone, move);
            int score = -Negamax(clone, depth - 1, -beta, -alpha, ct);
            alpha = Math.Max(alpha, score);
            if (alpha >= beta) break;
        }

        return alpha;
    }

    // ── Quiescence search ────────────────────────────────────────────────────
    private static int Quiesce(ChessBoard board, int alpha, int beta, CancellationToken ct)
    {
        int standPat = Evaluate(board);
        if (standPat >= beta) return beta;
        alpha = Math.Max(alpha, standPat);

        var allMoves = ChessEngine.GetAllLegalMoves(board, board.CurrentTurn);
        var captures = new List<ChessMove>(8);
        foreach (var m in allMoves)
            if (board.GetPiece(m.ToRow, m.ToCol) != null || m.IsEnPassant || m.PromotionPiece.HasValue)
                captures.Add(m);

        if (captures.Count == 0) return standPat;
        OrderMoves(board, captures);

        foreach (var move in captures)
        {
            if (ct.IsCancellationRequested) break;
            var clone = board.Clone();
            ChessEngine.ApplyMove(clone, move);
            int score = -Quiesce(clone, -beta, -alpha, ct);
            if (score >= beta) return beta;
            alpha = Math.Max(alpha, score);
        }

        return alpha;
    }

    // ── Evaluation ───────────────────────────────────────────────────────────
    // Two-pass design: first collect structural data, then score — avoids bugs
    // from reading totalPieces before it's fully accumulated.
    private static int Evaluate(ChessBoard board)
    {
        bool isWhiteTurn = board.CurrentTurn == PieceColor.White;

        // ── Pass 1: collect structural data ──
        int[] whitePawnCnt = new int[8];
        int[] blackPawnCnt = new int[8];
        // Most-advanced pawn per file for passed-pawn detection (O(1) check later).
        // White advances toward row 0; smallest row = most advanced.
        int[] wPawnFront = new int[8]; // min row of white pawn on file c; 8 if none
        // Black advances toward row 7; largest row = most advanced.
        int[] bPawnFront = new int[8]; // max row of black pawn on file c; -1 if none

        for (int i = 0; i < 8; i++) { wPawnFront[i] = 8; bPawnFront[i] = -1; }

        int whiteBishops = 0, blackBishops = 0;
        int wKr = 7, wKc = 4, bKr = 0, bKc = 4;
        int totalPieces  = 0;

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            var p = board.GetPiece(r, c);
            if (p == null) continue;

            bool isW = p.Color == PieceColor.White;

            if (p.Type != PieceType.King && p.Type != PieceType.Pawn)
                totalPieces += Material[(int)p.Type];

            switch (p.Type)
            {
                case PieceType.Pawn:
                    if (isW) { whitePawnCnt[c]++; if (r < wPawnFront[c]) wPawnFront[c] = r; }
                    else     { blackPawnCnt[c]++; if (r > bPawnFront[c]) bPawnFront[c] = r; }
                    break;
                case PieceType.Bishop:
                    if (isW) whiteBishops++; else blackBishops++;
                    break;
                case PieceType.King:
                    if (isW) { wKr = r; wKc = c; } else { bKr = r; bKc = c; }
                    break;
            }
        }

        bool isEndgame = totalPieces < 1300;

        // ── Pass 2: score ──
        int score = 0;

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            var p = board.GetPiece(r, c);
            if (p == null) continue;

            bool isW = p.Color == PieceColor.White;
            int  val = Material[(int)p.Type] + PstBonus(p, r, c, isEndgame);
            score += (isW == isWhiteTurn) ? val : -val;

            // Passed pawn bonus (O(1) using precomputed front rows)
            if (p.Type == PieceType.Pawn)
            {
                bool passed = true;
                if (isW)
                {
                    for (int fc = Math.Max(0,c-1); fc <= Math.Min(7,c+1); fc++)
                        if (bPawnFront[fc] < r) { passed = false; break; } // black pawn in front
                    if (passed)
                    {
                        int adv = (7 - r) switch { 1=>80, 2=>50, 3=>30, _=>15 };
                        score += isWhiteTurn ? adv : -adv;
                    }
                }
                else
                {
                    for (int fc = Math.Max(0,c-1); fc <= Math.Min(7,c+1); fc++)
                        if (wPawnFront[fc] > r) { passed = false; break; } // white pawn in front
                    if (passed)
                    {
                        int adv = r switch { 6=>80, 5=>50, 4=>30, _=>15 };
                        score += isWhiteTurn ? -adv : adv;
                    }
                }
            }

            // Rook on open / semi-open file
            if (p.Type == PieceType.Rook)
            {
                bool noFriendly = isW ? whitePawnCnt[c] == 0 : blackPawnCnt[c] == 0;
                bool noEnemy    = isW ? blackPawnCnt[c] == 0 : whitePawnCnt[c] == 0;
                int  bonus      = (noFriendly && noEnemy) ? 20 : noFriendly ? 10 : 0;
                score += (isW == isWhiteTurn) ? bonus : -bonus;
            }
        }

        // ── Bishop pair ──
        if (whiteBishops >= 2) score += isWhiteTurn ?  30 : -30;
        if (blackBishops >= 2) score += isWhiteTurn ? -30 :  30;

        // ── Pawn structure ──
        for (int c = 0; c < 8; c++)
        {
            if (whitePawnCnt[c] > 1) score += isWhiteTurn ? -(whitePawnCnt[c]-1)*20 :  (whitePawnCnt[c]-1)*20;
            if (blackPawnCnt[c] > 1) score += isWhiteTurn ?  (blackPawnCnt[c]-1)*20 : -(blackPawnCnt[c]-1)*20;

            bool wIso = whitePawnCnt[c]>0 && (c==0||whitePawnCnt[c-1]==0) && (c==7||whitePawnCnt[c+1]==0);
            bool bIso = blackPawnCnt[c]>0 && (c==0||blackPawnCnt[c-1]==0) && (c==7||blackPawnCnt[c+1]==0);
            if (wIso) score += isWhiteTurn ? -15 :  15;
            if (bIso) score += isWhiteTurn ?  15 : -15;
        }

        // ── King safety (middlegame only) ──
        if (!isEndgame)
        {
            int wSafe = 0, bSafe = 0;
            for (int dc = -1; dc <= 1; dc++)
            {
                int wfc = wKc + dc;
                if (wfc >= 0 && wfc < 8)
                {
                    var sh = wKr > 0 ? board.GetPiece(wKr-1, wfc) : null;
                    if (sh?.Type != PieceType.Pawn || sh.Color != PieceColor.White) wSafe -= 15;
                    if (whitePawnCnt[wfc] == 0) wSafe -= 20;
                }
                int bfc = bKc + dc;
                if (bfc >= 0 && bfc < 8)
                {
                    var sh = bKr < 7 ? board.GetPiece(bKr+1, bfc) : null;
                    if (sh?.Type != PieceType.Pawn || sh.Color != PieceColor.Black) bSafe -= 15;
                    if (blackPawnCnt[bfc] == 0) bSafe -= 20;
                }
            }
            score += isWhiteTurn ? (wSafe - bSafe) : (bSafe - wSafe);
        }

        return score;
    }

    private static int PstBonus(ChessPiece p, int row, int col, bool isEndgame)
    {
        int r = p.Color == PieceColor.White ? row : 7 - row;
        return p.Type switch
        {
            PieceType.Pawn   => PawnPst[r, col],
            PieceType.Knight => KnightPst[r, col],
            PieceType.Bishop => BishopPst[r, col],
            PieceType.Rook   => RookPst[r, col],
            PieceType.Queen  => QueenPst[r, col],
            PieceType.King   => isEndgame ? KingEndPst[r, col] : KingMidPst[r, col],
            _                => 0
        };
    }

    private static void OrderMoves(ChessBoard board, List<ChessMove> moves)
        => moves.Sort((a, b) => MvvLva(board, b).CompareTo(MvvLva(board, a)));

    private static int MvvLva(ChessBoard board, ChessMove m)
    {
        var victim   = board.GetPiece(m.ToRow, m.ToCol);
        var attacker = board.GetPiece(m.FromRow, m.FromCol);
        int score    = 0;
        if (victim != null && attacker != null)
            score += Material[(int)victim.Type] * 10 - Material[(int)attacker.Type];
        if (m.PromotionPiece.HasValue)
            score += Material[(int)m.PromotionPiece.Value];
        return score;
    }

    // ── Post-game analysis ───────────────────────────────────────────────────

    // 1-ply: rápido, usado pelo "Ver Análise".
    public static (ChessMove? BestMove, int CpLoss) AnalyzeMove(ChessBoard boardBefore, ChessMove playerMove)
    {
        var moves = ChessEngine.GetAllLegalMoves(boardBefore, boardBefore.CurrentTurn);
        if (moves.Count == 0) return (null, 0);

        ChessMove? bestMove        = null;
        int        bestScore       = int.MinValue;
        int        playerMoveScore = int.MinValue;
        string     playerUci       = MoveUci(playerMove);

        foreach (var m in moves)
        {
            var clone = boardBefore.Clone();
            ChessEngine.ApplyMove(clone, m);
            int score = -Evaluate(clone);
            if (score > bestScore) { bestScore = score; bestMove = m; }
            if (MoveUci(m) == playerUci) playerMoveScore = score;
        }

        int cpLoss = playerMoveScore == int.MinValue ? 0 : Math.Max(0, bestScore - playerMoveScore);
        return (bestMove, cpLoss);
    }

    // 3-ply Negamax + QS: preciso, usado pela "Revisão Completa".
    public static (ChessMove? BestMove, int CpLoss) AnalyzeMoveDeep(
        ChessBoard boardBefore, ChessMove playerMove, CancellationToken ct = default)
    {
        const int depth = 3;
        var moves = ChessEngine.GetAllLegalMoves(boardBefore, boardBefore.CurrentTurn);
        if (moves.Count == 0) return (null, 0);

        OrderMoves(boardBefore, moves);

        ChessMove? bestMove        = null;
        int        bestScore       = int.MinValue + 1;
        int        playerMoveScore = int.MinValue + 1;
        string     playerUci       = MoveUci(playerMove);

        foreach (var m in moves)
        {
            if (ct.IsCancellationRequested) break;
            var clone = boardBefore.Clone();
            ChessEngine.ApplyMove(clone, m);
            int score = -Negamax(clone, depth - 1, int.MinValue + 1, int.MaxValue - 1, ct);
            if (score > bestScore) { bestScore = score; bestMove = m; }
            if (MoveUci(m) == playerUci) playerMoveScore = score;
        }

        int cpLoss = playerMoveScore == int.MinValue + 1 ? 0 : Math.Max(0, bestScore - playerMoveScore);
        return (bestMove, cpLoss);
    }

    private static string MoveUci(ChessMove m) =>
        $"{(char)('a' + m.FromCol)}{8 - m.FromRow}{(char)('a' + m.ToCol)}{8 - m.ToRow}";

    internal static int EvaluateStatic(ChessBoard board) => Evaluate(board);
}
