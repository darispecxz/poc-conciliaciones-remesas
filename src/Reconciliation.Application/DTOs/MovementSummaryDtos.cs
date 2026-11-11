namespace Reconciliation.Application.DTOs;

/// <summary>
/// Consolidated movement summary per currency.
/// Maps to Excel Section E1: Resumen de Movimientos de Caja.
/// </summary>
public class MovementSummaryReport
{
    public DateTime Fecha { get; init; }
    public List<CurrencyMovementSummary> Currencies { get; init; } = new();
}

/// <summary>
/// Aggregated movement totals for one currency across all cashiers.
/// Signs are embedded: income is positive, expenses are negative.
/// </summary>
public class CurrencyMovementSummary
{
    public string Moneda { get; init; } = "";
    public decimal AperturaCaja { get; init; }
    public decimal CambioMoneda { get; init; }
    public decimal EnvioGiro { get; init; }
    public decimal PagoGiro { get; init; }
    public decimal EnvioRemesa { get; init; }
    public decimal PagoRemesa { get; init; }
    public decimal Egresos { get; init; }
    public decimal Transferencias { get; init; }
    public decimal FallasCaja { get; init; }
    public decimal CierreCaja { get; init; }
}
