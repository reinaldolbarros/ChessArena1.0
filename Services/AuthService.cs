using System.Linq;
using Microsoft.Maui.Authentication;
using Supabase.Gotrue;

namespace ChessMAUI.Services;

public class AuthService
{
    private const string KeyAnon    = "auth_is_anonymous";
    private const string KeySession = "supabase_session"; // chave do MauiSessionHandler
    private const string KeyResetVerifier = "auth_reset_pkce_verifier";

    // Esquema de URL próprio do app (registrado no Android/iOS) — usado tanto pro link de
    // "esqueci senha" (tratado manualmente em DeepLinkRouter) quanto pro retorno do login
    // Google (capturado direto pelo WebAuthenticator).
    private const string ResetCallbackUrl = "chessarena://reset-callback";
    private const string GoogleCallbackUrl = "chessarena://auth-callback";

    /// <summary>True quando o app acabou de validar um link de recuperação de senha e está
    /// esperando o usuário definir a nova senha (ver CompletePasswordRecoveryAsync).</summary>
    public bool HasPendingPasswordRecovery { get; private set; }

    /// <summary>Disparado quando a sessão de recuperação de senha fica pronta — permite que a
    /// LoginPage, se já estiver aberta, mostre o painel de nova senha na hora.</summary>
    public event Action? PasswordRecoveryReady;

    private Supabase.Client? Db => SupabaseService.Instance.IsReady
        ? SupabaseService.Instance.Client : null;

    // ── Propriedades síncronas (lidas do cache — não dependem de rede) ────────
    public bool IsAuthenticated =>
        Db?.Auth.CurrentUser != null
        || !string.IsNullOrEmpty(Preferences.Default.Get(KeySession, ""))
        || Preferences.Default.Get(KeyAnon, false);

    public bool IsAnonymous =>
        Preferences.Default.Get(KeyAnon, false) && Db?.Auth.CurrentUser == null;

    public string Email  => Db?.Auth.CurrentUser?.Email ?? "";
    public string UserId => Db?.Auth.CurrentUser?.Id    ?? "";

    public string Username
    {
        get
        {
            var meta = Db?.Auth.CurrentUser?.UserMetadata;
            if (meta != null && meta.TryGetValue("username", out var u))
                return u?.ToString() ?? "";
            return "";
        }
    }

