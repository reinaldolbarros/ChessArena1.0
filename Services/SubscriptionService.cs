namespace ChessMAUI.Services;

public enum SubscriptionTier { Free, Prata, Ouro }

public class SubscriptionService
{
    private const string KeyTier    = "sub_tier";
    private const string KeyExpires = "sub_expires";

    public SubscriptionTier Tier
    {
        get
        {
            if (!Enum.TryParse<SubscriptionTier>(Preferences.Default.Get(KeyTier, "Free"), out var t))
                return SubscriptionTier.Free;
            return t;
        }
        private set => Preferences.Default.Set(KeyTier, value.ToString());
    }

    public DateTime? ExpiresAt
    {
        get
        {
            var s = Preferences.Default.Get(KeyExpires, "");
            return DateTime.TryParse(s, out var d) ? d : null;
        }
        private set => Preferences.Default.Set(KeyExpires, value?.ToString("O") ?? "");
    }

    public bool IsActive => Tier != SubscriptionTier.Free
                         && ExpiresAt.HasValue
                         && ExpiresAt.Value > DateTime.Now;

    public bool HasAds => !IsActive;

    public SubscriptionTier ActiveTier => IsActive ? Tier : SubscriptionTier.Free;

    // Multiplicador aplicado ao bônus diário base
    public float BonusMultiplier => ActiveTier switch
    {
        SubscriptionTier.Ouro  => 2.0f,
        SubscriptionTier.Prata => 1.5f,
        _                      => 1.0f
    };

    // Fichas bônus flat creditadas junto ao bônus diário
    public int FlatDailyBonus => ActiveTier switch
    {
        SubscriptionTier.Ouro  => 50,
        SubscriptionTier.Prata => 25,
        _                      => 0
    };

    public string BadgeIcon => ActiveTier switch
    {
        SubscriptionTier.Ouro  => "◆",
        SubscriptionTier.Prata => "◈",
        _                      => ""
    };

    public string BadgeLabel => ActiveTier switch
    {
        SubscriptionTier.Ouro  => "Grande Mestre",
        SubscriptionTier.Prata => "Challenger",
        _                      => "Gratuito"
    };

    public string StatusText
    {
        get
        {
            if (!IsActive) return "Plano Gratuito";
            return $"{BadgeLabel} · até {ExpiresAt:dd/MM/yyyy}";
        }
    }

    // Preços e nomes dos planos (usados na UI)
    public static readonly (SubscriptionTier Tier, string Label, string Price, string[] Benefits)[] Plans =
    [
        (SubscriptionTier.Free,  "Gratuito",      "Grátis",
            ["Até 2 anúncios por dia", "Bônus diário base", "Buy-in padrão na Liga"]),

        (SubscriptionTier.Prata, "Challenger",    "R$ 5,90/mês",
            ["Sem anúncios", "Bônus diário +50%", "10% desconto no buy-in da Liga", "Badge ◈ no ranking"]),

        (SubscriptionTier.Ouro,  "Grande Mestre", "R$ 9,90/mês",
            ["Sem anúncios", "Bônus diário dobrado", "20% desconto no buy-in da Liga", "Badge ◆ em destaque", "Missão bônus diária"]),
    ];

    public void Subscribe(SubscriptionTier tier, int months = 1)
    {
        Tier      = tier;
        ExpiresAt = (IsActive && Tier == tier ? ExpiresAt!.Value : DateTime.Now).AddMonths(months);
    }

    public void Cancel()
    {
        Tier      = SubscriptionTier.Free;
        ExpiresAt = null;
    }

    // Missão bônus Ouro: "Jogar como assinante" — auto-completa 1x/dia
    private const string KeyOuroBonusDate = "sub_ouro_bonus_date";

    public bool ClaimOuroBonusMission()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (Preferences.Default.Get(KeyOuroBonusDate, "") == today) return false;
        Preferences.Default.Set(KeyOuroBonusDate, today);
        return true;
    }
}
