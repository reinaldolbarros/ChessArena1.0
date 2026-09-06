using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class DailyMissionsPage : ContentPage
{
    private const string ShownDateKey = "missions_shown_date";

    public DailyMissionsPage()
    {
        InitializeComponent();
    }

    public static bool ShouldShow()
    {
        var auth = AppState.Current.Auth;
        if (!auth.IsAuthenticated || auth.IsAnonymous) return false;

        string today = DateTime.Today.ToString("yyyy-MM-dd");
        return Preferences.Default.Get(ShownDateKey, "") != today;
    }

    public static void MarkShown() =>
        Preferences.Default.Set(ShownDateKey, DateTime.Today.ToString("yyyy-MM-dd"));

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        MarkShown();

        var state = AppState.Current;
        var daily = state.Daily;
        var stars = state.Stars;
        var prof  = state.Profile;

        // Saudação baseada no horário
        int hour = DateTime.Now.Hour;
        string period = hour < 12 ? "Bom dia" : hour < 18 ? "Boa tarde" : "Boa noite";
        GreetingLabel.Text = $"{period}, {prof.Name}!";
        StarsLabel.Text    = $"⭐ {stars.Balance} · {stars.DedicationTitle}";

        // Bônus diário
        bool claimed = daily.BonusClaimedToday;
        int streak   = daily.LoginStreak;
        BonusTitleLabel.Text    = claimed ? "Bônus Diário  ·  Resgatado ✓" : "Bônus Diário  ·  Disponível!";
        BonusTitleLabel.TextColor = claimed ? Color.FromArgb("#666688") : Color.FromArgb("#FFD700");
        BonusStreakLabel.Text   = $"Sequência: {streak} dia{(streak != 1 ? "s" : "")}";
        BonusFrame.Stroke       = new SolidColorBrush(claimed ? Color.FromArgb("#1A2840") : Color.FromArgb("#FFD700"));
        BonusBtn.IsVisible      = !claimed;

        BuildMissions(daily);

        // Animação de entrada
        this.Opacity = 0;
        await this.FadeTo(1, 350, Easing.CubicOut);
    }

    // ── Bônus ────────────────────────────────────────────────────────────────

    private async void OnBonusClicked(object? sender, EventArgs e)
    {
        var state = AppState.Current;
        int starsBonus  = state.Daily.ClaimDailyBonus();
        int starsStreak = state.Daily.TryClaimStreakMission();
        int total       = starsBonus + starsStreak;
        if (total > 0) state.Stars.Add(total);

        int    streak    = state.Daily.LoginStreak;
        string extraLine = starsStreak > 0 ? $"\nMissão de sequência completa! +{starsStreak} ⭐" : "";
        string next      = streak switch { >= 7 => "Máximo!", >= 5 => "7 dias = +5 ⭐", >= 3 => "5 dias", >= 2 => "3 dias", _ => "2 dias" };
        await DisplayAlert("Bônus Diário",
            $"+{starsBonus} ⭐  Sequência: {streak} dia{(streak != 1 ? "s" : "")}{extraLine}\nPróximo marco: {next}", "OK");

        // Atualiza UI
        StarsLabel.Text           = $"⭐ {state.Stars.Balance} · {state.Stars.DedicationTitle}";
        BonusTitleLabel.Text      = "Bônus Diário  ·  Resgatado ✓";
        BonusTitleLabel.TextColor = Color.FromArgb("#666688");
        BonusFrame.Stroke         = new SolidColorBrush(Color.FromArgb("#1A2840"));
        BonusBtn.IsVisible        = false;
        BuildMissions(state.Daily);
    }

    // ── Missões ───────────────────────────────────────────────────────────────

    private void BuildMissions(DailyService daily)
    {
        MissionContainer.Children.Clear();
        var missions = daily.GetMissions();

        foreach (var m in missions)
        {
            var card = new Border
            {
                BackgroundColor = m.Completed ? Color.FromArgb("#0E1E14") : Color.FromArgb("#0D1828"),
                Stroke          = new SolidColorBrush(m.Completed ? Color.FromArgb("#1A4030") : Color.FromArgb("#1A2840")),
                StrokeThickness = 1,
                StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding         = new Thickness(16, 14)
            };

            var grid = new Grid
            {
                ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }
            };

            // Info esquerda
            var info = new VerticalStackLayout { Spacing = 6 };

            var headerRow = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
            var descLabel = new Label
            {
                Text      = m.Description,
                TextColor = m.Completed ? Color.FromArgb("#4CAF50") : Colors.White,
                FontSize  = 15
            };
            var countLabel = new Label
            {
                Text              = $"{m.Progress}/{m.Target}",
                TextColor         = Color.FromArgb("#7A9AB8"),
                FontSize          = 13,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions   = LayoutOptions.Center
            };
            headerRow.Add(descLabel);
            headerRow.Add(countLabel);
            Grid.SetColumn(countLabel, 1);
            info.Add(headerRow);

            // Barra de progresso
            double screenW   = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density - 100;
            double pct       = m.Target > 0 ? Math.Min(1.0, (double)m.Progress / m.Target) : 0;
            var barTrack = new Grid { HeightRequest = 6 };
            barTrack.Add(new BoxView { Color = Color.FromArgb("#1A2840"), CornerRadius = 3, HorizontalOptions = LayoutOptions.Fill });
            barTrack.Add(new BoxView
            {
                Color             = m.Completed ? Color.FromArgb("#4CAF50") : Color.FromArgb("#3A6AB0"),
                CornerRadius      = 3,
                HorizontalOptions = LayoutOptions.Start,
                WidthRequest      = Math.Max(0, screenW * pct)
            });
            info.Add(barTrack);

            grid.Add(info);

            // Badge direita
            string badgeText = m.Rewarded ? "✓" : $"+{m.StarReward} ⭐";
            var badge = new Label
            {
                Text              = badgeText,
                TextColor         = m.Rewarded ? Color.FromArgb("#4CAF50") : Color.FromArgb("#C8A020"),
                FontSize          = 15,
                FontAttributes    = FontAttributes.Bold,
                VerticalOptions   = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End,
                Margin            = new Thickness(14, 0, 0, 0)
            };
            Grid.SetColumn(badge, 1);
            grid.Add(badge);

            card.Content = grid;
            MissionContainer.Children.Add(card);
        }
    }

    // ── Navegar ao lobby ──────────────────────────────────────────────────────

    private void OnPlayClicked(object? sender, EventArgs e)
    {
        if (Application.Current is not null)
            Application.Current.Windows[0].Page = new AppShell();
    }
}
