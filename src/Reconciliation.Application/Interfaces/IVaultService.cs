using Reconciliation.Application.DTOs;

namespace Reconciliation.Application.Interfaces;

public interface IVaultService
{
    /// <summary>Per-boveda reconciliation with movement breakdown.</summary>
    Task<VaultReconciliationReport> GetReconciliationAsync(DateTime fecha, int? idAgencia = null);

    /// <summary>Vault availability with exchange rate conversion (Section B).</summary>
    Task<VaultAvailabilityReport> GetAvailabilityAsync(DateTime fecha);
}
