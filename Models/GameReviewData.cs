using ChessMAUI.ViewModels;
using ChessMAUI.Services;

namespace ChessMAUI.Models;

public class GameReviewData
{
    public string  WhitePlayerName { get; set; } = "Brancas";
    public string  BlackPlayerName { get; set; } = "Pretas";
    public bool?   HumanWon        { get; set; }
    public bool    HumanIsWhite    { get; set; } = true;
    public List<ReviewMoveRecord> Moves       { get; set; } = [];
    public List<int>              EvalHistory { get; set; } = [];
    public List<KeyMoment>        KeyMoments  { get; set; } = [];
    public string                 MainLesson  { get; set; } = "";

    public double WhiteAccuracy => ComputeAccuracy(true);
    public double BlackAccuracy => ComputeAccuracy(false);

    public int WhiteBlunders     => Moves.Count(m =>  m.IsWhite && m.Quality == GameViewModel.MoveQuality.Blunder);
    public int WhiteMistakes     => Moves.Count(m =>  m.IsWhite && m.Quality == GameViewModel.MoveQuality.Mistake);
    public int WhiteInaccuracies => Moves.Count(m =>  m.IsWhite && m.Quality == GameViewModel.MoveQuality.Inaccuracy);
    public int BlackBlunders     => Moves.Count(m => !m.IsWhite && m.Quality == GameViewModel.MoveQuality.Blunder);
    public int BlackMistakes     => Moves.Count(m => !m.IsWhite && m.Quality == GameViewModel.MoveQuality.Mistake);
    public int BlackInaccuracies => Moves.Count(m => !m.IsWhite && m.Quality == GameViewModel.MoveQuality.Inaccuracy);

    private double ComputeAccuracy(bool isWhite)
    {
        var playerMoves = Moves.Where(m => m.IsWhite == isWhite).ToList();
        if (playerMoves.Count == 0) return 100.0;
        return Math.Round(playerMoves.Average(m => m.AccuracyPoints), 1);
    }

    public static List<KeyMoment> IdentifyKeyMoments(List<ReviewMoveRecord> moves, bool humanIsWhite = true)
    {
        if (moves.Count == 0) return [];

        // ── Helpers ───────────────────────────────────────────────────

        static string PieceName(PieceType t) => t switch
        {
            PieceType.King   => "rei",
            PieceType.Queen  => "rainha",
            PieceType.Rook   => "torre",
            PieceType.Bishop => "bispo",
            PieceType.Knight => "cavalo",
            _                => "peão"
        };

        static Color QColor(GameViewModel.MoveQuality q) => q switch
        {
            GameViewModel.MoveQuality.Blunder    => Color.FromArgb("#FF4444"),
            GameViewModel.MoveQuality.Mistake    => Color.FromArgb("#FF7744"),
            GameViewModel.MoveQuality.Inaccuracy => Color.FromArgb("#FFD700"),
            _                                    => Color.FromArgb("#4CAF50")
        };

        static string QIcon(GameViewModel.MoveQuality q) => q switch
        {
            GameViewModel.MoveQuality.Blunder    => "✗✗",
            GameViewModel.MoveQuality.Mistake    => "✗",
            _                                    => "?!"
        };

        // Detects net material loss for the moving player (lost a piece without compensation)
        static bool LostMaterial(ReviewMoveRecord m)
        {
            static int PieceValue(PieceType t) => t switch
            {
                PieceType.Queen  => 900,
                PieceType.Rook   => 500,
                PieceType.Bishop => 330,
                PieceType.Knight => 320,
                PieceType.Pawn   => 100,
                _                => 0
            };
            int Sum(ChessBoard b, bool white)
            {
                int v = 0;
                PieceColor col = white ? PieceColor.White : PieceColor.Black;
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++)
                    {
                        var p = b.GetPiece(r, c);
                        if (p != null && p.Color == col) v += PieceValue(p.Type);
                    }
                return v;
            }
            return Sum(m.BoardAfter, m.IsWhite) < Sum(m.BoardBefore, m.IsWhite) - 150;
        }

