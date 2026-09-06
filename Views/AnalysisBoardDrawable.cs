using ChessMAUI.Models;
using ChessMAUI.Services;

namespace ChessMAUI.Views;

public class AnalysisBoardDrawable : IDrawable
{
    public ChessBoard? Board { get; set; }

    private int   _h1r = -1, _h1c = -1, _h2r = -1, _h2c = -1;
    private Color _highlightColor = Colors.Transparent;

    // Actual-move arrow (red) — sequence badge "2"
    private int   _mvFr = -1, _mvFc = -1, _mvTr = -1, _mvTc = -1;
    private Color _moveArrowColor = Colors.Transparent;

    // Best-move arrow (green, dashed) — badge "✓"
    private int _arFr = -1, _arFc = -1, _arTr = -1, _arTc = -1;

    // Opponent move arrow (blue) — sequence badge "1"
    private int _opFr = -1, _opFc = -1, _opTr = -1, _opTc = -1;

    // Ghost piece at capture destination (captured piece shown semi-transparent)
    private ChessPiece? _ghostPiece;
    private int         _ghostRow = -1, _ghostCol = -1;

    public void SetHighlight(int fromRow, int fromCol, int toRow, int toCol, Color color)
    {
        _h1r = fromRow; _h1c = fromCol;
        _h2r = toRow;   _h2c = toCol;
        _highlightColor = color;
    }

    public void ClearHighlight()
    {
        _h1r = _h1c = _h2r = _h2c = -1;
        _highlightColor = Colors.Transparent;
    }

    public void SetMoveArrow(int fromRow, int fromCol, int toRow, int toCol, Color color)
        => (_mvFr, _mvFc, _mvTr, _mvTc, _moveArrowColor) = (fromRow, fromCol, toRow, toCol, color);

    public void ClearMoveArrow() { _mvFr = _mvFc = _mvTr = _mvTc = -1; _moveArrowColor = Colors.Transparent; }

    public void SetBestMoveArrow(int fromRow, int fromCol, int toRow, int toCol)
        => (_arFr, _arFc, _arTr, _arTc) = (fromRow, fromCol, toRow, toCol);

    public void ClearBestMoveArrow() => _arFr = _arFc = _arTr = _arTc = -1;

    public void SetOpponentArrow(int fromRow, int fromCol, int toRow, int toCol)
        => (_opFr, _opFc, _opTr, _opTc) = (fromRow, fromCol, toRow, toCol);

    public void ClearOpponentArrow() => _opFr = _opFc = _opTr = _opTc = -1;

    public void SetCapturedGhost(int row, int col, ChessPiece piece)
        => (_ghostRow, _ghostCol, _ghostPiece) = (row, col, piece);

    public void ClearCapturedGhost() { _ghostRow = _ghostCol = -1; _ghostPiece = null; }

    public void Draw(ICanvas canvas, RectF bounds)
    {
        if (Board == null) return;

        var (lightColor, darkColor) = BoardThemeService.BoardColors;
        float cw       = bounds.Width  / 8f;
        float ch       = bounds.Height / 8f;
        float fontSize = MathF.Min(cw, ch) * 0.76f;
        float off      = MathF.Max(1.5f, fontSize * 0.055f);

        canvas.Antialias = true;

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            bool  isLight = (r + c) % 2 == 0;
            float x = c * cw;
            float y = r * ch;

            canvas.FillColor = isLight ? lightColor : darkColor;
            canvas.FillRectangle(x, y, cw, ch);

            bool isH1 = r == _h1r && c == _h1c;
            bool isH2 = r == _h2r && c == _h2c;
            if ((isH1 || isH2) && _h1r >= 0)
            {
                canvas.FillColor = _highlightColor;
                canvas.FillRectangle(x, y, cw, ch);
            }

            // Ghost drawn after pieces loop — skip in tile loop

            var piece = Board?.GetPiece(r, c);
            if (piece == null) continue;
            DrawPieceSymbol(canvas, piece, x, y, cw, ch, fontSize, off, alphaMultiplier: 1.0f);
        }

        // ── Ghost captured piece (floating near destination) ─────────────────
        if (_ghostPiece != null && _ghostRow >= 0)
        {
            float gx = (_ghostCol + 1.0f) * cw;   // right edge of destination square
            float gy = (_ghostRow - 0.05f) * ch;  // slightly above destination square
            float gSize = cw * 0.72f;              // smaller than normal piece
            float gFont = fontSize * 0.72f;
            float gOff  = MathF.Max(1f, gFont * 0.05f);

            // Dark halo for legibility
            canvas.FillColor = Colors.Black.WithAlpha(0.45f);
            canvas.FillCircle(gx + gSize * 0.5f, gy + gSize * 0.5f, gSize * 0.56f);

            DrawPieceSymbol(canvas, _ghostPiece, gx, gy, gSize, gSize, gFont, gOff, alphaMultiplier: 0.90f);
        }

        // ── Arrows ───────────────────────────────────────────────────────────

        bool hasOpponent = _opFr >= 0;

