using ChessMAUI.Models;

namespace ChessMAUI.Services;

public static class ChessEngine
{
    // -------------------------------------------------------------------------
    // Ponto de entrada: aplica um movimento no tabuleiro
    // -------------------------------------------------------------------------
    public static void ApplyMove(ChessBoard board, ChessMove move)
    {
        var piece = board.GetPiece(move.FromRow, move.FromCol)!;
        var captured = board.GetPiece(move.ToRow, move.ToCol);

        // Relógio de meio-movimento (regra dos 50 movimentos)
        if (piece.Type == PieceType.Pawn || captured != null)
            board.HalfMoveClock = 0;
        else
            board.HalfMoveClock++;

        // Atualiza en passant
        board.EnPassantRow = -1;
        board.EnPassantCol = -1;

        if (piece.Type == PieceType.Pawn && Math.Abs(move.ToRow - move.FromRow) == 2)
        {
            board.EnPassantRow = (move.FromRow + move.ToRow) / 2;
            board.EnPassantCol = move.FromCol;
        }

        // En passant: remove o peão capturado
        if (move.IsEnPassant)
            board.SetPiece(move.FromRow, move.ToCol, null);

        // Roque: move a torre
        if (move.IsCastling)
        {
            int rookFromCol = move.ToCol > move.FromCol ? 7 : 0;
            int rookToCol   = move.ToCol > move.FromCol ? move.ToCol - 1 : move.ToCol + 1;
            var rook = board.GetPiece(move.FromRow, rookFromCol)!;
            board.SetPiece(move.FromRow, rookToCol, rook);
            board.SetPiece(move.FromRow, rookFromCol, null);
            rook.HasMoved = true;
        }

        // Move a peça
        board.SetPiece(move.ToRow, move.ToCol, piece);
        board.SetPiece(move.FromRow, move.FromCol, null);
        piece.HasMoved = true;

        // Promoção de peão
        if (piece.Type == PieceType.Pawn && (move.ToRow == 0 || move.ToRow == 7))
        {
            var promotion = move.PromotionPiece ?? PieceType.Queen;
            board.SetPiece(move.ToRow, move.ToCol, new ChessPiece(promotion, piece.Color) { HasMoved = true });
        }

        // Alterna turno
        if (board.CurrentTurn == PieceColor.Black)
            board.FullMoveNumber++;
        board.CurrentTurn = board.CurrentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;

        // Registra posição para repetição tripla
        var key = BoardKey(board);
        board.PositionHistory.TryGetValue(key, out int count);
        board.PositionHistory[key] = count + 1;
    }

    // -------------------------------------------------------------------------
    // Movimentos legais (filtrados: não deixam o rei em xeque)
    // -------------------------------------------------------------------------
    public static List<ChessMove> GetLegalMoves(ChessBoard board, int row, int col)
    {
        var piece = board.GetPiece(row, col);
        if (piece == null || piece.Color != board.CurrentTurn)
            return [];

        var pseudo   = GeneratePseudoMoves(board, row, col);
        var legal    = new List<ChessMove>();
        var color    = piece.Color;
        var opponent = Opponent(color);

        foreach (var move in pseudo)
        {
            // Validação especial de roque:
            // 1) Rei não pode estar em xeque antes do roque
            // 2) Rei não pode passar pela casa intermediária sob ataque
            if (move.IsCastling)
            {
                if (IsInCheck(board, color)) continue;
                int midCol = (move.FromCol + move.ToCol) / 2;
                if (IsSquareAttacked(board, move.FromRow, midCol, opponent)) continue;
            }

            var clone    = board.Clone();
            ApplyMove(clone, move);
            var ourColor = clone.CurrentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;
            if (!IsInCheck(clone, ourColor))
                legal.Add(move);
        }

        return legal;
    }

    public static List<ChessMove> GetAllLegalMoves(ChessBoard board, PieceColor color)
    {
        var moves = new List<ChessMove>();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                if (board.GetPiece(r, c)?.Color == color)
                    moves.AddRange(GetLegalMoves(board, r, c));
        return moves;
    }

    // -------------------------------------------------------------------------
    // Estado do jogo
    // -------------------------------------------------------------------------
    public static bool IsInCheck(ChessBoard board, PieceColor color)
    {
        var (kingRow, kingCol) = board.FindKing(color);
        if (kingRow < 0) return false;
        return IsSquareAttacked(board, kingRow, kingCol, Opponent(color));
    }

    public static GameState GetGameState(ChessBoard board)
    {
        var color = board.CurrentTurn;
        bool inCheck = IsInCheck(board, color);
        bool hasLegal = GetAllLegalMoves(board, color).Count > 0;

        if (inCheck && !hasLegal) return GameState.Checkmate;
        if (!inCheck && !hasLegal) return GameState.Stalemate;
        if (inCheck) return GameState.Check;
        if (board.HalfMoveClock >= 100) return GameState.Draw;           // 50 lances
        if (IsInsufficientMaterial(board)) return GameState.Draw;         // material insuficiente
        if (board.PositionHistory.Values.Any(v => v >= 4)) return GameState.Draw; // repetição quádrupla
        return GameState.Normal;
    }

