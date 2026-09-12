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

    /// <summary>Se o país ainda não foi definido, sugere um valor a partir da região do
    /// aparelho (sem pedir permissão nenhuma). O usuário pode corrigir depois no Perfil.</summary>
    public void EnsureCountryDefault()
    {
        if (!string.IsNullOrEmpty(Country)) return;
        var suggested = GeoData.SuggestCountryFromDevice();
        if (suggested != null) Country = suggested;
    }

    public void RecordWin()  => Wins++;
    public void RecordLoss() => Losses++;

    // ── Supabase sync ─────────────────────────────────────────────────────────

    // Bucket público criado manualmente no painel do Supabase (Storage → New bucket →
    // "avatars", marcado Public) — ver instruções no fim de supabase_schema.sql.
    private const string AvatarBucket = "avatars";

    /// <summary>
    /// Sobe a foto local (AvatarPath aponta pra um arquivo no aparelho) pro Supabase Storage
    /// e troca AvatarPath pela URL pública resultante — sem isso, a foto nunca sai do
    /// aparelho onde foi tirada/escolhida (ver ProfilePage.SaveAvatarPhotoAsync).
    /// Não faz nada se AvatarPath já for uma URL (http/https) ou estiver vazio.
    /// </summary>
    public async Task UploadAvatarIfLocalAsync()
    {
        if (string.IsNullOrEmpty(AvatarPath)) return;
        if (AvatarPath.StartsWith("http://") || AvatarPath.StartsWith("https://")) return;
        if (!File.Exists(AvatarPath)) return;

        var svc = SupabaseService.Instance;
        if (!svc.IsReady) return;
        var userId = AppState.Current.Auth.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            var bytes      = await File.ReadAllBytesAsync(AvatarPath);
            var storageKey = $"{userId}.jpg";
            await svc.Client.Storage.From(AvatarBucket).Upload(
                bytes, storageKey,
                new Supabase.Storage.FileOptions { Upsert = true, ContentType = "image/jpeg" });

            // Cache-bust: sem isso, um app que já baixou a foto antiga não percebe a troca
            // (a URL do bucket é sempre a mesma pro mesmo usuário).
            AvatarPath = $"{svc.Client.Storage.From(AvatarBucket).GetPublicUrl(storageKey)}?v={DateTime.UtcNow.Ticks}";
        }
        catch
        {
            // Sem internet ou bucket ainda não criado — mantém o caminho local; a próxima
            // tentativa de salvar tenta de novo.
        }
    }

    /// <summary>
    /// Limpa o cache local (Preferences) do perfil antes de logar — sem isso, trocar de conta
    /// no mesmo aparelho (ex.: fazer login com uma conta Google diferente) podia deixar o nome/
    /// Elo/avatar da conta anterior "grudados" na tela, porque LoadFromSupabaseAsync só
    /// sobrescreve um campo local quando o servidor manda um valor não vazio para ele.
    /// </summary>
    public void ResetLocal()
    {
        Preferences.Default.Remove(KeyName);
        Preferences.Default.Remove(KeyWins);
        Preferences.Default.Remove(KeyLosses);
        Preferences.Default.Remove(KeyTourneys);
        Preferences.Default.Remove(KeyAvatar);
        Preferences.Default.Remove(KeyAvatarPath);
        Preferences.Default.Remove(KeyPoints);
        Preferences.Default.Remove(KeyWeekPts);
        Preferences.Default.Remove(KeyWeekReset);
        Preferences.Default.Remove(KeyCountry);
        Preferences.Default.Remove(KeyState);
        Preferences.Default.Remove(KeyEloVersion);
        Preferences.Default.Remove(KeyPointTransactions);
    }

    /// <summary>Envia perfil local para a tabela profiles no Supabase (upsert).</summary>
    public async Task SyncToSupabaseAsync()
    {
        await UploadAvatarIfLocalAsync();
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

        // Logo após um login (email ou Google), o CurrentUser do SDK pode levar um instante
        // pra "assentar" — sem essa espera, UserId vinha vazio e o carregamento era abortado
        // silenciosamente, deixando o perfil com nome vazio (e a Lobby forçando a tela de
        // Perfil, achando que era um cadastro incompleto).
        var userId = AppState.Current.Auth.UserId;
        for (int i = 0; i < 15 && string.IsNullOrEmpty(userId); i++)
        {
            await Task.Delay(200);
            userId = AppState.Current.Auth.UserId;
        }
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

            // Modo admin: só existe se o próprio servidor confirmar is_admin=true pra esse
            // usuário (nunca é setado pelo app — ver protect_is_admin_trigger no schema).
            // Sem gesto secreto pra descobrir: o botão só aparece se isso vier true daqui.
            AppState.Current.IsAdminMode = row.IsAdmin;
        }
        catch { }
    }
}
