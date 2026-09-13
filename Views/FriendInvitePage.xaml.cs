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
    private DateTime           _codeExpiresAt;
    private SupabaseChallenge? _foundChallenge; // resultado da busca online
    private IDispatcherTimer?  _expiryTimer;

    // ── Init ──────────────────────────────────────────────────────────────────
    public FriendInvitePage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();

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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopExpiryTimer();
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
            StopExpiryTimer();
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

        string challenger = AppState.Current.Profile.Name ?? "Desafiante";

        _myCode = MakeCode();
        _codeExpiresAt = DateTime.UtcNow.AddMinutes(CodeExpiryMin);

        try
        {
            var challenge = new SupabaseChallenge
            {
                Code           = _myCode,
                ChallengerId   = AppState.Current.Auth.UserId,
                ChallengerName = challenger,
                TimeMinutes    = _selectedMinutes,
                Status         = "pending",
                ExpiresAt      = _codeExpiresAt,
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
        StartExpiryTimer();

        await ShareCode();
    }

    // ── Contagem regressiva de expiração do código ───────────────────────────

    private void StartExpiryTimer()
    {
        StopExpiryTimer();
        _expiryTimer = Application.Current!.Dispatcher.CreateTimer();
        _expiryTimer.Interval = TimeSpan.FromSeconds(1);
        _expiryTimer.Tick += (_, _) => UpdateExpiryLabel();
        _expiryTimer.Start();
        UpdateExpiryLabel();
    }

    private void StopExpiryTimer()
    {
        _expiryTimer?.Stop();
        _expiryTimer = null;
        ExpiryLabel.Text = "";
    }

    private void UpdateExpiryLabel()
    {
        var remaining = _codeExpiresAt - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            StopExpiryTimer();
            ExpiryLabel.Text        = "Código expirado — gere um novo";
            WaitingLabel.Text       = "Este código não é mais válido.";
            WaitingSpinner.IsVisible = false;
            return;
        }

        ExpiryLabel.Text = $"Expira em {(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
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

    private async void OnShareCodeClicked(object? sender, EventArgs e) => await ShareCode();

    private async Task ShareCode()
    {
        if (_myCode is null) return;
        // Só o código, sem link — evita depender de uma página externa (ex.: hospedada fora do
        // ChessArena) só pra repassar o convite. Quem recebe abre o app e usa "Entrar com Código".
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Desafio de Xadrez",
                Text  = $"🎯 Te desafiei no ChessArena! Abra o app, toque em \"Entrar com Código\" e digite: {_myCode}",
            });
        }
        catch { /* usuário cancelou a folha de compartilhamento — sem problema, ele ainda pode tocar em "Compartilhar" de novo */ }
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
