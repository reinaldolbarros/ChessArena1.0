namespace ChessMAUI.Models;

public class ChessMove
{
    public int FromRow { get; }
    public int FromCol { get; }
    public int ToRow { get; }
    public int ToCol { get; }
    public PieceType? PromotionPiece { get; set; }
    public bool IsEnPassant { get; set; }
    public bool IsCastling { get; set; }

    public ChessMove(int fromRow, int fromCol, int toRow, int toCol)
    {
        FromRow = fromRow;
        FromCol = fromCol;
        ToRow = toRow;
        ToCol = toCol;
    }

    public override bool Equals(object? obj)
    {
        if (obj is ChessMove other)
            return FromRow == other.FromRow && FromCol == other.FromCol &&
                   ToRow == other.ToRow && ToCol == other.ToCol;
        return false;
    }

    public override int GetHashCode() =>
        HashCode.Combine(FromRow, FromCol, ToRow, ToCol);
}
