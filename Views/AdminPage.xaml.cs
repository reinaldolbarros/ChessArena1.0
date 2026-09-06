using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class AdminPage : ContentPage
{
    public AdminPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshUI();
    }

    private void RefreshUI()
    {
        var admin = AppState.Current.Admin;
        var history = admin.LoadHistory();

        TotalRakeLabel.Text  = $"$ {admin.TotalRake:N0}";
        TodayRakeLabel.Text  = $"$ {admin.RakeToday:N0}";
        WeekRakeLabel.Text   = $"$ {admin.RakeThisWeek:N0}";
        AvgRakeLabel.Text    = $"$ {admin.AverageRake:N0}";
        TotalTournsLabel.Text = history.Count.ToString();

        RakeList.Children.Clear();

        if (history.Count == 0)
        {
            RakeList.Children.Add(new Label
            {
                Text = "Nenhum torneio registrado ainda.",
                TextColor = Color.FromArgb("#555577"),
                FontSize = 13,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            });
            return;
        }

        foreach (var entry in history)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new(GridLength.Star),
                    new(72),
                    new(60),
                    new(60)
                },
                BackgroundColor = Color.FromArgb("#12172A"),
                Padding = new Thickness(8, 7)
            };

            // Data + descrição
            var info = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 1 };
            info.Add(new Label
            {
                Text      = entry.Description,
                TextColor = Colors.White,
                FontSize  = 11
            });
            info.Add(new Label
            {
                Text      = entry.Date.ToString("dd/MM  HH:mm"),
                TextColor = Color.FromArgb("#555577"),
                FontSize  = 9
            });
            row.Add(info);

            // Tipo
            string typeIcon = entry.TournType switch
            {
                "Bounty"     => "🎯",
                "Satellite"  => "🎟",
                "HeadsUp"    => "⚔",
                "Turbo"      => "⚡",
                "HyperTurbo" => "🔥",
                _            => "♟"
            };
            var typeLbl = new Label
            {
                Text = $"{typeIcon}\n{entry.TournType}",
                TextColor = Color.FromArgb("#AAAACC"),
                FontSize = 9,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(typeLbl, 1);
            row.Add(typeLbl);

            // Buy-in
            var buyInLbl = new Label
            {
                Text = $"$ {entry.BuyIn:N0}",
                TextColor = Color.FromArgb("#FFD700"),
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(buyInLbl, 2);
            row.Add(buyInLbl);

            // Rake
            var rakeLbl = new Label
            {
                Text = $"$ {entry.Amount:N0}",
                TextColor = Color.FromArgb("#4CAF50"),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(rakeLbl, 3);
            row.Add(rakeLbl);

            RakeList.Children.Add(row);

            // Separador
            RakeList.Children.Add(new BoxView
            {
                Color = Color.FromArgb("#1A2030"),
                HeightRequest = 1
            });
        }
    }
}