        string BestHint(ReviewMoveRecord m)
        {
            if (m.BestMove == null) return "";
            bool isSame = m.BestMove.FromRow == m.Move.FromRow
                       && m.BestMove.FromCol == m.Move.FromCol
                       && m.BestMove.ToRow   == m.Move.ToRow
                       && m.BestMove.ToCol   == m.Move.ToCol;
            if (isSame) return "";
            if (m.BestMove.IsCastling)
                return " O roque era a jogada correta aqui.";
            var mover    = m.BoardBefore.GetPiece(m.BestMove.FromRow, m.BestMove.FromCol);
            var captured = m.BoardBefore.GetPiece(m.BestMove.ToRow,   m.BestMove.ToCol);
            if (mover == null) return "";
            string mn = PieceName(mover.Type);
            if (captured != null)
                return $" A jogada correta era capturar o {PieceName(captured.Type)} com o {mn}.";
            return mover.Type switch
            {
                PieceType.Knight => $" O {mn} deveria ter saltado para uma casa mais ativa.",
                PieceType.Pawn   => " O peão deveria ter avançado.",
                _                => $" A melhor opção era reposicionar o {mn}."
            };
        }

        string BuildComment(ReviewMoveRecord m)
        {
            string who  = m.IsWhite ? "Brancas" : "Pretas";
            string best = BestHint(m);
            var piece   = m.BoardBefore.GetPiece(m.Move.FromRow, m.Move.FromCol);
            string pn   = piece != null ? PieceName(piece.Type) : "peça";
            bool matLoss = LostMaterial(m);
            bool toEdge  = piece?.Type == PieceType.Knight
                        && (m.Move.ToCol == 0 || m.Move.ToCol == 7
                         || m.Move.ToRow == 0 || m.Move.ToRow == 7);
            string phase = m.HalfMoveIndex < 16 ? "abertura"
                         : m.HalfMoveIndex < 40 ? "meio-jogo" : "final";
            var captured = m.BoardBefore.GetPiece(m.Move.ToRow, m.Move.ToCol);

            if (m.Quality == GameViewModel.MoveQuality.Blunder)
            {
                if (matLoss && piece?.Type == PieceType.Queen)
                    return $"A dama das {who} foi deixada sem proteção e capturada — perda decisiva de força ofensiva.{best}";
                if (matLoss && piece?.Type == PieceType.Rook)
                    return $"A torre das {who} ficou pendurada e foi perdida sem qualquer compensação posicional.{best}";
                if (matLoss && piece?.Type == PieceType.Knight)
                    return $"O cavalo das {who} foi movido para uma casa sem defensor e foi capturado gratuitamente.{best}";
                if (matLoss && piece?.Type == PieceType.Bishop)
                    return $"O bispo das {who} ficou exposto e foi capturado — as pretas perderam o par de bispos.{best}";
                if (matLoss)
                    return $"O {pn} das {who} ficou sem proteção após esse lance e foi capturado na sequência.{best}";
                if (toEdge)
                    return $"Cavalo das {who} recuado para a borda perde influência central — vantagem cedida ao oponente.{best}";
                if (phase == "abertura")
                    return $"Lance de desenvolvimento equivocado das {who}: criou fraqueza na estrutura antes do roque.{best}";
                if (captured != null)
                    return $"Troca precipitada das {who}: capturou o {PieceName(captured.Type)} mas abriu linha de ataque ao próprio rei.{best}";
                return $"As {who} perderam o controle da posição com esse lance — a avaliação virou decisivamente.{best}";
            }

            if (m.Quality == GameViewModel.MoveQuality.Mistake)
            {
                if (matLoss)
                    return $"O {pn} das {who} ficou sem defensor após o recuo — perdeu uma peça desnecessariamente.{best}";
                if (toEdge)
                    return $"Cavalo das {who} deslocado para a borda: de 8 casas possíveis no centro, passa a controlar apenas 2.{best}";
                if (phase == "abertura")
                    return $"Desenvolvimento atrasado das {who}: mover a mesma peça duas vezes na abertura cede tempo ao oponente.{best}";
                if (captured != null)
                    return $"Troca desvantajosa das {who}: cedeu o {PieceName(captured.Type)} por uma peça de menor valor.{best}";
                if (phase == "final")
                    return $"No final de jogo, as {who} não ativaram o rei a tempo — peça ociosa em posição decisiva.{best}";
                return $"Lance passivo das {who}: cedeu a iniciativa sem necessidade e o oponente consolidou a vantagem.{best}";
            }

            // Turning point / good move
            if (captured != null)
                return $"Lance decisivo das {who}: captura do {PieceName(captured.Type)} garante vantagem difícil de reverter.";
            if (m.Move.IsCastling)
                return $"Roque executado no momento certo pelas {who} — rei protegido e torres conectadas.";
            return $"Lance preciso das {who} que consolidou a vantagem posicional de forma duradoura.";
        }

