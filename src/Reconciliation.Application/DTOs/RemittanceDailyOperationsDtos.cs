namespace Reconciliation.Application.DTOs;

// ═══════════════════════════════════════════════════════════════
// Endpoint 1: Daily Operations
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Daily operations report for the Bolivia–Chile remittance corridor.
/// Contains a summary, accumulated pending, and date-specific sent/paid/cancelled.
/// </summary>
public class RemittanceDailyOperationsReport
{
    public DateTime Fecha { get; init; }
    public int Mes { get; init; }
    public int Anio { get; init; }
    public DateTime FechaProcesamiento { get; init; }

    /// <summary>High-level totals for the day.</summary>
    public DailyOperationsSummary Summary { get; init; } = new();

    /// <summary>Remittances sent today (both directions, non-cancelled).</summary>
    public List<DirectionalSummary> SentToday { get; init; } = new();

    /// <summary>Remittances cancelled today (both directions).</summary>
    public List<DirectionalSummary> CancelledToday { get; init; } = new();

    /// <summary>Remittances paid today (both directions, by payment date).</summary>
    public List<DirectionalSummary> PaidToday { get; init; } = new();

    /// <summary>Accumulated pending in Bolivia (CHL→BOL direction, awaiting payment).</summary>
    public List<PendingSummary> PendingInBolivia { get; init; } = new();

    /// <summary>Accumulated pending in Chile (BOL→CHL direction, awaiting payment).</summary>
    public List<PendingSummary> PendingInChile { get; init; } = new();
}

public class DailyOperationsSummary
{
    public int TotalSentToday { get; init; }
    public decimal TotalSentAmountToday { get; init; }
    public int TotalCancelledToday { get; init; }
    public decimal TotalCancelledAmountToday { get; init; }
    public int TotalPaidToday { get; init; }
    public decimal TotalPaidAmountToday { get; init; }
    public int TotalPendingBolivia { get; init; }
    public decimal TotalPendingAmountBolivia { get; init; }
    public int TotalPendingChile { get; init; }
    public decimal TotalPendingAmountChile { get; init; }
}

// ═══════════════════════════════════════════════════════════════
// Shared types (used across multiple endpoints)
// ═══════════════════════════════════════════════════════════════

public class DirectionalSummary
{
    public string Direction { get; init; } = "";
    public string Moneda { get; init; } = "";
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = "";
}

public class PendingSummary
{
    public string Moneda { get; init; } = "";
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
}
