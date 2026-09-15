using System.Linq;
using Microsoft.Maui.Authentication;
using Supabase.Gotrue;

namespace ChessMAUI.Services;

public class AuthService
{
    private const string KeyAnon    = "auth_is_anonymous";
    private const string KeySession = "supabase_session"; // chave do MauiSessionHandler
    private const string KeyResetVerifier = "auth_reset_pkce_verifier";

    // Backup separado da sessão do visitante (SecureStorage, mesmo nível de proteção da sessão
    // principal — ver MauiSessionHandler). Existe porque logar com Google/e-mail SOBRESCREVE a
    // sessão ativa (a mesma guardada em "supabase_session") — sem um backup à parte, a conta
    // anônima do visitante ficaria irrecuperável (o Supabase não deixa "logar de volta" numa
    // conta anônima sem o refresh token dela), e voltar a ser visitante depois de usar uma
    // conta real sempre criaria um visitante novo, descartável.
    private const string KeyGuestAccess  = "guest_session_access";
    private const string KeyGuestRefresh = "guest_session_refresh";

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

    // Não usa Db.Auth.CurrentUser.IsAnonymous (o flag do próprio SDK) — depois de restaurar a
    // sessão do SecureStorage numa nova abertura do app, esse campo nem sempre sobrevive à
    // (des)serialização, o que fazia "Sair" achar que o visitante já não era mais anônimo e
    // encerrar a sessão de verdade (LogoutAsync), criando um visitante novo a cada vez. Esse
    // flag local é gravado/limpo por nós mesmos (LoginAnonymousAsync / todo login real), então
    // é a fonte confiável.
    public bool IsAnonymous => Preferences.Default.Get(KeyAnon, false);

    public string Email  => Db?.Auth.CurrentUser?.Email ?? "";
    public string UserId => Db?.Auth.CurrentUser?.Id    ?? "";

    /// <summary>True se a conta já tem senha própria (login por e-mail) — falso pra quem
    /// entrou só pelo Google, já que aí não existe senha nenhuma pra "trocar".</summary>
    public bool HasPasswordIdentity =>
        Db?.Auth.CurrentUser?.Identities?.Any(i => i.Provider == "email") ?? false;

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

