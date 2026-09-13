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
    private bool               _codeMade;
    private string?            _myCode;
    private SupabaseChallenge? _foundChallenge; // resultado da busca online

    // ── Init ──────────────────────────────────────────────────────────────────
    public FriendInvitePage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var profile = AppState.Current.Profile;
        if (!AppState.Current.Auth.IsAnonymous && !string.IsNullOrWhiteSpace(profile.Name))
            ChallengerNameEntry.Text = profile.Name;

        SetTime(Preferences.Default.Get(PrefKeyTime, 0));
        SetSubMode(FriendMode.OnlineCreate);

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

        CriarSection.IsVisible  = isCriar;
        EntrarSection.IsVisible = !isCriar;
        TimeCard.IsVisible      = isCriar;

        if (isCriar)
        {
            CodeDisplayCard.IsVisible = false;
            _codeMade = false;
            _myCode   = null;
        }
        else
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
            FriendMode.OnlineCreate when !_codeMade =>
                ("Gerar Código",          Color.FromArgb("#2A67B1"), true),

            FriendMode.OnlineCreate =>
                ("Aguardando amigo...",   Color.FromArgb("#1A3050"), false),

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
        if (_codeMade) return;

        var svc = SupabaseService.Instance;
        if (!svc.IsReady)
        {
            await DisplayAlert("Sem conexão", "Não foi possível conectar ao servidor. Verifique sua internet e tente de novo.", "OK");
            return;
        }

        string challenger = string.IsNullOrWhiteSpace(ChallengerNameEntry.Text)
            ? (AppState.Current.Profile.Name ?? "Desafiante")
            : ChallengerNameEntry.Text.Trim();

        _myCode = MakeCode();

        try
        {
            var challenge = new SupabaseChallenge
            {
                Code           = _myCode,
                ChallengerId   = AppState.Current.Auth.UserId,
                ChallengerName = challenger,
                TimeMinutes    = _selectedMinutes,
                Status         = "pending",
                ExpiresAt      = DateTime.UtcNow.AddMinutes(CodeExpiryMin),
            };
            await svc.Client.From<SupabaseChallenge>().Insert(challenge);
        }
        catch
        {
            await DisplayAlert("Erro", "Não foi possível criar o desafio. Tente novamente.", "OK");
            _myCode = null;
            return;
        }

        GeneratedCodeLabel.Text   = _myCode;
        CodeDisplayCard.IsVisible = true;
        WaitingSpinner.IsRunning  = true;
        WaitingSpinner.IsVisible  = true;
        _codeMade = true;
        UpdateActionBtn();

        SubscribeToOwnChallenge(_myCode);
    }

    /// <summary>Assina a própria linha em "challenges" — quando o amigo aceitar (status vira
    /// "accepted" e game_id é preenchido por accept_challenge()), entra na partida real.</summary>
    private void SubscribeToOwnChallenge(string code)
    {
        try
        {
            SupabaseService.Instance.Client
                .From<SupabaseChallenge>()
                .On(PostgresChangesOptions.ListenType.Updates, async (_, change) =>
                {
                    var row = change.Model<SupabaseChallenge>();
                    if (row == null || row.Code != code) return;
                    if (row.Status != "accepted" || string.IsNullOrEmpty(row.GameId)) return;

                    await MainThread.InvokeOnMainThreadAsync(() => EnterOnlineGame(row.GameId!, null));
                });
        }
        catch { }
    }

    private async void OnCopyCodeClicked(object? sender, EventArgs e)
    {
        if (_myCode is null) return;
        await Clipboard.Default.SetTextAsync(_myCode);
        CopyCodeBtn.Text = "✓ Copiado!";
        await Task.Delay(1400);
        CopyCodeBtn.Text = "Copiar";
    }

    private async void OnShareCodeClicked(object? sender, EventArgs e)
    {
        if (_myCode is null) return;
        // Link único (mesma página pra qualquer convite, o código vai na URL) — quem já tem o
        // app abre direto na tela de aceitar; quem não tem vê o código e como baixar o app.
        string link = $"https://claude.ai/code/artifact/c3b8d9d0-bf88-44e4-9d93-220c361e7757?code={_myCode}";
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Desafio de Xadrez",
            Text  = $"🎯 Vamos jogar xadrez! Toque no link pra aceitar meu desafio (código {_myCode}):\n{link}",
        });
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
        var online = AppState.Current.OnlineGame;
        var game   = await online.LoadGameAsync(gameId);
        if (game == null)
        {
            await DisplayAlert("Erro", "Não foi possível carregar a partida.", "OK");
            return;
        }

        string myId         = AppState.Current.Auth.UserId;
        bool   playerIsWhite = game.WhiteId == myId;
        string opponentId    = playerIsWhite ? game.BlackId : game.WhiteId;
        string opponentName  = knownOpponentName ?? await LookupProfileNameAsync(opponentId);

        var state = AppState.Current;
        state.PendingOnlineGameId   = gameId;
        state.IsOnlineGame          = true;
        state.PendingOnlineGame     = true;
        state.OnlineOpponentName    = opponentName;
        state.OnlineTimeMinutes     = game.TimeMinutes;
        state.OnlinePlayerIsWhite   = playerIsWhite;

        await Shell.Current.GoToAsync("GamePage");
    }

    private static async Task<string> LookupProfileNameAsync(string userId)
    {
        try
        {
            var row = await SupabaseService.Instance.Client
                .From<SupabaseProfile>()
                .Where(p => p.Id == userId)
                .Single();
            return string.IsNullOrWhiteSpace(row?.Name) ? "Adversário" : row!.Name;
        }
        catch { return "Adversário"; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MakeCode() =>
        new(Enumerable.Range(0, 6)
            .Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)])
            .ToArray());
}
