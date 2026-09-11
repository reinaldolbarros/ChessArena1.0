using ChessMAUI.Models;
using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class TournamentLobbyPage : ContentPage
{
    private static readonly decimal[]        BuyInFilters = [0, 10, 25, 50, 100, 250, 500, 1000, 2500];
    private static readonly TournamentType[] TypeFilters  =
    [
        TournamentType.Standard, TournamentType.HeadsUp,   TournamentType.Bounty,
        TournamentType.Satellite, TournamentType.Turbo,    TournamentType.HyperTurbo
    ];

    private TournamentType? _activeType   = null;
    private decimal         _activeFilter = 0;
    private bool            _premiumOnly  = false;
    private bool            _isCustom     = false;

    // Seleções do painel personalizado
    private int            _customSize      = 8;
    private int            _customTime      = 5;
    private decimal        _customBuyIn     = 25;
    private bool           _customIsPrivate = false;
    private CancellationTokenSource? _refreshCts;

    public TournamentLobbyPage()
    {
        InitializeComponent();
        BuildTypeTabs();
        BuildFilterBar();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BalanceLabel.Text = $"Rating: {AppState.Current.Profile.Points}";
        var svc = AppState.Current.RoomLobby;
        svc.RoomsUpdated -= OnRoomsUpdated;
        svc.RoomsUpdated += OnRoomsUpdated;
        if (_isCustom) RebuildCustomSummary();
        else RefreshList();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AppState.Current.RoomLobby.RoomsUpdated -= OnRoomsUpdated;
        _refreshCts?.Cancel();
    }

    private void OnRoomsUpdated()
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;
        Task.Run(async () =>
        {
            await Task.Delay(600, token);
            if (!token.IsCancellationRequested)
                MainThread.BeginInvokeOnMainThread(RefreshList);
        }, token);
    }

    // -----------------------------------------------------------------------
    // Abas de tipo
    // -----------------------------------------------------------------------
    private static readonly Dictionary<TournamentType, string> TypeTooltips = new()
    {
        { TournamentType.Standard,   "Mata-mata clássico com partidas de 5 a 20 minutos. Elimine os adversários e seja o campeão." },
        { TournamentType.HeadsUp,    "Duelo 1 contra 1. Melhor de 3 partidas decide o vencedor." },
        { TournamentType.Bounty,     "Além de avançar no torneio, você ganha uma recompensa em dinheiro por cada adversário eliminado." },
        { TournamentType.Satellite,  "Ganhe uma vaga gratuita (ticket) para participar de torneios de buy-in maior." },
        { TournamentType.Turbo,      "Tempo reduzido por jogada. Partidas rápidas que exigem raciocínio ágil." },
        { TournamentType.HyperTurbo, "Tempo mínimo por jogada. Formato extremamente veloz, ideal para quem domina o blitz." },
    };

    private void BuildTypeTabs()
    {
        TypeTabBar.Children.Clear();

        AddTypeTab(null, "Todos",            false, tooltip: "Exibe todos os torneios disponíveis.");
        AddTypeTab(null, "💎 Premium",       true,  tooltip: "Torneios de alto valor com buy-in a partir de $ 500.");
        AddTypeTab(null, "✏ Personalizado", false,  tooltip: "Crie seu próprio torneio com formato, tamanho e buy-in personalizados.", isCustomTab: true);
        foreach (var t in TypeFilters)
        {
            string label = t switch
            {
                TournamentType.Standard   => "Clássico",
                TournamentType.HeadsUp    => "⚔ Duelo",
                TournamentType.Bounty     => "🎯 Bounty",
                TournamentType.Satellite  => "🎟 Passaporte",
                TournamentType.Turbo      => "⚡ Turbo",
                TournamentType.HyperTurbo => "🔥 Hyper",
                _ => t.ToString()
            };
            string tip = TypeTooltips.TryGetValue(t, out var s) ? s : "";
            AddTypeTab(t, label, false, tooltip: tip);
        }
    }

    private void AddTypeTab(TournamentType? type, string label, bool isPremiumTab,
                            string tooltip = "", bool isCustomTab = false)
    {
        bool active = isCustomTab  ? _isCustom
                    : isPremiumTab ? (_premiumOnly && !_isCustom)
                    : (!_premiumOnly && !_isCustom && _activeType == type);

        Color bg = isCustomTab  ? (active ? Color.FromArgb("#7B68EE") : Color.FromArgb("#1E1A3A"))
                 : isPremiumTab ? (active ? Color.FromArgb("#FFD700")  : Color.FromArgb("#3A2800"))
                 : (active      ? Color.FromArgb("#FFD700")            : Color.FromArgb("#1C2A4A"));

        var btn = new Button
        {
            Text            = label,
            FontSize        = 11,
            CornerRadius    = 14,
            Padding         = new Thickness(10, 5),
            Margin          = new Thickness(0, 0, 4, 4),
            BackgroundColor = bg,
            TextColor       = active ? Colors.Black : Colors.White
        };
        if (!string.IsNullOrEmpty(tooltip))
            ToolTipProperties.SetText(btn, tooltip);
        btn.Clicked += (_, _) =>
        {
            _isCustom    = isCustomTab;
            _premiumOnly = !isCustomTab && isPremiumTab;
            _activeType  = (isCustomTab || isPremiumTab) ? null : type;
            BuildTypeTabs();
            if (_isCustom) { FilterScrollView.IsVisible = false; TableHeader.IsVisible = false;
                             RoomScrollView.IsVisible = false;   CustomPanel.IsVisible = true;
                             BuildCustomPanel(); }
            else           { FilterScrollView.IsVisible = true;  TableHeader.IsVisible = true;
                             RoomScrollView.IsVisible = true;    CustomPanel.IsVisible = false;
                             BuildFilterBar(); RefreshList(); }
        };
        TypeTabBar.Children.Add(btn);
    }

    // -----------------------------------------------------------------------
    // Filtros de buy-in (adapta conforme tab ativo)
    // -----------------------------------------------------------------------
    private void BuildFilterBar()
    {
        FilterBar.Children.Clear();

        decimal[] filters = _premiumOnly
            ? [0, 500, 1000, 2500]
            : BuyInFilters;

        foreach (var f in filters)
        {
            string label = f == 0 ? "Todos" : $"$ {f:N0}";
            bool active  = f == _activeFilter;
            var btn = new Button
            {
                Text            = label,
                FontSize        = 11,
                CornerRadius    = 14,
                Padding         = new Thickness(12, 4),
                BackgroundColor = active
                    ? (_premiumOnly ? Color.FromArgb("#B8860B") : Color.FromArgb("#0F3460"))
                    : Color.FromArgb("#252B45"),
                TextColor = Colors.White
            };
            decimal captured = f;
            btn.Clicked += (_, _) => { _activeFilter = captured; BuildFilterBar(); RefreshList(); };
            FilterBar.Children.Add(btn);
        }
    }

    // -----------------------------------------------------------------------
    // Lista de salas
    // -----------------------------------------------------------------------
    private void RefreshList()
    {
        var all = AppState.Current.RoomLobby.Rooms.AsEnumerable();

        if (_premiumOnly)
        {
            // Tab Premium: apenas salas de alto valor
            all = all.Where(r => r.IsHighStakes);
        }
        else if (_activeType != null)
        {
            // Tab de tipo específico: exclui premium (exclusivo das abas Todos e Premium)
            all = all.Where(r => r.Type == _activeType && !r.IsHighStakes);
        }
        // else: "Todos" — mostra tudo incluindo premium

        if (_activeFilter > 0)
            all = all.Where(r => r.BuyIn == _activeFilter);

        var rooms = all
            .OrderBy(r => r.BuyIn).ThenBy(r => r.Type).ThenBy(r => r.Size)
            .ToList();

        RoomList.Children.Clear();

        if (_premiumOnly)
        {
            // Agrupa por evento (Grand Prix / Master Series / Elite Cup)
            foreach (var group in rooms.GroupBy(r => r.HighStakesName))
            {
                RoomList.Children.Add(BuildPremiumHeader(group.Key, group.First().BuyIn));
                bool a = false;
                foreach (var room in group) { RoomList.Children.Add(BuildRow(room, a)); a = !a; }
            }
        }
        else
        {
            bool alt = false;
            foreach (var room in rooms) { RoomList.Children.Add(BuildRow(room, alt)); alt = !alt; }
        }
    }

    private View BuildPremiumHeader(string name, decimal buyIn)
    {
        string subtitle = buyIn switch
        {
            >= 2500 => "Buy-in $ 2.500  ·  Pool até $ 40.000",
            >= 1000 => "Buy-in $ 1.000  ·  Pool até $ 16.000",
            _       => "Buy-in $ 500  ·  Pool até $ 8.000"
        };
        return new Border
        {
            BackgroundColor = Color.FromArgb("#2A1D00"),
            Stroke          = new SolidColorBrush(Color.FromArgb("#FFD700")),
            StrokeThickness = 1,
            StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 0 },
            Padding         = new Thickness(14, 8),
            Content         = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = $"💎  {name}", TextColor = Color.FromArgb("#FFD700"),
                                FontSize = 14, FontAttributes = FontAttributes.Bold },
                    new Label { Text = subtitle, TextColor = Color.FromArgb("#CCAA55"), FontSize = 10 }
                }
            }
        };
    }

    private Grid BuildRow(TournamentRoom room, bool alt)
    {
        var profile    = AppState.Current.Profile;
        bool hasTicket = false;

        // Fundo especial para alto valor
        Color bg = room.IsHighStakes
            ? Color.FromArgb("#1C1400")
            : (alt ? Color.FromArgb("#16213E") : Color.FromArgb("#12192E"));

        var row = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(72), new(72), new(72), new(60) },
            BackgroundColor   = bg,
            Padding           = new Thickness(14, 9)
        };

        // Coluna 0: badge tipo + nome + status
        var nameStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2 };

        // Badge colorido do tipo + alto valor
        var badgeRow = new HorizontalStackLayout { Spacing = 6 };
        if (room.IsHighStakes)
            badgeRow.Add(new Label { Text = "💎", FontSize = 11 });
        if (!string.IsNullOrEmpty(room.TypeBadge))
            badgeRow.Add(new Label { Text = room.TypeBadge, FontSize = 11 });
        badgeRow.Add(new Label
        {
            Text      = room.IsHighStakes ? $"{room.HighStakesName} · {room.TypeLabel}" : room.TypeLabel,
            TextColor = room.IsHighStakes ? Color.FromArgb("#FFD700") : TypeColor(room.Type),
            FontSize  = 10, FontAttributes = FontAttributes.Bold
        });
        if (hasTicket)
            badgeRow.Add(new Label { Text = "🎟 Ticket", TextColor = Color.FromArgb("#4CAF50"), FontSize = 10, FontAttributes = FontAttributes.Bold });
        if (room.IsPlayerCreated)
            badgeRow.Add(new Label { Text = $"👤 {room.CreatorName}", TextColor = Color.FromArgb("#BB99FF"), FontSize = 10, FontAttributes = FontAttributes.Bold });
        if (room.IsPrivate)
            badgeRow.Add(new Label { Text = "🔒 Privado", TextColor = Color.FromArgb("#FF9800"), FontSize = 10, FontAttributes = FontAttributes.Bold });
        nameStack.Add(badgeRow);

        // Nome + tamanho
        string roomTitle = room.IsHighStakes
            ? $"{room.HighStakesName}  {SizeEmoji(room.Size)}"
            : $"{SizeEmoji(room.Size)}  #{room.Id}";
        nameStack.Add(new Label
        {
            Text      = roomTitle,
            TextColor = room.IsHighStakes ? Color.FromArgb("#FFD700") : Colors.White,
            FontSize  = 13, FontAttributes = FontAttributes.Bold
        });

        // Info extra por tipo
        string satelliteTargetName = room.SatelliteTarget switch
        {
            >= 2500 => "Elite Cup",
            >= 1000 => "Master Series",
            >= 500  => "Grand Prix",
            _       => $"$ {room.SatelliteTarget:N0}"
        };
        string extra = room.Type switch
        {
            TournamentType.Bounty    => $"🎯 Bounty: $ {room.BountyPerPlayer:N0}/elim.",
            TournamentType.Satellite => $"🎟 Vaga: {satelliteTargetName} ($ {room.SatelliteTarget:N0})",
            TournamentType.HeadsUp   => "Melhor de 3",
            _ => room.StatusLabel
        };
        nameStack.Add(new Label { Text = extra, TextColor = room.StatusColor, FontSize = 10 });
        row.Add(nameStack);

        // Jogadores + barra
        var progStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Spacing = 3 };
        progStack.Add(new Label
        {
            Text = $"{room.Joined}/{room.Size}", TextColor = Colors.White, FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center
        });
        progStack.Add(new ProgressBar
        {
            Progress        = room.Size > 0 ? (double)room.Joined / room.Size : 0,
            ProgressColor   = room.CanJoin ? Color.FromArgb("#4CAF50") : Color.FromArgb("#555555"),
            BackgroundColor = Color.FromArgb("#0F3460"),
            HeightRequest   = 4
        });
        Grid.SetColumn(progStack, 1);
        row.Add(progStack);

        // Buy-in
        string buyInText = hasTicket ? $"🎟\n$ {room.BuyIn:N0}" : $"$ {room.BuyIn:N0}";
        var buyInLbl = new Label
        {
            Text      = buyInText,
            TextColor = hasTicket ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FFD700"),
            FontSize  = room.IsHighStakes ? 13 : 12, FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(buyInLbl, 2);
        row.Add(buyInLbl);

        // Prêmio
        string prizeText = room.Type == TournamentType.Satellite
            ? $"🎟 Vaga\n{satelliteTargetName}"
            : room.IsHighStakes
                ? $"💎 $ {room.PrizePool:N0}"
                : $"$ {room.PrizePool:N0}";
        var prizeLbl = new Label
        {
            Text = prizeText,
            TextColor = room.IsHighStakes ? Color.FromArgb("#FFD700") : Color.FromArgb("#4CAF50"),
            FontSize = 11, HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(prizeLbl, 3);
        row.Add(prizeLbl);

        // Tempo
        var timeLbl = new Label
        {
            Text = room.TimeLabel, TextColor = Color.FromArgb("#AAAACC"),
            FontSize = 11, HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(timeLbl, 4);
        row.Add(timeLbl);

        if (room.CanJoin)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OnRoomTapped(room);
            row.GestureRecognizers.Add(tap);
        }
        else
        {
            row.Opacity = 0.45;
        }

        return row;
    }

    // -----------------------------------------------------------------------
    // Painel personalizado
    // -----------------------------------------------------------------------
    private static readonly int[]     CustomSizes   = [2, 8, 16, 32, 64, 128];
    private static readonly decimal[] CustomBuyIns  = [10, 25, 50, 100, 250, 500, 750, 1000];

    // Label de resumo — atualizada a cada seleção
    private Label? _customSummaryLabel;
    private Button? _createBtn;

    private void BuildCustomPanel()
    {
        CustomContainer.Children.Clear();

        // ── Visibilidade: Público / Privado ──────────────────────────────────
        var visibilityRow = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
            ColumnSpacing     = 8
        };
        var btnPublic = new Button
        {
            Text            = "🌐  Público",
            FontSize        = 13,
            CornerRadius    = 10,
            HeightRequest   = 44,
            BackgroundColor = !_customIsPrivate ? Color.FromArgb("#0F3460") : Color.FromArgb("#1C2440"),
            TextColor       = !_customIsPrivate ? Colors.White : Color.FromArgb("#AAAACC"),
            FontAttributes  = !_customIsPrivate ? FontAttributes.Bold : FontAttributes.None
        };
        var btnPrivate = new Button
        {
            Text            = "🔒  Privado",
            FontSize        = 13,
            CornerRadius    = 10,
            HeightRequest   = 44,
            BackgroundColor = _customIsPrivate ? Color.FromArgb("#2A1D3A") : Color.FromArgb("#1C2440"),
            TextColor       = _customIsPrivate ? Color.FromArgb("#BB99FF") : Color.FromArgb("#AAAACC"),
            FontAttributes  = _customIsPrivate ? FontAttributes.Bold : FontAttributes.None
        };
        btnPublic.Clicked  += (_, _) => { _customIsPrivate = false;  BuildCustomPanel(); };
        btnPrivate.Clicked += (_, _) => { _customIsPrivate = true;   BuildCustomPanel(); };
        Grid.SetColumn(btnPrivate, 1);
        visibilityRow.Add(btnPublic);
        visibilityRow.Add(btnPrivate);

        var visSection = new VerticalStackLayout { Spacing = 6 };
        visSection.Add(new Label { Text = "Visibilidade", TextColor = Color.FromArgb("#AAAACC"),
                                   FontSize = 12, FontAttributes = FontAttributes.Bold });
        visSection.Add(visibilityRow);
        if (_customIsPrivate)
            visSection.Add(new Label
            {
                Text      = "Um código único será gerado. Compartilhe com seus amigos para que entrem no torneio.",
                TextColor = Color.FromArgb("#888899"), FontSize = 11,
                Margin    = new Thickness(0, 4, 0, 0)
            });
        CustomContainer.Children.Add(visSection);

        // ── Nº de Jogadores ─────────────────────────────────────────────────
        CustomContainer.Children.Add(BuildCustomSection("Nº de Jogadores",
            CustomSizes.Select(s => SizeEmoji(s)).ToArray(),
            CustomSizes.Select(s => (object)s).ToArray(),
            _customSize,
            v => { _customSize = (int)v; RebuildCustomSummary(); }));

        // ── Tempo por Partida ───────────────────────────────────────────────
        CustomContainer.Children.Add(BuildTimeStepper(1, 20));

        // ── Buy-in ──────────────────────────────────────────────────────────
        CustomContainer.Children.Add(BuildCustomSection("Buy-in",
            CustomBuyIns.Select(b => $"$ {b:N0}").ToArray(),
            CustomBuyIns.Select(b => (object)b).ToArray(),
            _customBuyIn,
            v => { _customBuyIn = (decimal)v; RebuildCustomSummary(); }));

        // ── Resumo + botão ──────────────────────────────────────────────────
        _customSummaryLabel = new Label
        {
            TextColor = Color.FromArgb("#4CAF50"),
            FontSize  = 13,
            HorizontalTextAlignment = TextAlignment.Center
        };

        _createBtn = new Button
        {
            Text            = "CRIAR TORNEIO",
            BackgroundColor = Color.FromArgb("#7B68EE"),
            TextColor       = Colors.White,
            FontAttributes  = FontAttributes.Bold,
            FontSize        = 15,
            CornerRadius    = 12,
            HeightRequest   = 52
        };
        _createBtn.Clicked += async (_, _) => await OnCreateCustomTournament();

        var summaryFrame = new Border
        {
            BackgroundColor = Color.FromArgb("#16213E"),
            Stroke          = new SolidColorBrush(Color.FromArgb("#7B68EE")),
            StrokeThickness = 1,
            StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding         = new Thickness(16, 14),
            Content         = new VerticalStackLayout
            {
                Spacing  = 12,
                Children = { _customSummaryLabel, _createBtn }
            }
        };
        CustomContainer.Children.Add(summaryFrame);
        RebuildCustomSummary();
    }

    private View BuildTimeStepper(int min, int max)
    {
        var valueLabel = new Label
        {
            Text              = $"{_customTime} min",
            TextColor         = Colors.White,
            FontSize          = 18,
            FontAttributes    = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions   = LayoutOptions.Center,
            MinimumWidthRequest = 80,
            HorizontalTextAlignment = TextAlignment.Center
        };

        Button MakeBtn(string text, int delta) => new()
        {
            Text            = text,
            FontSize        = 20,
            CornerRadius    = 24,
            WidthRequest    = 48,
            HeightRequest   = 48,
            Padding         = Thickness.Zero,
            BackgroundColor = Color.FromArgb("#252B45"),
            TextColor       = Colors.White
        };

        var btnMinus = MakeBtn("−", -1);
        var btnPlus  = MakeBtn("+",  1);

        btnMinus.Clicked += (_, _) =>
        {
            _customTime = Math.Max(min, _customTime - 1);
            valueLabel.Text = $"{_customTime} min";
            RebuildCustomSummary();
        };
        btnPlus.Clicked += (_, _) =>
        {
            _customTime = Math.Min(max, _customTime + 1);
            valueLabel.Text = $"{_customTime} min";
            RebuildCustomSummary();
        };

        var row = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
            VerticalOptions   = LayoutOptions.Center
        };
        row.Add(btnMinus);
        Grid.SetColumn(valueLabel, 1);
        row.Add(valueLabel);
        Grid.SetColumn(btnPlus, 2);
        row.Add(btnPlus);

        var section = new VerticalStackLayout { Spacing = 8 };
        section.Add(new Label
        {
            Text = "Tempo por Partida", TextColor = Color.FromArgb("#AAAACC"),
            FontSize = 12, FontAttributes = FontAttributes.Bold
        });
        section.Add(row);
        return section;
    }

    private View BuildCustomSection(string title, string[] labels, object[] values,
                                    object selected, Action<object> onSelect)
    {
        var section = new VerticalStackLayout { Spacing = 8 };
        section.Add(new Label
        {
            Text = title, TextColor = Color.FromArgb("#AAAACC"),
            FontSize = 12, FontAttributes = FontAttributes.Bold
        });

        var chips = new FlexLayout
        {
            Wrap            = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent  = Microsoft.Maui.Layouts.FlexJustify.Start,
            Direction       = Microsoft.Maui.Layouts.FlexDirection.Row
        };

        for (int i = 0; i < labels.Length; i++)
        {
            var capturedVal   = values[i];
            bool isActive     = capturedVal.Equals(selected);
            var chip = new Button
            {
                Text            = labels[i],
                FontSize        = 12,
                CornerRadius    = 20,
                Padding         = new Thickness(16, 8),
                Margin          = new Thickness(0, 0, 6, 6),
                BackgroundColor = isActive ? Color.FromArgb("#7B68EE") : Color.FromArgb("#252B45"),
                TextColor       = isActive ? Colors.White : Color.FromArgb("#AAAACC")
            };
            chip.Clicked += (_, _) => { onSelect(capturedVal); BuildCustomPanel(); };
            chips.Add(chip);
        }
        section.Add(chips);
        return section;
    }

    private void RebuildCustomSummary()
    {
        if (_customSummaryLabel == null || _createBtn == null) return;

        decimal pool = _customBuyIn * _customSize;
        string  time = $"{_customTime} min";

        _customSummaryLabel.Text =
            $"{_customSize} jogadores  ·  {time}\n" +
            $"Buy-in: $ {_customBuyIn:N0}  →  Prêmio total: $ {pool:N0}";

        bool canAfford = true;
        _createBtn.BackgroundColor = canAfford
            ? Color.FromArgb("#7B68EE")
            : Color.FromArgb("#555555");
    }

    private async Task OnCreateCustomTournament()
    {
        var profile = AppState.Current.Profile;
        var lobby   = AppState.Current.RoomLobby;

        // Gera código para sala privada; públicas também têm código interno
        string code = RoomLobbyService.GenerateAccessCode();

        // Registra no lobby
        var playerRoom = new TournamentRoom
        {
            Size            = _customSize,
            BuyIn           = _customBuyIn,
            TimeMinutes     = _customTime,
            Type            = TournamentType.Standard,
            IsPlayerCreated = true,
            CreatorName     = profile.Name,
            IsPrivate       = _customIsPrivate,
            AccessCode      = code,
            Joined          = 1
        };
        lobby.CreatePlayerRoom(playerRoom);

        AppState.Current.Matchmaking.CreateRoom(
            _customSize, _customBuyIn, _customTime,
            profile.Name, profile.Avatar,
            TournamentType.Standard, satelliteTarget: 0,
            isPrivate: _customIsPrivate, accessCode: code);

        await Shell.Current.GoToAsync("WaitingRoomPage");
    }

    private static Color TypeColor(TournamentType t) => t switch
    {
        TournamentType.HeadsUp    => Color.FromArgb("#FF9800"),
        TournamentType.Bounty     => Color.FromArgb("#F44336"),
        TournamentType.Satellite  => Color.FromArgb("#2196F3"),
        TournamentType.Turbo      => Color.FromArgb("#FFEB3B"),
        TournamentType.HyperTurbo => Color.FromArgb("#FF5722"),
        _                         => Color.FromArgb("#4CAF50")
    };

    private static string SizeEmoji(int size) => size switch
    {
        2   => "⚔ 2",
        8   => "⚡ 8",
        16  => "🔥 16",
        32  => "💎 32",
        64  => "👑 64",
        128 => "🏆 128",
        _   => size.ToString()
    };

    // -----------------------------------------------------------------------
    // Validação e entrada na sala
    // -----------------------------------------------------------------------
    private async Task OnRoomTapped(TournamentRoom room)
    {
        var profile = AppState.Current.Profile;

        // Sala privada: pede o código antes de prosseguir
        if (room.IsPrivate)
        {
            string? code = await DisplayPromptAsync("Torneio Privado",
                "Digite o código de acesso:", "Entrar", "Cancelar",
                placeholder: "Ex: ABC123", maxLength: 6);
            if (string.IsNullOrWhiteSpace(code)) return;

            if (!room.AccessCode.Equals(code.Trim().ToUpper(), StringComparison.Ordinal))
            {
                await DisplayAlert("Código incorreto",
                    "O código informado não confere com esta sala.", "OK");
                return;
            }
        }

        AppState.Current.RoomLobby.JoinRoom(room);
        AppState.Current.Matchmaking.CreateRoom(
            room.Size, room.BuyIn, room.TimeMinutes,
            profile.Name, profile.Avatar,
            room.Type, room.SatelliteTarget, room.BountyPerPlayer);

        await Shell.Current.GoToAsync("WaitingRoomPage");
    }

    private async void OnRankingClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//RankingPage");
}
