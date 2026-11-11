namespace Reconciliation.Domain.Interfaces;

public interface IRemittanceRepository
{
    /// <summary>
    /// Remittances from Bolivia's remesa table for a given date range.
    /// Includes JOINs to agencia, moneda, usuario for descriptive fields.
    /// </summary>
    Task<IEnumerable<BoliviaRemittanceRow>> GetRemittancesAsync(DateTime fechaDesde, DateTime fechaHasta);

    /// <summary>
    /// Payments from Bolivia's pago table matching the given remittance codes.
    /// No date restriction — fetches all payments for these codes so that
    /// remittances paid after the report cutoff can be correctly classified as pending.
    /// </summary>
    Task<IEnumerable<BoliviaPaymentRow>> GetPaymentsByCodigosAsync(DateTime fechaDesde, DateTime fechaHasta);

    /// <summary>
    /// Sum of monto for paid BOL→CHL remittances created strictly before the given date.
    /// Used to compute the carry-forward accumulated balance for the General Reconciliation matrix.
    /// </summary>
    Task<decimal> GetHistoricalPaidTotalAsync(DateTime beforeDate);

    /// <summary>
    /// Count and sum of monto for BOL→CHL remittances still in pending status,
    /// created on or before the given date. Used for the carry-forward pending row.
    /// </summary>
    Task<CountAmountResult> GetPendingAsOfDateAsync(DateTime asOfDate);
}

/// <summary>Helper for Dapper mapping of aggregate queries returning count + amount.</summary>
public class CountAmountResult
{
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

// ── Flat rows mapped by Dapper ──────────────────────────────────────

public class BoliviaRemittanceRow
{
    // Identifiers
    public int IdRemesa { get; set; }
    public int Codigo { get; set; }
    public int IdOperacion { get; set; }
    public int IdAgencia { get; set; }
    public string Agencia { get; set; } = "";
    public int IdDestino { get; set; }
    public string Destino { get; set; } = "";
    public long CodUsuario { get; set; }
    public string LoginUsuario { get; set; } = "";
    public long Correlativo { get; set; }

    // Currency
    public int MonRec { get; set; }
    public int MonEnt { get; set; }
    public string MonedaRecAbreviatura { get; set; } = "";
    public string MonedaEntAbreviatura { get; set; } = "";

    // Amounts
    public decimal Monto { get; set; }
    public decimal TipoCambio { get; set; }
    public decimal TotalCambio { get; set; }
    public decimal Comision { get; set; }
    public decimal GastosCorresponsal { get; set; }
    public decimal ComisionGambarte { get; set; }
    public decimal Itf { get; set; }
    public decimal ItfBob { get; set; }
    public decimal Iva { get; set; }
    public decimal IvaBob { get; set; }
    public decimal Total { get; set; }
    public decimal ComisionBob { get; set; }
    public decimal GastosCorresponsalBob { get; set; }
    public decimal ComisionGambarteBob { get; set; }
    public decimal TotalBob { get; set; }
    public decimal TipoCambioClp { get; set; }
    public decimal TotalClp { get; set; }
    public decimal Recibido { get; set; }
    public decimal Cambio { get; set; }

    // Status & dates
    public int Estado { get; set; }
    public DateTime FecCreRemesa { get; set; }
    public DateTime? FecModRemesa { get; set; }

    // Origin/destination names
    public string Origen { get; set; } = "";
}

public class BoliviaPaymentRow
{
    public long IdPago { get; set; }
    public long Codigo { get; set; }
    public int IdOperacion { get; set; }
    public int IdAgencia { get; set; }
    public string Agencia { get; set; } = "";
    public long CodUsuario { get; set; }
    public string LoginUsuario { get; set; } = "";
    public int MonRec { get; set; }
    public string MonedaAbreviatura { get; set; } = "";
    public decimal Monto { get; set; }
    public decimal TipoCambio { get; set; }
    public decimal TotalBob { get; set; }
    public decimal ComisionBob { get; set; }
    public DateTime FecCreaPago { get; set; }
}
