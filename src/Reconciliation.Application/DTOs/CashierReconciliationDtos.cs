namespace Reconciliation.Application.DTOs;

/// <summary>
/// Full cashier reconciliation report for a given date.
/// Maps to Excel Section D: Reporte de Arqueos de Caja Consolidado Diario.
/// </summary>
public class CashierReconciliationReport
{
    public DateTime Fecha { get; init; }
    public List<CashierBreakdown> Cashiers { get; init; } = new();
    public int TotalDiscrepancies { get; init; }
}

/// <summary>
/// One cashier (usuario) with all their currency reconciliations.
/// A cashier may handle multiple currencies via separate cajas.
/// </summary>
public class CashierBreakdown
{
    public string Cajero { get; init; } = "";
    public int IdAgencia { get; init; }
    public string Agencia { get; init; } = "";
    public List<CurrencyReconciliation> Currencies { get; init; } = new();

    /// <summary>
    /// Known falla_caja records for this cashier's cajas on this date.
    /// Key = IdCaja, Value = registered difference.
    /// </summary>
    public Dictionary<int, decimal> FallasRegistradas { get; init; } = new();
}

/// <summary>
/// Reconciliation for one caja (one currency) on one date.
/// Contains all 16 transaction categories with counts, theoretical balance, and difference.
/// </summary>
public class CurrencyReconciliation
{
    public int IdCaja { get; init; }
    public int NumCaja { get; init; }
    public string Moneda { get; init; } = "";
    public decimal TipoCambioOficial { get; init; }

    // ── Ingresos ──
    public decimal AperturaCaja { get; init; }
    public TransactionEntry CambioMonedaIngreso { get; init; } = new(0, 0);
    public TransactionEntry EnvioGiro { get; init; } = new(0, 0);
    public TransactionEntry EnvioRemesa { get; init; } = new(0, 0);
    public TransactionEntry TraspasoDeCaja { get; init; } = new(0, 0);
    public TransactionEntry CobroCheque { get; init; } = new(0, 0);
    public TransactionEntry IngresoControlado { get; init; } = new(0, 0);
    public TransactionEntry TransferenciaBovedaIngreso { get; init; } = new(0, 0);

    // ── Egresos ──
    public TransactionEntry CambioMonedaEgreso { get; init; } = new(0, 0);
    public TransactionEntry PagoGiro { get; init; } = new(0, 0);
    public decimal DevolucionGiro { get; init; }
    public TransactionEntry PagoRemesa { get; init; } = new(0, 0);
    public decimal DevolucionRemesa { get; init; }
    public TransactionEntry TraspasoACaja { get; init; } = new(0, 0);
    public TransactionEntry DepositoBancario { get; init; } = new(0, 0);
    public TransactionEntry EgresoControlado { get; init; } = new(0, 0);
    public TransactionEntry TransferenciaBovedaEgreso { get; init; } = new(0, 0);

    // ── Computed totals ──
    public decimal TotalIngreso { get; init; }
    public decimal TotalEgreso { get; init; }

    /// <summary>TheoreticalBalance = TotalIngreso - TotalEgreso</summary>
    public decimal TotalSaldo { get; init; }

    /// <summary>Actual closing balance from saldo_caja WHERE conciliacion = 'C'</summary>
    public decimal CierreCaja { get; init; }

    /// <summary>CierreCaja - TotalSaldo. Zero when balanced.</summary>
    public decimal Diferencia { get; init; }
}
