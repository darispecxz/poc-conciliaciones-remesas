using Reconciliation.Application.DTOs;

namespace Reconciliation.Application.Interfaces;

public interface IRemittanceService
{
    /// <summary>
    /// Endpoint 1: Daily operations — sent/cancelled/paid for the specific date,
    /// pending accumulated across the month.
    /// </summary>
    Task<RemittanceDailyOperationsReport> GetDailyOperationsAsync(DateTime fecha);

    /// <summary>
    /// Endpoint 2: Accounting validation — full month of remittance vs co_asientos validation.
    /// </summary>
    Task<RemittanceAccountingValidationReport> GetAccountingValidationAsync(DateTime fecha);

    /// <summary>
    /// Endpoint 3: General reconciliation — daily matrix, payment validation,
    /// discrepancies, and balance for the full month.
    /// </summary>
    Task<RemittanceGeneralReconciliationReport> GetGeneralReconciliationAsync(DateTime fecha);
}
