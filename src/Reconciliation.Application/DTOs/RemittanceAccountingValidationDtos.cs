namespace Reconciliation.Application.DTOs;

// ═══════════════════════════════════════════════════════════════
// Endpoint 2: Accounting Validation
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Full-month accounting validation for the Bolivia–Chile remittance corridor.
/// Validates remittance records against co_asientos (accounting entries).
/// </summary>
public class RemittanceAccountingValidationReport
{
    public DateTime Fecha { get; init; }
    public int Mes { get; init; }
    public int Anio { get; init; }
    public DateTime FechaProcesamiento { get; init; }

    /// <summary>Accounting validation results for the full month.</summary>
    public AccountingValidationReport AccountingValidation { get; init; } = new();
}

// ═══════════════════════════════════════════════════════════════
// Accounting Validation types
// ═══════════════════════════════════════════════════════════════

public class AccountingValidationReport
{
    public int TotalValidated { get; init; }
    public int TotalValid { get; init; }
    public int TotalErrors { get; init; }
    public List<AccountingValidationEntry> Entries { get; init; } = new();
}

public class AccountingValidationEntry
{
    public int Codigo { get; init; }
    public string Direction { get; init; } = "";
    public string RemittanceStatus { get; init; } = "";
    public decimal ExpectedAmount { get; init; }
    public decimal? AccountingAmount { get; init; }
    public string ExpectedConcepto { get; init; } = "";
    public string? ActualConcepto { get; init; }
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public AccountingValidationType ValidationType { get; init; }
}

public enum AccountingValidationType
{
    /// <summary>BOL->CHL pending: concepto LIKE '%ENVIO DE%', amount = total_bob + itf_bob.</summary>
    BoliviaToChilePending,

    /// <summary>BOL->CHL paid: concepto LIKE 'PAGO EN DESTINO', amount = pago.total_bob.</summary>
    BoliviaToChilePaid,

    /// <summary>CHL->BOL paid: two entries with same amount (pendientes cobro + pago remesa CGR).</summary>
    ChileToBoliviaPaid
}
