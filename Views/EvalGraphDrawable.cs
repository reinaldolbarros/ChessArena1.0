namespace ChessMAUI.Views;

/// <summary>
/// Draws the centipawn evaluation graph across the game.
/// Evals are from White's perspective; positive = White winning.
/// </summary>
public class EvalGraphDrawable : IDrawable
{
    public List<int>? Evals        { get; set; }
    public int        CurrentIndex { get; set; } = -1;

    private static readonly Color BgColor     = Color.FromArgb("#0D1117");
    private static readonly Color GridColor   = Color.FromArgb("#1A2535");
    private static readonly Color WhiteArea   = Color.FromArgb("#33C8A040");
    private static readonly Color BlackArea   = Color.FromArgb("#334060C0");
    private static readonly Color LineColor   = Color.FromArgb("#7AAEE8");
    private static readonly Color MarkerColor = Color.FromArgb("#FFBB44");

    private const float ClampCp = 800f;

    public void Draw(ICanvas canvas, RectF bounds)
    {
        float w    = bounds.Width;
        float h    = bounds.Height;
        float midY = h / 2f;

        // Background
        canvas.FillColor = BgColor;
        canvas.FillRectangle(bounds);

        // Horizontal grid lines at ±400cp
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize  = 0.5f;
        float y400w = midY - midY * (400f / ClampCp);
        float y400b = midY + midY * (400f / ClampCp);
        canvas.DrawLine(0, y400w, w, y400w);
        canvas.DrawLine(0, y400b, w, y400b);
        canvas.DrawLine(0, midY, w, midY);

        var evals = Evals;
        if (evals == null || evals.Count < 2) return;

        int n = evals.Count;
        float XAt(int i) => (i + 0.5f) / n * w;
        float YAt(int i) => midY - Math.Clamp(evals[i], -(int)ClampCp, (int)ClampCp) / ClampCp * midY;

        // Fill areas (white above center, black below)
        var whitePath = new PathF();
        whitePath.MoveTo(XAt(0), midY);
        for (int i = 0; i < n; i++)
        {
            float x = XAt(i);
            float y = YAt(i);
            whitePath.LineTo(x, Math.Min(y, midY));
        }
        whitePath.LineTo(XAt(n - 1), midY);
        whitePath.Close();
        canvas.FillColor = WhiteArea;
        canvas.FillPath(whitePath);

        var blackPath = new PathF();
        blackPath.MoveTo(XAt(0), midY);
        for (int i = 0; i < n; i++)
        {
            float x = XAt(i);
            float y = YAt(i);
            blackPath.LineTo(x, Math.Max(y, midY));
        }
        blackPath.LineTo(XAt(n - 1), midY);
        blackPath.Close();
        canvas.FillColor = BlackArea;
        canvas.FillPath(blackPath);

        // Eval line
        var linePath = new PathF();
        linePath.MoveTo(XAt(0), YAt(0));
        for (int i = 1; i < n; i++)
            linePath.LineTo(XAt(i), YAt(i));
        canvas.StrokeColor = LineColor;
        canvas.StrokeSize  = 1.5f;
        canvas.DrawPath(linePath);

        // Current move marker (vertical line + dot)
        if (CurrentIndex >= 0 && CurrentIndex < n)
        {
            float cx = XAt(CurrentIndex);
            float cy = YAt(CurrentIndex);
            canvas.StrokeColor = MarkerColor;
            canvas.StrokeSize  = 1f;
            canvas.DrawLine(cx, 0, cx, h);
            canvas.FillColor = MarkerColor;
            canvas.FillCircle(cx, cy, 3.5f);
        }
    }
}
