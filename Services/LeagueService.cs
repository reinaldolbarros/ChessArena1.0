using System.Globalization;
using ChessMAUI.Models;

namespace ChessMAUI.Services;

public record ChampionInfo(string Name, string Avatar, string TitleIcon, string TitleLabel, int Points, bool IsHuman);

public class LeagueService
{
    private const string KeyRegistered = "league_registered";

    // ── Eventos agendados ─────────────────────────────────────────────────────

    public List<LeagueEvent> GetUpcomingEvents()
    {
        var now = DateTime.Now;
        return
        [
            NextSemanal(now),
            NextCopa(now),
            NextGrandeArena(now),
        ];
    }

    // Todo sábado às 20h
    private static LeagueEvent NextSemanal(DateTime from)
    {
        var date = from.Date;
        int days = ((int)DayOfWeek.Saturday - (int)date.DayOfWeek + 7) % 7;
        if (days == 0 && from.TimeOfDay >= TimeSpan.FromHours(22)) days = 7;
        return new LeagueEvent
        {
            Type        = LeagueEventType.Semanal,
            ScheduledAt = date.AddDays(days).AddHours(20),
            BuyIn       = 80
        };
    }

    // 1º e 3º domingo do mês às 18h
    private static LeagueEvent NextCopa(DateTime from)
    {
        var month = new DateTime(from.Year, from.Month, 1);

        for (int m = 0; m <= 1; m++)
        {
            var cur = month.AddMonths(m);
            var sundays = Enumerable.Range(0, DateTime.DaysInMonth(cur.Year, cur.Month))
                .Select(d => cur.AddDays(d))
                .Where(d => d.DayOfWeek == DayOfWeek.Sunday)
                .ToList();

            foreach (var s in new[] { sundays.ElementAtOrDefault(0), sundays.ElementAtOrDefault(2) })
            {
                if (s == default) continue;
                var scheduled = s.AddHours(18);
                if (scheduled.AddHours(2) > from)
                    return new LeagueEvent { Type = LeagueEventType.Copa, ScheduledAt = scheduled, BuyIn = 150 };
            }
        }

        // fallback: próximo domingo
        return new LeagueEvent { Type = LeagueEventType.Copa, ScheduledAt = from.Date.AddDays(7).AddHours(18), BuyIn = 150 };
    }

    // Último domingo do mês às 20h
    private static LeagueEvent NextGrandeArena(DateTime from)
    {
        static DateTime LastSunday(int year, int month)
        {
            var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            return last.AddDays(-((int)last.DayOfWeek % 7));
        }

        var scheduled = LastSunday(from.Year, from.Month).AddHours(20);
        if (scheduled.AddHours(2) <= from)
        {
            var next = from.Month == 12
                ? new DateTime(from.Year + 1, 1, 1)
                : new DateTime(from.Year, from.Month + 1, 1);
            scheduled = LastSunday(next.Year, next.Month).AddHours(20);
        }
        return new LeagueEvent { Type = LeagueEventType.GrandeArena, ScheduledAt = scheduled, BuyIn = 300 };
    }

    // ── Capacidade e prioridade ───────────────────────────────────────────────

    /// <summary>
    /// Retorna se o jogador pode entrar e se tem prioridade.
    /// Quando o evento está cheio (≥256 inscritos), apenas quem tem
    /// prioridade via Arena Casual consegue vaga.
    /// </summary>
    public (bool CanEnter, bool HasPriority) CanEnterWithPriority(
        LeagueEvent evt, CasualRankingService casual)
    {
        bool hasPriority = casual.HasLigaPriority;
        bool canEnter    = !evt.IsFull || hasPriority;
        return (canEnter, hasPriority);
    }

    // ── Inscrição ─────────────────────────────────────────────────────────────

    public bool IsRegistered(LeagueEvent evt)
        => GetKeys().Contains(EventKey(evt));

    public void Register(LeagueEvent evt)
    {
        var keys = GetKeys();
        keys.Add(EventKey(evt));
        SaveKeys(keys);
    }

    public void Unregister(LeagueEvent evt)
    {
        var keys = GetKeys();
        keys.Remove(EventKey(evt));
        SaveKeys(keys);
    }

    // ── Desconto de buy-in por assinatura ─────────────────────────────────────

