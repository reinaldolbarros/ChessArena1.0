using ChessMAUI.Views;

namespace ChessMAUI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Liga/Casual — COMENTADO: aguardando base de jogadores
        // Routing.RegisterRoute("WaitingRoomPage",       typeof(WaitingRoomPage));
        // Routing.RegisterRoute("BracketPage",           typeof(BracketPage));
        // Routing.RegisterRoute("TournamentLobbyPage",   typeof(TournamentLobbyPage));
        // Routing.RegisterRoute("TournamentHistoryPage", typeof(TournamentHistoryPage));
        // Routing.RegisterRoute("LeaguePage",            typeof(LeaguePage));
        // Routing.RegisterRoute("SeasonRankingPage",     typeof(SeasonRankingPage));

        Routing.RegisterRoute("GamePage",             typeof(GamePage));
        Routing.RegisterRoute("ExtractPage",          typeof(ExtractPage));
        Routing.RegisterRoute("PointsExtractPage",    typeof(PointsExtractPage));
        Routing.RegisterRoute("FriendInvitePage",     typeof(FriendInvitePage));
        Routing.RegisterRoute("LoginPage",            typeof(LoginPage));
        Routing.RegisterRoute("AdminPage",            typeof(AdminPage));
        // SubscriptionPage — COMENTADO: sem integração real de compra (Google Play Billing /
        // StoreKit). A tela ainda simula a assinatura localmente (Sub.Subscribe), o que as
        // lojas rejeitam se alcançável. Reativar só depois de integrar a compra de verdade.
        // Routing.RegisterRoute("SubscriptionPage",  typeof(SubscriptionPage));
        Routing.RegisterRoute("HallOfFamePage",       typeof(HallOfFamePage));
        Routing.RegisterRoute("CareerPage",       typeof(CareerPage));
        Routing.RegisterRoute("CareerFlowPage",   typeof(CareerFlowPage));
        Routing.RegisterRoute("RandomMatchPage",  typeof(RandomMatchPage));
        Routing.RegisterRoute("GameReviewPage",   typeof(GameReviewPage));
    }

    // Ranking e Perfil são abas do TabBar, não páginas empilhadas — trocar de aba não
    // gera histórico de navegação sozinho. Sem isso, voltar (seta física, gesto ou botão)
    // estando numa dessas abas fechava o app direto, em vez de voltar pro Início.
    protected override bool OnBackButtonPressed()
    {
        var route = Shell.Current?.CurrentState?.Location?.OriginalString ?? "";
        if (route.Contains("RankingPage") || route.Contains("ProfilePage"))
        {
            _ = Shell.Current!.GoToAsync("//LobbyPage");
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
