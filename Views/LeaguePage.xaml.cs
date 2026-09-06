using ChessMAUI.Models;
using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class LeaguePage : ContentPage
{
    public LeaguePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshUI();
    }

    private void RefreshUI()
    {
        var state  = AppState.Current;
        var titles = state.Titles;
        var season = state.Season;
        var sub    = state.Subscription;

        // Season header
        SeasonLabel.Text = season.CurrentSeasonLabel;

        // Player title
        TitleIcon.Text       = titles.TitleIcon;
        TitleLabelText.Text  = titles.TitleLabel;
        BigTitleIcon.Text    = titles.TitleIcon;
        BigTitleLabel.Text   = titles.TitleLabel;
        NextReqLabel.Text    = titles.NextRequirement;
        ParticipationsLabel.Text = titles.Participations.ToString();
        WinsLabel.Text           = titles.LeagueWins.ToString();

        // Season points + position
        var leaderboard = season.GetLeaderboard(titles, state.Profile);
        var human = leaderboard.FirstOrDefault(e => e.IsHuman);
        SeasonPointsLabel.Text  = season.CurrentPoints.ToString("N0");
        PlayerPositionLabel.Text = human != null ? $"{human.PositionLabel} no ranking" : "—";

        // Mini leaderboard (top 3 + human if outside top 3)
        BuildMiniLeaderboard(leaderboard, human);

        // Events
        BuildEvents(state.League, sub);
    }

    private void BuildMiniLeaderboard(List<SeasonEntry> board, SeasonEntry? human)
    {
        MiniLeaderboard.Children.Clear();

        var toShow = board.Take(3).ToList();
        if (human != null && human.Position > 3)
            toShow.Add(human);

        foreach (var e in toShow)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(32),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Margin = new Thickness(0, 2),
                BackgroundColor = e.IsHuman ? Color.FromArgb("#1C2A0A") : Colors.Transparent
            };

            row.Add(new Label
            {
                Text = e.PositionLabel,
                FontSize = e.Position <= 3 ? 14 : 12,
                TextColor = e.Position <= 3 ? Colors.White : Color.FromArgb("#7090B0"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalOptions = LayoutOptions.Center
            });

            var avatarLbl = new Label
            {
                Text = e.Avatar,
                FontSize = 14,
                Margin = new Thickness(4, 0),
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(avatarLbl, 1);
            row.Add(avatarLbl);

            var nameLbl2   = new Label { VerticalOptions = LayoutOptions.Center };
            var formatted2 = new FormattedString();
            formatted2.Spans.Add(new Span
            {
                Text           = e.Name,
                TextColor      = e.NameColor,
                FontSize       = 12,
                FontAttributes = e.IsHuman ? FontAttributes.Bold : FontAttributes.None
            });
            if (!string.IsNullOrEmpty(e.LocationLabel))
                formatted2.Spans.Add(new Span
                {
                    Text      = $"  {e.LocationLabel}",
                    TextColor = Color.FromArgb("#6080A0"),
                    FontSize  = 9
                });
            nameLbl2.FormattedText = formatted2;
            Grid.SetColumn(nameLbl2, 2);
            row.Add(nameLbl2);

            var pts = new Label
            {
                Text = $"{e.Points:N0} pts",
                TextColor = Color.FromArgb("#FFD700"),
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End
            };
            Grid.SetColumn(pts, 3);
            row.Add(pts);

            MiniLeaderboard.Children.Add(row);

            if (human != null && human.Position > 3 && e.Position == 3)
                MiniLeaderboard.Children.Add(new Label
                {
                    Text = "·  ·  ·",
                    TextColor = Color.FromArgb("#3A5070"),
                    HorizontalTextAlignment = TextAlignment.Center,
                    FontSize = 12
                });
        }
    }

    private void BuildEvents(LeagueService league, SubscriptionService sub)
    {
        EventsContainer.Children.Clear();
        var casual  = AppState.Current.CasualRanking;
        var events  = league.GetUpcomingEvents();

        foreach (var evt in events)
        {
            bool    registered     = league.IsRegistered(evt);
            decimal effectiveBuyIn = league.GetEffectiveBuyIn(evt, sub);
            bool    hasDiscount    = sub.IsActive && effectiveBuyIn < evt.BuyIn;
            var     (canEnter, hasPriority) = league.CanEnterWithPriority(evt, casual);

            string accentHex = evt.Type switch
            {
                LeagueEventType.Semanal     => "#2A5090",
                LeagueEventType.Copa        => "#3A2878",
                LeagueEventType.GrandeArena => "#6B3A00",
                _ => "#2A5090"
            };
            string iconText = evt.Type switch
            {
                LeagueEventType.Semanal     => "♟",
                LeagueEventType.Copa        => "♛",
                LeagueEventType.GrandeArena => "♚",
                _ => "♟"
            };

            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#0D1828"),
                Stroke          = new SolidColorBrush(Color.FromArgb(accentHex)),
                StrokeThickness = evt.IsFull && !hasPriority ? 1 : 1,
                StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding         = new Thickness(16, 14)
            };

            var content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            // Left: name + info
            var left = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };

            // Título + ícone de prioridade
            var titleRow = new HorizontalStackLayout { Spacing = 6 };
            titleRow.Children.Add(new Label { Text = iconText, FontSize = 18,
                TextColor = Color.FromArgb(accentHex.Replace("2A","4A").Replace("3A","5A").Replace("6B","9B")) });
            titleRow.Children.Add(new Label { Text = evt.Name, TextColor = Colors.White,
                FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center });
            if (hasPriority)
            {
                var priLbl = new Label { Text = "⚡", FontSize = 14, VerticalOptions = LayoutOptions.Center };
                ToolTipProperties.SetText(priLbl, "Prioridade garantida via Arena Casual");
                titleRow.Children.Add(priLbl);
            }
            left.Add(titleRow);

            // Inscritos + bracket + tempo
            left.Add(new Label
            {
                Text = $"{evt.SimulatedRegistrations} inscritos · bracket {evt.EffectiveSize} · {evt.TimeMinutes}min/jogo",
                TextColor = Color.FromArgb("#8AADCC"),
                FontSize = 11
            });

            left.Add(new Label
            {
                Text = evt.ScheduledAt.ToString("dd/MM  HH:mm"),
                TextColor = Color.FromArgb("#607890"),
                FontSize = 11
            });

            // Buy-in
            var buyInRow = new HorizontalStackLayout { Spacing = 6 };
            if (hasDiscount)
            {
                buyInRow.Children.Add(new Label { Text = $"$ {evt.BuyIn:N0}",
                    TextColor = Color.FromArgb("#4A6070"), FontSize = 11,
                    TextDecorations = TextDecorations.Strikethrough });
                buyInRow.Children.Add(new Label { Text = $"$ {effectiveBuyIn:N0}  {sub.BadgeIcon}",
                    TextColor = Color.FromArgb("#4CAF50"), FontSize = 11,
                    FontAttributes = FontAttributes.Bold });
            }
            else
            {
                buyInRow.Children.Add(new Label { Text = $"Buy-in: $ {effectiveBuyIn:N0}",
                    TextColor = Color.FromArgb("#8AADCC"), FontSize = 11 });
            }
            left.Add(buyInRow);

            // Capacidade / prioridade
            if (evt.IsFull)
                left.Add(new Label
                {
                    Text = hasPriority
                        ? "⚡ Vagas esgotadas · Você tem prioridade garantida"
                        : "🔴 Vagas esgotadas · Jogue Arena Casual para ganhar prioridade",
                    TextColor = hasPriority ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF5252"),
                    FontSize = 10
                });
            else
                left.Add(new Label
                {
                    Text = evt.TimeUntilLabel,
                    TextColor = evt.IsOpen ? Color.FromArgb("#4CAF50") : Color.FromArgb("#607890"),
                    FontSize = 11,
                    FontAttributes = evt.IsOpen ? FontAttributes.Bold : FontAttributes.None
                });

            content.Add(left);

            // Right: botão de ação
            var actionStack = new VerticalStackLayout
            {
                VerticalOptions   = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End,
                Spacing = 6,
                MinimumWidthRequest = 110
            };
            Grid.SetColumn(actionStack, 1);

            if (registered)
            {
                actionStack.Children.Add(new Label
                {
                    Text = "INSCRITO ✓",
                    TextColor = Color.FromArgb("#4CAF50"),
                    FontSize = 11, FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.End
                });
                var cancelLbl = new Label
                {
                    Text = "cancelar", TextColor = Color.FromArgb("#4A6070"),
                    FontSize = 10, HorizontalTextAlignment = TextAlignment.End
                };
                cancelLbl.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    CommandParameter = evt,
                    Command = new Command<LeagueEvent>(async ev => await OnCancelRegistration(ev))
                });
                actionStack.Children.Add(cancelLbl);
            }
            else if (!canEnter)
            {
                actionStack.Children.Add(new Label
                {
                    Text = "SEM VAGA",
                    TextColor = Color.FromArgb("#FF5252"),
                    FontSize = 11, FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.End
                });
                actionStack.Children.Add(new Label
                {
                    Text = "Jogue\nArena Casual",
                    TextColor = Color.FromArgb("#607890"),
                    FontSize = 9, HorizontalTextAlignment = TextAlignment.End
                });
            }
            else
            {
                string btnLabel = evt.IsOpen ? "ENTRAR" : "PRÉ-INSCRIÇÃO";
                var enterBtn = new Button
                {
                    Text            = btnLabel,
                    BackgroundColor = evt.IsOpen ? Color.FromArgb("#769656") : Color.FromArgb("#1A3A65"),
                    TextColor       = Colors.White,
                    FontSize        = 11, FontAttributes = FontAttributes.Bold,
                    CornerRadius    = 8, HeightRequest = 36, WidthRequest = 110,
                    CommandParameter = evt
                };
                enterBtn.Clicked += OnEnterEventClicked;
                actionStack.Children.Add(enterBtn);
            }

            content.Add(actionStack);
            card.Content = content;
            EventsContainer.Children.Add(card);
        }
    }

    private async void OnEnterEventClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not LeagueEvent evt) return;

        var state = AppState.Current;
        decimal cost = state.League.GetEffectiveBuyIn(evt, state.Subscription);

        var prizes = LeagueService.BuildPrizes(evt);
        prizes.TryGetValue(1, out decimal firstPrize);
        string priorityNote = state.CasualRanking.HasLigaPriority
            ? "\n⚡ Vaga garantida via Arena Casual"
            : "";

        bool confirm = await DisplayAlert(
            evt.Name,
            $"Buy-in: $ {cost:N0}{priorityNote}\n" +
            $"Bracket: {evt.EffectiveSize} jogadores · {evt.TimeMinutes}min/jogo\n" +
            $"1º lugar: $ {firstPrize:N0}\n\n" +
            "Bots preenchem vagas restantes. Confirmar?",
            "Entrar", "Cancelar");

        if (!confirm) return;

        // Registra participação (título + season)
        state.Titles.RecordLeagueParticipation();
        state.Season.AddPoints(evt.ParticipationPoints);
        state.Daily.RecordWin();

        // Inscreve
        state.League.Register(evt);

        // Cria torneio com tamanho e tempo dinâmicos
        var profile = state.Profile;
        var tourn   = state.TournSvc.Create(
            profile.Name, evt.EffectiveSize, cost, profile.Avatar);

        // Marca como torneio da Liga e carrega tabela de pontos de temporada
        tourn.IsLiga          = true;
        tourn.LeagueIsSemanal = evt.Type == LeagueEventType.Semanal;
        tourn.LeaguePointsTable = Enumerable.Range(1, evt.EffectiveSize)
            .ToDictionary(pos => pos, pos => evt.GetPoints(pos));

        // Sobrescreve prize pool com pool real (inscritos × buy-in, bots não pagam)
        tourn.PrizeTable = prizes;

        state.ActiveTournament = tourn;
        state.MatchResultReady = false;
        state.TournamentTimeMinutes = evt.TimeMinutes;

        await Shell.Current.GoToAsync("BracketPage");
    }

    private async Task OnCancelRegistration(LeagueEvent evt)
    {
        bool confirm = await DisplayAlert("Cancelar inscrição",
            $"Deseja cancelar sua inscrição em {evt.Name}?\nSeu buy-in NÃO será devolvido.", "Cancelar inscrição", "Manter");
        if (!confirm) return;

        AppState.Current.League.Unregister(evt);
        RefreshUI();
    }

    private async void OnRankingClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("SeasonRankingPage");

    private async void OnHallOfFameClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("HallOfFamePage");
}
