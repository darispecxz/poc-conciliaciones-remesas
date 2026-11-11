using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.Interfaces;

namespace Reconciliation.Functions.Functions;

public class DailyReconciliationFunction
{
    private readonly ICashierService _cashierService;
    private readonly IVaultService _vaultService;
    private readonly ILogger<DailyReconciliationFunction> _logger;

    public DailyReconciliationFunction(
        ICashierService cashierService,
        IVaultService vaultService,
        ILogger<DailyReconciliationFunction> logger)
    {
        _cashierService = cashierService;
        _vaultService = vaultService;
        _logger = logger;
    }

    /// <summary>
    /// Runs daily at 23:30 UTC. Generates the full reconciliation for today.
    /// Idempotent: safe to re-run (read-only queries, no duplicate writes).
    /// </summary>
    //[Function("DailyReconciliation")]
    public async Task RunAsync([TimerTrigger("0 30 23 * * *")] TimerInfo timerInfo)
    {
        var fecha = DateTime.UtcNow.Date;

        _logger.LogInformation("[DailyReconciliation] Starting for {Fecha}", fecha.ToString("yyyy-MM-dd"));

        try
        {
            // Run all report sections in parallel
            var cashierTask = _cashierService.GetReconciliationAsync(fecha);
            var vaultTask = _vaultService.GetReconciliationAsync(fecha);
            var availabilityTask = _vaultService.GetAvailabilityAsync(fecha);
            var summaryTask = _cashierService.GetDailySummaryAsync(fecha);
            var closingsTask = _cashierService.GetAgencyClosingsAsync(fecha);
            var movementsTask = _cashierService.GetMovementSummaryAsync(fecha);

            await Task.WhenAll(cashierTask, vaultTask, availabilityTask, summaryTask, closingsTask, movementsTask);

            var cashierReport = cashierTask.Result;
            var vaultReport = vaultTask.Result;
            var availability = availabilityTask.Result;
            var summary = summaryTask.Result;
            var closings = closingsTask.Result;
            var movements = movementsTask.Result;

            // ── Structured summary log ──────────────────────────
            _logger.LogInformation(
                "[DailyReconciliation] {Fecha} completed | " +
                "Cashiers={CashierCount} Discrepancies={Discrepancies} | " +
                "Agencies={AgencyCount} Bovedas={BovedaCount} | " +
                "VaultTotalBOB={VaultTotal:N2} | Currencies={CurrencyCount}",
                fecha.ToString("yyyy-MM-dd"),
                cashierReport.Cashiers.Count,
                cashierReport.TotalDiscrepancies,
                vaultReport.Agencies.Count,
                vaultReport.Agencies.Sum(a => a.Movimientos.Count),
                availability.TotalSaldoBob,
                summary.Currencies.Count);

            // ── Log each discrepancy for alerting ───────────────
            if (cashierReport.TotalDiscrepancies > 0)
            {
                _logger.LogWarning(
                    "[DailyReconciliation] {Fecha}: {Count} DISCREPANCIES detected — review required",
                    fecha.ToString("yyyy-MM-dd"), cashierReport.TotalDiscrepancies);
            }

            // ── Agency closings summary ─────────────────────────
            foreach (var agency in closings.Agencies)
            {
                var bobClosing = agency.CurrencyClosings.GetValueOrDefault("BOB");
                _logger.LogInformation(
                    "[AgencyClosing] {Agencia}: BOB={BobClosing:N2}, {CurrencyCount} currencies",
                    agency.Agencia, bobClosing, agency.CurrencyClosings.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[DailyReconciliation] FAILED for {Fecha}", fecha.ToString("yyyy-MM-dd"));
            throw;
        }
    }
}
