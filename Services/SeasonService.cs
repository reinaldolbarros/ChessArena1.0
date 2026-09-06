using System.Globalization;
using System.Text.Json;
using ChessMAUI.Models;

namespace ChessMAUI.Services;

public class SeasonService
{
    private const string KeyPoints        = "season_points";
    private const string KeyMonth         = "season_month";       // "2025-05"
    private const string KeyHallOfFame    = "season_hof";
    private const string KeyLastProcessed = "season_last_proc";

    private static readonly (string Avatar, string Name)[] BotPool =
    [
        ("🦁","Magnus AI"),    ("🐉","Kasparov Bot"),  ("👑","Fischer AI"),   ("⚡","Tal Bot"),
        ("🎯","Karpov AI"),    ("🔥","Anand Bot"),     ("💎","Carlsen X"),    ("🌟","Morphy Bot"),
        ("🎭","Polgar AI"),    ("🛡️","Lasker Bot"),    ("♛","Alekhine AI"),  ("♜","Petrosian X"),
        ("♝","Spassky Bot"),  ("♞","Bronstein AI"),  ("🦊","Smyslov Bot"),  ("🐺","Euwe AI"),
        ("🦅","Botvinnik X"), ("🐯","Lputian Bot"),   ("🌊","Morozevich X"), ("🏔","Grischuk AI"),
    ];

    private static readonly (string Country, string State)[] LocationPool =
    [
        ("Brasil", "SP"),  ("Brasil", "RJ"),  ("Brasil", "MG"),
        ("Brasil", "RS"),  ("Brasil", "PR"),  ("Brasil", "BA"),
        ("Brasil", "CE"),  ("Brasil", "GO"),  ("Brasil", "PE"),
        ("Argentina", "BA"), ("Portugal", "Lisboa"),
        ("EUA", "NY"),       ("Japão", "Tóquio"),
        ("França", "Paris"), ("Alemanha", "Berlim"),
        ("Rússia", "Moscou"),("China", "Xangai"),
        ("Reino Unido", "Londres"), ("Índia", "Mumbai"),
        ("Espanha", "Madri"),
    ];

    // ── Temporada atual ────────────────────────────────────────────────────────

    public int CurrentPoints
    {
        get { EnsureCurrentSeason(); return Preferences.Default.Get(KeyPoints, 0); }
    }

    public string CurrentSeasonLabel
    {
        get
        {
            string raw = DateTime.Today.ToString("MMMM yyyy", new CultureInfo("pt-BR"));
            return char.ToUpper(raw[0]) + raw[1..];
        }
    }

    public void AddPoints(int pts)
    {
        EnsureCurrentSeason();
        Preferences.Default.Set(KeyPoints, CurrentPoints + pts);
    }

    // ── Leaderboard da temporada ───────────────────────────────────────────────

    public List<SeasonEntry> GetLeaderboard(TitleService titles, ProfileService profile,
                                            int? overridePoints = null)
    {
        EnsureCurrentSeason();
        int humanPts = overridePoints ?? CurrentPoints;

        var seed = int.Parse(DateTime.Today.ToString("yyyyMM"));
        var rng  = new Random(seed);

        var bots = BotPool.Select((b, i) => new SeasonEntry
        {
            Name       = b.Name,
            Avatar     = b.Avatar,
            TitleLabel = "Competidor",
            TitleIcon  = "♟",
            Points     = rng.Next(10, 450),
            IsHuman    = false,
            Country    = LocationPool[i % LocationPool.Length].Country,
            State      = LocationPool[i % LocationPool.Length].State,
        }).ToList();

        var human = new SeasonEntry
        {
            Name       = profile.Name,
            Avatar     = profile.Avatar,
            AvatarPath = profile.AvatarPath,
            TitleLabel = titles.TitleLabel,
            TitleIcon  = titles.TitleIcon,
            Points     = humanPts,
            IsHuman    = true,
            Country    = profile.Country,
            State      = profile.State,
        };

        var all = bots.Append(human)
                      .OrderByDescending(e => e.Points)
                      .ToList();

        for (int i = 0; i < all.Count; i++) all[i].Position = i + 1;
        return all;
    }

    public SeasonEntry GetMonthlyLeader(TitleService titles, ProfileService profile)
        => GetLeaderboard(titles, profile)[0];

    // ── Hall of Fame ──────────────────────────────────────────────────────────

    public List<HallOfFameEntry> GetHallOfFame() => LoadHof();

    // ── Reset mensal ─────────────────────────────────────────────────────────

    private void EnsureCurrentSeason()
    {
        var current = SeasonKey(DateTime.Today);
        var stored  = Preferences.Default.Get(KeyMonth, "");
        if (stored == current) return;

        if (!string.IsNullOrEmpty(stored))
            ProcessSeasonEnd(stored);

        Preferences.Default.Set(KeyMonth,  current);
        Preferences.Default.Set(KeyPoints, 0);
    }

    private void ProcessSeasonEnd(string seasonKey)
    {
        if (Preferences.Default.Get(KeyLastProcessed, "") == seasonKey) return;
        Preferences.Default.Set(KeyLastProcessed, seasonKey);

        int pts = Preferences.Default.Get(KeyPoints, 0);
        if (pts <= 0) return;

        if (!DateTime.TryParseExact(seasonKey + "-01", "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return;

        string raw   = date.ToString("MMMM yyyy", new CultureInfo("pt-BR"));
        string label = char.ToUpper(raw[0]) + raw[1..];

        var state = AppState.Current;
        var board = GetLeaderboard(state.Titles, state.Profile, pts);
        var top3  = board.Take(3).ToList();

        var human = board.FirstOrDefault(e => e.IsHuman);
        if (human != null)
        {
            if (human.Position == 1) state.Titles.RecordMonthlyChampion();
            else if (human.Position <= 3) state.Titles.RecordMonthlyTop3();
        }

        var hof = LoadHof();
        hof.Insert(0, new HallOfFameEntry
        {
            SeasonLabel    = label,
            ChampionName   = top3.Count > 0 ? top3[0].Name   : "—",
            ChampionAvatar = top3.Count > 0 ? top3[0].Avatar : "♟",
            TitleLabel     = top3.Count > 0 ? top3[0].TitleLabel : "",
            Points         = top3.Count > 0 ? top3[0].Points  : 0,
            SecondName     = top3.Count > 1 ? top3[1].Name    : "—",
            ThirdName      = top3.Count > 2 ? top3[2].Name    : "—",
        });
        SaveHof(hof.Take(24).ToList());
    }

    private static string SeasonKey(DateTime d) => d.ToString("yyyy-MM");

    private List<HallOfFameEntry> LoadHof()
    {
        var json = Preferences.Default.Get(KeyHallOfFame, "[]");
        try { return JsonSerializer.Deserialize<List<HallOfFameEntry>>(json) ?? []; }
        catch { return []; }
    }

    private void SaveHof(List<HallOfFameEntry> list)
        => Preferences.Default.Set(KeyHallOfFame, JsonSerializer.Serialize(list));
}
