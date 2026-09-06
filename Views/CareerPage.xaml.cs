using ChessMAUI.Models;
using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class CareerPage : ContentPage
{
    private CareerService Svc => AppState.Current.Career;

    public CareerPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var state = AppState.Current;
        if (state.IsCareerGame && state.MatchResultReady)
        {
            state.IsCareerGame     = false;
            state.MatchResultReady = false;

            var prog = Svc.Progress;
            if (prog.ActiveTournament != null)
            {
                var result = state.LastMatchHumanWon ? CareerRoundResult.Win
                           : state.LastMatchWasDraw  ? CareerRoundResult.Draw
                           : CareerRoundResult.Loss;
                Svc.RecordRound(prog.ActiveTournament, state.CareerOpponentName, result);
                Svc.Save(prog);
            }
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        var prog = Svc.Progress;

        if (prog.IsCareerCompleted) { ShowCareerCompleted(prog); return; }
        if (prog.ActiveTournament == null) { ShowWelcome();      return; }

        var t = prog.ActiveTournament;

        TournamentNameLabel.Text = t.LevelName;
        LevelLabel.Text          = $"Ciclo {prog.EffectiveCycleYear}  ·  Nível {(int)t.Level + 1}/7";
        LevelSubLabel.Text       = t.LevelSubtitle + TitlesTag(prog);

        if (!t.IsCompleted)
            ShowInProgress(t);
        else
            ShowResult(t, prog);
    }

    // ── Welcome ───────────────────────────────────────────────────────────────

    private static string TitlesTag(CareerProgress prog) =>
        prog.TitlesWon > 0 ? $"  ·  {prog.TitlesWon}× Campeão" : "";

    private void ShowWelcome()
    {
        var prog = Svc.Progress;
        TournamentNameLabel.Text = "Modo Carreira";
        RoundInfoLabel.Text      = "Do Torneio Local ao Campeonato Mundial";
        LevelLabel.Text          = "7 níveis · Circuito FIDE";
        LevelSubLabel.Text       = "Suba a pirâmide e torne-se Campeão Mundial";

        WelcomeSection.IsVisible   = true;
        StandingsSection.IsVisible = false;
        CopaSection.IsVisible      = false;
        MundialSection.IsVisible   = false;
        OpponentSection.IsVisible  = false;
        PlayBtn.IsVisible          = false;
        ResultSection.IsVisible    = false;
        NextBtn.IsVisible          = false;
    }

    // ── In Progress ───────────────────────────────────────────────────────────

    private void ShowInProgress(CareerTournamentState t)
    {
        WelcomeSection.IsVisible = false;
        ResultSection.IsVisible  = false;
        NextBtn.IsVisible        = false;

        switch (t.Format)
        {
            case CareerFormat.Swiss:       ShowSwissInProgress(t);       break;
            case CareerFormat.Elimination: ShowCopaInProgress(t);        break;
            case CareerFormat.BestOfN:     ShowMundialInProgress(t);     break;
        }
    }

    private void ShowSwissInProgress(CareerTournamentState t)
    {
        RoundInfoLabel.Text      = $"Rodada {t.CurrentRound}/{t.TotalRounds}";
        RoundProgressBar.Progress = (double)(t.CurrentRound - 1) / t.TotalRounds;

        CopaSection.IsVisible    = false;
        MundialSection.IsVisible = false;
        BuildStandings(t);

        var opp = Svc.GetNextOpponent(t);
        SetOpponent("Próximo adversário", opp.Name, opp.Difficulty);

        PlayBtn.Text      = $"Jogar Rodada {t.CurrentRound}";
        PlayBtn.IsVisible = true;
    }

    private void ShowCopaInProgress(CareerTournamentState t)
    {
        RoundInfoLabel.Text       = t.CurrentRound switch { 1 => "Oitavas", 2 => "Semifinal", _ => "Final" };
        RoundProgressBar.Progress = (t.CurrentRound - 1) / 3.0;

        StandingsSection.IsVisible = false;
        MundialSection.IsVisible   = false;
        BuildCopaBracket(t);
        CopaSection.IsVisible = true;

        var opp = Svc.GetNextOpponent(t);
        SetOpponent(t.CurrentRound == 3 ? "Finalista — adversário" : "Adversário desta fase",
                    opp.Name, opp.Difficulty);

        PlayBtn.Text      = t.CurrentRound == 3 ? "Jogar a Final" : $"Jogar Fase {t.CurrentRound}";
        PlayBtn.IsVisible = true;
    }

    private void ShowMundialInProgress(CareerTournamentState t)
    {
        RoundInfoLabel.Text       = $"Partida {t.CurrentRound}  ·  {t.WinsNeeded} vitórias";
        RoundProgressBar.Progress = 0;

        StandingsSection.IsVisible = false;
        CopaSection.IsVisible      = false;
        BuildMundialScore(t);
        MundialSection.IsVisible  = true;

        var opp = Svc.GetNextOpponent(t);
        SetOpponent("Adversário — Campeão Mundial", opp.Name, opp.Difficulty);

        PlayBtn.Text      = $"Jogar Partida {t.CurrentRound}";
        PlayBtn.IsVisible = true;
    }

    // ── Result ────────────────────────────────────────────────────────────────

    private void ShowResult(CareerTournamentState t, CareerProgress prog)
    {
        WelcomeSection.IsVisible  = false;
        OpponentSection.IsVisible = false;
        PlayBtn.IsVisible         = false;

        // Show format-specific summary alongside result
        StandingsSection.IsVisible = t.Format == CareerFormat.Swiss;
        CopaSection.IsVisible      = t.Format == CareerFormat.Elimination;
        MundialSection.IsVisible   = t.Format == CareerFormat.BestOfN;

        switch (t.Format)
        {
            case CareerFormat.Swiss:       BuildStandings(t);       break;
            case CareerFormat.Elimination: BuildCopaBracket(t);     break;
            case CareerFormat.BestOfN:     BuildMundialScore(t);    break;
        }

        bool advanced = t.Outcome is CareerStageOutcome.Advanced or CareerStageOutcome.AdvancedDirect;

        RoundInfoLabel.Text = advanced ? "Classificado!" : "Eliminado";

        ResultSection.IsVisible = true;

        if (t.Level == CareerLevel.Mundial && advanced)
        {
            ResultIcon.Text           = "🏆";
            ResultTitle.Text          = "Campeão Mundial!";
            ResultDetail.Text         = "Você conquistou o título máximo do xadrez.";
            ResultTitle.TextColor     = Color.FromArgb("#FFD700");
            ResultDestination.Text    = "";
            NextBtn.Text              = "Celebrar Título";
            NextBtn.BackgroundColor   = Color.FromArgb("#4A3800");
            NextBtn.IsVisible         = true;
            return;
        }

        if (advanced)
        {
            ResultIcon.Text       = t.Outcome == CareerStageOutcome.AdvancedDirect ? "⚡" : "✓";
            ResultTitle.Text      = t.Outcome == CareerStageOutcome.AdvancedDirect
                ? "Classificação Direta!"
                : "Classificado!";
            ResultDetail.Text = t.Level switch
            {
                CareerLevel.CopaMundo  => "Você venceu a Copa do Mundo FIDE!",
                CareerLevel.GrandSwiss => "Top 2 do Grand Swiss!",
                CareerLevel.GrandPrix  => "Vencedor do Grand Prix!",
                CareerLevel.Candidatos => "Campeão dos Candidatos — você é o desafiante!",
                _ => "Você avançou de fase!"
            };
            ResultTitle.TextColor = Color.FromArgb("#4CAF50");
        }
        else
        {
            ResultIcon.Text   = "✗";
            ResultTitle.Text  = "Eliminado";
            ResultDetail.Text = t.Level switch
            {
                CareerLevel.CopaMundo  => "Você foi eliminado da Copa do Mundo.",
                CareerLevel.GrandSwiss => "Não classificou entre os Top 2.",
                CareerLevel.GrandPrix  => "Não venceu o Grand Prix.",
                CareerLevel.Candidatos => "Não venceu o Candidatos.",
                CareerLevel.Mundial    => "Você perdeu o match do Mundial.",
                _ => "Você não se classificou."
            };
            ResultTitle.TextColor = Color.FromArgb("#FF5252");
        }

        ResultDestination.Text = (t.Level == CareerLevel.Zonal && !advanced)
            ? (prog.ZonalRetries >= 1 ? "→ Volta ao Torneio Local" : "→ Nova tentativa no Zonal (1 chance restante)")
            : Svc.NextDestinationText(t.Level, t.Outcome);

        string btnText = (t.Level, advanced) switch
        {
            (CareerLevel.Local,      true)  => "Jogar Torneio Zonal",
            (CareerLevel.Local,      false) => "Tentar novamente",
            (CareerLevel.Zonal,      true)  => "Jogar Copa do Mundo FIDE",
            (CareerLevel.Zonal,      false) => prog.ZonalRetries >= 1
                                               ? "Volta ao Torneio Local"
                                               : "Tentar novamente no Zonal",
            (CareerLevel.CopaMundo,  true)  => "Jogar Candidatos",
            (CareerLevel.CopaMundo,  false) => "Jogar Grand Swiss",
            (CareerLevel.GrandSwiss, true)  => "Jogar Candidatos",
            (CareerLevel.GrandSwiss, false) => "Jogar Grand Prix",
            (CareerLevel.GrandPrix,  true)  => "Jogar Candidatos",
            (CareerLevel.GrandPrix,  false) => "Nova Copa do Mundo",
            (CareerLevel.Candidatos, true)  => "Jogar o Mundial!",
            (CareerLevel.Candidatos, false) => "Nova Copa do Mundo",
            (CareerLevel.Mundial,    _)     => "Jogar Candidatos novamente",
            _                               => "Continuar"
        };
        NextBtn.Text             = btnText;
        NextBtn.BackgroundColor  = advanced ? Color.FromArgb("#1A5C1A") : Color.FromArgb("#5C1A1A");
        NextBtn.IsVisible        = true;
    }

    private void ShowCareerCompleted(CareerProgress prog)
    {
        int nextYear = prog.EffectiveCycleYear + 2;
        int titles   = prog.TitlesWon + 1; // +1 porque ainda não chamou StartNewCycle

        TournamentNameLabel.Text   = $"Campeão Mundial — Ciclo {prog.EffectiveCycleYear}";
        RoundInfoLabel.Text        = titles == 1 ? "Primeiro título conquistado!"
                                   : $"{titles}× Campeão Mundial";
        LevelLabel.Text            = "Ciclo FIDE concluído";
        LevelSubLabel.Text         = titles > 1 ? $"Você é um lendário {titles}× campeão" : "";
        WelcomeSection.IsVisible   = false;
        StandingsSection.IsVisible = false;
        CopaSection.IsVisible      = false;
        MundialSection.IsVisible   = false;
        OpponentSection.IsVisible  = false;
        PlayBtn.IsVisible          = false;
        ResultSection.IsVisible    = true;
        ResultIcon.Text            = "🏆";
        ResultTitle.Text           = "Campeão Mundial!";
        ResultDetail.Text          = $"Você conquistou o título no Ciclo {prog.EffectiveCycleYear}.";
        ResultDestination.Text     = $"Próximo ciclo: {nextYear}";
        ResultTitle.TextColor      = Color.FromArgb("#FFD700");
        NextBtn.Text               = $"Defender o Título — Ciclo {nextYear}";
        NextBtn.BackgroundColor    = Color.FromArgb("#4A3800");
        NextBtn.IsVisible          = true;
    }

    // ── Swiss Standings ───────────────────────────────────────────────────────

    private void BuildStandings(CareerTournamentState t)
    {
        StandingsSection.IsVisible = true;

        ZoneLabel.Text = t.AdvancementSpots > 1
            ? $"↑ Top {t.AdvancementSpots} avançam"
            : "↑ Apenas o 1º avança";

        StandingsList.Children.Clear();
        var standings = t.Standings;

        for (int i = 0; i < standings.Count; i++)
        {
            var  p      = standings[i];
            int  pos    = i + 1;
            bool isAdv  = pos <= t.AdvancementSpots;
            bool isLast = i == standings.Count - 1;

            // Container com barra lateral colorida para zona de classificação
            var outer = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(new(4), new(GridLength.Star))
            };
            outer.Add(new BoxView
            {
                BackgroundColor = isAdv ? Color.FromArgb("#2E8C45") : Colors.Transparent,
                VerticalOptions = LayoutOptions.Fill
            });

            Color bgColor = p.IsHuman
                ? (isAdv ? Color.FromArgb("#0A2A14") : Color.FromArgb("#1C140A"))
                : (isAdv ? Color.FromArgb("#071A10") : Colors.Transparent);

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new(28), new(GridLength.Star), new(22), new(GridLength.Auto)),
                BackgroundColor = bgColor,
                Padding         = new Thickness(10, 9)
            };
            Grid.SetColumn(row, 1);

            row.Add(new Label
            {
                Text                    = $"{pos}º",
                FontSize                = 14,
                TextColor               = isAdv ? Color.FromArgb("#4CAF50") : Color.FromArgb("#6A8AAA"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalOptions         = LayoutOptions.Center
            });

            var nameLbl = new Label
            {
                Text           = p.IsHuman ? "Você" : p.Name,
                TextColor      = p.IsHuman ? Color.FromArgb("#4CAF50")
                               : isAdv     ? Color.FromArgb("#F2F5F8")
                               :             Color.FromArgb("#9FB3C8"),
                FontSize       = 14,
                FontAttributes = p.IsHuman ? FontAttributes.Bold : FontAttributes.None,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(nameLbl, 1);
            row.Add(nameLbl);

            if (isAdv && !t.IsCompleted)
            {
                var arrowLbl = new Label
                {
                    Text                    = "↑",
                    TextColor               = Color.FromArgb("#4CAF50"),
                    FontSize                = 13,
                    VerticalOptions         = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center
                };
                Grid.SetColumn(arrowLbl, 2);
                row.Add(arrowLbl);
            }

            var ptsLbl = new Label
            {
                Text            = p.Points.ToString("0.0"),
                TextColor       = p.IsHuman ? Color.FromArgb("#4CAF50")
                                : isAdv     ? Color.FromArgb("#F2F5F8")
                                :             Color.FromArgb("#9FB3C8"),
                FontSize        = 14,
                FontAttributes  = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(ptsLbl, 3);
            row.Add(ptsLbl);

            outer.Add(row);
            StandingsList.Children.Add(outer);

            // Linha verde mais espessa separando zona de classificação da eliminação
            if (pos == t.AdvancementSpots && pos < standings.Count)
                StandingsList.Children.Add(new BoxView { Color = Color.FromArgb("#2E8C45"), HeightRequest = 1.5 });
            else if (!isLast)
                StandingsList.Children.Add(new BoxView { Color = Color.FromArgb("#0E2030"), HeightRequest = 1 });
        }
    }

    // ── Adversário ────────────────────────────────────────────────────────────

    private static readonly (string Color, string Rating, string Icon)[] DiffStyles =
    [
        ("#4CAF50", "~800",  "♙"),
        ("#4AA3FF", "~1100", "♞"),
        ("#4AA3FF", "~1400", "♞"),
        ("#FFD447", "~1700", "♝"),
        ("#A56CFF", "~2000", "♜"),
        ("#FF8C42", "~2400", "♛"),
        ("#FF5B5B", "~2700", "♚"),
    ];

    private void SetOpponent(string context, string name, int difficulty)
    {
        var (color, rating, icon) = difficulty >= 1 && difficulty <= DiffStyles.Length
            ? DiffStyles[difficulty - 1] : ("#9FB3C8", "—", "♞");

        OpponentContextLabel.Text    = context;
        OpponentNameLabel.Text       = name;
        OpponentRatingLabel.Text     = $"Rating estimado: {rating}";
        OpponentDiffLabel.Text       = Svc.DiffLabel(difficulty);
        OpponentDiffLabel.TextColor  = Color.FromArgb(color);
        OppDiffBadge.BackgroundColor = Color.FromArgb(color).WithAlpha(0.12f);
        OppAvatarLabel.Text          = icon;
        OpponentSection.IsVisible    = true;
    }

    // ── Copa Bracket ──────────────────────────────────────────────────────────

    private void BuildCopaBracket(CareerTournamentState t)
    {
        CopaBracket.Children.Clear();

        string[] phaseLabels = ["Oitavas", "Semifinal", "Final"];
        string[] oppNames    = t.Players
            .Where(p => !p.IsHuman)
            .Select(p => p.Name)
            .ToArray();

        for (int i = 0; i < 3; i++)
        {
            CareerRound? round = t.Rounds.FirstOrDefault(r => r.Number == i + 1);
            bool isCurrent = (i + 1) == t.CurrentRound && !t.IsCompleted;
            bool isFuture  = round == null && !isCurrent;

            string icon  = round?.Result switch
            {
                CareerRoundResult.Win  => "✓",
                CareerRoundResult.Loss or CareerRoundResult.Draw => "✗",
                _ => isCurrent ? "▶" : "○"
            };
            string iconColor = round?.Result switch
            {
                CareerRoundResult.Win  => "#4CAF50",
                CareerRoundResult.Loss or CareerRoundResult.Draw => "#FF5252",
                _ => isCurrent ? "#FFD700" : "#8A9AAA"
            };

            string oppName = i < oppNames.Length ? oppNames[i] : "—";
            string rowColor = isCurrent ? "#1A2A0A" : "#0D1828";

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new(22), new(70), new(GridLength.Star), new(GridLength.Auto)),
                BackgroundColor = Color.FromArgb(rowColor),
                Padding = new Thickness(8, 8),
            };

            row.Add(new Label
            {
                Text = icon, TextColor = Color.FromArgb(iconColor),
                FontSize = 14, VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            });

            var phaseLbl = new Label
            {
                Text = phaseLabels[i], TextColor = Color.FromArgb("#A8C4E0"),
                FontSize = 15, VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(phaseLbl, 1);
            row.Add(phaseLbl);

            var nameLbl = new Label
            {
                Text = oppName,
                TextColor = isFuture ? Color.FromArgb("#8A9AAA")
                          : round?.Result == CareerRoundResult.Win ? Color.FromArgb("#4CAF50")
                          : round?.Result != null ? Color.FromArgb("#FF7070")
                          : Colors.White,
                FontSize = 15, FontAttributes = isCurrent ? FontAttributes.Bold : FontAttributes.None,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(nameLbl, 2);
            row.Add(nameLbl);

            string diffText = round != null ? Svc.DiffLabel(round.Difficulty)
                            : isCurrent && i < oppNames.Length
                                ? Svc.DiffLabel(t.Players.FirstOrDefault(p => p.Name == oppName)?.Difficulty ?? 3)
                                : "";
            var diffLbl = new Label
            {
                Text = diffText, TextColor = Color.FromArgb("#9AB8D8"),
                FontSize = 15, VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(diffLbl, 3);
            row.Add(diffLbl);

            CopaBracket.Children.Add(row);

            if (i < 2)
                CopaBracket.Children.Add(new BoxView
                {
                    Color = Color.FromArgb("#1A2840"), HeightRequest = 1
                });
        }
    }

    // ── Mundial Score ─────────────────────────────────────────────────────────

    private void BuildMundialScore(CareerTournamentState t)
    {
        var opp = t.Players.FirstOrDefault(p => !p.IsHuman);
        MundialScoreLabel.Text = $"{t.HumanWins} × {t.HumanLosses}";
        MundialSubLabel.Text   = $"Você vs {opp?.Name ?? "Magnus"}  ·  Melhor de {t.WinsNeeded * 2 - 1}";

        MundialHistory.Children.Clear();
        foreach (var r in t.Rounds)
        {
            string icon  = r.Result == CareerRoundResult.Win  ? "✓ Vitória"
                         : r.Result == CareerRoundResult.Loss ? "✗ Derrota"
                         : "= Empate";
            string color = r.Result == CareerRoundResult.Win  ? "#4CAF50"
                         : r.Result == CareerRoundResult.Loss ? "#FF5252"
                         : "#607890";
            MundialHistory.Children.Add(new Label
            {
                Text = $"Partida {r.Number}: {icon}",
                TextColor = Color.FromArgb(color),
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center
            });
        }
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void OnStartCareerClicked(object? sender, EventArgs e)
    {
        Svc.StartCareer();
        RefreshUI();
    }

    private async void OnPlayRoundClicked(object? sender, EventArgs e)
    {
        var prog = Svc.Progress;
        var t    = prog.ActiveTournament;
        if (t == null) return;

        var opp   = Svc.GetNextOpponent(t);
        var state = AppState.Current;

        state.PendingCareerGame     = true;
        state.IsCareerGame          = true;
        state.MatchResultReady      = false;
        state.PendingTournamentGame = false;
        state.PendingFriendGame     = false;
        state.CareerOpponentName    = opp.Name;
        state.CareerAIDepth         = CareerService.GetAIDepth(opp.Difficulty);
        state.CareerTimeMinutes     = CareerService.GetTimeMinutes(opp.Difficulty);

        await Shell.Current.GoToAsync("GamePage");
    }

    private void OnNextActionClicked(object? sender, EventArgs e)
    {
        var prog = Svc.Progress;
        if (prog.IsCareerCompleted)
            Svc.StartNewCycle(prog);
        else
            Svc.ApplyStageResult(prog);
        RefreshUI();
    }

    private async void OnShowFlowClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("CareerFlowPage");
}
