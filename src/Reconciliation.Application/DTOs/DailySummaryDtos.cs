namespace Reconciliation.Application.DTOs;

/// <summary>
/// Daily reconciliation summary: system totals vs accounting.
/// Maps to Excel Section C: Cuadre Diario de Cajas.
/// </summary>
public class DailySummaryReport
{
    public DateTime Fecha { get; init; }
    public List<CurrencyTotal> Currencies { get; init; } = new();
    public decimal GrandTotalBob { get; init; }
}

public class CurrencyTotal
{
    public string Moneda { get; init; } = "";
    public decimal TotalCajas { get; init; }
    public decimal TotalBoveda { get; init; }

    /// <summary>TotalCajas + TotalBoveda</summary>
    public decimal TotalCajasBoveda { get; init; }

    public decimal TipoCambioOficial { get; init; }

    /// <summary>TotalCajasBoveda × TipoCambioOficial</summary>
    public decimal MonedasEnBob { get; init; }

    /// <summary>
    /// External accounting value. Null when not provided.
    /// Source: external system (not in CGR database).
    /// </summary>
    public decimal? Contabilidad { get; init; }

    /// <summary>MonedasEnBob - Contabilidad (null when Contabilidad is not available)</summary>
    public decimal? Diferencia { get; init; }
}
