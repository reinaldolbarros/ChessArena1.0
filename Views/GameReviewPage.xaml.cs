using ChessMAUI.Models;
using ChessMAUI.ViewModels;

namespace ChessMAUI.Views;

public partial class GameReviewPage : ContentPage
{
    private GameReviewData?       _reviewData;
    private AnalysisBoardDrawable _boardDrawable = new();
    private EvalGraphDrawable     _evalDrawable  = new();

    private List<KeyMoment> _moments = [];
    private int             _currentIndex;
    // Total slots = moments + (1 lesson if available)
    private int _totalItems;

    public GameReviewPage()
    {
        InitializeComponent();
        ReviewBoard.Drawable = _boardDrawable;
        EvalGraph.Drawable   = _evalDrawable;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _reviewData = AppState.Current.PendingReviewData;
        if (_reviewData == null) return;
        PopulateUI();
    }

    private void PopulateUI()
    {
        var data = _reviewData!;

        // Header
        string result = data.HumanWon switch
        {
            true  => "Vitória",
            false => "Derrota",
            null  => "Partida"
        };
        HeaderSubtitle.Text = $"{data.WhitePlayerName} vs {data.BlackPlayerName} · {result}";
        AiBadge.IsVisible   = false;

        // Accuracy
        WhiteAccuracyLabel.Text = $"{data.WhiteAccuracy:F0}%";
        BlackAccuracyLabel.Text = $"{data.BlackAccuracy:F0}%";
        WhiteNameLabel.Text     = data.WhitePlayerName;
        BlackNameLabel.Text     = data.BlackPlayerName;

        WhiteBlunderLabel.Text = data.WhiteBlunders     > 0 ? $"✗✗ {data.WhiteBlunders}"    : "";
        WhiteMistakeLabel.Text = data.WhiteMistakes     > 0 ? $"✗ {data.WhiteMistakes}"      : "";
        WhiteInaccLabel.Text   = data.WhiteInaccuracies > 0 ? $"?! {data.WhiteInaccuracies}" : "";
        BlackBlunderLabel.Text = data.BlackBlunders     > 0 ? $"✗✗ {data.BlackBlunders}"    : "";
        BlackMistakeLabel.Text = data.BlackMistakes     > 0 ? $"✗ {data.BlackMistakes}"      : "";
        BlackInaccLabel.Text   = data.BlackInaccuracies > 0 ? $"?! {data.BlackInaccuracies}" : "";

        // Eval graph
        _evalDrawable.Evals = data.EvalHistory.Count > 0
            ? data.EvalHistory
            : data.Moves.Select(m => m.Evaluation).ToList();
        EvalGraph.Invalidate();

        LoadingOverlay.IsVisible = false;

        DebugErrorLabel.IsVisible = false;

        // Lesson text
        if (!string.IsNullOrWhiteSpace(data.MainLesson))
            LessonLabel.Text = data.MainLesson;

        // Set up navigation — sem card de resumo
        _moments    = data.KeyMoments;
        _totalItems = _moments.Count;

        if (_totalItems == 0)
        {
            NoErrorsLabel.IsVisible = true;
            NavRow.IsVisible        = false;
            MomentCard.IsVisible    = false;
            LessonCard.IsVisible    = false;
            ClearBoard();
            return;
        }

        NoErrorsLabel.IsVisible = false;
        NavRow.IsVisible        = true;
        _currentIndex           = 0;
        NavigateTo(0);
    }

    // ── Navegação ────────────────────────────────────────────────────────────

    private void NavigateTo(int index)
    {
        _currentIndex = Math.Clamp(index, 0, _totalItems - 1);
        bool isLesson = _currentIndex >= _moments.Count;

        MomentProgressLabel.Text = $"{_currentIndex + 1} / {_totalItems}";

        // Dim/highlight nav buttons
        PrevBtn.Opacity = _currentIndex > 0 ? 1.0 : 0.35;
        NextBtn.Opacity = _currentIndex < _totalItems - 1 ? 1.0 : 0.35;

        if (isLesson)
        {
            MomentCard.IsVisible = false;
            LessonCard.IsVisible = true;
        }
        else
        {
            LessonCard.IsVisible = false;
            var km = _moments[_currentIndex];
            ShowMomentCard(km);
            ShowMoveRecord(km.MoveIndex);
        }
    }

    private void ShowMomentCard(KeyMoment km)
    {
        MomentCard.IsVisible             = true;
        MomentCard.Stroke                = km.AccentColor.WithAlpha(0.5f);
        MomentAccentBar.BackgroundColor  = km.AccentColor;
        MomentIconBorder.Stroke          = km.AccentColor;
        MomentIconBorder.BackgroundColor = km.AccentColor.WithAlpha(0.15f);
        MomentIconLabel.Text             = km.Icon;
        MomentIconLabel.TextColor        = km.AccentColor;
        MomentTitleLabel.Text            = km.Title;
        MomentTitleLabel.TextColor       = km.AccentColor;
        MomentCommentLabel.Text          = km.Commentary;
    }

