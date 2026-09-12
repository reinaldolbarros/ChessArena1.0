using Android.App;
using Android.Content;

namespace ChessMAUI;

// Recebe exclusivamente o retorno do login Google (chessarena://auth-callback) — o
// WebAuthenticator do MAUI completa a autenticação sozinho a partir daqui.
[Activity(NoHistory = true, LaunchMode = Android.Content.PM.LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "chessarena", DataHost = "auth-callback")]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
