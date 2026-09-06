using ChessMAUI.Models;

namespace ChessMAUI.Views;

public partial class TournamentHistoryPage : ContentPage
{
    private enum TxFilter { All, Gains, Losses }
    private TxFilter _filter = TxFilter.All;

    public TournamentHistoryPage()
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

        int totalGains  = all.Where(t => t.Amount > 0).Sum(t => (int)t.Amount);
        int totalLosses = all.Where(t => t.Amount < 0).Sum(t => (int)Math.Abs(t.Amount));

        BalanceLabel.Text  = $"{profile.Points:N0}";
        CreditsLabel.Text  = $"+{totalGains}";
        DebitsLabel.Text   = $"-{totalLosses}";

        FilterBar.Children.Clear();
        (string Label, TxFilter Value)[] filters =
        [
            ("Todos",  TxFilter.All),
            ("Ganhos", TxFilter.Gains),
            ("Perdas", TxFilter.Losses),
        ];
        foreach (var (label, value) in filters)
        {
            bool active = _filter == value;
            var btn = new Button
            {
                Text            = label,
                FontSize        = 12,
                CornerRadius    = 16,
                Padding         = new Thickness(16, 6),
                Margin          = new Thickness(0, 0, 6, 0),
                BackgroundColor = active ? Color.FromArgb("#1A1400") : Color.FromArgb("#252B45"),
                TextColor       = active ? Color.FromArgb("#FFD700") : Colors.White,
                FontAttributes  = active ? FontAttributes.Bold : FontAttributes.None
            };
            var cap = value;
            btn.Clicked += (_, _) => { _filter = cap; Refresh(); };
            FilterBar.Children.Add(btn);
        }

        var filtered = _filter switch
        {
            TxFilter.Gains  => all.Where(t => t.Amount > 0).ToList(),
            TxFilter.Losses => all.Where(t => t.Amount < 0).ToList(),
            _               => all
        };

        TransactionList.Children.Clear();

        if (filtered.Count == 0)
        {
            TransactionList.Children.Add(new Label
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
        foreach (var group in filtered.GroupBy(t => t.Date.Date).OrderByDescending(g => g.Key))
        {
            TransactionList.Children.Add(new Label
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
                    Text             = string.IsNullOrEmpty(tx.Icon) ? (isGain ? "📈" : "📉") : tx.Icon,
                    FontSize         = 18,
                    VerticalOptions  = LayoutOptions.Center,
                    Margin           = new Thickness(0, 0, 12, 0)
                });

                var info = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2 };
                info.Add(new Label { Text = tx.Description, TextColor = Colors.White, FontSize = 13 });
                info.Add(new Label { Text = tx.Date.ToString("HH:mm"), TextColor = Color.FromArgb("#444466"), FontSize = 10 });
                Grid.SetColumn(info, 1);
                row.Add(info);

                var amt = new Label
                {
                    Text              = isGain ? $"+{(int)tx.Amount}" : $"−{(int)Math.Abs(tx.Amount)}",
                    TextColor         = isGain ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF5252"),
                    FontSize          = 13,
                    FontAttributes    = FontAttributes.Bold,
                    VerticalOptions   = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.End
                };
                Grid.SetColumn(amt, 2);
                row.Add(amt);

                TransactionList.Children.Add(row);
                alt = !alt;
            }
        }
    }
}
