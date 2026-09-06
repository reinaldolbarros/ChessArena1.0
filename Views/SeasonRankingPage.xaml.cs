using ChessMAUI.Models;

namespace ChessMAUI.Views;

public partial class SeasonRankingPage : ContentPage
{
    public SeasonRankingPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var state = AppState.Current;

        SeasonLabel.Text    = state.Season.CurrentSeasonLabel;
        var board           = state.Season.GetLeaderboard(state.Titles, state.Profile);
        var human           = board.FirstOrDefault(e => e.IsHuman);
        MyPointsLabel.Text  = state.Season.CurrentPoints.ToString("N0");
        MyPositionLabel.Text = human != null ? $"{human.PositionLabel} no ranking" : "—";

        BuildList(board, human);
    }

    private void BuildList(List<SeasonEntry> board, SeasonEntry? human)
    {
        RankingList.Children.Clear();
        bool separatorAdded = false;

        foreach (var e in board)
        {
            if (e.Position > 20 && !e.IsHuman) continue;

            if (e.IsHuman && e.Position > 20 && !separatorAdded)
            {
                separatorAdded = true;
                RankingList.Children.Add(new Label
                {
                    Text = "·  ·  ·", TextColor = Color.FromArgb("#3A5070"),
                    HorizontalTextAlignment = TextAlignment.Center,
                    FontSize = 12, Margin = new Thickness(0, 4)
                });
            }

            bool isPodium = e.Position <= 3;
            string bgHex  = e.IsHuman ? "#1C2A0A"
                          : e.Position == 1 ? "#1A1400"
                          : e.Position == 2 ? "#111118"
                          : e.Position == 3 ? "#140C00" : "#0D1828";

            var card = new Border
            {
                BackgroundColor = Color.FromArgb(bgHex),
                Stroke          = new SolidColorBrush(e.IsHuman
                    ? Color.FromArgb("#2A5020")
                    : isPodium ? Color.FromArgb("#2A2010") : Color.FromArgb("#1A2030")),
                StrokeThickness = e.IsHuman ? 1 : 0,
                StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Padding         = new Thickness(12, 7)
            };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new(36), new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto))
            };

            // Posição
            string medal = $"{e.Position}º";
            string posColor = e.Position switch { 1 => "#FFD700", 2 => "#C0C0D0", 3 => "#CD7F32", _ => "#607890" };
            row.Add(new Label
            {
                Text = medal, FontSize = 13,
                TextColor = Color.FromArgb(posColor),
                HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center
            });

            // Avatar
            var avatarLbl = new Label
            {
                Text = e.Avatar, FontSize = 14,
                Margin = new Thickness(4, 0), VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(avatarLbl, 1);
            row.Add(avatarLbl);

            // Nome + localização na mesma linha
            string loc    = e.LocationLabel;
            var nameLbl   = new Label { VerticalOptions = LayoutOptions.Center };
            var formatted = new FormattedString();
            formatted.Spans.Add(new Span
            {
                Text           = e.Name,
                TextColor      = e.IsHuman ? Color.FromArgb("#4CAF50") : Colors.White,
                FontSize       = 12,
                FontAttributes = e.IsHuman ? FontAttributes.Bold : FontAttributes.None
            });
            if (!string.IsNullOrEmpty(loc))
                formatted.Spans.Add(new Span
                {
                    Text      = $"  {loc}",
                    TextColor = Color.FromArgb("#6080A0"),
                    FontSize  = 9
                });
            nameLbl.FormattedText = formatted;
            Grid.SetColumn(nameLbl, 2);
            row.Add(nameLbl);

            // Pontos
            string ptsColor = e.Position == 1 ? "#FFD700" : e.Position == 2 ? "#C0C0D0"
                            : e.Position == 3 ? "#CD7F32" : e.IsHuman ? "#4CAF50" : "#607890";
            var pts = new Label
            {
                Text = $"{e.Points:N0} pts",
                TextColor = Color.FromArgb(ptsColor),
                FontSize = 11,
                FontAttributes = FontAttributes.None,
                VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End
            };
            Grid.SetColumn(pts, 3);
            row.Add(pts);

            card.Content = row;
            RankingList.Children.Add(card);
        }
    }
}