    public decimal GetEffectiveBuyIn(LeagueEvent evt, SubscriptionService sub)
    {
        decimal disc = sub.ActiveTier switch
        {
            SubscriptionTier.Ouro  => 0.20m,
            SubscriptionTier.Prata => 0.10m,
            _                      => 0m
        };
        return Math.Round(evt.BuyIn * (1m - disc));
    }

    // ── Prêmios — redistribuição total (sem rake) ─────────────────────────────

    public static Dictionary<int, decimal> BuildPrizes(LeagueEvent evt)
    {
        // Pool baseado nos inscritos reais, não no bracket total (bots não pagam)
        decimal pool = evt.BuyIn * evt.SimulatedRegistrations;
        return evt.EffectiveSize switch
        {
            <= 16  => Dist(pool, (1,.55m),(2,.25m),(3,.13m),(4,.07m)),
            <= 32  => Dist(pool, (1,.45m),(2,.22m),(3,.13m),(4,.09m),(5,.06m),(6,.05m)),
            <= 64  => Dist(pool, (1,.38m),(2,.22m),(3,.14m),(4,.10m),(5,.04m),(6,.04m),(7,.04m),(8,.04m)),
            <= 128 => Dist(pool, (1,.30m),(2,.18m),(3,.12m),(4,.09m),(5,.07m),(6,.06m),(7,.05m),(8,.05m),
                               (9,.02m),(10,.02m),(11,.02m),(12,.02m)),
            _      => Dist(pool, (1,.25m),(2,.15m),(3,.10m),(4,.08m),(5,.06m),(6,.05m),(7,.04m),(8,.04m),
                               (9,.03m),(10,.03m),(11,.02m),(12,.02m),(13,.02m),(14,.02m),(15,.02m),(16,.02m),
                               (17,.01m),(18,.01m),(19,.01m),(20,.01m))
        };
    }

    private static Dictionary<int, decimal> Dist(decimal pool, params (int pos, decimal pct)[] entries)
        => entries.ToDictionary(e => e.pos, e => Math.Round(pool * e.pct));

    // ── Campeão Semanal ───────────────────────────────────────────────────────

    private static readonly (string Avatar, string Name)[] ChampBotPool =
    [
        ("🦁","Magnus AI"), ("🐉","Kasparov Bot"), ("👑","Fischer AI"), ("⚡","Tal Bot"),
        ("🎯","Karpov AI"), ("🔥","Anand Bot"),    ("💎","Carlsen X"), ("🌟","Morphy Bot"),
        ("🎭","Polgar AI"), ("🛡️","Lasker Bot"),  ("♛","Alekhine AI"),("♜","Petrosian X"),
    ];

    private static string WeekKey()
    {
        var today = DateTime.Today;
        int week  = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            today, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{today.Year}{week:D2}";
    }

    public void RecordSemanalWin(ProfileService profile, TitleService titles)
    {
        string key = $"league_semanal_champ_{WeekKey()}";
        string val = $"{profile.Name}|{profile.Avatar}|{titles.TitleIcon}|{titles.TitleLabel}|{profile.Points}";
        Preferences.Default.Set(key, val);
    }

    public ChampionInfo GetWeeklyChampion(ProfileService profile, TitleService titles)
    {
        string raw = Preferences.Default.Get($"league_semanal_champ_{WeekKey()}", "");
        if (!string.IsNullOrEmpty(raw))
        {
            var p = raw.Split('|');
            if (p.Length >= 5 && int.TryParse(p[4], out int pts))
                return new ChampionInfo(p[0], p[1], p[2], p[3], pts, p[0] == profile.Name);
        }
        var rng = new Random(int.Parse(WeekKey()));
        var bot = ChampBotPool[rng.Next(ChampBotPool.Length)];
        return new ChampionInfo(bot.Name, bot.Avatar, "♟", "Competidor", rng.Next(150, 450), false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string EventKey(LeagueEvent e) => $"{e.Type}_{e.ScheduledAt:yyyyMMddHH}";

    private HashSet<string> GetKeys()
    {
        var raw = Preferences.Default.Get(KeyRegistered, "");
        return string.IsNullOrEmpty(raw) ? [] : [.. raw.Split(',')];
    }

    private void SaveKeys(HashSet<string> keys)
        => Preferences.Default.Set(KeyRegistered, string.Join(',', keys));
}
