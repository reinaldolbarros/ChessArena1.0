using System.Text.Json;

namespace ChessMAUI.Services;

public class RakeEntry
{
    public DateTime Date        { get; set; } = DateTime.Now;
    public decimal  Amount      { get; set; }
    public string   Description { get; set; } = "";
    public string   TournType   { get; set; } = "";
    public int      Size        { get; set; }
    public decimal  BuyIn       { get; set; }
}

public class AdminService
{
    private const string KeyRakeTotal   = "admin_rake_total";
    private const string KeyRakeHistory = "admin_rake_history";

    public decimal TotalRake
    {
        get => (decimal)Preferences.Default.Get(KeyRakeTotal, 0.0);
        private set => Preferences.Default.Set(KeyRakeTotal, (double)value);
    }

    public int TotalTournaments => LoadHistory().Count;

    public decimal AverageRake
    {
        get
        {
            var h = LoadHistory();
            return h.Count == 0 ? 0 : Math.Round(TotalRake / h.Count, 2);
        }
    }

    public decimal RakeToday
    {
        get
        {
            var today = DateTime.Today;
            return LoadHistory().Where(e => e.Date.Date == today).Sum(e => e.Amount);
        }
    }

    public decimal RakeThisWeek
    {
        get
        {
            var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
            return LoadHistory().Where(e => e.Date.Date >= monday).Sum(e => e.Amount);
        }
    }

    public void RecordRake(decimal amount, string description, string tournType, int size, decimal buyIn)
    {
        if (amount <= 0) return;
        TotalRake += amount;
        var history = LoadHistory();
        history.Insert(0, new RakeEntry
        {
            Amount      = amount,
            Description = description,
            TournType   = tournType,
            Size        = size,
            BuyIn       = buyIn
        });
        if (history.Count > 500) history = history.Take(500).ToList();
        SaveHistory(history);
    }

    public List<RakeEntry> LoadHistory()
    {
        var json = Preferences.Default.Get(KeyRakeHistory, "[]");
        try { return JsonSerializer.Deserialize<List<RakeEntry>>(json) ?? []; }
        catch { return []; }
    }

    private void SaveHistory(List<RakeEntry> history)
        => Preferences.Default.Set(KeyRakeHistory, JsonSerializer.Serialize(history));
}
