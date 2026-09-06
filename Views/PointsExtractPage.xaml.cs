namespace ChessMAUI.Views;

public partial class PointsExtractPage : ContentPage
{

    public PointsExtractPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        var profile = AppState.Current.Profile;
        var all     = profile.GetPointTransactions();

        TotalLabel.Text    = $"{profile.Points:N0}";
        WeekLabel.Text     = $"{profile.WeekPoints:N0}";
        TourneysLabel.Text = $"{profile.TournamentsWon}";

        ExtractList.Children.Clear();

        if (all.Count == 0)
        {
            ExtractList.Children.Add(new Label
            {
                Text              = "Nenhuma movimentação registrada ainda.",
                TextColor         = Color.FromArgb("#555577"),
                FontSize          = 12,
                HorizontalOptions = LayoutOptions.Center,
                Margin            = new Thickness(0, 14)
            });
            return;
        }

        bool alt = false;
        foreach (var group in all.GroupBy(t => t.Date.Date).OrderByDescending(g => g.Key))
        {
            ExtractList.Children.Add(new Label
            {
                Text = group.Key == DateTime.Today             ? "Hoje"
                     : group.Key == DateTime.Today.AddDays(-1) ? "Ontem"
                     : group.Key.ToString("dd/MM/yyyy"),
                TextColor       = Color.FromArgb("#444466"),
                FontSize        = 10,
                FontAttributes  = FontAttributes.Bold,
                Padding         = new Thickness(16, 5, 16, 3),
                BackgroundColor = Color.FromArgb("#0A0F1E")
            });

            foreach (var tx in group.OrderByDescending(t => t.Date))
            {
                bool isGain = tx.Amount >= 0;
                var row = new Grid
                {
                    ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
                    BackgroundColor   = alt ? Color.FromArgb("#14192E") : Color.FromArgb("#0E1322"),
                    Padding           = new Thickness(16, 9)
                };

                row.Add(new Label
                {
                    Text            = string.IsNullOrEmpty(tx.Icon) ? "⭐" : tx.Icon,
                    FontSize        = 18,
                    VerticalOptions = LayoutOptions.Center,
                    Margin          = new Thickness(0, 0, 12, 0)
                });

                var info = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2 };
                info.Add(new Label { Text = tx.Description, TextColor = Colors.White, FontSize = 13 });
                info.Add(new Label { Text = tx.Date.ToString("HH:mm"), TextColor = Color.FromArgb("#444466"), FontSize = 10 });
                Grid.SetColumn(info, 1);
                row.Add(info);

                var pts = new Label
                {
                    Text              = isGain ? $"+{(int)tx.Amount}" : $"−{(int)Math.Abs(tx.Amount)}",
                    TextColor         = isGain ? Color.FromArgb("#FFD700") : Color.FromArgb("#FF5252"),
                    FontSize          = 13,
                    FontAttributes    = FontAttributes.Bold,
                    VerticalOptions   = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.End
                };
                Grid.SetColumn(pts, 2);
                row.Add(pts);

                ExtractList.Children.Add(row);
                alt = !alt;
            }
        }
    }
}
