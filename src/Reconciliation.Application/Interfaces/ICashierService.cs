using Reconciliation.Application.DTOs;

namespace Reconciliation.Application.Interfaces;

public interface ICashierService
{
    /// <summary>Full per-cashier reconciliation (Section D).</summary>
    Task<CashierReconciliationReport> GetReconciliationAsync(DateTime fecha, int? idAgencia = null);

    /// <summary>Consolidated movement summary per currency (Section E1).</summary>
    Task<MovementSummaryReport> GetMovementSummaryAsync(DateTime fecha);

    /// <summary>Agency closing balances matrix (Section E2).</summary>
    Task<AgencyClosingReport> GetAgencyClosingsAsync(DateTime fecha);

    /// <summary>Daily summary combining caja + boveda totals (Section C).</summary>
    Task<DailySummaryReport> GetDailySummaryAsync(DateTime fecha);
}
