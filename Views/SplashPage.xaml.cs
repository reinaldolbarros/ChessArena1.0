using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Supabase inicia em background — não bloqueia o splash
        _ = SupabaseService.Instance.InitializeAsync();

        await Task.Delay(1000);

        var auth    = AppState.Current.Auth;
        var profile = AppState.Current.Profile;

        // Fire-and-forget: espera internamente até Supabase estar pronto
        if (auth.IsAuthenticated && !auth.IsAnonymous)
            _ = profile.LoadFromSupabaseAsync();

        Page next;
        if (!auth.IsAuthenticated)
            next = new LoginPage();
        else if (DailyMissionsPage.ShouldShow())
            next = new DailyMissionsPage();
        else
            next = new AppShell();

        if (Application.Current is not null)
            Application.Current.Windows[0].Page = next;
    }
}
