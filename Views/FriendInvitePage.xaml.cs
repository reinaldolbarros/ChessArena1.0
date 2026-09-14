using ChessMAUI.Models;
using ChessMAUI.Services;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Postgrest.Constants;

namespace ChessMAUI.Views;

public partial class FriendInvitePage : ContentPage
{
    // ── Constantes ────────────────────────────────────────────────────────────
    private const string PrefKeyTime   = "friend_time_minutes";
    private const int    MaxTime       = 20;
    private const int    CodeExpiryMin = 10;

    private static readonly char[] CodeAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray(); // sem I, O, 0, 1

    // ── Estado da tela ────────────────────────────────────────────────────────
    private enum FriendMode { OnlineCreate, OnlineJoin }

    private FriendMode         _mode = FriendMode.OnlineCreate;
    private int                _selectedMinutes;
    private SupabaseChallenge? _foundChallenge; // resultado da busca online (aba "Entrar")

    // Desafios que EU criei e ainda estão pendentes — ficam listados (com contagem
    // regressiva) até serem aceitos ou expirarem, mesmo saindo desta tela e voltando depois.
    // Chave = código (já único e conhecido antes mesmo de inserir no servidor).
    private readonly Dictionary<string, SupabaseChallenge> _activeChallenges = new();
    private readonly Dictionary<string, Label>             _countdownLabels = new();
    private readonly Dictionary<string, Border>             _challengeRows  = new();
    private IDispatcherTimer? _countdownTimer;
    private bool              _subscribedToOwnChallenges;

    // ── Init ──────────────────────────────────────────────────────────────────
    public FriendInvitePage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SetTime(Preferences.Default.Get(PrefKeyTime, 0));
        SetSubMode(FriendMode.OnlineCreate);
        _ = LoadActiveChallengesAsync();

        // Se o app foi aberto por um link de convite (chessarena://invite?code=...) antes de
        // chegar aqui, o código já está esperando — preenche e tenta aceitar sozinho.
        var pendingCode = AppState.Current.PendingInviteCode;
        if (!string.IsNullOrEmpty(pendingCode))
        {
            AppState.Current.PendingInviteCode = null;
            SetSubMode(FriendMode.OnlineJoin);
            CodeEntry.Text = pendingCode;
            _ = SearchOrAccept();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _countdownTimer?.Stop();
        _countdownTimer = null;
    }

    // ── Troca de sub-modo ──────────────────────────────────────────────────────

    private void OnSubTabCriarTapped(object? sender, TappedEventArgs e) =>
        SetSubMode(FriendMode.OnlineCreate);

    private void OnSubTabEntrarTapped(object? sender, TappedEventArgs e) =>
        SetSubMode(FriendMode.OnlineJoin);

    private void SetSubMode(FriendMode sub)
    {
        bool isCriar = sub == FriendMode.OnlineCreate;
        _mode = sub;

        ApplyTabStyle(SubTabCriarCard,  SubTabCriarLabel,  isCriar);
        ApplyTabStyle(SubTabEntrarCard, SubTabEntrarLabel, !isCriar);

        // Trocar de aba NÃO mexe nos desafios ativos — eles continuam contando mesmo que o
        // usuário esteja olhando "Entrar com Código" ou saia da tela e volte depois.
        CriarSection.IsVisible  = isCriar;
        EntrarSection.IsVisible = !isCriar;
        TimeCard.IsVisible      = isCriar;

        if (!isCriar)
        {
            _foundChallenge                  = null;
            ChallengeFoundCard.IsVisible    = false;
            ChallengeNotFoundCard.IsVisible = false;
            CodeEntry.Text = "";
        }

        UpdateActionBtn();
    }

    private static void ApplyTabStyle(Border card, Label label, bool active)
    {
        card.BackgroundColor = active ? Color.FromArgb("#071F3C") : Color.FromArgb("#040F1A");
        card.Stroke          = new SolidColorBrush(
            active ? Color.FromArgb("#2E7DDB") : Color.FromArgb("#1E3A5A"));
        card.StrokeThickness = active ? 1.5 : 1.0;
        label.TextColor      = active ? Color.FromArgb("#4AA3FF") : Color.FromArgb("#6A8AAA");
    }