    // ── Anônimo (visitante — uma conta anônima real do Supabase por aparelho) ─
    /// <summary>
    /// Entra como visitante. O visitante é PERMANENTE neste aparelho: se já existe uma sessão
    /// anônima (persistida pelo MauiSessionHandler — e que "Sair"/LogoutAsync não destrói,
    /// justamente por isso), reaproveita a MESMA conta/nome/pontuação pra sempre. Só cria uma
    /// conta nova na primeira vez que o app roda neste aparelho (ou se o usuário nunca chegou
    /// a entrar como visitante antes).
    /// </summary>
    /// <returns>true se uma conta de visitante NOVA foi criada agora; false se reaproveitou uma já existente.</returns>
    public async Task<bool> LoginAnonymousAsync()
    {
        // Dá um tempo pro Supabase terminar de inicializar (e restaurar a sessão persistida,
        // se houver) antes de concluir que "não existe visitante ainda" — sem isso, uma
        // inicialização lenta faria parecer que não há sessão prévia e criaria um visitante
        // novo à toa, mesmo já existindo um salvo neste aparelho.
        for (int i = 0; i < 25 && !SupabaseService.Instance.IsReady; i++)
            await Task.Delay(200);

        System.Diagnostics.Debug.WriteLine(
            $"[ChessArena] LoginAnonymousAsync: KeyAnon={Preferences.Default.Get(KeyAnon, false)} " +
            $"currentUser={Db?.Auth.CurrentUser?.Id ?? "null"} sdkIsAnonymous={Db?.Auth.CurrentUser?.IsAnonymous}");

        // Já existe uma sessão de visitante válida neste aparelho — reaproveita (mesmo nome,
        // mesmo perfil/pontuação), em vez de criar uma conta anônima nova a cada entrada.
        // KeyAnon (nosso próprio flag) decide isso, não o IsAnonymous do SDK — ver o porquê no
        // comentário da propriedade IsAnonymous acima.
        if (Db?.Auth.CurrentUser != null && Preferences.Default.Get(KeyAnon, false))
        {
            _ = SaveGuestSessionSnapshotAsync(); // atualiza o backup com o token mais recente
            return false;
        }

        // Trocando de uma conta real pra visitante: encerra a sessão real antes.
        if (Db?.Auth.CurrentUser != null)
            await Db.Auth.SignOut();

        // Antes de criar um visitante NOVO, tenta recuperar o de sempre deste aparelho — cobre
        // o caso de ter entrado com Google/e-mail nesse meio tempo (o que sobrescreve a sessão
        // ativa) e depois voltado a tocar em "Visitante": sem isso, seria sempre um descartável.
        if (await TryRestoreGuestSessionAsync())
        {
            Preferences.Default.Set(KeyAnon, true);
            System.Diagnostics.Debug.WriteLine($"[ChessArena] Visitante restaurado do backup, uid={Db?.Auth.CurrentUser?.Id}");

            // A sessão do servidor já voltou a ser a do visitante, mas o perfil local (nome,
            // pontos) ainda está com o que ficou em cache da conta real usada nesse meio tempo
            // (LoadFromSupabaseAsync roda no login com Google/e-mail) — sem recarregar aqui, a
            // tela mostrava o nome/pontuação de quem tinha acabado de logar com Google, mesmo
            // já estando de volta na conta anônima certa por trás.
            await AppState.Current.Profile.LoadFromSupabaseAsync();
            AppState.Current.Ranking.InvalidateCache();
            return false;
        }

        // Cria uma sessão anônima de verdade no Supabase (auth.uid() real) — sem isso, o
        // visitante não passa nas policies de RLS que exigem authenticated + challenger_id/
        // white_id/black_id = auth.uid() (ex.: criar desafio em "Jogar com Amigo", found_match).
        // Se não houver internet ou o projeto não tiver "Anonymous Sign-Ins" habilitado no
        // painel do Supabase, cai pro modo 100% local de antes (perfil não sincroniza, e
        // funcionalidades que dependem do servidor ficam indisponíveis pro visitante).
        try
        {
            if (Db != null)
                await Db.Auth.SignInAnonymously();
            System.Diagnostics.Debug.WriteLine($"[ChessArena] SignInAnonymously OK, uid={Db?.Auth.CurrentUser?.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChessArena] SignInAnonymously FAILED: {ex}");
        }

        Preferences.Default.Set(KeyAnon, true);
        _ = SaveGuestSessionSnapshotAsync();

        // Visitante novo: limpa qualquer dado local que tenha sobrado de uma conta anterior no
        // aparelho (mesmo motivo do ResetLocal() antes de um login real — ver LoginPage) e
        // sorteia um nome novo (o nome do visitante não é editável — ver ProfilePage).
        var profile = AppState.Current.Profile;
        profile.ResetLocal();
        profile.Name = $"Visitante{Random.Shared.Next(1000, 9999)}";
        AppState.Current.Ranking.InvalidateCache();

        // Sem isso a linha em "profiles" ficava com nome vazio (só id, via handle_new_user)
        // até o visitante abrir a tela de Perfil e salvar — e enquanto isso, ele aparecia no
        // ranking (via jogos reais/finalize_game, que já grava elo direto ali) mas sem nome.
        _ = profile.SyncToSupabaseAsync();
        return true;
    }

    /// <summary>Guarda o access/refresh token da sessão anônima ATUAL num slot separado do
    /// SecureStorage — chamado sempre que confirmamos/criamos o visitante deste aparelho, e
    /// também logo antes de trocar pra uma conta real (ver TryLoginAsync/TrySignInWithGoogleAsync/
    /// TryRegisterAsync), já que a troca sobrescreve a sessão "ativa" (a mesma guardada pelo
    /// MauiSessionHandler) e sem este backup à parte o visitante ficaria irrecuperável.</summary>
    private async Task SaveGuestSessionSnapshotAsync()
    {
        // Só salva se a sessão atual REALMENTE for a do visitante — chamar isso por engano com
        // uma conta real ativa sobrescreveria o backup do visitante com o token errado.
        if (!Preferences.Default.Get(KeyAnon, false)) return;

        var session = Db?.Auth.CurrentSession;
        if (session == null || string.IsNullOrEmpty(session.AccessToken) || string.IsNullOrEmpty(session.RefreshToken))
            return;
        try
        {
            await SecureStorage.Default.SetAsync(KeyGuestAccess,  session.AccessToken);
            await SecureStorage.Default.SetAsync(KeyGuestRefresh, session.RefreshToken);
            System.Diagnostics.Debug.WriteLine($"[ChessArena] SaveGuestSessionSnapshotAsync: salvo uid={Db?.Auth.CurrentUser?.Id}");
        }
        catch { }
    }