    /// <summary>Nome sugerido a partir do provedor OAuth (ex.: nome da conta Google) — usado
    /// pra preencher o perfil automaticamente após login social, sem exigir cadastro manual.</summary>
    public string SuggestedDisplayName
    {
        get
        {
            var meta = Db?.Auth.CurrentUser?.UserMetadata;
            if (meta == null) return "";
            foreach (var key in new[] { "full_name", "name", "given_name" })
                if (meta.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v?.ToString()))
                    return v!.ToString()!;
            return "";
        }
    }

    // ── Anônimo (local — visitante não precisa de conta Supabase) ─────────────
    public async Task LoginAnonymousAsync()
    {
        // Encerra qualquer sessão Supabase ativa antes de entrar como visitante
        if (Db?.Auth.CurrentUser != null)
            await Db.Auth.SignOut();

        Preferences.Default.Set(KeyAnon, true);

        // Atribui nome aleatório se o perfil estiver vazio
        var profile = AppState.Current.Profile;
        if (string.IsNullOrWhiteSpace(profile.Name))
            profile.Name = $"Visitante{Random.Shared.Next(1000, 9999)}";
    }

    // ── Login por e-mail + senha ──────────────────────────────────────────────
    public async Task<bool> TryLoginAsync(string email, string password)
    {
        try
        {
            var session = await Db.Auth.SignIn(email.Trim().ToLower(), password);
            return session?.User != null;
        }
        catch { return false; }
    }

    // ── Cadastro ──────────────────────────────────────────────────────────────
    // NeedsConfirmation = true quando o Supabase exige confirmação por e-mail:
    // o usuário é criado, mas a sessão só fica ativa depois do clique no link.
    public async Task<(bool Ok, bool NeedsConfirmation, string Error)> TryRegisterAsync(
        string username, string email, string password)
    {
        try
        {
            var opts = new SignUpOptions
            {
                Data = new Dictionary<string, object>
                {
                    ["username"] = username.Trim(),
                }
            };
            var session = await Db.Auth.SignUp(email.Trim().ToLower(), password, opts);
            if (session?.User == null)
                return (false, false, "");

            bool needsConfirmation = string.IsNullOrEmpty(session.AccessToken);
            return (true, needsConfirmation, "");
        }
        catch (Exception ex)
        {
            string msg = ex.Message.Contains("already registered")
                ? "E-mail já cadastrado."
                : "Erro ao cadastrar. Tente novamente.";
            return (false, false, msg);
        }
    }

    // ── Redefinição de senha via link por e-mail ──────────────────────────────
    // Fluxo PKCE: o link do e-mail volta pro app como chessarena://reset-callback?code=...
    // (ver DeepLinkRouter). Guardamos o "verifier" agora porque o app pode até ter sido
    // fechado até o usuário clicar no link.
    public async Task<bool> SendPasswordResetAsync(string email)
    {
        try
        {
            var state = await Db.Auth.ResetPasswordForEmail(new ResetPasswordForEmailOptions(email.Trim().ToLower())
            {
                FlowType   = Constants.OAuthFlowType.PKCE,
                RedirectTo = ResetCallbackUrl,
            });

            if (!string.IsNullOrEmpty(state?.PKCEVerifier))
                await SecureStorage.Default.SetAsync(KeyResetVerifier, state!.PKCEVerifier);

            return true;
        }
        catch { return false; }
    }

    /// <summary>Chamado pelo DeepLinkRouter quando o link de "esqueci senha" chega de volta
    /// no app. Troca o código PKCE pela sessão de recuperação — só a partir daqui é seguro
    /// deixar o usuário definir uma senha nova.</summary>
    public async Task<bool> CompletePasswordRecoveryAsync(string code)
    {
        try
        {
            var verifier = await SecureStorage.Default.GetAsync(KeyResetVerifier);
            if (string.IsNullOrEmpty(verifier)) return false;

            await Db.Auth.ExchangeCodeForSession(code, verifier);
            SecureStorage.Default.Remove(KeyResetVerifier);

            HasPendingPasswordRecovery = true;
            PasswordRecoveryReady?.Invoke();
            return true;
        }
        catch { return false; }
    }

    /// <summary>Define a nova senha depois que CompletePasswordRecoveryAsync já validou o
    /// link do e-mail — diferente de TryUpdatePasswordAsync, não exige a senha atual, porque
    /// o próprio link clicado já provou que é o dono da conta.</summary>
    public async Task<(bool Ok, string Error)> TrySetNewPasswordAsync(string newPassword)
    {
        try
        {
            await Db.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = newPassword });
            HasPendingPasswordRecovery = false;
            return (true, "");
        }
        catch { return (false, "Não foi possível redefinir a senha. Peça um novo link."); }
    }

    // ── Login com Google ─────────────────────────────────────────────────────
    public async Task<(bool Ok, string Error)> TrySignInWithGoogleAsync()
    {
        try
        {
            // Fluxo Implicit em vez de PKCE: o Supabase devolve o access/refresh token direto
            // na URL de retorno, sem precisar de uma segunda troca (code+verifier) contra o
            // "flow state" guardado no servidor — evita um bug confirmado do lado do Supabase
            // nesse tipo de troca (ver auth.flow_state: o código que o servidor emite não bate
            // com o que o app recebe de volta, mesmo com tudo certo do lado do app).
            var state = await Db.Auth.SignIn(Constants.Provider.Google, new SignInOptions
            {
                FlowType   = Constants.OAuthFlowType.Implicit,
                RedirectTo = GoogleCallbackUrl,
            });
            if (state?.Uri == null) return (false, "Não foi possível iniciar o login com Google.");

            var result = await WebAuthenticator.Default.AuthenticateAsync(
                new WebAuthenticatorOptions { Url = state.Uri, CallbackUrl = new Uri(GoogleCallbackUrl) });

            if (string.IsNullOrEmpty(result.AccessToken) || string.IsNullOrEmpty(result.RefreshToken))
                return (false, "Login com Google cancelado.");

            var session = await Db.Auth.SetSession(result.AccessToken, result.RefreshToken);
            return session?.User != null
                ? (true, "")
                : (false, "Não foi possível concluir o login com Google.");
        }
        catch (TaskCanceledException) { return (false, "Login com Google cancelado."); }
        catch { return (false, "Não foi possível entrar com Google. Tente novamente."); }
    }

    // ── Alterar senha (verifica a senha atual antes de atualizar) ─────────────
    public async Task<(bool Ok, string Error)> TryUpdatePasswordAsync(
        string currentPassword, string newPassword)
    {
        try
        {
            // Reautentica para confirmar a senha atual
            var email = Email;
            if (string.IsNullOrEmpty(email)) return (false, "Conta sem e-mail registrado.");

            var session = await Db.Auth.SignIn(email, currentPassword);
            if (session?.User == null) return (false, "Senha atual incorreta.");

            await Db.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = newPassword });
            return (true, "");
        }
        catch { return (false, "Não foi possível alterar a senha."); }
    }

    // ── Logout ────────────────────────────────────────────────────────────────
    public async Task LogoutAsync()
    {
        Preferences.Default.Remove(KeyAnon);
        if (Db?.Auth.CurrentUser != null)
            await Db.Auth.SignOut();
    }
}
