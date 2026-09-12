using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;

namespace ChessMAUI;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// Link de "esqueci senha" clicado no e-mail (chessarena://reset-callback?code=...) — o login
// Google usa um esquema diferente, tratado por WebAuthenticationCallbackActivity.
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "chessarena", DataHost = "reset-callback")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetBackgroundDrawable(new ColorDrawable(Android.Graphics.Color.ParseColor("#060B14")));
        Window?.SetSoftInputMode(SoftInput.AdjustResize);
        HandleDeepLink(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleDeepLink(intent);
    }

    private static void HandleDeepLink(Intent? intent)
    {
        var data = intent?.Data;
        if (data == null) return;
        try { ChessMAUI.Services.DeepLinkRouter.Handle(new Uri(data.ToString()!)); }
        catch { }
    }
}