        // Capture of enemy piece without losing material (good capture that Stockfish may
        // over-penalise at lower depth because the capturing piece is temporarily attacked)
        static bool IsGoodCapture(ReviewMoveRecord m)
        {
            PieceColor enemyCol = m.IsWhite ? PieceColor.Black : PieceColor.White;
            var captured = m.BoardBefore.GetPiece(m.Move.ToRow, m.Move.ToCol);
            return captured?.Color == enemyCol && !LostMaterial(m);
        }

        // ── Seleção de candidatos ──────────────────────────────────────
        // Pega os 12 lances humanos com maior cpLoss (≥ 40), ordenados cronologicamente.
        // Claude seleciona 4-6 mais instrutivos. Sem filtro por qualidade aqui para garantir
        // volume suficiente mesmo em partidas com poucos erros graves.
        const int MaxMoments = 12;
        var chosen = moves
            .Select((m, i) => (m, i))
            .Where(x =>
                x.m.IsWhite == humanIsWhite
                && x.m.Quality != GameViewModel.MoveQuality.Good  // lances bons nunca são momentos-chave
                && x.m.CpLoss >= 40)
            .OrderByDescending(x => x.m.CpLoss)
            .Take(MaxMoments)
            .OrderBy(x => x.i)
            .ToList();


        // ── Constrói os KeyMoments ─────────────────────────────────────
        return chosen.Select(x =>
        {
            var  m     = x.m;
            string title = m.Quality switch
            {
                GameViewModel.MoveQuality.Blunder    => "Erro Grave",
                GameViewModel.MoveQuality.Mistake    => "Erro",
                GameViewModel.MoveQuality.Inaccuracy => "Imprecisão",
                _                                    => "Lance Decisivo"
            };

            return new KeyMoment
            {
                MoveIndex   = x.i,
                Title       = title,
                Icon        = QIcon(m.Quality),
                AccentColor = QColor(m.Quality),
                Commentary  = BuildComment(m)
            };
        }).ToList();
    }
}

public class ReviewMoveRecord
{
    public int        HalfMoveIndex { get; set; }
    public int        MoveNumber    => HalfMoveIndex / 2 + 1;
    public bool       IsWhite       => HalfMoveIndex % 2 == 0;
    public ChessBoard BoardBefore   { get; set; } = null!;
    public ChessBoard BoardAfter    { get; set; } = null!;
    public ChessMove  Move          { get; set; } = null!;
    public string     Notation      { get; set; } = "";
    public int        Evaluation    { get; set; }
    public int        CpLoss        { get; set; }
    public GameViewModel.MoveQuality Quality  { get; set; } = GameViewModel.MoveQuality.Good;
    public ChessMove? BestMove      { get; set; }
    public string?    BestMoveUci   { get; set; }
    public string     Comment       { get; set; } = "";

    public double AccuracyPoints =>
        Math.Max(0.0, Math.Min(100.0, 103.1668 * Math.Exp(-0.04354 * CpLoss / 10.0) - 3.1669));

    public string QualityBadge => Quality switch
    {
        GameViewModel.MoveQuality.Blunder    => "✗✗",
        GameViewModel.MoveQuality.Mistake    => "✗",
        GameViewModel.MoveQuality.Inaccuracy => "?!",
        _                                    => ""
    };

    public Color QualityColor => Quality switch
    {
        GameViewModel.MoveQuality.Blunder    => Color.FromArgb("#FF4444"),
        GameViewModel.MoveQuality.Mistake    => Color.FromArgb("#FF7744"),
        GameViewModel.MoveQuality.Inaccuracy => Color.FromArgb("#FFD700"),
        _                                    => Color.FromArgb("#4CAF50")
    };
}

public class KeyMoment
{
    public int    MoveIndex   { get; init; }
    public string Title       { get; init; } = "";
    public string Commentary  { get; set;  } = "";
    public Color  AccentColor { get; init; } = Colors.Gray;
    public string Icon        { get; init; } = "";
}