    public static bool IsInsufficientMaterial(ChessBoard board)
    {
        var pieces = new List<(PieceType type, PieceColor color)>();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var p = board.GetPiece(r, c);
                if (p != null) pieces.Add((p.Type, p.Color));
            }

        // Rei vs Rei
        if (pieces.Count == 2) return true;

        // Rei vs Rei + bispo ou cavaleiro
        if (pieces.Count == 3 && pieces.Any(p => p.type is PieceType.Bishop or PieceType.Knight))
            return true;

        // Rei + bispo vs Rei + bispo (mesma cor de casa)
        var bishops = pieces.Where(p => p.type == PieceType.Bishop).ToList();
        if (pieces.Count == 4 && bishops.Count == 2 &&
            pieces.Count(p => p.type == PieceType.King) == 2)
            return true;

        return false;
    }

    private static string BoardKey(ChessBoard board)
    {
        var sb = new System.Text.StringBuilder(70);
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var p = board.GetPiece(r, c);
                sb.Append(p == null ? '.' : (p.Color == PieceColor.White ? char.ToUpper(PieceChar(p.Type)) : char.ToLower(PieceChar(p.Type))));
            }
        sb.Append(board.CurrentTurn == PieceColor.White ? 'w' : 'b');
        sb.Append(board.EnPassantCol);
        return sb.ToString();
    }

    private static char PieceChar(PieceType t) => t switch
    {
        PieceType.Pawn   => 'p', PieceType.Knight => 'n', PieceType.Bishop => 'b',
        PieceType.Rook   => 'r', PieceType.Queen  => 'q', PieceType.King   => 'k',
        _ => '?'
    };

    // -------------------------------------------------------------------------
    // Gera movimentos pseudo-legais (sem verificar xeque)
    // -------------------------------------------------------------------------
    private static List<ChessMove> GeneratePseudoMoves(ChessBoard board, int row, int col)
    {
        var piece = board.GetPiece(row, col)!;
        return piece.Type switch
        {
            PieceType.Pawn   => PawnMoves(board, row, col, piece.Color),
            PieceType.Knight => KnightMoves(board, row, col, piece.Color),
            PieceType.Bishop => SlidingMoves(board, row, col, piece.Color, DiagonalDirs),
            PieceType.Rook   => SlidingMoves(board, row, col, piece.Color, StraightDirs),
            PieceType.Queen  => SlidingMoves(board, row, col, piece.Color, AllDirs),
            PieceType.King   => KingMoves(board, row, col, piece.Color),
            _ => []
        };
    }

    // -------------------------------------------------------------------------
    // Peão
    // -------------------------------------------------------------------------
    private static List<ChessMove> PawnMoves(ChessBoard board, int row, int col, PieceColor color)
    {
        var moves = new List<ChessMove>();
        int dir      = color == PieceColor.White ? -1 : 1;
        int startRow = color == PieceColor.White ? 6 : 1;

        // Avanço simples
        int nr = row + dir;
        if (ChessBoard.IsInBounds(nr, col) && board.GetPiece(nr, col) == null)
        {
            AddPawnMove(moves, row, col, nr, col);

            // Avanço duplo
            int nr2 = row + 2 * dir;
            if (row == startRow && board.GetPiece(nr2, col) == null)
                moves.Add(new ChessMove(row, col, nr2, col));
        }

        // Capturas diagonais
        foreach (int dc in new[] { -1, 1 })
        {
            int nc = col + dc;
            if (!ChessBoard.IsInBounds(nr, nc)) continue;

            var target = board.GetPiece(nr, nc);
            if (target != null && target.Color != color)
                AddPawnMove(moves, row, col, nr, nc);

            // En passant
            if (board.EnPassantRow == nr && board.EnPassantCol == nc)
                moves.Add(new ChessMove(row, col, nr, nc) { IsEnPassant = true });
        }

        return moves;
    }

    private static void AddPawnMove(List<ChessMove> moves, int fr, int fc, int tr, int tc)
    {
        if (tr == 0 || tr == 7)
        {
            foreach (var p in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight })
                moves.Add(new ChessMove(fr, fc, tr, tc) { PromotionPiece = p });
        }
        else
        {
            moves.Add(new ChessMove(fr, fc, tr, tc));
        }
    }

    // -------------------------------------------------------------------------
    // Cavalo
    // -------------------------------------------------------------------------
    private static readonly (int dr, int dc)[] KnightOffsets =
        [(-2,-1),(-2,1),(-1,-2),(-1,2),(1,-2),(1,2),(2,-1),(2,1)];

    private static List<ChessMove> KnightMoves(ChessBoard board, int row, int col, PieceColor color)
    {
        var moves = new List<ChessMove>();
        foreach (var (dr, dc) in KnightOffsets)
        {
            int nr = row + dr, nc = col + dc;
            if (ChessBoard.IsInBounds(nr, nc) && board.GetPiece(nr, nc)?.Color != color)
                moves.Add(new ChessMove(row, col, nr, nc));
        }
        return moves;
    }

    // -------------------------------------------------------------------------
    // Peças deslizantes (bispo, torre, rainha)
    // -------------------------------------------------------------------------
    private static readonly (int, int)[] DiagonalDirs = [(-1,-1),(-1,1),(1,-1),(1,1)];
    private static readonly (int, int)[] StraightDirs = [(-1,0),(1,0),(0,-1),(0,1)];
    private static readonly (int, int)[] AllDirs       = [(-1,-1),(-1,1),(1,-1),(1,1),(-1,0),(1,0),(0,-1),(0,1)];

    private static List<ChessMove> SlidingMoves(ChessBoard board, int row, int col,
        PieceColor color, (int dr, int dc)[] dirs)
    {
        var moves = new List<ChessMove>();
        foreach (var (dr, dc) in dirs)
        {
            int nr = row + dr, nc = col + dc;
            while (ChessBoard.IsInBounds(nr, nc))
            {
                var target = board.GetPiece(nr, nc);
                if (target == null)
                    moves.Add(new ChessMove(row, col, nr, nc));
                else
                {
                    if (target.Color != color)
                        moves.Add(new ChessMove(row, col, nr, nc));
                    break;
                }
                nr += dr; nc += dc;
            }
        }
        return moves;
    }

    // -------------------------------------------------------------------------
    // Rei
    // -------------------------------------------------------------------------

    // Apenas os 8 movimentos básicos — SEM roque e SEM chamar IsInCheck.
    // Usado por IsSquareAttacked para evitar recursão infinita.
    private static List<ChessMove> KingMovesBasic(ChessBoard board, int row, int col, PieceColor color)
    {
        var moves = new List<ChessMove>();
        foreach (var (dr, dc) in AllDirs)
        {
            int nr = row + dr, nc = col + dc;
            if (ChessBoard.IsInBounds(nr, nc) && board.GetPiece(nr, nc)?.Color != color)
                moves.Add(new ChessMove(row, col, nr, nc));
        }
        return moves;
    }

    // Movimentos completos do rei: básicos + roque (sem verificar xeque aqui).
    // A validação "rei não pode estar em xeque / não pode passar por xeque"
    // é feita em GetLegalMoves, que filtra cada pseudo-movimento.
    private static List<ChessMove> KingMoves(ChessBoard board, int row, int col, PieceColor color)
    {
        var moves = KingMovesBasic(board, row, col, color);

        var king = board.GetPiece(row, col)!;
        if (!king.HasMoved)
        {
            TryCastle(board, row, col, color, kingSide: true,  moves);
            TryCastle(board, row, col, color, kingSide: false, moves);
        }

        return moves;
    }

    private static void TryCastle(ChessBoard board, int row, int col, PieceColor color,
        bool kingSide, List<ChessMove> moves)
    {
        int rookCol  = kingSide ? 7 : 0;
        int kingDest = kingSide ? col + 2 : col - 2;

        var rook = board.GetPiece(row, rookCol);
        if (rook?.Type != PieceType.Rook || rook.HasMoved) return;

        // Verifica casas vazias entre rei e torre
        int start = kingSide ? col + 1 : 1;
        int end   = kingSide ? rookCol - 1 : col - 1;
        for (int c = start; c <= end; c++)
            if (board.GetPiece(row, c) != null) return;

        // Adiciona o pseudo-movimento; GetLegalMoves filtrará se passar por xeque
        moves.Add(new ChessMove(row, col, row, kingDest) { IsCastling = true });
    }

    // -------------------------------------------------------------------------
    // Verifica se uma casa está sob ataque
    // Usa GenerateAttackMoves (sem roque) para evitar recursão infinita.
    // -------------------------------------------------------------------------
    public static bool IsSquareAttacked(ChessBoard board, int row, int col, PieceColor byColor)
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var piece = board.GetPiece(r, c);
                if (piece == null || piece.Color != byColor) continue;

                var attacks = GenerateAttackMoves(board, r, c);
                if (attacks.Any(m => m.ToRow == row && m.ToCol == col))
                    return true;
            }
        return false;
    }

    // Pseudo-movimentos usados para calcular ataques: rei usa apenas KingMovesBasic
    private static List<ChessMove> GenerateAttackMoves(ChessBoard board, int row, int col)
    {
        var piece = board.GetPiece(row, col)!;
        return piece.Type switch
        {
            PieceType.Pawn   => PawnMoves(board, row, col, piece.Color),
            PieceType.Knight => KnightMoves(board, row, col, piece.Color),
            PieceType.Bishop => SlidingMoves(board, row, col, piece.Color, DiagonalDirs),
            PieceType.Rook   => SlidingMoves(board, row, col, piece.Color, StraightDirs),
            PieceType.Queen  => SlidingMoves(board, row, col, piece.Color, AllDirs),
            PieceType.King   => KingMovesBasic(board, row, col, piece.Color),
            _ => []
        };
    }

    private static PieceColor Opponent(PieceColor c) =>
        c == PieceColor.White ? PieceColor.Black : PieceColor.White;
}

public enum GameState
{
    Normal,
    Check,
    Checkmate,
    Stalemate,
    Draw
}