    private void OnPrevMoment(object? sender, EventArgs e)
    {
        if (_currentIndex > 0)
            NavigateTo(_currentIndex - 1);
    }

    private void OnNextMoment(object? sender, EventArgs e)
    {
        if (_currentIndex < _totalItems - 1)
            NavigateTo(_currentIndex + 1);
    }

    // ── Tabuleiro ─────────────────────────────────────────────────────────────

    private void OnReviewBoardSizeChanged(object? sender, EventArgs e)
    {
        if (ReviewBoard.Width > 0 && Math.Abs(ReviewBoard.HeightRequest - ReviewBoard.Width) > 2)
            ReviewBoard.HeightRequest = ReviewBoard.Width;
    }

    private void ClearBoard()
    {
        _boardDrawable.Board = null;
        _boardDrawable.ClearMoveArrow();
        _boardDrawable.ClearBestMoveArrow();
        _boardDrawable.ClearOpponentArrow();
        _boardDrawable.ClearCapturedGhost();
        ReviewBoard.Invalidate();
        MoveBadge.Text     = "";
        MoveInfoLabel.Text = "";
        EvalLabel.Text     = "";
    }

    private void ShowMoveRecord(int moveIdx)
    {
        if (_reviewData == null || moveIdx < 0 || moveIdx >= _reviewData.Moves.Count) return;

        var rec = _reviewData.Moves[moveIdx];

        _boardDrawable.Board = rec.BoardAfter;

        // Subtle square highlight (from/to)
        _boardDrawable.SetHighlight(
            rec.Move.FromRow, rec.Move.FromCol,
            rec.Move.ToRow,   rec.Move.ToCol,
            rec.QualityColor.WithAlpha(0.30f));

        // Ghost: captured piece shown transparently at destination
        var capturedPiece = rec.BoardBefore.GetPiece(rec.Move.ToRow, rec.Move.ToCol);
        if (capturedPiece != null && !rec.Move.IsCastling)
            _boardDrawable.SetCapturedGhost(rec.Move.ToRow, rec.Move.ToCol, capturedPiece);
        else
            _boardDrawable.ClearCapturedGhost();

        // Red arrow = the wrong move; green arrow = best move (set below)
        _boardDrawable.SetMoveArrow(
            rec.Move.FromRow, rec.Move.FromCol,
            rec.Move.ToRow,   rec.Move.ToCol,
            Color.FromArgb("#CCDD2222"));

        bool isBest = rec.BestMove == null
            || (rec.BestMove.FromRow == rec.Move.FromRow
             && rec.BestMove.FromCol == rec.Move.FromCol
             && rec.BestMove.ToRow   == rec.Move.ToRow
             && rec.BestMove.ToCol   == rec.Move.ToCol);

        if (!isBest && rec.BestMove != null)
            _boardDrawable.SetBestMoveArrow(
                rec.BestMove.FromRow, rec.BestMove.FromCol,
                rec.BestMove.ToRow,   rec.BestMove.ToCol);
        else
            _boardDrawable.ClearBestMoveArrow();

        // Seta azul: lance anterior do adversário (contexto para o comentário)
        if (moveIdx > 0)
        {
            var prev = _reviewData.Moves[moveIdx - 1];
            if (prev.IsWhite != rec.IsWhite) // é o adversário
                _boardDrawable.SetOpponentArrow(
                    prev.Move.FromRow, prev.Move.FromCol,
                    prev.Move.ToRow,   prev.Move.ToCol);
            else
                _boardDrawable.ClearOpponentArrow();
        }
        else
        {
            _boardDrawable.ClearOpponentArrow();
        }

        ReviewBoard.Invalidate();

        _evalDrawable.CurrentIndex = moveIdx;
        EvalGraph.Invalidate();

        MoveBadge.Text      = rec.QualityBadge == "" ? "·" : rec.QualityBadge;
        MoveBadge.TextColor = rec.QualityColor;

        string pfx         = rec.IsWhite ? $"{rec.MoveNumber}." : $"{rec.MoveNumber}...";
        MoveInfoLabel.Text = $"{pfx} {rec.Notation}";

        EvalLabel.Text      = FormatEval(rec.Evaluation);
        EvalLabel.TextColor = rec.Evaluation >= 0
            ? Color.FromArgb("#C8A040")
            : Color.FromArgb("#9AAED8");
    }

    private static string FormatEval(int cp)
    {
        if (cp >=  29_000) return "#";
        if (cp <= -29_000) return "#";
        double pawns = cp / 100.0;
        return pawns >= 0 ? $"+{pawns:F1}" : $"{pawns:F1}";
    }

    private void OnBackTapped(object sender, EventArgs e)
        => Shell.Current.GoToAsync("..");
}
