namespace ChessMAUI.Models;

public enum TransactionType { Credit, Debit }

public class TransactionEntry
{
    public DateTime        Date        { get; set; } = DateTime.Now;
    public string          Description { get; set; } = "";
    public string          Icon        { get; set; } = "";
    public decimal         Amount      { get; set; }   // positivo = entrada, negativo = saída
    public TransactionType Type        => Amount >= 0 ? TransactionType.Credit : TransactionType.Debit;
}
