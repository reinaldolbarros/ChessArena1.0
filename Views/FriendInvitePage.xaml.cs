using ChessMAUI.Models;
using ChessMAUI.Services;
using static Supabase.Postgrest.Constants;

namespace ChessMAUI.Views;

public partial class FriendInvitePage : ContentPage
{
    // ── Constantes ────────────────────────────────────────────────────────────
    private const string PrefKeyTime   = "friend_time_minutes";
    private const int    MaxTime       = 20;
    private const int    CodeExpiryMin = 10;

    // ── Fallback em memória (quando Supabase não disponível) ──────────────────
    private static readonly Dictionary<string, PendingChallenge> _pending = [];
    private static readonly char[] CodeAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray(); // sem I, O, 0, 1

    private record PendingChallenge(string ChallengerName, int TimeMinutes, DateTime ExpiresAt);

    // ── Estado da tela ────────────────────────────────────────────────────────
    private enum FriendMode { SameDevice, OnlineCreate, OnlineJoin }

    private FriendMode         _mode           = FriendMode.SameDevice;
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
        {
            Player1Entry.Text        = profile.Name;
            ChallengerNameEntry.Text = profile.Name;
        }

        SetTime(Preferences.Default.Get(PrefKeyTime, 0));
        SetMode(FriendMode.SameDevice);
        UpdateColorInfo();
        PurgeStaleCodes();
    }

    // ── Troca de modo principal ───────────────────────────────────────────────

    private void OnTabSameTelaTapped(object? sender, TappedEventArgs e) =>
        SetMode(FriendMode.SameDevice);

    private void OnTabOnlineTapped(object? sender, TappedEventArgs e) =>
        SetMode(FriendMode.OnlineCreate);

    private void OnSubTabCriarTapped(object? sender, TappedEventArgs e) =>
        SetSubMode(FriendMode.OnlineCreate);

    private void OnSubTabEntrarTapped(object? sender, TappedEventArgs e) =>
        SetSubMode(FriendMode.OnlineJoin);

    private void SetMode(FriendMode mode)
    {
        bool isSame = mode == FriendMode.SameDevice;
        ApplyTabStyle(TabSameTelaCard, TabSameTelaLabel, isSame);
        ApplyTabStyle(TabOnlineCard,   TabOnlineLabel,  !isSame);

        SameDeviceSection.IsVisible = isSame;
        OnlineSection.IsVisible     = !isSame;

        if (isSame)
        {
            _mode              = FriendMode.SameDevice;
            _codeMade          = false;
            TimeCard.IsVisible = true;
            UpdateActionBtn();
        }
        else
        {
            SetSubMode(FriendMode.OnlineCreate);
        }
    }

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
            _codeMade          = false;
            _myCode            = null;
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
            FriendMode.SameDevice =>
                ("▶  Iniciar Partida",    Color.FromArgb("#1F6B36"), true),

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


    // ── Info de cores ─────────────────────────────────────────────────────────

    private void OnNamesChanged(object? sender, TextChangedEventArgs e) => UpdateColorInfo();

    private void UpdateColorInfo()
    {
        bool   p1White = AppState.Current.FriendGameCount % 2 == 0;
        string p1      = string.IsNullOrWhiteSpace(Player1Entry.Text) ? "J1" : Player1Entry.Text.Trim();
        string p2      = string.IsNullOrWhiteSpace(Player2Entry.Text) ? "J2" : Player2Entry.Text.Trim();
        string white   = p1White ? p1 : p2;
        string black   = p1White ? p2 : p1;
        ColorInfoLabel.Text = $"Nesta partida: ♙ {white} (Brancas)  ·  ♟ {black} (Pretas)";
    }

    // ── Dispatcher do botão de ação ───────────────────────────────────────────

    private async void OnActionClicked(object? sender, EventArgs e)
    {
        switch (_mode)
        {
            case FriendMode.SameDevice:   StartSameDeviceGame();        break;
            case FriendMode.OnlineCreate: await GenerateChallengeCode(); break;
            case FriendMode.OnlineJoin:   await SearchOrAccept();        break;
        }
    }

    // ── Fluxo: Mesma Tela ─────────────────────────────────────────────────────

    private async void StartSameDeviceGame()
    {
        string p1    = string.IsNullOrWhiteSpace(Player1Entry.Text) ? "Jogador 1" : Player1Entry.Text.Trim();
        string p2    = string.IsNullOrWhiteSpace(Player2Entry.Text) ? "Jogador 2" : Player2Entry.Text.Trim();
        var    state = AppState.Current;

        state.PendingFriendGame     = true;
        state.PendingTournamentGame = false;
        state.FriendPlayer1Name     = p1;
        state.FriendOpponentName    = p2;
        state.FriendTimeMinutes     = _selectedMinutes;

        await Shell.Current.GoToAsync("GamePage");
    }

    // ── Fluxo: Criar código ───────────────────────────────────────────────────

    private async Task GenerateChallengeCode()
    {
        if (_codeMade) return;

        string challenger = string.IsNullOrWhiteSpace(ChallengerNameEntry.Text)
            ? (AppState.Current.Profile.Name ?? "Desafiante")
            : ChallengerNameEntry.Text.Trim();

        _myCode = MakeCode();

        var svc = SupabaseService.Instance;
        if (svc.IsReady)
        {
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
                // Supabase offline → guarda em memória como fallback
                _pending[_myCode] = new PendingChallenge(
                    challenger, _selectedMinutes,
                    DateTime.UtcNow.AddMinutes(CodeExpiryMin));
            }
        }
        else
        {
            _pending[_myCode] = new PendingChallenge(
                challenger, _selectedMinutes,
                DateTime.UtcNow.AddMinutes(CodeExpiryMin));
        }

        GeneratedCodeLabel.Text   = _myCode;
        CodeDisplayCard.IsVisible = true;
        WaitingSpinner.IsRunning  = true;
        WaitingSpinner.IsVisible  = true;
        _codeMade = true;
        UpdateActionBtn();
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
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Desafio de Xadrez",
            Text  = $"🎯 Vamos jogar xadrez!\n\nCódigo do desafio: {_myCode}\n\n"
                  + $"Abra o app → Com Amigo → Código Online → Entrar com Código",
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
        if (svc.IsReady)
        {
            await SearchOnSupabase(code);
        }
        else
        {
            PurgeStaleCodes();
            SearchInMemory(code);
        }

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

    private void SearchInMemory(string code)
    {
        if (_pending.TryGetValue(code, out var ch))
        {
            ShowFoundChallenge(ch.ChallengerName, ch.TimeMinutes);
        }
        else
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
        string challengerName;
        int    timeMinutes;

        var svc = SupabaseService.Instance;
        if (_foundChallenge != null && svc.IsReady)
        {
            // Marca como aceito no Supabase
            try
            {
                _foundChallenge.Status = "accepted";
                await svc.Client.From<SupabaseChallenge>().Upsert(_foundChallenge);
            }
            catch { }

            challengerName = _foundChallenge.ChallengerName;
            timeMinutes    = _foundChallenge.TimeMinutes;
        }
        else
        {
            string code = (CodeEntry.Text ?? "").Trim().ToUpperInvariant();
            if (!_pending.TryGetValue(code, out var ch)) return;
            _pending.Remove(code);
            challengerName = ch.ChallengerName;
            timeMinutes    = ch.TimeMinutes;
        }

        var state = AppState.Current;
        state.PendingFriendGame     = true;
        state.PendingTournamentGame = false;
        state.FriendPlayer1Name     = challengerName;
        state.FriendOpponentName    = state.Profile.Name ?? "Amigo";
        state.FriendTimeMinutes     = timeMinutes;

        await Shell.Current.GoToAsync("GamePage");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MakeCode() =>
        new(Enumerable.Range(0, 6)
            .Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)])
            .ToArray());

    private static void PurgeStaleCodes()
    {
        var now     = DateTime.UtcNow;
        var expired = _pending.Keys.Where(k => _pending[k].ExpiresAt < now).ToList();
        foreach (var k in expired) _pending.Remove(k);
    }
}
