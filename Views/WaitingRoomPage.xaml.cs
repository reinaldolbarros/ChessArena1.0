using ChessMAUI.Models;
using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class WaitingRoomPage : ContentPage
{
    private CancellationTokenSource? _cts;
    private bool _navigating;

    public WaitingRoomPage()
    {
        InitializeComponent();
    }

    // -----------------------------------------------------------------------
    // Ao entrar: inicializa UI e começa a preencher a sala
    // -----------------------------------------------------------------------
    protected override void OnAppearing()
    {
        base.OnAppearing();

        _navigating = false;
        var mm = AppState.Current.Matchmaking;

        // Exibe frame de código para salas privadas criadas por este jogador
        if (mm.IsPrivate && !string.IsNullOrEmpty(mm.AccessCode))
        {
            PrivateCodeFrame.IsVisible = true;
            AccessCodeLabel.Text       = mm.AccessCode;
        }
        else
        {
            PrivateCodeFrame.IsVisible = false;
        }

        // Desregistra antes de registrar (seguro para re-entrada)
        mm.PlayerJoined -= OnPlayerJoined;
        mm.RoomFull     -= OnRoomFull;
        mm.PlayerJoined += OnPlayerJoined;
        mm.RoomFull     += OnRoomFull;

        // Cabeçalho
        TournTitle.Text    = $"Torneio de {mm.TotalSlots} Jogadores";
        TournSubtitle.Text = $"Mata-mata  •  Prêmio total: $ {mm.BuyIn * mm.TotalSlots * (1m - TournamentService.RakePct):N0}";
        BuyInLabel.Text    = $"$ {mm.BuyIn:N0}";
        TimeLabel.Text     = mm.TimeMinutes > 0 ? $"{mm.TimeMinutes} min" : "Livre";

        // Reconstrói slots com jogadores já na sala (pode ser re-entrada)
        RebuildSlots();
        UpdateProgress();

        // Inicia preenchimento de bots (se ainda não está cheio)
        if (!mm.IsReady)
        {
            _cts = new CancellationTokenSource();
            _ = mm.FillBotsAsync(_cts.Token);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        var mm = AppState.Current.Matchmaking;
        mm.PlayerJoined -= OnPlayerJoined;
        mm.RoomFull     -= OnRoomFull;
    }

    // -----------------------------------------------------------------------
    // Evento: novo jogador entrou — anima o card e exibe nome no status
    // -----------------------------------------------------------------------
    private void OnPlayerJoined(RoomPlayer player)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Flash do nome no status (apenas para bots, não para o próprio jogador)
            if (!player.IsHuman)
            {
                StatusLabel.Text      = $"✓  {player.Name} entrou na sala!";
                StatusLabel.TextColor = Color.FromArgb("#4CAF50");
            }

            // Constrói o card em estado invisível para animação
            var card          = BuildPlayerCard(player);
            card.Opacity      = 0;
            card.TranslationY = 20;

            // Remove o primeiro slot vazio e insere o card
            var emptySlot = SlotsContainer.Children
                .OfType<Border>().FirstOrDefault(f => f.StyleId == "empty");
            if (emptySlot != null)
                SlotsContainer.Children.Remove(emptySlot);

            int insertAt = player.IsHuman
                ? 0
                : Math.Max(0, SlotsContainer.Children.Count - CountEmptySlots());
            SlotsContainer.Children.Insert(insertAt, card);

            UpdateProgress();

            // Slide-in: fade + subida suave
            await Task.WhenAll(
                card.FadeTo(1, 280),
                card.TranslateTo(0, 0, 280, Easing.CubicOut));

            // Restaura texto do status após breve pausa
            if (!player.IsHuman)
            {
                await Task.Delay(800);
                var mm = AppState.Current.Matchmaking;
                if (mm.Players.Count < mm.TotalSlots)
                {
                    StatusLabel.Text      = "Aguardando jogadores...";
                    StatusLabel.TextColor = Color.FromArgb("#E0E0F4");
                }
            }
        });
    }

    // -----------------------------------------------------------------------
    // Evento: sala cheia → contagem regressiva
    // -----------------------------------------------------------------------
    private void OnRoomFull()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_navigating) return;
            _navigating = true;

            CancelBtn.IsVisible      = false;
            CountdownFrame.IsVisible = true;

            for (int i = 3; i >= 1; i--)
            {
                CountdownLabel.Text = $"🎮  SALA COMPLETA!  Iniciando em {i}...";
                await Task.Delay(1000);
            }

            CountdownLabel.Text = "🎮  Iniciando torneio!";
            await Task.Delay(500);

            // Cria o torneio com os jogadores da sala
            var state = AppState.Current;
            var mm    = state.Matchmaking;
            var t     = state.TournSvc.CreateFromRoom(mm.Players, mm.BuyIn,
                              mm.RoomType, mm.SatelliteTarget, mm.BountyPerPlayer);
            state.ActiveTournament      = t;
            state.TournamentTimeMinutes = mm.TimeMinutes;

            // Registra rake administrativo (10% do pool bruto)
            decimal rake = TournamentService.GetRakeAmount(mm.TotalSlots, mm.BuyIn);
            state.Admin.RecordRake(rake,
                $"{mm.TotalSlots} jogadores · $ {mm.BuyIn:N0} buy-in",
                mm.RoomType.ToString(), mm.TotalSlots, mm.BuyIn);

            // Zera estado de partida anterior para não processar resultado fantasma
            state.MatchResultReady      = false;
            state.LastMatchHumanWon     = false;
            state.PendingTournamentGame = false;

            await Shell.Current.GoToAsync("BracketPage");
        });
    }

    // -----------------------------------------------------------------------
    // Botão: copiar código de acesso
    // -----------------------------------------------------------------------
    private async void OnCopyCodeClicked(object? sender, EventArgs e)
    {
        var code = AppState.Current.Matchmaking.AccessCode;
        await Clipboard.Default.SetTextAsync(code);
        CopyCodeBtn.Text = "✓ Copiado";
        await Task.Delay(1500);
        CopyCodeBtn.Text = "Copiar";
    }

    // -----------------------------------------------------------------------
    // Botão: iniciar agora (preenche vagas com bots imediatamente)
    // -----------------------------------------------------------------------
    private void OnStartNowClicked(object? sender, EventArgs e)
    {
        if (_navigating) return;
        _cts?.Cancel();                          // para o fill lento
        StartNowBtn.IsEnabled = false;
        var mm = AppState.Current.Matchmaking;
        // Fecha a sala no lobby para evitar novas entradas
        AppState.Current.RoomLobby.ClosePlayerRoom(mm.AccessCode);
        _ = mm.FillBotsImmediateAsync();         // preenche rápido e dispara RoomFull
    }

    // -----------------------------------------------------------------------
    // Botão: cancelar → devolve buy-in e volta ao lobby
    // -----------------------------------------------------------------------
    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        // Fecha sala no lobby se era criada pelo jogador
        var mm = AppState.Current.Matchmaking;
        if (mm.IsPrivate) AppState.Current.RoomLobby.ClosePlayerRoom(mm.AccessCode);
        await Shell.Current.GoToAsync("..");
    }

    // -----------------------------------------------------------------------
    // Constrói a lista de slots do zero (para re-entradas)
    // -----------------------------------------------------------------------
    private void RebuildSlots()
    {
        SlotsContainer.Children.Clear();

        // Adiciona os jogadores já na sala
        foreach (var p in AppState.Current.Matchmaking.Players)
            AddSlotCard(p);

        // Adiciona slots vazios
        int empty = AppState.Current.Matchmaking.TotalSlots - AppState.Current.Matchmaking.Players.Count;
        for (int i = 0; i < empty; i++)
            SlotsContainer.Children.Add(BuildEmptySlot());
    }

    // -----------------------------------------------------------------------
    // Adiciona um card de jogador sem animação (usado apenas no RebuildSlots)
    // -----------------------------------------------------------------------
    private void AddSlotCard(RoomPlayer player)
    {
        var emptySlot = SlotsContainer.Children
            .OfType<Border>().FirstOrDefault(f => f.StyleId == "empty");
        if (emptySlot != null)
            SlotsContainer.Children.Remove(emptySlot);

        int insertAt = player.IsHuman ? 0 : Math.Max(0, SlotsContainer.Children.Count - CountEmptySlots());
        SlotsContainer.Children.Insert(insertAt, BuildPlayerCard(player));
    }

    private int CountEmptySlots() =>
        SlotsContainer.Children.OfType<Border>().Count(f => f.StyleId == "empty");

    // -----------------------------------------------------------------------
    // Card de jogador ocupado
    // -----------------------------------------------------------------------
    private static Border BuildPlayerCard(RoomPlayer player)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }
        };

        grid.Add(new Label
        {
            Text            = player.Avatar,
            FontSize        = 20,
            VerticalOptions = LayoutOptions.Center,
            Margin          = new Thickness(0, 0, 10, 0)
        });

        var nameStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 1 };
        nameStack.Add(new Label
        {
            Text           = player.Name,
            TextColor      = Colors.White,
            FontSize       = 14,
            FontAttributes = player.IsHuman ? FontAttributes.Bold : FontAttributes.None
        });
        int    playerRating = player.IsHuman ? AppState.Current.Profile.Points : ProfileService.EloRatingForAI(player.Strength);
        string sub          = player.IsHuman ? $"Você  ·  Rating {playerRating}" : $"Rating {playerRating}";
        nameStack.Add(new Label
        {
            Text      = sub,
            TextColor = player.IsHuman ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0F4"),
            FontSize  = 15
        });
        Grid.SetColumn(nameStack, 1);
        grid.Add(nameStack);

        var statusLbl = new Label
        {
            Text              = "✓ Pronto",
            TextColor         = Color.FromArgb("#4CAF50"),
            FontSize          = 12,
            FontAttributes    = FontAttributes.Bold,
            VerticalOptions   = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        Grid.SetColumn(statusLbl, 2);
        grid.Add(statusLbl);

        return new Border
        {
            BackgroundColor = player.IsHuman ? Color.FromArgb("#1C2A1C") : Color.FromArgb("#16213E"),
            Stroke          = new SolidColorBrush(player.IsHuman ? Color.FromArgb("#4CAF50") : Color.FromArgb("#0F3460")),
            StrokeThickness = 1,
            StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding         = new Thickness(12, 8),
            Content         = grid
        };
    }

    // -----------------------------------------------------------------------
    // Slot vazio
    // -----------------------------------------------------------------------
    private static Border BuildEmptySlot()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }
        };

        grid.Add(new Label
        {
            Text            = "⏳",
            FontSize        = 20,
            VerticalOptions = LayoutOptions.Center,
            Margin          = new Thickness(0, 0, 10, 0)
        });

        var nameLbl = new Label
        {
            Text            = "Aguardando...",
            TextColor       = Color.FromArgb("#6A6A99"),
            FontSize        = 14,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(nameLbl, 1);
        grid.Add(nameLbl);

        return new Border
        {
            StyleId         = "empty",
            BackgroundColor = Color.FromArgb("#12172A"),
            Stroke          = new SolidColorBrush(Color.FromArgb("#252B45")),
            StrokeThickness = 1,
            StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding         = new Thickness(14, 10),
            Content         = grid
        };
    }

    // -----------------------------------------------------------------------
    // Atualiza barra de progresso e contador
    // -----------------------------------------------------------------------
    private void UpdateProgress()
    {
        var mm      = AppState.Current.Matchmaking;
        int current = mm.Players.Count;
        int total   = mm.TotalSlots;

        CounterLabel.Text  = $"{current}/{total}";
        JoinProgress.Progress = (double)current / total;

        StatusLabel.Text = current == total
            ? "Sala completa!"
            : $"Aguardando jogadores...";
    }
}