        // Blue arrow: opponent's move — badge "1"
        if (hasOpponent)
        {
            var col = Color.FromArgb("#BB3399FF");
            DrawArrow(canvas, cw, ch,
                (_opFc + 0.5f) * cw, (_opFr + 0.5f) * ch,
                (_opTc + 0.5f) * cw, (_opTr + 0.5f) * ch,
                col, dashed: false);
            DrawBadge(canvas,
                (_opFc + 0.5f) * cw, (_opFr + 0.5f) * ch,
                cw * 0.17f, "1", col);
        }

        // Red arrow: human's actual move — badge "1" or "2" depending on context
        if (_mvFr >= 0)
        {
            string humanBadge = hasOpponent ? "2" : "1";
            DrawArrow(canvas, cw, ch,
                (_mvFc + 0.5f) * cw, (_mvFr + 0.5f) * ch,
                (_mvTc + 0.5f) * cw, (_mvTr + 0.5f) * ch,
                _moveArrowColor, dashed: false);
            DrawBadge(canvas,
                (_mvFc + 0.5f) * cw, (_mvFr + 0.5f) * ch,
                cw * 0.17f, humanBadge, _moveArrowColor);
        }

        // Green arrow: best alternative — dashed + badge "✓"
        if (_arFr >= 0)
        {
            var bestCol = Color.FromArgb("#DD28A745");
            DrawArrow(canvas, cw, ch,
                (_arFc + 0.5f) * cw, (_arFr + 0.5f) * ch,
                (_arTc + 0.5f) * cw, (_arTr + 0.5f) * ch,
                bestCol, dashed: true);
            DrawBadge(canvas,
                (_arFc + 0.5f) * cw, (_arFr + 0.5f) * ch,
                cw * 0.17f, "✓", bestCol);
        }
    }

    // ── Piece rendering ───────────────────────────────────────────────────────

    private static void DrawPieceSymbol(ICanvas canvas, ChessPiece piece,
        float x, float y, float cw, float ch, float fontSize, float off, float alphaMultiplier)
    {
        bool isWhite = piece.Color == PieceColor.White;
        canvas.FontSize = piece.Type == PieceType.King ? fontSize * 1.20f : fontSize;

        if (isWhite)
        {
            canvas.FontColor = Colors.Black.WithAlpha(0.30f * alphaMultiplier);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                canvas.DrawString(piece.Symbol,
                    x + dx * off * 1.8f, y + dy * off * 1.8f, cw, ch,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            canvas.FontColor = Color.FromArgb("#F5F0DC").WithAlpha(alphaMultiplier);
            canvas.DrawString(piece.Symbol, x, y, cw, ch,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        else
        {
            canvas.FontColor = Colors.White.WithAlpha(0.78f * alphaMultiplier);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                canvas.DrawString(piece.Symbol,
                    x + dx * off * 1.2f, y + dy * off * 1.2f, cw, ch,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            canvas.FontColor = Color.FromArgb("#1C1C2C").WithAlpha(alphaMultiplier);
            canvas.DrawString(piece.Symbol, x, y, cw, ch,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    // ── Arrow drawing ─────────────────────────────────────────────────────────

    private static void DrawArrow(ICanvas canvas, float cw, float ch,
        float sx, float sy, float ex, float ey, Color color, bool dashed)
    {
        float dx  = ex - sx;
        float dy  = ey - sy;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;

        float nx = dx / len;
        float ny = dy / len;

        float headLen  = cw * 0.38f;
        float headAngle = 0.42f;
        float lineEnd  = len - headLen * 0.65f;

        canvas.Antialias  = true;
        canvas.StrokeColor = color;
        canvas.StrokeSize  = cw * 0.13f;

        if (dashed)
        {
            // Draw as dashes along the shaft
            float dashLen = cw * 0.14f;
            float gapLen  = cw * 0.09f;
            float pos     = 0f;
            while (pos < lineEnd)
            {
                float segEnd = MathF.Min(pos + dashLen, lineEnd);
                canvas.DrawLine(sx + nx * pos, sy + ny * pos,
                                sx + nx * segEnd, sy + ny * segEnd);
                pos += dashLen + gapLen;
            }
        }
        else
        {
            canvas.DrawLine(sx, sy, sx + nx * lineEnd, sy + ny * lineEnd);
        }

        // Arrowhead (always solid)
        var path = new PathF();
        path.MoveTo(ex, ey);
        path.LineTo(ex - headLen * MathF.Cos(MathF.Atan2(dy, dx) - headAngle),
                    ey - headLen * MathF.Sin(MathF.Atan2(dy, dx) - headAngle));
        path.LineTo(ex - headLen * MathF.Cos(MathF.Atan2(dy, dx) + headAngle),
                    ey - headLen * MathF.Sin(MathF.Atan2(dy, dx) + headAngle));
        path.Close();
        canvas.FillColor = color;
        canvas.FillPath(path);
    }

    private static void DrawBadge(ICanvas canvas, float cx, float cy,
        float radius, string label, Color bgColor)
    {
        canvas.FillColor = bgColor;
        canvas.FillCircle(cx, cy, radius);

        // White border for legibility
        canvas.StrokeColor = Colors.White.WithAlpha(0.7f);
        canvas.StrokeSize  = radius * 0.25f;
        canvas.DrawCircle(cx, cy, radius);

        canvas.FontColor = Colors.White;
        canvas.FontSize  = radius * 1.5f;
        canvas.DrawString(label,
            cx - radius, cy - radius, radius * 2, radius * 2,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
