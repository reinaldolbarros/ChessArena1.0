namespace ChessMAUI.Services;

public class DailyMission
{
    public string Id         { get; set; } = "";
    public string Description { get; set; } = "";
    public int    Target     { get; set; }
    public int    Progress   { get; set; }
    public int    StarReward { get; set; }
    public bool   Completed  => Progress >= Target;
    public bool   Rewarded   { get; set; }
}

/// <summary>Gerencia bônus diário e missões diárias.</summary>
public class DailyService
{
    private const string KeyLastLogin    = "daily_last_login";
    private const string KeyStreak       = "daily_streak";
    private const string KeyBonusClaimed = "daily_bonus_claimed";
    private const string KeyMissionDate  = "daily_mission_date";
    private const string KeyM1Progress   = "daily_m1_progress";
    private const string KeyM2Progress   = "daily_m2_progress";
    private const string KeyM3Progress   = "daily_m3_progress";
    private const string KeyM1Rewarded   = "daily_m1_rewarded";
    private const string KeyM2Rewarded   = "daily_m2_rewarded";
    private const string KeyM3Rewarded   = "daily_m3_rewarded";

    // ── Streak ───────────────────────────────────────────────────────────────
    public int LoginStreak
    {
        get => Preferences.Default.Get(KeyStreak, 0);
        private set => Preferences.Default.Set(KeyStreak, value);
    }

    // ── Bônus diário ─────────────────────────────────────────────────────────
    public bool BonusClaimedToday
        => Preferences.Default.Get(KeyBonusClaimed, "") == TodayKey;

    /// <summary>
    /// Reivindica o bônus diário. Retorna quantas estrelas conceder (sempre 1).
    /// </summary>
    public int ClaimDailyBonus()
    {
        UpdateStreak();
        Preferences.Default.Set(KeyBonusClaimed, TodayKey);
        return 1; // +1 ⭐
    }

    private void UpdateStreak()
    {
        var lastStr = Preferences.Default.Get(KeyLastLogin, "");
        var today   = DateTime.Today;

        if (!DateTime.TryParse(lastStr, out var last))
        {
            LoginStreak = 1;
        }
        else
        {
            int diff = (today - last.Date).Days;
            if (diff == 0) return;
            LoginStreak = diff == 1 ? LoginStreak + 1 : 1;
        }
        Preferences.Default.Set(KeyLastLogin, today.ToString("yyyy-MM-dd"));
    }

    // ── Missões diárias ───────────────────────────────────────────────────────
    public List<DailyMission> GetMissions()
    {
        EnsureTodayReset();

        return
        [
            new DailyMission
            {
                Id = "m1", Description = "Jogar 3 partidas (IA ou Casual)",
                Target = 3, Progress = Preferences.Default.Get(KeyM1Progress, 0),
                StarReward = 2,
                Rewarded = Preferences.Default.Get(KeyM1Rewarded, "") == TodayKey
            },
            new DailyMission
            {
                Id = "m2", Description = "Vencer 1 partida",
                Target = 1, Progress = Preferences.Default.Get(KeyM2Progress, 0),
                StarReward = 3,
                Rewarded = Preferences.Default.Get(KeyM2Rewarded, "") == TodayKey
            },
            new DailyMission
            {
                Id = "m3", Description = "Manter sequência de login (7 dias)",
                Target = 7, Progress = Math.Min(7, Preferences.Default.Get(KeyStreak, 0)),
                StarReward = 5,
                Rewarded = Preferences.Default.Get(KeyM3Rewarded, "") == TodayKey
            },
        ];
    }

    /// <summary>
    /// Registra uma partida jogada.
    /// Retorna estrelas a conceder (0 se missão já recompensada ou não completou agora).
    /// </summary>
    public int RecordGamePlayed()
    {
        EnsureTodayReset();
        int progress = Preferences.Default.Get(KeyM1Progress, 0);
        if (progress >= 3) return 0;

        progress++;
        Preferences.Default.Set(KeyM1Progress, progress);
        if (progress < 3) return 0;

        if (Preferences.Default.Get(KeyM1Rewarded, "") == TodayKey) return 0;
        Preferences.Default.Set(KeyM1Rewarded, TodayKey);
        return 2; // +2 ⭐
    }

    /// <summary>
    /// Registra uma vitória do jogador.
    /// Retorna estrelas a conceder (0 se já recompensado hoje).
    /// </summary>
    public int RecordWin()
    {
        EnsureTodayReset();
        if (Preferences.Default.Get(KeyM2Progress, 0) >= 1) return 0;

        Preferences.Default.Set(KeyM2Progress, 1);

        if (Preferences.Default.Get(KeyM2Rewarded, "") == TodayKey) return 0;
        Preferences.Default.Set(KeyM2Rewarded, TodayKey);
        return 3; // +3 ⭐
    }

    /// <summary>
    /// Verifica se a missão de sequência de 7 dias está completa e ainda não recompensada.
    /// Retorna estrelas a conceder (0 se não elegível).
    /// </summary>
    public int TryClaimStreakMission()
    {
        EnsureTodayReset();
        if (LoginStreak < 7) return 0;
        if (Preferences.Default.Get(KeyM3Rewarded, "") == TodayKey) return 0;

        Preferences.Default.Set(KeyM3Rewarded, TodayKey);
        return 5; // +5 ⭐
    }

    // ── Interno ───────────────────────────────────────────────────────────────
    private void EnsureTodayReset()
    {
        if (Preferences.Default.Get(KeyMissionDate, "") == TodayKey) return;

        Preferences.Default.Set(KeyMissionDate, TodayKey);
        Preferences.Default.Set(KeyM1Progress,  0);
        Preferences.Default.Set(KeyM2Progress,  0);
        Preferences.Default.Set(KeyM3Progress,  0);
        Preferences.Default.Set(KeyM1Rewarded,  "");
        Preferences.Default.Set(KeyM2Rewarded,  "");
        Preferences.Default.Set(KeyM3Rewarded,  "");
    }

    private static string TodayKey => DateTime.Today.ToString("yyyy-MM-dd");
}
