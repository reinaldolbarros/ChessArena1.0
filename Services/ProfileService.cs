using System.Text.Json;
using ChessMAUI.Models;

namespace ChessMAUI.Services;

// Supabase sync é fire-and-forget após cada operação local que altera pontuação ou perfil.

/// <summary>Perfil do jogador persistido via Preferences.</summary>
public class ProfileService
{
    private const string KeyName       = "profile_name";
    private const string KeyWins       = "profile_wins";
    private const string KeyLosses     = "profile_losses";
    private const string KeyTourneys   = "profile_tourneys";
    private const string KeyAvatar     = "profile_avatar";
    private const string KeyAvatarPath = "profile_avatar_path";
    private const string KeyPoints     = "profile_points";
    private const string KeyWeekPts    = "profile_week_points";
    private const string KeyWeekReset  = "profile_week_reset";
    private const string KeyCountry    = "profile_country";
    private const string KeyState      = "profile_state";

    public string Name
    {
        get => Preferences.Default.Get(KeyName, "");
        set => Preferences.Default.Set(KeyName, value);
    }

    public int Wins
    {
        get => Preferences.Default.Get(KeyWins, 0);
        set => Preferences.Default.Set(KeyWins, value);
    }

    public int Losses
    {
        get => Preferences.Default.Get(KeyLosses, 0);
        set => Preferences.Default.Set(KeyLosses, value);
    }

    public int TournamentsWon
    {
        get => Preferences.Default.Get(KeyTourneys, 0);
        set => Preferences.Default.Set(KeyTourneys, value);
    }

    public string Avatar
    {
        get => Preferences.Default.Get(KeyAvatar, "♟");
        set => Preferences.Default.Set(KeyAvatar, value);
    }

    public string AvatarPath
    {
        get => Preferences.Default.Get(KeyAvatarPath, "");
        set => Preferences.Default.Set(KeyAvatarPath, value);
    }

    public string Country
    {
        get => Preferences.Default.Get(KeyCountry, "");
        set => Preferences.Default.Set(KeyCountry, value);
    }

    public string State
    {
        get => Preferences.Default.Get(KeyState, "");
        set => Preferences.Default.Set(KeyState, value);
    }

    private const string KeyEloVersion = "profile_elo_v2";

    public int Points
    {
        get
        {
            // Migração única: na primeira leitura com o novo sistema Elo, reseta para 1200
            if (!Preferences.Default.Get(KeyEloVersion, false))
            {
                Preferences.Default.Set(KeyPoints,     1200);
                Preferences.Default.Set(KeyWeekPts,    0);
                Preferences.Default.Set(KeyEloVersion, true);
                return 1200;
            }
            return Preferences.Default.Get(KeyPoints, 1200);
        }
        set => Preferences.Default.Set(KeyPoints, value);
    }

    public int WeekPoints
    {
        get
        {
            CheckWeekReset();
            return Preferences.Default.Get(KeyWeekPts, 0);
        }
        private set => Preferences.Default.Set(KeyWeekPts, value);
    }

    private void CheckWeekReset()
    {
        var lastReset = DateTime.Parse(Preferences.Default.Get(KeyWeekReset, DateTime.MinValue.ToString()));
        var dow       = (int)DateTime.Today.DayOfWeek;
        var monday    = DateTime.Today.AddDays(-(dow == 0 ? 6 : dow - 1));
        if (lastReset < monday)
        {
            Preferences.Default.Set(KeyWeekPts,   0);
            Preferences.Default.Set(KeyWeekReset, monday.ToString("yyyy-MM-dd"));
        }
    }

    public void AddPoints(int pts, string description = "", string icon = "⭐")
    {
        Points     += pts;
        WeekPoints += pts;
        if (pts != 0) AddPointTransaction(pts, description, icon);
    }

    // ── Elo rating ───────────────────────────────────────────────────────────
    private const int EloFloor   = 100;
    private int EloK => (Wins + Losses) < 30 ? 40 : 20;

    /// <summary>Rating simulado dos oponentes IA por nível de dificuldade (depth).</summary>
    public static int EloRatingForAI(int depth) => depth switch
    {
        1 => 800,
        2 => 1000,
        3 => 1200,
        4 => 1500,
        _ => 1800
    };

