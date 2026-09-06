namespace ChessMAUI.Models;

public enum LeagueEventType { Semanal, Copa, GrandeArena }

public enum PlayerTitle { Recruta = 0, Competidor = 1, Cavaleiro = 2, Mestre = 3, GraoMestre = 4, Lenda = 5 }

public class LeagueEvent
{
    public const int HardCap = 256;

    public LeagueEventType Type        { get; init; }
    public DateTime        ScheduledAt { get; init; }
    public decimal         BuyIn       { get; init; }

    // ── Inscrições dinâmicas ───────────────────────────────────────────────────
    // Seed determinístico por evento: mesmo valor durante toda a janela do evento
    public int SimulatedRegistrations
    {
        get
        {
            int seed = int.Parse(ScheduledAt.ToString("yyyyMMddHH")) + (int)Type * 37;
            var rng  = new Random(seed);
            return Type switch
            {
                LeagueEventType.Semanal     => rng.Next(20,  181),
                LeagueEventType.Copa        => rng.Next(60,  230),
                LeagueEventType.GrandeArena => rng.Next(120, 257),
                _                           => rng.Next(16,  65)
            };
        }
    }

    // Próxima potência de 2 ≥ inscritos, máx 256
    public int EffectiveSize
    {
        get
        {
            int reg  = SimulatedRegistrations;
            int size = 16;
            while (size < reg && size < HardCap) size *= 2;
            return size;
        }
    }

    // Tempo reduzido conforme o bracket cresce
    public int TimeMinutes => EffectiveSize switch
    {
        <= 16  => 10,
        <= 32  => 5,
        <= 64  => 3,
        <= 128 => 2,
        _      => 1
    };

    public bool IsFull     => SimulatedRegistrations >= HardCap;

    public DateTime RegistrationOpens => ScheduledAt.AddHours(-2);
    public DateTime EntryDeadline     => ScheduledAt.AddHours(2);

    public bool IsOpen     => DateTime.Now >= RegistrationOpens && DateTime.Now < EntryDeadline;
    public bool IsUpcoming => DateTime.Now < RegistrationOpens;

    public string Name => Type switch
    {
        LeagueEventType.Semanal     => "Torneio Semanal",
        LeagueEventType.Copa        => "Copa ChessArena",
        LeagueEventType.GrandeArena => "Grande Arena",
        _ => ""
    };

    public string Subtitle => Type switch
    {
        LeagueEventType.Semanal     => $"{SimulatedRegistrations} inscritos · Todo sábado",
        LeagueEventType.Copa        => $"{SimulatedRegistrations} inscritos · Quinzenal",
        LeagueEventType.GrandeArena => $"{SimulatedRegistrations} inscritos · Mensal",
        _ => ""
    };

    public int ParticipationPoints => Type switch
    {
        LeagueEventType.Semanal    => 3,
        LeagueEventType.Copa       => 5,
        LeagueEventType.GrandeArena => 8,
        _ => 0
    };

    public int GetPoints(int position) => position switch
    {
        1 => Type switch { LeagueEventType.Semanal => 50,  LeagueEventType.Copa => 100, _ => 200 },
        2 => Type switch { LeagueEventType.Semanal => 30,  LeagueEventType.Copa => 60,  _ => 120 },
        3 => Type switch { LeagueEventType.Semanal => 20,  LeagueEventType.Copa => 40,  _ => 80  },
        4 => Type switch { LeagueEventType.Semanal => 14,  LeagueEventType.Copa => 28,  _ => 56  },
        _ when position <= 8  => Type switch { LeagueEventType.Semanal => 7, LeagueEventType.Copa => 14, _ => 28 },
        _ when position <= 16 => Type switch { LeagueEventType.Copa => 6, LeagueEventType.GrandeArena => 12, _ => 0 },
        _ when position <= 32 => Type == LeagueEventType.GrandeArena ? 5 : 0,
        _ => ParticipationPoints
    };

    public string TimeUntilLabel
    {
        get
        {
            var diff = RegistrationOpens - DateTime.Now;
            if (diff <= TimeSpan.Zero && IsOpen)  return "Inscrições abertas agora";
            if (diff.TotalDays >= 1)               return $"Em {(int)diff.TotalDays} dia{((int)diff.TotalDays != 1 ? "s" : "")}";
            if (diff.TotalHours >= 1)              return $"Em {(int)diff.TotalHours}h {diff.Minutes:00}min";
            return $"Em {(int)diff.TotalMinutes} min";
        }
    }
}

public class SeasonEntry
{
    public string Name       { get; set; } = "";
    public string Avatar     { get; set; } = "";
    public string AvatarPath { get; set; } = "";
    public string TitleLabel { get; set; } = "";
    public string TitleIcon  { get; set; } = "";
    public string Country    { get; set; } = "";
    public string State      { get; set; } = "";
    public int    Points     { get; set; }
    public int    Position   { get; set; }
    public bool   IsHuman    { get; set; }

    public string PositionLabel  => Position switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"{Position}º" };
    public Color  NameColor      => IsHuman ? Color.FromArgb("#4CAF50") : Colors.White;
    public string LocationLabel  => State.Length > 0 && Country.Length > 0 ? $"{Country} · {State}"
                                  : Country.Length > 0 ? Country
                                  : State.Length > 0   ? State : "";
}

public class HallOfFameEntry
{
    public string SeasonLabel    { get; set; } = "";
    public string ChampionName   { get; set; } = "";
    public string ChampionAvatar { get; set; } = "";
    public string TitleLabel     { get; set; } = "";
    public int    Points         { get; set; }
    public string SecondName     { get; set; } = "";
    public string ThirdName      { get; set; } = "";
}
