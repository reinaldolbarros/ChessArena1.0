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

        // Supabase inicia em background — não bloqueia o splash. Aproveita esse tempo ocioso
        // (2s) pra já esquentar o cache do ranking, evitando o atraso perceptível de antes
        // quando a tela inicial pedia isso pela primeira vez.
        _ = WarmUpRankingAsync();

        await Task.Delay(2000);

        var auth    = AppState.Current.Auth;
        var profile = AppState.Current.Profile;

        if (auth.IsAuthenticated && auth.IsAnonymous)
        {
            // Upgrade silencioso: visitante de uma versão anterior do app guardava só uma
            // flag local (sem conta real no Supabase) — LoginAnonymousAsync não faz nada se
            // já existir uma sessão anônima de verdade, e só cria uma na primeira vez que
            // isso roda neste aparelho. Sem isso, quem já tinha entrado como visitante antes
            // dessa correção nunca conseguiria usar recursos de servidor (criar desafio,
            // aparecer no ranking) mesmo depois de atualizar o app.
            await auth.LoginAnonymousAsync();
        }
        else if (auth.IsAuthenticated) // fire-and-forget: espera internamente até Supabase estar pronto
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

    private static async Task WarmUpRankingAsync()
    {
        try
        {
            await SupabaseService.Instance.InitializeAsync();
            await AppState.Current.Ranking.GetGlobalAsync(AppState.Current.Profile);
        }
        catch { /* a tela inicial ainda busca sozinha se isso falhar */ }
    }
}
