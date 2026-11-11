namespace Reconciliation.Application.DTOs;

// ═══════════════════════════════════════════════════════════════
// Endpoint 3: General Reconciliation
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// General reconciliation report for the Bolivia–Chile remittance corridor.
/// Contains the daily matrix, payment validation, discrepancies, and balance.
/// </summary>
public class RemittanceGeneralReconciliationReport
{
    public DateTime Fecha { get; init; }
    public int Mes { get; init; }
    public int Anio { get; init; }
    public DateTime FechaProcesamiento { get; init; }

    /// <summary>Daily matrix with carry-forward, one row per day, and totals.</summary>
    public GeneralReconciliationMatrix DailyMatrix { get; init; } = new();

    /// <summary>Payment validation (field-level matching).</summary>
    public PaymentValidationReport PaymentValidation { get; init; } = new();

    /// <summary>Cross-database discrepancies.</summary>
    public DiscrepancyReport Discrepancies { get; init; } = new();

    /// <summary>Balance calculation between countries.</summary>
    public BalanceReport Balance { get; init; } = new();
}

// ═══════════════════════════════════════════════════════════════
// Daily Matrix types (new)
// ═══════════════════════════════════════════════════════════════

public class GeneralReconciliationMatrix
{
    /// <summary>Carry-forward from previous month (pending + accumulated balance).</summary>
    public MatrixCarryForwardRow CarryForward { get; init; } = new();

    /// <summary>One row per day in the month up to the report date.</summary>
    public List<MatrixDailyRow> DailyRows { get; init; } = new();

    /// <summary>Totals row summarizing all daily rows (excluding carry-forward).</summary>
    public MatrixTotalsRow Totals { get; init; } = new();
}

public class MatrixCarryForwardRow
{
    /// <summary>CHL→BOL pending count carried from previous month.</summary>
    public int ChlToBolPendingCount { get; init; }
    public decimal ChlToBolPendingAmount { get; init; }

    /// <summary>BOL→CHL pending count carried from previous month.</summary>
    public int BolToChlPendingCount { get; init; }
    public decimal BolToChlPendingAmount { get; init; }

    /// <summary>Accumulated balance from all history before this month.</summary>
    public decimal SaldoAcumulado { get; init; }
}

public class MatrixDailyRow
{
    public DateTime Fecha { get; init; }

    // CHL → BOL direction
    public StatusBreakdown ChlToBolRecibidas { get; init; } = new();
    public StatusBreakdown ChlToBolAnuladas { get; init; } = new();
    public StatusBreakdown ChlToBolPendientes { get; init; } = new();   // Running snapshot
    public StatusBreakdown ChlToBolPagadas { get; init; } = new();

    // BOL → CHL direction
    public StatusBreakdown BolToChlEnviadas { get; init; } = new();
    public StatusBreakdown BolToChlAnuladas { get; init; } = new();
    public StatusBreakdown BolToChlPendientes { get; init; } = new();   // Running snapshot
    public StatusBreakdown BolToChlPagadas { get; init; } = new();

    /// <summary>CHL→BOL pagadas amount - BOL→CHL pagadas amount for this day.</summary>
    public decimal SaldoFavorContra { get; init; }

    /// <summary>Running accumulated balance (carry-forward + sum of daily SaldoFavorContra).</summary>
    public decimal SaldoAcumulado { get; init; }
}

public class MatrixTotalsRow
{
    // CHL → BOL direction
    public StatusBreakdown ChlToBolRecibidas { get; init; } = new();
    public StatusBreakdown ChlToBolAnuladas { get; init; } = new();
    public StatusBreakdown ChlToBolPagadas { get; init; } = new();

    // BOL → CHL direction
    public StatusBreakdown BolToChlEnviadas { get; init; } = new();
    public StatusBreakdown BolToChlAnuladas { get; init; } = new();
    public StatusBreakdown BolToChlPagadas { get; init; } = new();

    public decimal SaldoFavorContraTotal { get; init; }
}

// ═══════════════════════════════════════════════════════════════
// Shared types: StatusBreakdown, Balance, Discrepancy, Payment
// ═══════════════════════════════════════════════════════════════

