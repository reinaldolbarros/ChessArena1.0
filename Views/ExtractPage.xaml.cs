using ChessMAUI.Models;

namespace ChessMAUI.Views;

public partial class ExtractPage : ContentPage
{
    private enum Filter { All, Gains, Losses }
    private Filter _filter = Filter.All;

    public ExtractPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BuildFilterBar();
        RefreshList();
    }

    private void BuildFilterBar()
    {
        FilterBar.Children.Clear();

        (string Label, Filter Value)[] options =
        [
            ("Todos",    Filter.All),
            ("Ganhos",   Filter.Gains),
            ("Perdas",   Filter.Losses),
        ];

        foreach (var (label, value) in options)
        {
            bool active = _filter == value;
            var btn = new Button
            {
                Text            = label,
                FontSize        = 12,
                CornerRadius    = 16,
                Padding         = new Thickness(16, 6),
                Margin          = new Thickness(0, 0, 6, 0),
                BackgroundColor = active ? Color.FromArgb("#0F3460") : Color.FromArgb("#252B45"),
                TextColor       = Colors.White,
                FontAttributes  = active ? FontAttributes.Bold : FontAttributes.None
            };
            var captured = value;
            btn.Clicked += (_, _) => { _filter = captured; BuildFilterBar(); RefreshList(); };
            FilterBar.Children.Add(btn);
        }
    }

    private void RefreshList()
    {
        var profile = AppState.Current.Profile;
        var all     = profile.GetPointTransactions();

        int totalGain = all.Where(t => t.Amount > 0).Sum(t => (int)t.Amount);
        int totalLoss = all.Where(t => t.Amount < 0).Sum(t => (int)Math.Abs(t.Amount));

        BalanceLabel.Text   = $"{profile.Points:N0}";
        TotalInLabel.Text   = $"+{totalGain}";
        TotalOutLabel.Text  = $"-{totalLoss}";

        var filtered = _filter switch
        {
            Filter.Gains  => all.Where(t => t.Amount > 0).ToList(),
            Filter.Losses => all.Where(t => t.Amount < 0).ToList(),
            _             => all
        };

        TransactionList.Children.Clear();

        if (filtered.Count == 0)
        {
            TransactionList.Children.Add(new Label
            {
                Text              = "Nenhuma movimentação encontrada.",
                TextColor         = Color.FromArgb("#555577"),
                FontSize          = 14,
                HorizontalOptions = LayoutOptions.Center,
                Margin            = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        var groups = filtered
            .GroupBy(t => t.Date.Date)
            .OrderByDescending(g => g.Key);

        bool alt = false;
        foreach (var group in groups)
        {
            TransactionList.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#12172A"),
                Stroke          = new SolidColorBrush(Color.FromArgb("#1E2A4A")),
                StrokeThickness = 1,
                StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 0 },
                Padding         = new Thickness(16, 6),
                Content         = new Label
                {
                    Text           = FormatDate(group.Key),
                    TextColor      = Color.FromArgb("#7788AA"),
                    FontSize       = 11,
                    FontAttributes = FontAttributes.Bold
                }
            });

            foreach (var tx in group.OrderByDescending(t => t.Date))
            {
                TransactionList.Children.Add(BuildRow(tx, alt));
                alt = !alt;
            }
        }
    }

    private static Grid BuildRow(TransactionEntry tx, bool alt)
    {
        bool isGain = tx.Amount >= 0;
        var  row    = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
            BackgroundColor   = alt ? Color.FromArgb("#16213E") : Color.FromArgb("#12192E"),
            Padding           = new Thickness(16, 10)
        };

        row.Add(new Label
        {
            Text            = string.IsNullOrEmpty(tx.Icon) ? (isGain ? "📈" : "📉") : tx.Icon,
            FontSize        = 20,
            VerticalOptions = LayoutOptions.Center,
            Margin          = new Thickness(0, 0, 12, 0)
        });

        var info = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2 };
        info.Add(new Label { Text = tx.Description, TextColor = Colors.White, FontSize = 13 });
        info.Add(new Label { Text = tx.Date.ToString("HH:mm"), TextColor = Color.FromArgb("#666688"), FontSize = 10 });
        Grid.SetColumn(info, 1);
        row.Add(info);

        var amountLbl = new Label
        {
            Text              = isGain ? $"+{(int)tx.Amount}" : $"−{(int)Math.Abs(tx.Amount)}",
            TextColor         = isGain ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF5252"),
            FontSize          = 14,
            FontAttributes    = FontAttributes.Bold,
            VerticalOptions   = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        Grid.SetColumn(amountLbl, 2);
        row.Add(amountLbl);

        return row;
    }

    private static string FormatDate(DateTime date)
    {
        if (date == DateTime.Today)             return "Hoje";
        if (date == DateTime.Today.AddDays(-1)) return "Ontem";
        return date.ToString("dd/MM/yyyy");
    }
}
