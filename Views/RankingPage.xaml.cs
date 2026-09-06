using ChessMAUI.Models;
using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class RankingPage : ContentPage
{
    private bool _showWeekly = false;

    public RankingPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var svc = AppState.Current.Ranking;
        svc.RankingUpdated -= Refresh;
        svc.RankingUpdated += Refresh;
        Refresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AppState.Current.Ranking.RankingUpdated -= Refresh;
    }

    // -----------------------------------------------------------------------
    // Abas
    // -----------------------------------------------------------------------
    private void OnGlobalTab(object? sender, EventArgs e)
    {
        _showWeekly = false;
        GlobalTab.BackgroundColor = Color.FromArgb("#1A5276"); GlobalTab.TextColor = Colors.White;
        WeeklyTab.BackgroundColor = Color.FromArgb("#0F3460"); WeeklyTab.TextColor = Color.FromArgb("#AAAACC");
        Refresh();
    }

    private void OnWeeklyTab(object? sender, EventArgs e)
    {
        _showWeekly = true;
        WeeklyTab.BackgroundColor = Color.FromArgb("#1A5276"); WeeklyTab.TextColor = Colors.White;
        GlobalTab.BackgroundColor = Color.FromArgb("#0F3460"); GlobalTab.TextColor = Color.FromArgb("#AAAACC");
        Refresh();
    }

    private async void OnExtractClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("PointsExtractPage");

    // -----------------------------------------------------------------------
    // Atualiza lista
    // -----------------------------------------------------------------------
    private const int ListLimit = 20;

    private void Refresh()
    {
        var profile = AppState.Current.Profile;
        var svc     = AppState.Current.Ranking;
        var entries = _showWeekly ? svc.GetWeekly(profile) : svc.GetGlobal(profile);

        var displayed    = entries.Take(ListLimit).ToList();
        bool playerInList = displayed.Any(e => e.IsHuman);

        TableHeader.IsVisible = true;

        RankList.Children.Clear();
        foreach (var e in displayed)
            RankList.Children.Add(BuildRow(e, _showWeekly));

        var me = entries.First(e => e.IsHuman);
        MyPositionFrame.IsVisible = !playerInList;
        if (!playerInList)
        {
            MyPosLabel.Text    = me.PositionLabel;
            MyAvatarLabel.Text = me.Avatar;
            MyNameLabel.Text   = me.Name;
            MyPointsLabel.Text = _showWeekly ? $"{me.WeekPoints:N0}" : $"{me.Points:N0}";
        }
    }

    private static Grid BuildRow(RankingEntry e, bool weekly)
    {
        // Semanal: sem coluna de faixa (faixa é conceito global, baseado em pts totais)
        if (weekly)
        {
            var wrow = new Grid
            {
                ColumnDefinitions = { new(44), new(GridLength.Star), new(80) },
                BackgroundColor   = e.IsHuman ? Color.FromArgb("#1C2A0A") : e.RowColor,
                Padding           = new Thickness(10, 7)
            };

            wrow.Add(new Label
            {
                Text = e.PositionLabel, FontSize = e.Position <= 3 ? 16 : 13,
                TextColor = e.Position <= 3 ? Colors.White : Color.FromArgb("#AAAACC"),
                HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center
            });

            var wname = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
            wname.Add(new Label { Text = e.Avatar, FontSize = 14, VerticalOptions = LayoutOptions.Center });
            wname.Add(new Label { Text = e.Name, TextColor = e.NameColor, FontSize = 13,
                FontAttributes = e.IsHuman ? FontAttributes.Bold : FontAttributes.None,
                VerticalOptions = LayoutOptions.Center });
            Grid.SetColumn(wname, 1);
            wrow.Add(wname);

            var wpts = new Label
            {
                Text = $"{e.WeekPoints:N0}", TextColor = Color.FromArgb("#4CAF50"),
                FontSize = 13, FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(wpts, 2);
            wrow.Add(wpts);
            return wrow;
        }

        // Global
        var row = new Grid
        {
            ColumnDefinitions = { new(44), new(GridLength.Star), new(80) },
            BackgroundColor   = e.IsHuman ? Color.FromArgb("#1C2A0A") : e.RowColor,
            Padding           = new Thickness(10, 7)
        };

        row.Add(new Label
        {
            Text = e.PositionLabel, FontSize = e.Position <= 3 ? 16 : 13,
            TextColor = e.Position <= 3 ? Colors.White : Color.FromArgb("#AAAACC"),
            HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center
        });

        var nameStack = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        nameStack.Add(new Label { Text = e.Avatar, FontSize = 14, VerticalOptions = LayoutOptions.Center });
        nameStack.Add(new Label { Text = e.Name, TextColor = e.NameColor, FontSize = 13,
            FontAttributes = e.IsHuman ? FontAttributes.Bold : FontAttributes.None,
            VerticalOptions = LayoutOptions.Center });
        Grid.SetColumn(nameStack, 1);
        row.Add(nameStack);

        var ptsLbl = new Label
        {
            Text = $"{e.Points:N0}", TextColor = Color.FromArgb("#4CAF50"),
            FontSize = 13, FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(ptsLbl, 2);
        row.Add(ptsLbl);

        return row;
    }
}
