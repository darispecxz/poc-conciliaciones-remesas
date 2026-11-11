namespace Reconciliation.Domain.Interfaces;

public interface IGerencialRepository
{
    // ── From Bolivia DB ─���────────────────────────────────────────

    /// <summary>All agencies with their department.</summary>
    Task<IEnumerable<GerencialAgencyRow>> GetAgenciesAsync();

    /// <summary>
    /// Remittance aggregation by day × agency × currency.
    /// Filters by id_operacion IN (4, 16) and estado NOT anulado.
    /// </summary>
    Task<IEnumerable<GerencialRemittanceRow>> GetRemittanceSummaryAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? idAgencia);

    /// <summary>
    /// Paid remittances (estado=1) by modification date × agency × currency.
    /// </summary>
    Task<IEnumerable<GerencialRemittanceRow>> GetRemittancesPaidAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? idAgencia);

    /// <summary>
    /// Exchange (cambio) aggregation by day × agency — all currencies.
    /// Tracks BOB received/delivered for profit calculation.
    /// </summary>
    Task<IEnumerable<GerencialExchangeRow>> GetExchangeSummaryAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? idAgencia);

    // ── From Accounting DB ─────────────��─────────────────────────

    /// <summary>
    /// Operational alerts from co_asientos:
    /// flagged, large, AML, reversed, large-exchange transactions.
    /// </summary>
    Task<IEnumerable<GerencialAlertRow>> GetAccountingAlertsAsync(
        DateTime fechaDesde, DateTime fechaHasta);
}
