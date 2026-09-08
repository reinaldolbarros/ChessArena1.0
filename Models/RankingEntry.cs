namespace ChessMAUI.Models;

public class RankingEntry
{
    public string Id         { get; set; } = "";   // id do usuário no Supabase ("" = local/sem conta)
    public int    Position   { get; set; }         // 0 = sem posição real no ranking global
    public string Avatar     { get; set; } = "";
    public string Name       { get; set; } = "";
    public int    Points     { get; set; }
    public int    WeekPoints { get; set; }
    public bool   IsHuman    { get; set; }

    public string PositionLabel => Position switch
    {
        <= 0 => "—",
        1    => "🥇",
        2    => "🥈",
        3    => "🥉",
        _    => $"{Position}º"
    };
    public Color  RowColor      => Position switch
    {
        1 => Color.FromArgb("#2A2500"),
        2 => Color.FromArgb("#1C1C1C"),
        3 => Color.FromArgb("#1A1200"),
        _ => Colors.Transparent
    };
    public Color NameColor => IsHuman ? Color.FromArgb("#FFD700") : Colors.White;
}