    /// <summary>Tenta reativar a sessão de visitante salva por SaveGuestSessionSnapshotAsync.
    /// Retorna false (sem exceção) se nunca houve um backup, ou se o token não é mais válido —
    /// nesses casos o chamador segue o fluxo normal de criar um visitante novo.</summary>
    private async Task<bool> TryRestoreGuestSessionAsync()
    {
        try
        {
            if (Db == null) return false;
            var access  = await SecureStorage.Default.GetAsync(KeyGuestAccess);
            var refresh = await SecureStorage.Default.GetAsync(KeyGuestRefresh);
            System.Diagnostics.Debug.WriteLine(
                $"[ChessArena] TryRestoreGuestSessionAsync: hasAccess={!string.IsNullOrEmpty(access)} hasRefresh={!string.IsNullOrEmpty(refresh)}");
            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh)) return false;

            var session = await Db.Auth.SetSession(access, refresh);
            System.Diagnostics.Debug.WriteLine($"[ChessArena] TryRestoreGuestSessionAsync: restored uid={session?.User?.Id}");
            return session?.User != null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChessArena] TryRestoreGuestSessionAsync FAILED: {ex}");
            return false;
        }
    }

    // ── Login por e-mail + senha ──────────────────────────────────────────────
    public async Task<bool> TryLoginAsync(string email, string password)
    {
        try
        {
            // Guarda o token do visitante ANTES de trocar de sessão (ver SaveGuestSessionSnapshotAsync).
            await SaveGuestSessionSnapshotAsync();
            var session = await Db.Auth.SignIn(email.Trim().ToLower(), password);
            bool ok = session?.User != null;
            if (ok)
            {
                Preferences.Default.Remove(KeyAnon);
                AppState.Current.Ranking.InvalidateCache();
            }
            return ok;
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
            await SaveGuestSessionSnapshotAsync();
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
            if (!needsConfirmation)
            {
                Preferences.Default.Remove(KeyAnon);
                AppState.Current.Ranking.InvalidateCache();
            }
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
            await SaveGuestSessionSnapshotAsync();

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
            if (session?.User == null) return (false, "Não foi possível concluir o login com Google.");
            Preferences.Default.Remove(KeyAnon);
            AppState.Current.Ranking.InvalidateCache();
            System.Diagnostics.Debug.WriteLine($"[ChessArena] TrySignInWithGoogleAsync OK, uid={session.User.Id}");
            return (true, "");
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

    // ── Criar senha (contas que só têm login pelo Google, sem senha prévia) ───
    public async Task<(bool Ok, string Error)> TrySetInitialPasswordAsync(string newPassword)
    {
        try
        {
            await Db.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = newPassword });
            return (true, "");
        }
        catch { return (false, "Não foi possível criar a senha."); }
    }

    // ── Logout ────────────────────────────────────────────────────────────────
    public async Task LogoutAsync()
    {
        System.Diagnostics.Debug.WriteLine(
            $"[ChessArena] LogoutAsync: KeyAnon={Preferences.Default.Get(KeyAnon, false)} " +
            $"currentUser={Db?.Auth.CurrentUser?.Id ?? "null"}");

        // Visitante: a conta anônima é permanente neste aparelho — "Sair" é só navegação de
        // volta pra tela de login, sem destruir a sessão. Uma vez destruída (SignOut), o
        // Supabase não deixa recuperar a MESMA conta anônima depois — reentrar como visitante
        // sempre criaria uma conta descartável nova, que era exatamente o comportamento que
        // devia deixar de existir.
        if (IsAnonymous) return;

        Preferences.Default.Remove(KeyAnon);
        if (Db?.Auth.CurrentUser != null)
            await Db.Auth.SignOut();
    }
}
