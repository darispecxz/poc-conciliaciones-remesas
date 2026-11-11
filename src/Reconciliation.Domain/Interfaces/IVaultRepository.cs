namespace Reconciliation.Domain.Interfaces;

public interface IVaultRepository
{
    /// <summary>
    /// Per-boveda reconciliation with movement breakdown.
    /// One row per agency × currency × boveda per date.
    /// </summary>
    Task<IEnumerable<VaultReconciliationRow>> GetReconciliationAsync(DateTime fecha, int? idAgencia = null);

    /// <summary>
    /// Vault balances with exchange rate conversion (Section B).
    /// One row per agency × currency.
    /// </summary>
    Task<IEnumerable<VaultAvailabilityRow>> GetAvailabilityAsync(DateTime fecha);

    /// <summary>Aggregated vault totals per currency (for Section C daily summary).</summary>
    Task<IEnumerable<VaultCurrencyTotalRow>> GetCurrencyTotalsAsync(DateTime fecha);
}

// ── Flat rows ──────────────────────────────────────────────────────

public class VaultReconciliationRow
{
    public int IdAgencia { get; set; }
    public string Agencia { get; set; } = "";
    public int IdMoneda { get; set; }
    public string MonedaAbreviatura { get; set; } = "";
    public int IdBoveda { get; set; }
    public int NumBoveda { get; set; }

    public decimal SaldoApertura { get; set; }

    // Egresos
    public decimal SalidaAperturaCaja { get; set; }
    public decimal SalidaTraspasoBoveda { get; set; }
    public decimal SalidaTraspasoCaja { get; set; }
    public decimal TotalEgresos { get; set; }

    // Ingresos
    public decimal EntradaCierreCaja { get; set; }
    public decimal EntradaTraspasoBoveda { get; set; }
    public decimal EntradaTraspasoCasa { get; set; }
    public decimal TotalIngresos { get; set; }

    public decimal SaldoFinal { get; set; }
}

public class VaultAvailabilityRow
{
    public int IdAgencia { get; set; }
    public string Agencia { get; set; } = "";
    public int IdMoneda { get; set; }
    public string MonedaAbreviatura { get; set; } = "";
    public decimal Saldo { get; set; }
    public decimal TipoCambioOficial { get; set; }
    public decimal SaldoBob { get; set; }
    public DateTime? FechaCotizacion { get; set; }
}

public class VaultCurrencyTotalRow
{
    public string MonedaAbreviatura { get; set; } = "";
    public decimal TotalBoveda { get; set; }
    public decimal TipoCambioOficial { get; set; }
}
