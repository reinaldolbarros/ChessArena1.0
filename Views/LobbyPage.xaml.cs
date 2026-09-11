using ChessMAUI.Models;
using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class LobbyPage : ContentPage
{
    public LobbyPage()
    {
        InitializeComponent();
        // Tenta carregar a bandeira de novo quando a internet voltar — sem internet
        // no primeiro carregamento, o app só fica aguardando em segundo plano,
        // sem exibir erro nenhum pro usuário.
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.Internet) return;
        MainThread.BeginInvokeOnMainThread(() => SetFlag(AppState.Current.Profile.Country));
    }

    private void SetFlag(string country)
    {
        CountryLabel.Text      = country;
        CountryLabel.IsVisible = !string.IsNullOrEmpty(country);

        // FlagImage.IsVisible fica true assim que há URL pra tentar — enquanto a
        // imagem não termina de baixar (ou falha por falta de internet), o espaço
        // reservado (WidthRequest/HeightRequest no XAML) fica só em branco, sem
        // nenhuma indicação de erro ou carregamento.
        string? flagUrl     = GeoData.FlagUrl(country);
        FlagImage.Source    = flagUrl;
        FlagImage.IsVisible = flagUrl != null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        AppState.Current.Stars.RecordOpen();

        var profile = AppState.Current.Profile;

        if (profile.IsNew && !AppState.Current.Auth.IsAnonymous)
        {
            await Shell.Current.GoToAsync("//ProfilePage");
            return;
        }

        await RefreshUI();
    }

    private async Task RefreshUI()
    {
        var p = AppState.Current.Profile;
        p.EnsureCountryDefault();

        // Foto pode ser uma URL do Supabase Storage (outro aparelho) ou um arquivo local
        // (ainda não sincronizado, ou visitante sem conta) — File.Exists só em background
        // pra não bloquear a thread de UI.
        bool isRemote = !string.IsNullOrEmpty(p.AvatarPath)
            && (p.AvatarPath.StartsWith("http://") || p.AvatarPath.StartsWith("https://"));
        bool hasLocalPhoto = !isRemote && !string.IsNullOrEmpty(p.AvatarPath)
            && await Task.Run(() => File.Exists(p.AvatarPath));
        bool hasPhoto = isRemote || hasLocalPhoto;

        AvatarImage.IsVisible = hasPhoto;
        AvatarLabel.IsVisible = !hasPhoto;
        if (isRemote)         AvatarImage.Source = ImageSource.FromUri(new Uri(p.AvatarPath));
        else if (hasLocalPhoto) AvatarImage.Source = ImageSource.FromFile(p.AvatarPath);
        else                   AvatarLabel.Text    = p.Avatar;

        NameLabel.Text   = p.Name;
        PointsLabel.Text = $"{p.Points} Elo";

        SetFlag(p.Country);

        // Localização — removida do cabeçalho (ver LobbyPage.xaml)
        // string loc = p.Country.Length > 0 && p.State.Length > 0 ? $"{p.Country} · {p.State}"
        //            : p.Country.Length > 0 ? p.Country
        //            : p.State.Length > 0   ? p.State : "";
        // ProfileLocationLabel.Text = loc;
        int total   = p.Wins + p.Losses;
        int rate    = total > 0 ? p.Wins * 100 / total : 0;
        WinRateLabel.Text      = total > 0 ? $"{rate}%" : "—";
        WinRateLabel.TextColor = total == 0 ? Color.FromArgb("#5A7A9A")
                               : rate >= 55  ? Color.FromArgb("#4CAF50")
                               : rate >= 40  ? Color.FromArgb("#CFAB30")
                               : Color.FromArgb("#DD5040");
        WinRecordLabel.Text    = total > 0 ? $"{p.Wins}V  ·  {p.Losses}D" : "";
        GamesPlayedLabel.Text  = total.ToString();
        // TournWinsLabel.Text = p.TournamentsWon.ToString(); — COMENTADO: card Torneios
        // desativado na Fase 0 (ver LobbyPage.xaml). Reativar junto com a Liga em v2.0.

        bool isSub = AppState.Current.Subscription.IsActive;

        // Missão / Bônus diário — COMENTADO: Fase 0 (lançamento sem monetização/gamificação,
        // foco em crescer a base). Card fica oculto (MissionCard.IsVisible=False no XAML).
        // Reativar preenchendo os labels abaixo quando decidirmos religar a funcionalidade.
        // bool claimed  = daily.BonusClaimedToday;
        // int  streak   = daily.LoginStreak;
        // var  mission  = daily.GetMissions().First(m => m.Id == "m1");
        // BonusTitle.Text      = "Missão de Hoje";
        // BonusTitle.TextColor = Color.FromArgb("#FFFFFF");
        // BonusStreakLabel.Text      = mission.Description;
        // BonusStreakLabel.TextColor = Color.FromArgb("#C8A020");
        // BonusProgressLabel.Text = claimed
        //     ? $"✓ Concluída  ·  {streak} dias seguidos"
        //     : $"{mission.Progress}/{mission.Target}  ·  +{mission.StarReward} ⭐";
        // BonusProgressLabel.TextColor  = Color.FromArgb("#C8A020");
        // MissionProgressBar.Progress = claimed ? 1.0 : Math.Min(1.0, (double)mission.Progress / mission.Target);
        // BonusBtn.IsVisible     = !claimed;
        // BonusArrow.IsVisible   = claimed;
        // BonusArrow.Text        = "✓";
        // BonusArrow.TextColor   = Color.FromArgb("#C8A020");

        AdminBtn.IsVisible = AppState.Current.IsAdminMode && AppState.Current.AdminUiRevealed && !AppState.Current.Auth.IsAnonymous;

        var starsSvc = AppState.Current.Stars;
        StarsLabel.Text = $"⭐ {starsSvc.Balance} · {starsSvc.DedicationTitle}";

        // Casual / Liga — COMENTADO: aguardando base de jogadores
        // var casual = AppState.Current.CasualRanking;
        // string priorityStr = casual.HasLigaPriority ? "  ·  ⚡ Prioridade Liga" : "";
        // RatingLabel.Text = $"{p.Points:N0} pts{ticketStr}{priorityStr}";
        // int    wpts      = casual.WeeklyPoints;
        // int    threshold = CasualRankingService.PriorityThreshold;
        // double fillPct   = Math.Min(1.0, (double)wpts / threshold);
        // bool   hasPrio   = casual.HasLigaPriority;
        // double barMax = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density - 120;
        // CasualBarFill.WidthRequest = Math.Max(0, barMax * fillPct);
        // CasualBarFill.Color        = hasPrio ? Color.FromArgb("#4CAF50") : Color.FromArgb("#3A6AB0");
        // CasualPtsLabel.Text        = hasPrio ? "✓ Prioridade" : $"{wpts}/{threshold} pts";
        // CasualPtsLabel.TextColor   = hasPrio ? Color.FromArgb("#4CAF50") : Color.FromArgb("#5A7898");
        // CasualBorder.Stroke        = new SolidColorBrush(hasPrio ? Color.FromArgb("#2A6040") : Color.FromArgb("#2A5090"));
        // CasualStatusLabel.Text     = hasPrio ? "⚡ Vaga prioritária garantida na Liga esta semana!"
        //     : wpts > 0 ? $"Continue jogando — faltam {threshold - wpts} pts para prioridade"
        //     : "Jogue para garantir vaga prioritária na Liga";
        // CasualStatusLabel.TextColor = hasPrio ? Color.FromArgb("#4CAF50") : Color.FromArgb("#4A6888");

        // Career.Progress faz JSON deserialization — executa em background
        var career = await Task.Run(() => AppState.Current.Career.Progress);
        // Selo de título — só aparece aqui, na tela inicial (não é repetido dentro do
        // Modo Carreira). Vai na linha do nível (mais curta), não na de Rodada/Adversário
        // (que já pode ficar longa e cortar o selo). Usa o nome de verdade (Bicampeão...).
        string cycleTag = career.TitlesWon > 0
            ? $"  ·  🏆 {CareerService.ChampionTitleName(career.TitlesWon)}"
            : "";
        CareerTournamentLabel.Text = (career.ActiveTournament != null
            ? career.ActiveTournament.LevelName
            : "Do Local ao Mundial") + (career.IsCareerCompleted ? "" : cycleTag);
        string careerSub;
        if (career.IsCareerCompleted)
        {
            careerSub = $"🏆 Campeão do Ciclo {career.EffectiveCycleYear}{cycleTag}";
        }
        else if (career.ActiveTournament != null)
        {
            var t = career.ActiveTournament;
            var nextRound = t.Rounds.FirstOrDefault(r => r.Result == CareerRoundResult.NotPlayed);
            string oppName = nextRound?.Opponent ?? t.Opponent?.Name ?? "";
            careerSub = oppName.Length > 0
                ? $"Rodada {t.CurrentRound}/{t.TotalRounds}  ·  Adversário {oppName}"
                : $"Rodada {t.CurrentRound}/{t.TotalRounds}";
        }
        else
        {
            careerSub = "Do Torneio Local ao Campeonato Mundial";
        }
        CareerSubLabel.Text = careerSub;

        // Liga — COMENTADO: aguardando base de jogadores
        // SeasonSubLabel.Text = AppState.Current.Season.CurrentSeasonLabel;
        // BuildChampions(AppState.Current);


        // Liga — COMENTADO: aguardando base de jogadores
        // BuildMiniRanking();

        await BuildRankingPreviewAsync();
    }

    // -----------------------------------------------------------------------
    // Topo do ranking
    // -----------------------------------------------------------------------
    private async void OnRankingSeeAllTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//RankingPage");

    private async Task BuildRankingPreviewAsync()
    {
        RankingPreviewList.Children.Clear();

        var profile = AppState.Current.Profile;
        var entries = await AppState.Current.Ranking.GetGlobalAsync(profile);

        var top3 = entries.Take(3).ToList();
        foreach (var entry in top3)
            RankingPreviewList.Children.Add(BuildRankingPreviewRow(entry));

        var me = entries.FirstOrDefault(x => x.IsHuman);
        if (me != null && !top3.Contains(me))
        {
            RankingPreviewList.Children.Add(new BoxView
            {
                HeightRequest   = 1,
                BackgroundColor = Color.FromArgb("#1A2535"),
                Margin          = new Thickness(0, 2)
            });
            RankingPreviewList.Children.Add(BuildRankingPreviewRow(me));
        }
    }

    private static Grid BuildRankingPreviewRow(RankingEntry entry)
    {
        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(28), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Padding           = new Thickness(2, 3)
        };

        row.Add(new Label
        {
            Text                    = entry.PositionLabel,
            FontSize                = 13,
            TextColor               = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions         = LayoutOptions.Center
        });

        var nameLabel = new Label
        {
            Text            = entry.Name,
            FontSize        = 13,
            TextColor       = entry.NameColor,
            FontAttributes  = entry.IsHuman ? FontAttributes.Bold : FontAttributes.None,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode   = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(nameLabel, 1);
        row.Add(nameLabel);

        var pointsLabel = new Label
        {
            Text            = $"{entry.Points:N0}",
            FontSize        = 12,
            TextColor       = Color.FromArgb("#8AAAC8"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(pointsLabel, 2);
        row.Add(pointsLabel);

        return row;
    }

    // -----------------------------------------------------------------------
    // Destaques da Liga — COMENTADO: aguardando base de jogadores
    // -----------------------------------------------------------------------
    // private void BuildChampions(AppState state)
    // {
    //     var weekly = state.League.GetWeeklyChampion(state.Profile, state.Titles);
    //     WeekChampAvatar.Text     = weekly.Avatar;
    //     WeekChampName.Text       = weekly.Name;
    //     WeekChampName.TextColor  = weekly.IsHuman ? Color.FromArgb("#4CAF50") : Colors.White;
    //     var monthly = state.Season.GetMonthlyLeader(state.Titles, state.Profile);
    //     MonthChampAvatar.Text    = monthly.Avatar;
    //     MonthChampName.Text      = monthly.Name;
    //     MonthChampName.TextColor = monthly.IsHuman ? Color.FromArgb("#4CAF50") : Colors.White;
    // }

    // -----------------------------------------------------------------------
    // Bônus diário
    // -----------------------------------------------------------------------
    private async void OnBonusCardTapped(object? sender, TappedEventArgs e)
    {
        await FlashTap(MissionCard);
        if (!AppState.Current.Daily.BonusClaimedToday)
            OnBonusClicked(sender, e);
    }

    private async void OnBonusClicked(object? sender, EventArgs e)
    {
        await FlashTap(MissionCard);
        var state = AppState.Current;
        int starsBonus = state.Daily.ClaimDailyBonus();
        int starsStreak = state.Daily.TryClaimStreakMission();
        int totalStars = starsBonus + starsStreak;
        if (totalStars > 0) state.Stars.Add(totalStars);

        int streak = state.Daily.LoginStreak;
        string streakLine = starsStreak > 0
            ? $"\nMissão de sequência completa! +{starsStreak} ⭐"
            : streak >= 7 ? "\nSequência máxima mantida!" : "";
        string next = streak switch { >= 7 => "Máximo!", >= 5 => "7 dias = +5 ⭐", >= 3 => "5 dias", >= 2 => "3 dias", _ => "2 dias" };
        await DisplayAlert("Bônus Diário",
            $"+{starsBonus} ⭐  Sequência: {streak} dia{(streak != 1 ? "s" : "")}{streakLine}\nPróximo marco: {next}", "OK");

        await RefreshUI();
    }

    // -----------------------------------------------------------------------
    // Missões diárias
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // Mini ranking da Liga — COMENTADO: aguardando base de jogadores
    // -----------------------------------------------------------------------
    // private void BuildMiniRanking()
    // {
    //     MiniRankContainer.Children.Clear();
    //     var state   = AppState.Current;
    //     var board   = state.Season.GetLeaderboard(state.Titles, state.Profile);
    //     var sub     = state.Subscription;
    //     var human   = board.FirstOrDefault(e => e.IsHuman);
    //     var toShow  = board.Take(5).ToList();
    //     bool humanOutside = human != null && human.Position > 5;
    //     if (humanOutside) toShow.Add(human!);
    //     bool separatorAdded = false;
    //     foreach (var e in toShow)
    //     {
    //         if (humanOutside && e.IsHuman && !separatorAdded)
    //         {
    //             separatorAdded = true;
    //             MiniRankContainer.Children.Add(new Label { Text = "·  ·  ·", TextColor = Color.FromArgb("#3A5070"),
    //                 HorizontalTextAlignment = TextAlignment.Center, FontSize = 10, Margin = new Thickness(0, 1) });
    //         }
    //         string bgHex    = e.IsHuman ? "#1C2A0A" : "transparent";
    //         string posColor = e.Position switch { 1 => "#FFD700", 2 => "#C0C0D0", 3 => "#CD7F32", _ => "#7090B0" };
    //         string medal    = e.PositionLabel;
    //         var row = new Grid
    //         {
    //             ColumnDefinitions = new ColumnDefinitionCollection(new(26), new(GridLength.Star), new(GridLength.Auto)),
    //             Padding = new Thickness(2, 2),
    //             BackgroundColor = bgHex == "transparent" ? Colors.Transparent : Color.FromArgb(bgHex)
    //         };
    //         row.Add(new Label { Text = medal, FontSize = 12, TextColor = Color.FromArgb(posColor),
    //             HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center });
    //         string subBadge = e.IsHuman && sub.IsActive ? $" {sub.BadgeIcon}" : "";
    //         string loc      = e.LocationLabel;
    //         var nameLbl = new Label { VerticalOptions = LayoutOptions.Center };
    //         var fmt     = new FormattedString();
    //         fmt.Spans.Add(new Span { Text = e.Name + subBadge,
    //             TextColor = e.IsHuman ? Color.FromArgb("#4CAF50") : e.NameColor, FontSize = 13,
    //             FontAttributes = e.IsHuman ? FontAttributes.Bold : FontAttributes.None });
    //         if (!string.IsNullOrEmpty(loc))
    //             fmt.Spans.Add(new Span { Text = $"  {loc}", TextColor = Color.FromArgb("#506070"), FontSize = 11 });
    //         nameLbl.FormattedText = fmt;
    //         Grid.SetColumn(nameLbl, 1);
    //         row.Add(nameLbl);
    //         var pts = new Label { Text = $"{e.Points:N0} pts",
    //             TextColor = Color.FromArgb(e.Position == 1 ? "#FFD700" : e.Position == 2 ? "#C0C0D0" : e.Position == 3 ? "#CD7F32" : "#607890"),
    //             FontSize = 12, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
    //         Grid.SetColumn(pts, 2);
    //         row.Add(pts);
    //         MiniRankContainer.Children.Add(row);
    //     }
    // }

    // -----------------------------------------------------------------------
    // Admin: 5 toques rápidos no saldo ativa/desativa
    // -----------------------------------------------------------------------
    private int      _adminTapCount = 0;
    private DateTime _lastAdminTap  = DateTime.MinValue;

    private async void OnAdminActivate(object? sender, TappedEventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastAdminTap).TotalSeconds > 1.5)
            _adminTapCount = 0;

        _lastAdminTap = now;
        _adminTapCount++;

        if (_adminTapCount < 5) return;
        _adminTapCount = 0;

        // Sem efeito nenhum (nem alerta) se a conta não for confirmada admin pelo servidor —
        // pra quem não é admin, esse gesto não revela que existe algo aqui.
        if (!AppState.Current.IsAdminMode) return;

        var state = AppState.Current;
        state.AdminUiRevealed = !state.AdminUiRevealed;
        AdminBtn.IsVisible     = state.AdminUiRevealed && !state.Auth.IsAnonymous;

        string msg = state.AdminUiRevealed
            ? "⚙ Controles admin revelados nesta sessão."
            : "Controles admin ocultados.";
        await DisplayAlert("Admin", msg, "OK");
    }

    private async void OnAdminPageClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("AdminPage");

    // -----------------------------------------------------------------------
    // Avatar: toque → abre ProfilePage
    // -----------------------------------------------------------------------
    private async void OnAvatarTapped(object? sender, EventArgs e)
    {
        if (sender is View v) await FlashTap(v);
        await Shell.Current.GoToAsync("//ProfilePage");
    }

    // -----------------------------------------------------------------------
    // Feedback visual de toque
    // -----------------------------------------------------------------------
    private static async Task FlashTap(View card)
    {
        await card.ScaleTo(0.96, 70, Easing.CubicIn);
        await card.ScaleTo(1.00, 80, Easing.CubicOut);
    }

    // -----------------------------------------------------------------------
    // Navegação
    // -----------------------------------------------------------------------
    private async void OnOnlineCardTapped(object? sender, TappedEventArgs e)
    {
        await FlashTap(OnlineCard);
        await Shell.Current.GoToAsync("RandomMatchPage");
    }

    private async void OnCareerTapped(object? sender, TappedEventArgs e)
    {
        await FlashTap(CareerCard);
        await Shell.Current.GoToAsync("CareerPage");
    }

    private async void OnFriendGameTapped(object? sender, TappedEventArgs e)
    {
        await FlashTap(FriendCard);
        await Shell.Current.GoToAsync("FriendInvitePage");
    }

    private async void OnQuickPlayTapped(object? sender, TappedEventArgs e)
    {
        await FlashTap(IaCard);
        AppState.Current.PendingTournamentGame = false;
        AppState.Current.PendingFriendGame     = false;
        await Shell.Current.GoToAsync("GamePage");
    }

    // Liga/Casual — COMENTADO: aguardando base de jogadores
    // private async void OnLeagueClicked(object? sender, TappedEventArgs e)
    //     => await Shell.Current.GoToAsync("LeaguePage");
    // private async void OnSeasonRankingClicked(object? sender, TappedEventArgs e)
    //     => await Shell.Current.GoToAsync("SeasonRankingPage");
    // private async void OnTournamentsClicked(object? sender, EventArgs e)
    //     => await Shell.Current.GoToAsync("TournamentLobbyPage");
    // private async void OnHistoryClicked(object? sender, EventArgs e)
    //     => await Shell.Current.GoToAsync("TournamentHistoryPage");

    private async Task OnChangePasswordAsync()
    {
        var auth = AppState.Current.Auth;

        if (auth.IsAnonymous)
        {
            await DisplayAlert("Aviso", "Visitantes não possuem senha. Crie uma conta para usar esta funcionalidade.", "OK");
            return;
        }

        string? currentPass = await DisplayPromptAsync(
            "Alterar Senha", "Senha atual:", maxLength: 64, keyboard: Keyboard.Default);
        if (currentPass == null) return;

        string? newPass = await DisplayPromptAsync(
            "Alterar Senha", "Nova senha (mín. 6 caracteres):", maxLength: 64, keyboard: Keyboard.Default);
        if (newPass == null) return;
        if (newPass.Length < 6)
        {
            await DisplayAlert("Erro", "A nova senha deve ter pelo menos 6 caracteres.", "OK");
            return;
        }

        string? confirmPass = await DisplayPromptAsync(
            "Alterar Senha", "Confirmar nova senha:", maxLength: 64, keyboard: Keyboard.Default);
        if (confirmPass == null) return;
        if (newPass != confirmPass)
        {
            await DisplayAlert("Erro", "As senhas não conferem.", "OK");
            return;
        }

        var (ok, error) = await auth.TryUpdatePasswordAsync(currentPass, newPass);
        if (!ok)
        {
            await DisplayAlert("Erro", error, "OK");
            return;
        }
        await DisplayAlert("✓ Concluído", "Sua senha foi alterada com sucesso.", "OK");
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        MenuChangePasswordItem.IsVisible = !AppState.Current.Auth.IsAnonymous;
        MenuDropdown.IsVisible    = true;
        MenuDismissOverlay.IsVisible = true;
    }

    private void OnMenuLabelTapped(object? sender, TappedEventArgs e)
        => OnMenuClicked(sender, e);

    private void OnMenuDismiss(object? sender, TappedEventArgs e)
    {
        MenuDropdown.IsVisible    = false;
        MenuDismissOverlay.IsVisible = false;
    }

    private async void OnMenuChangePasswordTapped(object? sender, TappedEventArgs e)
    {
        MenuDropdown.IsVisible    = false;
        MenuDismissOverlay.IsVisible = false;
        await OnChangePasswordAsync();
    }

    private async void OnMenuLogoutTapped(object? sender, TappedEventArgs e)
    {
        MenuDropdown.IsVisible    = false;
        MenuDismissOverlay.IsVisible = false;
        await OnLogoutAsync();
    }

    private async Task OnLogoutAsync()
    {
        bool confirm = await DisplayAlert("Sair", "Deseja sair da sua conta?", "Sair", "Cancelar");
        if (!confirm) return;

        await AppState.Current.Auth.LogoutAsync();
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null) window.Page = new LoginPage();
    }
}
