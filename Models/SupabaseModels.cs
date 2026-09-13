using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ChessMAUI.Models;

[Table("profiles")]
public class SupabaseProfile : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = "";

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("elo")]
    public int Elo { get; set; } = 1200;

    [Column("week_elo")]
    public int WeekElo { get; set; } = 0;

    [Column("wins")]
    public int Wins { get; set; } = 0;

    [Column("losses")]
    public int Losses { get; set; } = 0;

    [Column("tournaments_won")]
    public int TournamentsWon { get; set; } = 0;

    [Column("avatar")]
    public string Avatar { get; set; } = "♟";

    [Column("avatar_path")]
    public string? AvatarPath { get; set; }

    [Column("country")]
    public string? Country { get; set; }

    [Column("state_abbr")]
    public string? StateAbbr { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Só é lido, nunca escrito pelo cliente (ver ProfileService.SyncToSupabaseAsync) — a
    // única forma de ficar true é o próprio desenvolvedor mudar direto no painel do Supabase.
    [Column("is_admin")]
    public bool IsAdmin { get; set; } = false;
}

[Table("challenges")]
public class SupabaseChallenge : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = "";

    [Column("code")]
    public string Code { get; set; } = "";

    [Column("challenger_id")]
    public string? ChallengerId { get; set; }

    [Column("challenger_name")]
    public string ChallengerName { get; set; } = "";

    [Column("time_minutes")]
    public int TimeMinutes { get; set; } = 0;

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    // Preenchido pela função accept_challenge() assim que alguém aceita o desafio — o
    // criador descobre isso assinando a própria linha por Realtime (ver FriendInvitePage).
    [Column("game_id")]
    public string? GameId { get; set; }
}

[Table("games")]
public class SupabaseGame : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = "";

    [Column("white_id")]
    public string WhiteId { get; set; } = "";

    [Column("black_id")]
    public string BlackId { get; set; } = "";

    [Column("time_minutes")]
    public int TimeMinutes { get; set; }

    // Lances em UCI (ex. "e2e4") — o gatilho validate_game_move só aceita anexar
    // exatamente 1 lance novo por UPDATE, mantendo o histórico anterior intacto.
    [Column("moves")]
    public List<string> Moves { get; set; } = new();

    [Column("turn")]
    public string Turn { get; set; } = "white";

    [Column("white_remaining_ms")]
    public long WhiteRemainingMs { get; set; }

    [Column("black_remaining_ms")]
    public long BlackRemainingMs { get; set; }

    [Column("last_move_at")]
    public DateTime LastMoveAt { get; set; }

    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("result")]
    public string? Result { get; set; }

    [Column("end_reason")]
    public string? EndReason { get; set; }

    [Column("draw_offered_by")]
    public string? DrawOfferedBy { get; set; }

    [Column("white_claim")]
    public string? WhiteClaim { get; set; }

    [Column("black_claim")]
    public string? BlackClaim { get; set; }
}
