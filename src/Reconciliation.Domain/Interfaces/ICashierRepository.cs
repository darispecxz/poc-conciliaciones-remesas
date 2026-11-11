namespace Reconciliation.Domain.Interfaces;

public interface ICashierRepository
{
    /// <summary>
    /// Per-caja reconciliation with all transaction categories + counts.
    /// One row per caja (= one user + one currency) per date.
    /// </summary>
    Task<IEnumerable<CashierReconciliationRow>> GetReconciliationAsync(DateTime fecha, int? idAgencia = null);

    /// <summary>Known discrepancies registered for the date.</summary>
    Task<IEnumerable<FallaCajaRow>> GetFallasCajaAsync(DateTime fecha, int? idAgencia = null);

    /// <summary>Aggregated caja totals per currency (for Section C daily summary).</summary>
    Task<IEnumerable<CurrencyTotalRow>> GetCurrencyTotalsAsync(DateTime fecha);

    /// <summary>Cierre amounts grouped by agency × currency (for Section E2).</summary>
    Task<IEnumerable<AgencyClosingRow>> GetAgencyClosingsAsync(DateTime fecha);

    /// <summary>Idempotency check: whether a cierre record exists for this caja on this date.</summary>
    Task<bool> HasClosingRecordAsync(DateTime fecha, int idCaja);
}

// ── Flat rows mapped by Dapper ──────────────────────────────────────

public class CashierReconciliationRow
{
    // Identifiers
    public int IdCaja { get; set; }
    public int NumCaja { get; set; }
    public string LoginUsuario { get; set; } = "";
    public int IdAgencia { get; set; }
    public string Agencia { get; set; } = "";
    public string MonedaAbreviatura { get; set; } = "";
    public decimal TipoCambioOficial { get; set; }

    // ── Ingresos (amounts) ──
    public decimal AperturaCaja { get; set; }
    public decimal CambioMonedaIngreso { get; set; }
    public decimal EnvioGiro { get; set; }
    public decimal EnvioRemesa { get; set; }
    public decimal EntradaTransferenciaBoveda { get; set; }
    public decimal TraspasoDeCaja { get; set; }
    public decimal CobroCheque { get; set; }
    public decimal IngresoControlado { get; set; }

    // ── Egresos (amounts) ──
    public decimal CambioMonedaEgreso { get; set; }
    public decimal PagoGiro { get; set; }
    public decimal DevolucionGiro { get; set; }
    public decimal PagoRemesa { get; set; }
    public decimal DevolucionRemesa { get; set; }
    public decimal SalidaTransferenciaBoveda { get; set; }
    public decimal TraspasoACaja { get; set; }
    public decimal DepositoBancario { get; set; }
    public decimal EgresoControlado { get; set; }

    // ── Cierre ──
    public decimal CierreCaja { get; set; }

    // ── Counts (for # columns in report) ──
    public int CambioMonedaIngresoCount { get; set; }
    public int CambioMonedaEgresoCount { get; set; }
    public int EnvioGiroCount { get; set; }
    public int PagoGiroCount { get; set; }
    public int EnvioRemesaCount { get; set; }
    public int PagoRemesaCount { get; set; }
    public int DevolucionGiroCount { get; set; }
    public int DevolucionRemesaCount { get; set; }
    public int TraspasoACajaCount { get; set; }
    public int TraspasoDeCajaCount { get; set; }
    public int DepositoBancarioCount { get; set; }
    public int CobroChequeCount { get; set; }
    public int EgresoControladoCount { get; set; }
    public int IngresoControladoCount { get; set; }
    public int TransferenciaBovedaIngresoCount { get; set; }
    public int TransferenciaBovedaEgresoCount { get; set; }
}

public class FallaCajaRow
{
    public int IdCaja { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Diferencia { get; set; }
    public string Glosa { get; set; } = "";
    public string Estado { get; set; } = "";
}

public class CurrencyTotalRow
{
    public string MonedaAbreviatura { get; set; } = "";
    public decimal TotalCajas { get; set; }
    public decimal TipoCambioOficial { get; set; }
}

public class AgencyClosingRow
{
    public int IdAgencia { get; set; }
    public string Agencia { get; set; } = "";
    public string MonedaAbreviatura { get; set; } = "";
    public decimal CierreCaja { get; set; }
}
