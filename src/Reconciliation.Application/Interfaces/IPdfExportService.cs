using Reconciliation.Application.DTOs;

namespace Reconciliation.Application.Interfaces;

public interface IPdfExportService
{
    byte[] GenerateCashierReconciliation(CashierReconciliationReport report);
    byte[] GenerateVaultReconciliation(VaultReconciliationReport report);
    byte[] GenerateVaultAvailability(VaultAvailabilityReport report);
    byte[] GenerateDailySummary(DailySummaryReport report);
    byte[] GenerateAgencyClosings(AgencyClosingReport report);
    byte[] GenerateMovementSummary(MovementSummaryReport report);
    byte[] GenerateRemittanceDailyOperations(RemittanceDailyOperationsReport report);
    byte[] GenerateRemittanceAccountingValidation(RemittanceAccountingValidationReport report);
    byte[] GenerateRemittanceGeneralReconciliation(RemittanceGeneralReconciliationReport report);
}