    private void UpdateActionBtn()
    {
        (ActionBtn.Text, ActionBtn.BackgroundColor, ActionBtn.IsEnabled) = _mode switch
        {
            FriendMode.OnlineCreate =>
                ("Desafiar",              Color.FromArgb("#2A67B1"), true),

            FriendMode.OnlineJoin when ChallengeFoundCard.IsVisible =>
                ("Aceitar e Jogar",       Color.FromArgb("#1F6B36"), true),

            _ =>
                ("Buscar Desafio",        Color.FromArgb("#2A67B1"), true),
        };
    }

    // ── Stepper de tempo ──────────────────────────────────────────────────────

    private void OnDecrease(object? sender, EventArgs e) =>
        SetTime(_selectedMinutes == 0 ? 0 : _selectedMinutes - 1);

    private void OnIncrease(object? sender, EventArgs e) =>
        SetTime(_selectedMinutes == 0 ? 1 : _selectedMinutes + 1);

    private void SetTime(int value)
    {
        _selectedMinutes = Math.Clamp(value, 0, MaxTime);
        Preferences.Default.Set(PrefKeyTime, _selectedMinutes);

        bool unlimited = _selectedMinutes == 0;
        TimeValueLabel.Text = unlimited ? "∞" : _selectedMinutes.ToString();
        TimeUnitLabel.Text  = unlimited ? "sem limite" : "minuto(s)";

        BtnDecrease.IsEnabled = _selectedMinutes > 0;
        BtnIncrease.IsEnabled = _selectedMinutes < MaxTime;
    }

    // ── Dispatcher do botão de ação ───────────────────────────────────────────

    private async void OnActionClicked(object? sender, EventArgs e)
    {
        switch (_mode)
        {
            case FriendMode.OnlineCreate: await GenerateChallengeCode(); break;
            case FriendMode.OnlineJoin:   await SearchOrAccept();        break;
        }
    }

    // ── Fluxo: Criar código ───────────────────────────────────────────────────

    private async Task GenerateChallengeCode()
    {
        var svc = SupabaseService.Instance;
        if (!svc.IsReady)
        {
            await DisplayAlert("Sem conexão", "Não foi possível conectar ao servidor. Verifique sua internet e tente de novo.", "OK");
            return;
        }

        string challenger = AppState.Current.Profile.Name ?? "Desafiante";
        var challenge = new SupabaseChallenge
        {
            Code           = MakeCode(),
            ChallengerId   = AppState.Current.Auth.UserId,
            ChallengerName = challenger,
            TimeMinutes    = _selectedMinutes,
            Status         = "pending",
            ExpiresAt      = DateTime.UtcNow.AddMinutes(CodeExpiryMin),
        };

        try
        {
            await svc.Client.From<SupabaseChallenge>().Insert(challenge);
        }
        catch
        {
            await DisplayAlert("Erro", "Não foi possível criar o desafio. Tente novamente.", "OK");
            return;
        }

        AddChallengeRow(challenge);
        StartCountdownTimerIfNeeded();
        SubscribeToOwnChallengesOnce();

        await ShareCode(challenge.Code);
    }

    // ── Lista de desafios ativos ──────────────────────────────────────────────

    /// <summary>Busca (de novo) os desafios que eu criei e ainda estão pendentes — chamado ao
    /// abrir a tela, pra a lista continuar aparecendo mesmo depois de sair e voltar.</summary>
    private async Task LoadActiveChallengesAsync()
    {
        var svc = SupabaseService.Instance;
        if (!svc.IsReady) return;

        try
        {
            string myId = AppState.Current.Auth.UserId;
            var result = await svc.Client
                .From<SupabaseChallenge>()
                .Filter("challenger_id", Operator.Equals, myId)
                .Filter("status",        Operator.Equals, "pending")
                .Get();

            var active = result.Models.Where(c => c.ExpiresAt > DateTime.UtcNow).ToList();
            if (active.Count == 0) return;

            foreach (var ch in active)
                if (!_activeChallenges.ContainsKey(ch.Code))
                    AddChallengeRow(ch);

            StartCountdownTimerIfNeeded();
            SubscribeToOwnChallengesOnce();
        }
        catch { /* sem internet — a lista fica vazia até a próxima tentativa */ }
    }

