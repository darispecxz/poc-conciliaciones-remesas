namespace Reconciliation.Domain.Interfaces;

public interface IExchangeRateRepository
{
    /// <summary>
    /// Latest official exchange rates (BCB) per currency.
    /// Uses cotizacion_oficial WHERE id_operacion = 1 with MAX(fec_crea_cotizacion_oficial).
    /// </summary>
    Task<IEnumerable<ExchangeRateRow>> GetLatestRatesAsync();
}

public class ExchangeRateRow
{
    public int IdMoneda { get; set; }
    public string MonedaAbreviatura { get; set; } = "";
    public decimal Valor { get; set; }
    public DateTime FechaCotizacion { get; set; }
}
