using ChessMAUI.Models;

namespace ChessMAUI.Services;

/// <summary>
/// Gerencia o ranking global e semanal.
/// Bots simulados dão sensação de comunidade ativa.
/// </summary>
public class RankingService
{
    private static readonly (string Avatar, string Name, int Points)[] BotSeeds =
    [
        ("🦁","Magnus AI",    2800), ("🐉","Kasparov Bot",  2700),
        ("👑","Fischer AI",   2650), ("⚡","Tal Bot",        2580),
        ("🎯","Karpov AI",    2500), ("🔥","Anand Bot",      2440),
        ("💎","Carlsen X",    2380), ("🌟","Capablanca AI",  2300),
        ("🎭","Morphy Bot",   2200), ("🛡️","Polgar AI",      2100),
        ("♛","Lasker Bot",   2000), ("♜","Alekhine AI",    1900),
        ("♝","Petrosian X",  1800), ("♞","Spassky Bot",    1650),
        ("🦊","Bronstein AI", 1500), ("🐺","Smyslov Bot",   1400),
        ("🦅","Euwe AI",      1350), ("🐯","Botvinnik X",   1280),
        ("🌊","Lputian Bot",  1230), ("🏔","Morozevich X",  1210),
    ];

    private readonly List<RankingEntry> _bots;
    private readonly Random             _rng = Random.Shared;

    public event Action? RankingUpdated;

    public RankingService()
    {
        _bots = BotSeeds.Select(b => new RankingEntry
        {
            Avatar     = b.Avatar,
            Name       = b.Name,
            Points     = b.Points,
            WeekPoints = _rng.Next(0, Math.Max(1, b.Points / 100)),
            IsHuman    = false
        }).ToList();

        _ = SimulateActivityAsync();
    }

    // -----------------------------------------------------------------------
    // Ranking global — bots + humano, ordenado por pontos
    // -----------------------------------------------------------------------
    public List<RankingEntry> GetGlobal(ProfileService profile)
    {
        var human = HumanEntry(profile);
        var all   = _bots.Concat([human])
                         .OrderByDescending(e => e.Points)
                         .ToList();

        for (int i = 0; i < all.Count; i++)
            all[i].Position = i + 1;

        return all;
    }

    // -----------------------------------------------------------------------
    // Ranking semanal — ordenado por pontos da semana
    // -----------------------------------------------------------------------
    public List<RankingEntry> GetWeekly(ProfileService profile)
    {
        var human = HumanEntry(profile);
        var all   = _bots.Concat([human])
                         .OrderByDescending(e => e.WeekPoints)
                         .ToList();

        for (int i = 0; i < all.Count; i++)
            all[i].Position = i + 1;

        return all;
    }

    private static RankingEntry HumanEntry(ProfileService p) => new()
    {
        Avatar     = p.Avatar,
        Name       = p.Name,
        Points     = p.Points,
        WeekPoints = p.WeekPoints,
        IsHuman    = true
    };

    // -----------------------------------------------------------------------
    // Simula pequenas variações de pontos nos bots ao longo do tempo
    // -----------------------------------------------------------------------
    private async Task SimulateActivityAsync()
    {
        while (true)
        {
            await Task.Delay(_rng.Next(8000, 20000));
            var bot = _bots[_rng.Next(_bots.Count)];
            int delta = _rng.Next(1, 8);
            bot.Points     += delta;
            bot.WeekPoints += delta;
            MainThread.BeginInvokeOnMainThread(() => RankingUpdated?.Invoke());
        }
    }
}
