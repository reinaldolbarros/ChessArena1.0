namespace ChessMAUI.Models;

/// <summary>Registro histórico de um torneio disputado.</summary>
public class TournamentRecord
{
    public DateTime Date          { get; set; } = DateTime.Now;
    public int      Size          { get; set; }
    public decimal  BuyIn         { get; set; }
    public decimal  Prize         { get; set; }
    public int      Position      { get; set; }
    public string   Result        => Position == 1 ? "🏆 Campeão"
                                   : Prize > 0     ? $"💰 {Position}º lugar"
                                   : $"❌ {Position}º lugar";
    public string   PrizeText     => Prize > 0 ? $"+$ {Prize:N0}" : "-$ " + $"{BuyIn:N0}";
    public Color    PrizeColor    => Prize > BuyIn ? Color.FromArgb("#4CAF50")
                                   : Prize > 0     ? Color.FromArgb("#FFD700")
                                   : Color.FromArgb("#FF5252");
}
