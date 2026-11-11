namespace Reconciliation.Application.DTOs;

/// <summary>
/// Agency closing balances matrix: agency rows × currency columns.
/// Maps to Excel Section E2.
/// </summary>
public class AgencyClosingReport
{
    public DateTime Fecha { get; init; }
    public List<AgencyClosingEntry> Agencies { get; init; } = new();
    public Dictionary<string, decimal> Totals { get; init; } = new();
}

public class AgencyClosingEntry
{
    public int IdAgencia { get; init; }
    public string Agencia { get; init; } = "";

    /// <summary>Key = currency abbreviation (e.g., "BOB"), Value = cierre amount.</summary>
    public Dictionary<string, decimal> CurrencyClosings { get; init; } = new();
}
