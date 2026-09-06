namespace ChessMAUI.Services;

/// <summary>Gerencia estrelas de dedicação com decaimento por inatividade.</summary>
public class StarService
{
    private const string KeyBalance      = "star_balance";
    private const string KeyLastOpenDate = "star_last_open";
    private const string KeyAllTimeStars = "star_alltime_stars";

    public const int MaxStars     = 100;
    private const int DecayPerDay = 5;   // a partir do 2º dia ausente
    private const int GraceDays   = 1;   // 1º dia sem abrir = sem perda

    // ── Saldo atual ──────────────────────────────────────────────────────────
    public int Balance => Preferences.Default.Get(KeyBalance, 0);

    public int AllTimeStars => Preferences.Default.Get(KeyAllTimeStars, 0);

    /// <summary>
    /// Deve ser chamado uma vez ao abrir o app (OnAppearing do Lobby).
    /// Calcula dias ausentes, aplica decaimento e registra a data de hoje.
    /// </summary>
    public void RecordOpen()
    {
        string todayStr  = DateTime.Today.ToString("yyyy-MM-dd");
        string lastStr   = Preferences.Default.Get(KeyLastOpenDate, todayStr);

        if (lastStr != todayStr)
        {
            int daysAbsent = (DateTime.Today - DateTime.Parse(lastStr)).Days;
            int decayDays  = Math.Max(0, daysAbsent - GraceDays);
            int loss       = decayDays * DecayPerDay;

            if (loss > 0)
            {
                int current = Preferences.Default.Get(KeyBalance, 0);
                Preferences.Default.Set(KeyBalance, Math.Max(0, current - loss));
            }

            Preferences.Default.Set(KeyLastOpenDate, todayStr);
        }
    }

    /// <summary>Adiciona estrelas (respeita o teto). Retorna o novo saldo.</summary>
    public int Add(int amount)
    {
        if (amount <= 0) return Balance;

        int newBalance = Math.Min(MaxStars, Preferences.Default.Get(KeyBalance, 0) + amount);
        int newAllTime = Preferences.Default.Get(KeyAllTimeStars, 0) + amount;
        Preferences.Default.Set(KeyBalance,      newBalance);
        Preferences.Default.Set(KeyAllTimeStars, newAllTime);
        return newBalance;
    }

    // ── Título baseado no saldo atual ────────────────────────────────────────
    public string DedicationTitle => Balance switch
    {
        >= 70 => "Imparável",
        >= 40 => "Intenso",
        >= 20 => "Dedicado",
        >= 8  => "Regular",
        _     => "Casual"
    };
}
