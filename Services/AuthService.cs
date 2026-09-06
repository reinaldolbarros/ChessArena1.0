using Supabase.Gotrue;

namespace ChessMAUI.Services;

public class AuthService
{
    private const string KeyAnon    = "auth_is_anonymous";
    private const string KeySession = "supabase_session"; // chave do MauiSessionHandler

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
    public async Task<(bool Ok, string Error)> TryRegisterAsync(
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
            return (session?.User != null, "");
        }
        catch (Exception ex)
        {
            string msg = ex.Message.Contains("already registered")
                ? "E-mail já cadastrado."
                : "Erro ao cadastrar. Tente novamente.";
            return (false, msg);
        }
    }

    // ── Redefinição de senha via link por e-mail ──────────────────────────────
    public async Task<bool> SendPasswordResetAsync(string email)
    {
        try
        {
            await Db.Auth.ResetPasswordForEmail(email.Trim().ToLower());
            return true;
        }
        catch { return false; }
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
