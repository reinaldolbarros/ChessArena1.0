using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class LoginPage : ContentPage
{
    private readonly AuthService    _auth    = AppState.Current.Auth;
    private readonly ProfileService _profile = AppState.Current.Profile;

    public LoginPage()
    {
        InitializeComponent();
        Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page
            .SetUseSafeArea(this, true);

        RegCountryPicker.ItemsSource = GeoData.Countries.ToList();
        RegStatePicker.ItemsSource   = GeoData.BrazilStates.ToList();

        // Se o link de "esqueci senha" já tiver sido confirmado (deep link recebido antes
        // dessa página existir, ex. app aberto direto pelo link) ou for confirmado enquanto
        // essa página estiver na tela, mostra a etapa de nova senha automaticamente.
        _auth.PasswordRecoveryReady += OnPasswordRecoveryReady;
        if (_auth.HasPendingPasswordRecovery) ShowNewPasswordStep();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _auth.PasswordRecoveryReady -= OnPasswordRecoveryReady;
    }

    private void OnPasswordRecoveryReady() => ShowNewPasswordStep();

    private void ShowNewPasswordStep() => MainThread.BeginInvokeOnMainThread(() =>
    {
        ResetPanel.IsVisible           = true;
        FormPanel.IsVisible            = false;
        ResetStepEmail.IsVisible       = false;
        ResetStepNewPassword.IsVisible = true;
    });

    private void OnRegCountryChanged(object? sender, EventArgs e)
    {
        bool isBrazil = RegCountryPicker.SelectedItem as string == "Brasil";
        RegStateSection.IsVisible = isBrazil;
        if (!isBrazil) RegStatePicker.SelectedIndex = -1;
    }

    // ── Alternar entre login e cadastro ──────────────────────────────────────
    private void OnShowRegister(object? sender, TappedEventArgs e)
    {
        EnterFields.IsVisible    = false;
        RegisterFields.IsVisible = true;
        ClearErrors();
        RegLoginEntry.Focus();
    }

    private void OnShowLogin(object? sender, TappedEventArgs e)
    {
        RegisterFields.IsVisible = false;
        EnterFields.IsVisible    = true;
        ClearErrors();
    }

    // ── Exibir / ocultar senha ───────────────────────────────────────────────
    private void OnToggleLoginPwd(object? sender, EventArgs e)
        => TogglePassword(LoginPasswordEntry, ToggleLoginPwdBtn);

    private void OnToggleRegPwd(object? sender, EventArgs e)
        => TogglePassword(RegPasswordEntry, ToggleRegPwdBtn);

    private void OnToggleRegConfirmPwd(object? sender, EventArgs e)
        => TogglePassword(RegConfirmPasswordEntry, ToggleRegConfirmPwdBtn);

    private void OnToggleNewPwd(object? sender, EventArgs e)
        => TogglePassword(NewPasswordEntry, ToggleNewPwdBtn);

    private void OnToggleConfirmPwd(object? sender, EventArgs e)
        => TogglePassword(ConfirmPasswordEntry, ToggleConfirmPwdBtn);

    private static void TogglePassword(Entry entry, ImageButton btn)
    {
        entry.IsPassword = !entry.IsPassword;
        btn.Source       = entry.IsPassword ? "eye_open.svg" : "eye_closed.svg";
    }

    // ── Navegação entre campos (Return key) ──────────────────────────────────
    private void OnLoginEntryCompleted(object? sender, EventArgs e)         => LoginPasswordEntry.Focus();
    private void OnLoginPasswordCompleted(object? sender, EventArgs e)      => OnEntrarClicked(sender, e);
    private void OnRegLoginCompleted(object? sender, EventArgs e)           => RegEmailEntry.Focus();
    private void OnRegEmailCompleted(object? sender, EventArgs e)           => RegPasswordEntry.Focus();
    private void OnRegPasswordCompleted(object? sender, EventArgs e)        => RegConfirmPasswordEntry.Focus();
    private void OnRegConfirmPasswordCompleted(object? sender, EventArgs e) => OnCadastrarClicked(sender, e);
    private void OnResetCredentialCompleted(object? sender, EventArgs e)    => OnResetPasswordClicked(sender, e);
    private void OnNewPasswordCompleted(object? sender, EventArgs e)        => ConfirmPasswordEntry.Focus();
    private void OnConfirmPasswordCompleted(object? sender, EventArgs e)    => OnConfirmNewPasswordClicked(sender, e);

    // ── Entrar ───────────────────────────────────────────────────────────────
    private async void OnEntrarClicked(object? sender, EventArgs e)
    {
        var email    = LoginEntry.Text?.Trim() ?? "";
        var password = LoginPasswordEntry.Text ?? "";

        if (string.IsNullOrEmpty(email))
            { ShowLoginError("Informe seu e-mail."); return; }
        if (password.Length < 6)
            { ShowLoginError("Senha deve ter pelo menos 6 caracteres."); return; }

        bool ok = await _auth.TryLoginAsync(email, password);
        if (!ok)
            { ShowLoginError("E-mail ou senha incorretos."); return; }

        _profile.ResetLocal();
        await _profile.LoadFromSupabaseAsync();
        await GoToShell();
    }

    // ── Cadastrar ────────────────────────────────────────────────────────────
    private async void OnCadastrarClicked(object? sender, EventArgs e)
    {
        var login    = RegLoginEntry.Text?.Trim() ?? "";
        var email    = RegEmailEntry.Text?.Trim().ToLower() ?? "";
        var password = RegPasswordEntry.Text ?? "";
        var confirm  = RegConfirmPasswordEntry.Text ?? "";

        if (string.IsNullOrEmpty(login))
            { ShowRegisterError("Informe um nome de usuário."); return; }
        if (login.Length < 3)
            { ShowRegisterError("Nome deve ter pelo menos 3 caracteres."); return; }
        if (string.IsNullOrEmpty(email) || !email.Contains('@') || !email.Contains('.'))
            { ShowRegisterError("Informe um e-mail válido."); return; }
        if (password.Length < 6)
            { ShowRegisterError("Senha deve ter pelo menos 6 caracteres."); return; }
        if (password != confirm)
            { ShowRegisterError("As senhas não conferem."); return; }

        var (ok, needsConfirmation, error) = await _auth.TryRegisterAsync(login, email, password);
        if (!ok)
            { ShowRegisterError(error); return; }

        if (needsConfirmation)
        {
            await DisplayAlert("✓ Cadastro realizado",
                "Verifique seu e-mail e clique no link de confirmação antes de entrar.", "OK");
            OnShowLogin(sender, new TappedEventArgs(null));
            LoginEntry.Text = email;
            return;
        }

        _profile.Name    = login;
        _profile.Country = RegCountryPicker.SelectedItem as string ?? "";
        _profile.State   = GeoData.StateAbbr(RegStatePicker.SelectedItem as string);
        _ = _profile.SyncToSupabaseAsync();
        await GoToShell();
    }

    // ── Google ───────────────────────────────────────────────────────────────
    private bool _googleSignInInProgress;

    private async void OnGoogleSignInClicked(object? sender, TappedEventArgs e)
    {
        if (_googleSignInInProgress) return;
        _googleSignInInProgress = true;

        try
        {
            var (ok, error) = await _auth.TrySignInWithGoogleAsync();
            if (!ok)
                { ShowLoginError(error); return; }

            _profile.ResetLocal();
            // Espera terminar de vez — se fosse "fire and forget", a LobbyPage podia checar
            // IsNew antes do nome real chegar do servidor e forçar a tela de Perfil à toa.
            await _profile.LoadFromSupabaseAsync();

            // Login social não pede cadastro: se ainda não existe nome (primeira vez com essa
            // conta), usa o nome da própria conta Google em vez de obrigar a passar pela tela
            // de Perfil — o usuário pode trocar depois lá se quiser.
            if (_profile.IsNew)
            {
                _profile.Name = !string.IsNullOrWhiteSpace(_auth.SuggestedDisplayName)
                    ? _auth.SuggestedDisplayName
                    : $"Jogador{Random.Shared.Next(1000, 9999)}";
                _ = _profile.SyncToSupabaseAsync();
            }

            await GoToShell();
        }
        finally
        {
            _googleSignInInProgress = false;
        }
    }

    // ── Visitante ────────────────────────────────────────────────────────────
    private async void OnAnonymousClicked(object? sender, EventArgs e)
    {
        _profile.ResetLocal();
        await _auth.LoginAnonymousAsync();
        await GoToShell();
    }

    private async void OnPrivacyPolicyTapped(object? sender, TappedEventArgs e)
    {
        try { await Launcher.Default.OpenAsync("https://claude.ai/code/artifact/f7251e8b-63c3-4b64-82a7-f8d25b422457"); }
        catch { }
    }

    // ── Redefinir senha (link por e-mail via Supabase) ────────────────────────
    private void OnForgotPassword(object? sender, TappedEventArgs e)
    {
        ResetCredentialEntry.Text      = LoginEntry.Text ?? "";
        ResetErrorLabel.IsVisible      = false;
        ResetStepEmail.IsVisible       = true;
        ResetStepNewPassword.IsVisible = false;
        FormPanel.IsVisible  = false;
        ResetPanel.IsVisible = true;
        ResetCredentialEntry.Focus();
    }

    private async void OnResetPasswordClicked(object? sender, EventArgs e)
    {
        var email = ResetCredentialEntry.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            { ShowResetError("Informe o e-mail da conta."); return; }

        bool sent = await _auth.SendPasswordResetAsync(email);
        if (!sent)
            { ShowResetError("Não foi possível enviar o link. Verifique o e-mail."); return; }

        await DisplayAlert("✓ Link enviado",
            "Verifique seu e-mail e clique no link para confirmar. O app vai abrir sozinho na tela de nova senha.", "OK");

        OnBackFromReset(sender, new TappedEventArgs(null));
    }

    // Só chamado depois que o link do e-mail já confirmou a sessão de recuperação
    // (ver AuthService.CompletePasswordRecoveryAsync / ShowNewPasswordStep).
    private async void OnConfirmNewPasswordClicked(object? sender, EventArgs e)
    {
        var pwd     = NewPasswordEntry.Text ?? "";
        var confirm = ConfirmPasswordEntry.Text ?? "";

        if (pwd.Length < 6)
            { ShowResetStep2Error("Senha deve ter pelo menos 6 caracteres."); return; }
        if (pwd != confirm)
            { ShowResetStep2Error("As senhas não conferem."); return; }

        var (ok, error) = await _auth.TrySetNewPasswordAsync(pwd);
        if (!ok)
            { ShowResetStep2Error(error); return; }

        await DisplayAlert("✓ Senha redefinida", "Faça login com a sua nova senha.", "OK");
        OnBackFromReset(sender, new TappedEventArgs(null));
    }

    private void OnBackFromReset(object? sender, TappedEventArgs e)
    {
        ResetPanel.IsVisible           = false;
        FormPanel.IsVisible            = true;
        EnterFields.IsVisible          = true;
        RegisterFields.IsVisible       = false;
        ResetStepEmail.IsVisible       = true;
        ResetStepNewPassword.IsVisible = false;
        NewPasswordEntry.Text          = "";
        ConfirmPasswordEntry.Text      = "";
        ClearErrors();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static Task GoToShell()
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window != null) window.Page = new AppShell();
        return Task.CompletedTask;
    }

    private void ClearErrors()
    {
        LoginErrorLabel.IsVisible      = false;
        RegisterErrorLabel.IsVisible   = false;
        ResetErrorLabel.IsVisible      = false;
        ResetStep2ErrorLabel.IsVisible = false;
    }

    private void ShowLoginError(string msg)
    {
        LoginErrorLabel.Text      = msg;
        LoginErrorLabel.IsVisible = true;
    }

    private void ShowRegisterError(string msg)
    {
        RegisterErrorLabel.Text      = msg;
        RegisterErrorLabel.IsVisible = true;
    }

    private void ShowResetError(string msg)
    {
        ResetErrorLabel.Text      = msg;
        ResetErrorLabel.IsVisible = true;
    }

    private void ShowResetStep2Error(string msg)
    {
        ResetStep2ErrorLabel.Text      = msg;
        ResetStep2ErrorLabel.IsVisible = true;
    }
}
