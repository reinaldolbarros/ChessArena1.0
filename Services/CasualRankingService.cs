namespace ChessMAUI.Services;

/// <summary>Rastreia pontuação semanal do Arena Casual — define prioridade de entrada na Liga.</summary>
public class CasualRankingService
{
    private const string KeyWeekPts   = "casual_week_pts";
    private const string KeyWeekStart = "casual_week_start";

    public const int PriorityThreshold = 5;

    // ── Pontos semanais ────────────────────────────────────────────────────────

    public int WeeklyPoints
    {
        get { EnsureCurrentWeek(); return Preferences.Default.Get(KeyWeekPts, 0); }
        private set => Preferences.Default.Set(KeyWeekPts, value);
    }

    public bool HasLigaPriority => WeeklyPoints >= PriorityThreshold;

    public string PriorityStatusLabel => HasLigaPriority
        ? "✓ Prioridade Liga garantida"
        : $"Arena Casual: {WeeklyPoints}/{PriorityThreshold} pts para prioridade";

    public string PriorityBadge => HasLigaPriority ? "⚡ PRIORIDADE" : "";

    // ── Registrar resultado de torneio casual ──────────────────────────────────

    public void AddPointsForPosition(int position, int tournamentSize)
    {
        EnsureCurrentWeek();
        int half = tournamentSize / 2;

        int pts = position switch
        {
            1 => 20,
            2 => 12,
            3 => 8,
            4 => 5,
            _ when position <= half => 2,
            _ => 1
        };

        WeeklyPoints += pts;
    }

    // ── Reset semanal (segunda-feira) ─────────────────────────────────────────

    private void EnsureCurrentWeek()
    {
        string current = CurrentWeekKey;
        if (Preferences.Default.Get(KeyWeekStart, "") != current)
        {
            Preferences.Default.Set(KeyWeekStart, current);
            Preferences.Default.Set(KeyWeekPts, 0);
        }
    }

    private static string CurrentWeekKey
    {
        get
        {
            var today = DateTime.Today;
            int days  = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return today.AddDays(-days).ToString("yyyy-MM-dd");
        }
    }
}
