namespace Reconciliation.Domain.Interfaces;

public interface IAccountingRepository
{
    /// <summary>
    /// Accounting entries from co_asientos matching remittance-related concepto patterns.
    /// Read-only access to the accounting database.
    /// </summary>
    Task<IEnumerable<AccountingEntryRow>> GetRemittanceEntriesAsync(DateTime fechaDesde, DateTime fechaHasta);
}

// ── Flat row mapped by Dapper ──────────────────────────────────────

public class AccountingEntryRow
{
    public int IdAsiento { get; set; }
    public DateTime Fecha { get; set; }
    public string Concepto { get; set; } = "";
    public string CodEjercicio { get; set; } = "";
    public decimal Importe { get; set; }
    public int Numero { get; set; }
    public string Documento { get; set; } = "";
    public string TipoDocumento { get; set; } = "";
}
