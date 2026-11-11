namespace Reconciliation.Application.DTOs;

/// <summary>
/// Vault availability report with exchange rate conversion.
/// Maps to Excel Section B: Reporte de Disponibilidad en Bóveda.
/// </summary>
public class VaultAvailabilityReport
{
    public DateTime Fecha { get; init; }
    public List<VaultAvailabilityEntry> Entries { get; init; } = new();
    public decimal TotalSaldoBob { get; init; }
}

public class VaultAvailabilityEntry
{
    public int IdAgencia { get; init; }
    public string Agencia { get; init; } = "";
    public string Moneda { get; init; } = "";
    public decimal Saldo { get; init; }
    public decimal TipoCambioOficial { get; init; }

    /// <summary>Saldo × TipoCambioOficial</summary>
    public decimal SaldoBob { get; init; }

    public DateTime? FechaCotizacion { get; init; }
}
