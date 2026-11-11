namespace Reconciliation.Domain.Entities;

public class SaldoBoveda
{
    public int IdSaldoBoveda { get; set; }
    public int IdBoveda { get; set; }
    public string Conciliacion { get; set; } = string.Empty;
    public decimal Saldo { get; set; }
    public DateTime FecCreaSaldo { get; set; }
}
