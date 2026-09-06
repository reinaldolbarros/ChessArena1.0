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
        Routing.RegisterRoute("SubscriptionPage",     typeof(SubscriptionPage));
        Routing.RegisterRoute("HallOfFamePage",       typeof(HallOfFamePage));
        Routing.RegisterRoute("CareerPage",       typeof(CareerPage));
        Routing.RegisterRoute("CareerFlowPage",   typeof(CareerFlowPage));
        Routing.RegisterRoute("RandomMatchPage",  typeof(RandomMatchPage));
        Routing.RegisterRoute("GameReviewPage",   typeof(GameReviewPage));
    }
}
