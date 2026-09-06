namespace ChessMAUI.Services;

/// <summary>
/// Controla a exibição de anúncios para usuários gratuitos.
/// Em produção, substituir SimulateInterstitialAsync por chamada real ao AdMob/Unity Ads.
/// </summary>
public class AdService
{
    private const string KeyAdDate  = "ads_date";
    private const string KeyAdCount = "ads_shown_today";
    private const int    MaxAdsPerDay = 2;

    private static string TodayKey => DateTime.Today.ToString("yyyy-MM-dd");

    public int AdsShownToday
    {
        get
        {
            if (Preferences.Default.Get(KeyAdDate, "") != TodayKey)
            {
                Preferences.Default.Set(KeyAdDate,  TodayKey);
                Preferences.Default.Set(KeyAdCount, 0);
                return 0;
            }
            return Preferences.Default.Get(KeyAdCount, 0);
        }
    }

    public int AdsRemaining => Math.Max(0, MaxAdsPerDay - AdsShownToday);

    public bool ShouldShowAd(SubscriptionService sub)
        => sub.HasAds && AdsShownToday < MaxAdsPerDay;

    public void RecordAdShown()
    {
        _ = AdsShownToday; // garante reset de data
        Preferences.Default.Set(KeyAdCount, Preferences.Default.Get(KeyAdCount, 0) + 1);
    }

    // Simula um anúncio intersticial. Substituir pelo SDK real em produção.
    public async Task SimulateInterstitialAsync(Page page)
    {
        await page.DisplayAlert(
            "Patrocinado",
            "Anúncio — AssineJá e não veja mais anúncios!\n\n[espaço reservado para AdMob]",
            "Continuar");
        RecordAdShown();
    }
}
