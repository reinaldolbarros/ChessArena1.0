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
}
