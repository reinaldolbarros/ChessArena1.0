namespace ChessMAUI.Models;

public class ChessPiece
{
    public PieceType Type { get; set; }
    public PieceColor Color { get; set; }
    public bool HasMoved { get; set; }

    public ChessPiece(PieceType type, PieceColor color)
    {
        Type = type;
        Color = color;
        HasMoved = false;
    }

    public ChessPiece Clone() => new ChessPiece(Type, Color) { HasMoved = HasMoved };

    // Todos os tipos usam os símbolos sólidos — cor diferenciada pelo TextColor da Label
    public string Symbol => Type switch
    {
        PieceType.King   => "♚",
        PieceType.Queen  => "♛",
        PieceType.Rook   => "♜",
        PieceType.Bishop => "♝",
        PieceType.Knight => "♞",
        PieceType.Pawn   => "♟",
        _                => "?"
    };
}