public class StatusBreakdown
{
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
}

// ── Balance ──────────────────────────────────────────────────

public class BalanceReport
{
    /// <summary>Positive means Bolivia is owed money (favor Bolivia).</summary>
    public decimal BalancePositivo { get; init; }

    /// <summary>Negative means Chile is owed money (favor Chile).</summary>
    public decimal BalanceNegativo { get; init; }

    /// <summary>Net = BalancePositivo - BalanceNegativo.</summary>
    public decimal SaldoNeto { get; init; }

    /// <summary>Accumulated balance entries by date.</summary>
    public List<DailyBalance> SaldoAcumulado { get; init; } = new();
}

public class DailyBalance
{
    public DateTime Fecha { get; init; }
    public decimal ChileToBoliviaAmount { get; init; }
    public decimal BoliviaToChileAmount { get; init; }
    public decimal NetDaily { get; init; }
    public decimal Accumulated { get; init; }
}

// ── Discrepancies ────────────────────────────────────────────

public class DiscrepancyReport
{
    /// <summary>Remittances in Bolivia DB but not found in Chile DB.</summary>
    public List<DiscrepancyEntry> InBoliviaNotInChile { get; init; } = new();

    /// <summary>Remittances in Chile DB but not found in Bolivia DB.</summary>
    public List<DiscrepancyEntry> InChileNotInBolivia { get; init; } = new();

    /// <summary>Remittances without accounting entries.</summary>
    public List<DiscrepancyEntry> MissingAccountingEntries { get; init; } = new();

    /// <summary>Accounting entries that don't match any remittance.</summary>
    public List<OrphanAccountingEntry> OrphanAccountingEntries { get; init; } = new();

    /// <summary>Amount mismatches between Bolivia and Chile.</summary>
    public List<AmountMismatch> AmountMismatches { get; init; } = new();
}

public class DiscrepancyEntry
{
    public int Codigo { get; init; }
    public string Source { get; init; } = "";
    public string Direction { get; init; } = "";
    public decimal Amount { get; init; }
    public string Status { get; init; } = "";
    public DateTime Fecha { get; init; }
}

public class OrphanAccountingEntry
{
    public int IdAsiento { get; init; }
    public DateTime Fecha { get; init; }
    public string Concepto { get; init; } = "";
    public decimal Importe { get; init; }
}

public class AmountMismatch
{
    public int Codigo { get; init; }
    public decimal BoliviaAmount { get; init; }
    public decimal ChileAmount { get; init; }
    public decimal Difference { get; init; }
}

// ── Payment Validation ───────────────────────────────────────

public class PaymentValidationReport
{
    public int TotalRemittances { get; init; }
    public int TotalWithPayment { get; init; }
    public int TotalMissing { get; init; }
    public int TotalDuplicates { get; init; }
    public int TotalFieldErrors { get; init; }
    public int TotalValid { get; init; }
    public List<PaymentValidationEntry> Entries { get; init; } = new();
}

public class PaymentValidationEntry
{
    /// <summary>Direction: "CHL->BOL" or "BOL->CHL".</summary>
    public string Direction { get; init; } = "";
    public int Codigo { get; init; }
    public string Moneda { get; init; } = "";
    public decimal RemittanceEnvio { get; init; }
    public string RemittanceEstadoLabel { get; init; } = "";
    public DateTime RemittanceFecha { get; init; }

    /// <summary>Payment data from pago table (null if missing).</summary>
    public decimal? PagoMonto { get; init; }
    public decimal? PagoTipoCambio { get; init; }
    public decimal? PagoTotal { get; init; }
    public decimal? PagoComisionBob { get; init; }
    public DateTime? PagoFecCrea { get; init; }

    /// <summary>Field-level match results (only meaningful when payment exists).</summary>
    public bool MontoMatch { get; init; }
    public bool TipoCambioMatch { get; init; }
    public bool TotalMatch { get; init; }
    public bool ComisionMatch { get; init; }

    public PaymentValidationStatus Status { get; init; }
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum PaymentValidationStatus
{
    Valid,
    Missing,
    Duplicate,
    FieldMismatch
}
