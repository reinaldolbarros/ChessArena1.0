namespace ChessMAUI.Services;

/// <summary>
/// Recebe a URI quando o usuário clica no link de "esqueci senha" enviado por e-mail
/// (chessarena://reset-callback?code=...) e completa a troca de código por sessão de
/// recuperação. Chamado a partir do código nativo de cada plataforma (MainActivity.OnNewIntent
/// no Android, AppDelegate.OpenUrl no iOS/MacCatalyst) — o login com Google não passa por aqui,
/// esse é resolvido de forma síncrona pelo próprio WebAuthenticator em AuthService.
/// </summary>
public static class DeepLinkRouter
{
    public static void Handle(Uri uri)
    {
        if (!string.Equals(uri.Host, "reset-callback", StringComparison.OrdinalIgnoreCase))
            return;

        var code = GetQueryParam(uri, "code");
        if (string.IsNullOrEmpty(code)) return;

        _ = AppState.Current.Auth.CompletePasswordRecoveryAsync(code);
    }

    private static string? GetQueryParam(Uri uri, string key)
    {
        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && Uri.UnescapeDataString(kv[0]) == key)
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }
}
