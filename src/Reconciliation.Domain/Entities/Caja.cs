namespace Reconciliation.Domain.Entities;

public class Caja
{
    public int IdCaja { get; set; }
    public int IdAgencia { get; set; }
    public int IdMoneda { get; set; }
    public long CodUsuario { get; set; }
    public int NumCaja { get; set; }
    public decimal Maximo { get; set; }
    public decimal Minimo { get; set; }
    public DateTime FecCreaCaja { get; set; }
    public string Estado { get; set; } = "A";
}
