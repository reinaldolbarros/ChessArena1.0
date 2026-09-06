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
    }

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

    private static void TogglePassword(Entry entry, Button btn)
    {
        entry.IsPassword = !entry.IsPassword;
        btn.Text         = entry.IsPassword ? "👁" : "🔒";
    }

    // ── Navegação entre campos (Return key) ──────────────────────────────────
    private void OnLoginEntryCompleted(object? sender, EventArgs e)         => LoginPasswordEntry.Focus();
    private void OnLoginPasswordCompleted(object? sender, EventArgs e)      => OnEntrarClicked(sender, e);
    private void OnRegLoginCompleted(object? sender, EventArgs e)           => RegEmailEntry.Focus();
    private void OnRegEmailCompleted(object? sender, EventArgs e)           => RegPasswordEntry.Focus();
    private void OnRegPasswordCompleted(object? sender, EventArgs e)        => RegConfirmPasswordEntry.Focus();
    private void OnRegConfirmPasswordCompleted(object? sender, EventArgs e) => OnCadastrarClicked(sender, e);
    private void OnResetCredentialCompleted(object? sender, EventArgs e)    => OnResetPasswordClicked(sender, e);
    private void OnNewPasswordCompleted(object? sender, EventArgs e)        { }
    private void OnConfirmPasswordCompleted(object? sender, EventArgs e)    => OnResetPasswordClicked(sender, e);

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

        _ = _profile.LoadFromSupabaseAsync();
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

        var (ok, error) = await _auth.TryRegisterAsync(login, email, password);
        if (!ok)
            { ShowRegisterError(error); return; }

        _profile.Name    = login;
        _profile.Country = RegCountryPicker.SelectedItem as string ?? "";
        _profile.State   = GeoData.StateAbbr(RegStatePicker.SelectedItem as string);
        _ = _profile.SyncToSupabaseAsync();
        await GoToShell();
    }

    // ── Visitante ────────────────────────────────────────────────────────────
    private async void OnAnonymousClicked(object? sender, EventArgs e)
    {
        await _auth.LoginAnonymousAsync();
        await GoToShell();
    }

    // ── Redefinir senha (link por e-mail via Supabase) ────────────────────────
    private void OnForgotPassword(object? sender, TappedEventArgs e)
    {
        ResetCredentialEntry.Text = LoginEntry.Text ?? "";
        ResetErrorLabel.IsVisible = false;
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
            "Verifique seu e-mail e clique no link para redefinir a senha.", "OK");

        OnBackFromReset(sender, new TappedEventArgs(null));
    }

    private void OnBackFromReset(object? sender, TappedEventArgs e)
    {
        ResetPanel.IsVisible     = false;
        FormPanel.IsVisible      = true;
        EnterFields.IsVisible    = true;
        RegisterFields.IsVisible = false;
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
        LoginErrorLabel.IsVisible    = false;
        RegisterErrorLabel.IsVisible = false;
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
}
