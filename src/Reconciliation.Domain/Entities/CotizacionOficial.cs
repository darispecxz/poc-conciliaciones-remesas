namespace Reconciliation.Domain.Entities;

public class CotizacionOficial
{
    public int IdCotizacionOficial { get; set; }
    public int IdMoneda { get; set; }
    public int IdOperacion { get; set; }
    public decimal Valor { get; set; }
    public DateTime FecCreaCotizacionOficial { get; set; }
}
