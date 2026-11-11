namespace Reconciliation.Domain.Entities;

public class SaldoCaja
{
    public int IdSaldoCaja { get; set; }
    public int IdCaja { get; set; }
    public string Conciliacion { get; set; } = string.Empty;
    public decimal Saldo { get; set; }
    public DateTime FecCreaSaldo { get; set; }
}