    private void AddChallengeRow(SupabaseChallenge ch)
    {
        _activeChallenges[ch.Code] = ch;

        var countdownLbl = new Label
        {
            TextColor = Color.FromArgb("#6A8AAA"), FontSize = 12
        };
        _countdownLabels[ch.Code] = countdownLbl;

        var codeLbl = new Label
        {
            Text = ch.Code, TextColor = Color.FromArgb("#4AA3FF"),
            FontSize = 22, FontAttributes = FontAttributes.Bold, CharacterSpacing = 3
        };
        codeLbl.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await Clipboard.Default.SetTextAsync(ch.Code))
        });

        var codeStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        codeStack.Add(codeLbl);
        codeStack.Add(countdownLbl);

        var shareLbl = new Label
        {
            Text = "↗  Compartilhar", TextColor = Color.FromArgb("#4AA3FF"), FontSize = 13,
            FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center
        };
        shareLbl.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await ShareCode(ch.Code))
        });

        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        grid.Add(codeStack);
        Grid.SetColumn(shareLbl, 1);
        grid.Add(shareLbl);

        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#061F33"),
            Stroke          = new SolidColorBrush(Color.FromArgb("#2E7DDB")),
            StrokeThickness = 1.2,
            StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding         = new Thickness(16, 12),
            Content         = grid
        };
        _challengeRows[ch.Code] = border;

        ActiveChallengesList.Children.Insert(0, border); // mais recente no topo
        ActiveChallengesSection.IsVisible = true;
        UpdateCountdownLabel(ch.Code);
    }

    private void RemoveChallengeRow(string code)
    {
        if (_challengeRows.TryGetValue(code, out var border))
            ActiveChallengesList.Children.Remove(border);

        _activeChallenges.Remove(code);
        _countdownLabels.Remove(code);
        _challengeRows.Remove(code);

        ActiveChallengesSection.IsVisible = _activeChallenges.Count > 0;
    }

    // ── Contagem regressiva (uma só timer pra todos os desafios ativos) ──────

    private void StartCountdownTimerIfNeeded()
    {
        if (_countdownTimer != null) return;
        _countdownTimer = Application.Current!.Dispatcher.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.Tick += (_, _) =>
        {
            foreach (var code in _activeChallenges.Keys.ToList())
                UpdateCountdownLabel(code);
        };
        _countdownTimer.Start();
    }

    private void UpdateCountdownLabel(string code)
    {
        if (!_activeChallenges.TryGetValue(code, out var ch)) return;
        if (!_countdownLabels.TryGetValue(code, out var lbl)) return;

        var remaining = ch.ExpiresAt - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            RemoveChallengeRow(code);
            return;
        }

        lbl.Text = $"Expira em {(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }

    /// <summary>Assina (uma única vez) mudanças em QUALQUER desafio meu — quando um amigo
    /// aceitar (status vira "accepted" e game_id é preenchido por accept_challenge()), remove
    /// da lista e entra na partida real.</summary>
    private void SubscribeToOwnChallengesOnce()
    {
        if (_subscribedToOwnChallenges) return;
        _subscribedToOwnChallenges = true;

        try
        {
            string myId = AppState.Current.Auth.UserId;
            SupabaseService.Instance.Client
                .From<SupabaseChallenge>()
                .On(PostgresChangesOptions.ListenType.Updates, async (_, change) =>
                {
                    var row = change.Model<SupabaseChallenge>();
                    if (row == null || row.ChallengerId != myId) return;
                    if (row.Status != "accepted" || string.IsNullOrEmpty(row.GameId)) return;

                    await MainThread.InvokeOnMainThreadAsync(() => HandleChallengeAcceptedAsync(row));
                });
        }
        catch { }
    }

    private Task HandleChallengeAcceptedAsync(SupabaseChallenge row)
    {
        RemoveChallengeRow(row.Code);
        return EnterOnlineGame(row.GameId!, null);
    }

    private async Task ShareCode(string code)
    {
        // Só o código, sem link — evita depender de uma página externa (ex.: hospedada fora do
        // ChessArena) só pra repassar o convite. Quem recebe abre o app e usa "Entrar com Código".
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Compartilhando desafio",
                Text  = $"♞ Te desafiei no ChessArena! Abra o app, toque em \"Entrar com Código\" e digite: {code}",
            });
        }
        catch { /* usuário cancelou a folha de compartilhamento — sem problema, pode tocar em "Compartilhar" de novo na lista */ }
    }

    // ── Fluxo: Entrar com código ──────────────────────────────────────────────

    private void OnCodeEntryChanged(object? sender, TextChangedEventArgs e)
    {
        string upper = e.NewTextValue.ToUpperInvariant();
        if (upper != e.NewTextValue) { CodeEntry.Text = upper; return; }

        _foundChallenge                  = null;
        ChallengeFoundCard.IsVisible    = false;
        ChallengeNotFoundCard.IsVisible = false;
        UpdateActionBtn();
    }

    private async Task SearchOrAccept()
    {
        if (ChallengeFoundCard.IsVisible) { await AcceptChallenge(); return; }

        string code = (CodeEntry.Text ?? "").Trim().ToUpperInvariant();
        if (code.Length < 6)
        {
            await DisplayAlert("Código inválido", "O código tem 6 caracteres.", "OK");
            return;
        }

        var svc = SupabaseService.Instance;
        if (!svc.IsReady)
        {
            await DisplayAlert("Sem conexão", "Não foi possível conectar ao servidor. Verifique sua internet e tente de novo.", "OK");
            return;
        }

        await SearchOnSupabase(code);
        UpdateActionBtn();
    }

    private async Task SearchOnSupabase(string code)
    {
        try
        {
            var result = await SupabaseService.Instance.Client
                .From<SupabaseChallenge>()
                .Filter("code",   Operator.Equals, code)
                .Filter("status", Operator.Equals, "pending")
                .Single();

            if (result != null && result.ExpiresAt > DateTime.UtcNow)
            {
                _foundChallenge = result;
                ShowFoundChallenge(result.ChallengerName, result.TimeMinutes);
            }
            else
            {
                ChallengeFoundCard.IsVisible    = false;
                ChallengeNotFoundCard.IsVisible = true;
            }
        }
        catch
        {
            ChallengeFoundCard.IsVisible    = false;
            ChallengeNotFoundCard.IsVisible = true;
        }
    }

    private void ShowFoundChallenge(string name, int minutes)
    {
        ChallengeInfoLabel.Text         = $"De: {name}";
        ChallengeTimeLabel.Text         = minutes == 0
            ? "Sem limite de tempo"
            : $"{minutes} minuto(s)";
        ChallengeFoundCard.IsVisible    = true;
        ChallengeNotFoundCard.IsVisible = false;
    }

    private async Task AcceptChallenge()
    {
        if (_foundChallenge == null) return;
        var svc = SupabaseService.Instance;
        if (!svc.IsReady)
        {
            await DisplayAlert("Sem conexão", "Não foi possível conectar ao servidor. Verifique sua internet e tente de novo.", "OK");
            return;
        }

        try
        {
            // accept_challenge() já cria a partida real (games) e devolve o id — nenhuma
            // escrita direta do cliente na tabela challenges.
            var response = await svc.Client.Rpc("accept_challenge",
                new Dictionary<string, object> { ["p_code"] = _foundChallenge.Code });
            var gameId = response?.Content?.Trim('"');
            if (string.IsNullOrEmpty(gameId))
            {
                await DisplayAlert("Erro", "Não foi possível aceitar o desafio.", "OK");
                return;
            }

            await EnterOnlineGame(gameId, _foundChallenge.ChallengerName);
        }
        catch
        {
            await DisplayAlert("Erro", "Código inválido, expirado, ou você já usou esse código.", "OK");
        }
    }

    // ── Entrar na partida real ────────────────────────────────────────────────

    private async Task EnterOnlineGame(string gameId, string? knownOpponentName)
    {
        bool ok = await AppState.Current.OnlineGame.EnterGameAsync(gameId, knownOpponentName);
        if (!ok)
        {
            await DisplayAlert("Erro", "Não foi possível carregar a partida.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("GamePage");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MakeCode() =>
        new(Enumerable.Range(0, 6)
            .Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)])
            .ToArray());
}
