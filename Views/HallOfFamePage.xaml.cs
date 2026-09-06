using ChessMAUI.Models;

namespace ChessMAUI.Views;

public partial class HallOfFamePage : ContentPage
{
    public HallOfFamePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BuildList();
    }

    private void BuildList()
    {
        HofContainer.Children.Clear();
        var entries = AppState.Current.Season.GetHallOfFame();

        EmptyLabel.IsVisible = entries.Count == 0;

        foreach (var entry in entries)
        {
            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#0D1828"),
                Stroke          = new SolidColorBrush(Color.FromArgb("#B8860B")),
                StrokeThickness = 1,
                StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding         = new Thickness(16, 14)
            };

            var content = new VerticalStackLayout { Spacing = 6 };

            // Season label
            content.Add(new Label
            {
                Text = entry.SeasonLabel.ToUpper(),
                TextColor = Color.FromArgb("#C0A020"),
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 1.5
            });

            // Champion row
            var champRow = new HorizontalStackLayout { Spacing = 10 };
            champRow.Children.Add(new Label { Text = "🥇", FontSize = 24, VerticalOptions = LayoutOptions.Center });
            champRow.Children.Add(new Label { Text = entry.ChampionAvatar, FontSize = 24, VerticalOptions = LayoutOptions.Center });
            var champInfo = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 1 };
            champInfo.Add(new Label
            {
                Text = entry.ChampionName,
                TextColor = Colors.White,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold
            });
            champInfo.Add(new Label
            {
                Text = $"{entry.TitleLabel}  ·  {entry.Points:N0} pts",
                TextColor = Color.FromArgb("#C0A020"),
                FontSize = 11
            });
            champRow.Children.Add(champInfo);
            content.Add(champRow);

            // 2nd and 3rd
            var podium = new HorizontalStackLayout { Spacing = 20, Margin = new Thickness(0, 4, 0, 0) };
            if (!string.IsNullOrEmpty(entry.SecondName))
                podium.Children.Add(new Label
                {
                    Text = $"🥈 {entry.SecondName}",
                    TextColor = Color.FromArgb("#A0A0C0"),
                    FontSize = 12
                });
            if (!string.IsNullOrEmpty(entry.ThirdName))
                podium.Children.Add(new Label
                {
                    Text = $"🥉 {entry.ThirdName}",
                    TextColor = Color.FromArgb("#A08060"),
                    FontSize = 12
                });
            content.Add(podium);

            card.Content = content;
            HofContainer.Children.Add(card);
        }
    }
}
