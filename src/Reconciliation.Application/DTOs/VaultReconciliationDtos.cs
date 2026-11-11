namespace Reconciliation.Application.DTOs;

/// <summary>
/// Vault reconciliation report, grouped by agency.
/// </summary>
public class VaultReconciliationReport
{
    public DateTime Fecha { get; init; }
    public List<AgencyVaultBreakdown> Agencies { get; init; } = new();
}

public class AgencyVaultBreakdown
{
    public int IdAgencia { get; init; }
    public string Agencia { get; init; } = "";
    public List<VaultMovement> Movimientos { get; init; } = new();
}

/// <summary>
/// Movement breakdown for one boveda (one currency) on one date.
/// </summary>
public class VaultMovement
{
    public int IdBoveda { get; init; }
    public int NumBoveda { get; init; }
    public int IdMoneda { get; init; }
    public string Moneda { get; init; } = "";

    public decimal SaldoApertura { get; init; }

    // Egresos
    public decimal SalidaAperturaCaja { get; init; }
    public decimal SalidaTraspasoBoveda { get; init; }
    public decimal SalidaTraspasoCaja { get; init; }
    public decimal TotalEgresos { get; init; }

    // Ingresos
    public decimal EntradaCierreCaja { get; init; }
    public decimal EntradaTraspasoBoveda { get; init; }
    public decimal EntradaTraspasoCasa { get; init; }
    public decimal TotalIngresos { get; init; }

    public decimal SaldoFinal { get; init; }
}