    /// <summary>
    /// Aplica a fórmula Elo após uma partida.
    /// Retorna o delta (positivo = ganhou rating, negativo = perdeu).
    /// </summary>
    public int UpdateElo(int opponentRating, bool won, bool isDraw)
    {
        double expected = 1.0 / (1.0 + Math.Pow(10.0, (opponentRating - Points) / 400.0));
        double score    = isDraw ? 0.5 : (won ? 1.0 : 0.0);
        int    delta    = (int)Math.Round(EloK * (score - expected));
        int    newRating = Math.Max(EloFloor, Points + delta);
        delta   = newRating - Points;
        Points  = newRating;
        WeekPoints += delta;
        string result = won ? "vitória" : isDraw ? "empate" : "derrota";
        string sign   = delta >= 0 ? "+" : "";
        string icon   = delta >= 0 ? "📈" : "📉";
        AddPointTransaction(delta, $"Elo {sign}{delta}  ·  {result} vs {opponentRating}", icon);
        return delta;
    }

    // ── Extrato de pontos ────────────────────────────────────────────────────
    private const string KeyPointTransactions = "profile_point_transactions";
    private static readonly TimeSpan ExtractWindow = TimeSpan.FromDays(30);

    public void AddPointTransaction(int pts, string description, string icon = "⭐")
    {
        var cutoff = DateTime.Now - ExtractWindow;
        var list   = GetPointTransactions();
        list.Insert(0, new TransactionEntry
        {
            Date        = DateTime.Now,
            Description = description,
            Icon        = icon,
            Amount      = pts
        });
        list = list.Where(t => t.Date >= cutoff).ToList();
        Preferences.Default.Set(KeyPointTransactions,
            JsonSerializer.Serialize(list));
    }

    public List<TransactionEntry> GetPointTransactions()
    {
        var json = Preferences.Default.Get(KeyPointTransactions, "[]");
        try { return JsonSerializer.Deserialize<List<TransactionEntry>>(json) ?? []; }
        catch { return []; }
    }

    public bool IsNew => string.IsNullOrWhiteSpace(Name);

    public void RecordWin()  => Wins++;
    public void RecordLoss() => Losses++;

    // ── Supabase sync ─────────────────────────────────────────────────────────

    /// <summary>Envia perfil local para a tabela profiles no Supabase (upsert).</summary>
    public async Task SyncToSupabaseAsync()
    {
        for (int i = 0; i < 100 && !SupabaseService.Instance.IsReady; i++)
            await Task.Delay(200);

        var svc = SupabaseService.Instance;
        if (!svc.IsReady) return;
        var userId = AppState.Current.Auth.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            var row = new SupabaseProfile
            {
                Id             = userId,
                Name           = Name,
                Elo            = Points,
                WeekElo        = WeekPoints,
                Wins           = Wins,
                Losses         = Losses,
                TournamentsWon = TournamentsWon,
                Avatar         = Avatar,
                AvatarPath     = string.IsNullOrEmpty(AvatarPath)  ? null : AvatarPath,
                Country        = string.IsNullOrEmpty(Country)     ? null : Country,
                StateAbbr      = string.IsNullOrEmpty(State)       ? null : State,
                UpdatedAt      = DateTime.UtcNow,
            };
            await svc.Client.From<SupabaseProfile>().Upsert(row);
        }
        catch { }
    }

    /// <summary>Carrega perfil do Supabase e atualiza o cache local.</summary>
    public async Task LoadFromSupabaseAsync()
    {
        // Aguarda Supabase inicializar (máx 20 s) — pode ser chamado antes do init completar
        for (int i = 0; i < 100 && !SupabaseService.Instance.IsReady; i++)
            await Task.Delay(200);

        var svc = SupabaseService.Instance;
        if (!svc.IsReady) return;
        var userId = AppState.Current.Auth.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            var row = await svc.Client
                .From<SupabaseProfile>()
                .Where(x => x.Id == userId)
                .Single();

            if (row == null) return;

            if (!string.IsNullOrEmpty(row.Name))   Name           = row.Name;
            Wins           = row.Wins;
            Losses         = row.Losses;
            TournamentsWon = row.TournamentsWon;
            Avatar         = row.Avatar;
            if (!string.IsNullOrEmpty(row.AvatarPath)) AvatarPath = row.AvatarPath!;
            if (!string.IsNullOrEmpty(row.Country))    Country    = row.Country!;
            if (!string.IsNullOrEmpty(row.StateAbbr))  State      = row.StateAbbr!;
            Points     = row.Elo;
        }
        catch { }
    }
}
