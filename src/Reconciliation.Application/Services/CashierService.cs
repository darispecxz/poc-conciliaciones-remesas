using Microsoft.Extensions.Logging;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;
using Reconciliation.Domain.Interfaces;

namespace Reconciliation.Application.Services;

public class CashierService : ICashierService
{
    private readonly ICashierRepository _cashierRepo;
    private readonly IVaultRepository _vaultRepo;
    private readonly ILogger<CashierService> _logger;

    public CashierService(
        ICashierRepository cashierRepo,
        IVaultRepository vaultRepo,
        ILogger<CashierService> logger)
    {
        _cashierRepo = cashierRepo;
        _vaultRepo = vaultRepo;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    // Section D — Full per-cashier reconciliation
    // ═══════════════════════════════════════════════════════════════

    public async Task<CashierReconciliationReport> GetReconciliationAsync(DateTime fecha, int? idAgencia = null)
    {
        var rowsTask = _cashierRepo.GetReconciliationAsync(fecha, idAgencia);
        var fallasTask = _cashierRepo.GetFallasCajaAsync(fecha, idAgencia);
        await Task.WhenAll(rowsTask, fallasTask);

        var rows = rowsTask.Result;
        var fallas = fallasTask.Result;

        // Index fallas by caja for O(1) lookup
        var fallasMap = new Dictionary<int, FallaCajaRow>();
        foreach (var f in fallas)
            fallasMap.TryAdd(f.IdCaja, f);

        // Group by cashier (usuario): one user may have multiple cajas (one per currency)
        var cashiers = rows
            .GroupBy(r => (r.LoginUsuario, r.IdAgencia, r.Agencia))
            .Select(g => MapCashier(g.Key, g.ToList(), fallasMap))
            .OrderBy(c => c.Agencia)
            .ThenBy(c => c.Cajero)
            .ToList();

        var totalDiscrepancies = cashiers
            .SelectMany(c => c.Currencies)
            .Count(c => c.Diferencia != 0);

        if (totalDiscrepancies > 0)
        {
            _logger.LogWarning(
                "[Reconciliation] {Fecha}: {DiscrepancyCount} discrepancies across {CashierCount} cashiers",
                fecha.ToString("yyyy-MM-dd"), totalDiscrepancies, cashiers.Count);
        }
        else
        {
            _logger.LogInformation(
                "[Reconciliation] {Fecha}: All {CashierCount} cashiers balanced",
                fecha.ToString("yyyy-MM-dd"), cashiers.Count);
        }

        return new CashierReconciliationReport
        {
            Fecha = fecha,
            Cashiers = cashiers,
            TotalDiscrepancies = totalDiscrepancies
        };
    }

    private CashierBreakdown MapCashier(
        (string LoginUsuario, int IdAgencia, string Agencia) key,
        List<CashierReconciliationRow> rows,
        Dictionary<int, FallaCajaRow> fallasMap)
    {
        var currencies = new List<CurrencyReconciliation>(rows.Count);
        var fallasRegistradas = new Dictionary<int, decimal>();

        foreach (var row in rows.OrderBy(r => r.MonedaAbreviatura))
        {
            var (totalIngreso, totalEgreso) = ComputeTotals(row);
            var theoreticalBalance = totalIngreso - Math.Abs(totalEgreso);
            var diferencia = row.CierreCaja - theoreticalBalance;

            if (diferencia != 0)
            {
                _logger.LogWarning(
                    "[Discrepancy] Cajero={Cajero} Caja={NumCaja} {Moneda}: " +
                    "Theoretical={Theoretical:N4} Cierre={Cierre:N4} Diff={Diff:N4}",
                    key.LoginUsuario, row.NumCaja, row.MonedaAbreviatura,
                    theoreticalBalance, row.CierreCaja, diferencia);
            }

            if (fallasMap.TryGetValue(row.IdCaja, out var falla))
                fallasRegistradas[row.IdCaja] = falla.Diferencia;

            currencies.Add(new CurrencyReconciliation
            {
                IdCaja = row.IdCaja,
                NumCaja = row.NumCaja,
                Moneda = row.MonedaAbreviatura,
                TipoCambioOficial = row.TipoCambioOficial,

                AperturaCaja = row.AperturaCaja,
                CambioMonedaIngreso = new TransactionEntry(row.CambioMonedaIngresoCount, row.CambioMonedaIngreso),
                EnvioGiro = new TransactionEntry(row.EnvioGiroCount, row.EnvioGiro),
                EnvioRemesa = new TransactionEntry(row.EnvioRemesaCount, row.EnvioRemesa),
                TraspasoDeCaja = new TransactionEntry(row.TraspasoDeCajaCount, row.TraspasoDeCaja),
                CobroCheque = new TransactionEntry(row.CobroChequeCount, row.CobroCheque),
                IngresoControlado = new TransactionEntry(row.IngresoControladoCount, row.IngresoControlado),
                TransferenciaBovedaIngreso = new TransactionEntry(row.TransferenciaBovedaIngresoCount, row.EntradaTransferenciaBoveda),

                CambioMonedaEgreso = new TransactionEntry(row.CambioMonedaEgresoCount, row.CambioMonedaEgreso),
                PagoGiro = new TransactionEntry(row.PagoGiroCount, row.PagoGiro),
                DevolucionGiro = row.DevolucionGiro,
                PagoRemesa = new TransactionEntry(row.PagoRemesaCount, row.PagoRemesa),
                DevolucionRemesa = row.DevolucionRemesa,
                TraspasoACaja = new TransactionEntry(row.TraspasoACajaCount, row.TraspasoACaja),
                DepositoBancario = new TransactionEntry(row.DepositoBancarioCount, row.DepositoBancario),
                EgresoControlado = new TransactionEntry(row.EgresoControladoCount, row.EgresoControlado),
                TransferenciaBovedaEgreso = new TransactionEntry(row.TransferenciaBovedaEgresoCount, row.SalidaTransferenciaBoveda),

                TotalIngreso = totalIngreso,
                TotalEgreso = totalEgreso,
                TotalSaldo = theoreticalBalance,
                CierreCaja = row.CierreCaja,
                Diferencia = diferencia
            });
        }

        return new CashierBreakdown
        {
            Cajero = key.LoginUsuario,
            IdAgencia = key.IdAgencia,
            Agencia = key.Agencia,
            Currencies = currencies,
            FallasRegistradas = fallasRegistradas
        };
    }

    /// <summary>
    /// Core business rule: TheoreticalBalance = TotalIngreso - TotalEgreso.
    /// Matches the formula from Excel Section D column 55.
    /// </summary>
    private static (decimal TotalIngreso, decimal TotalEgreso) ComputeTotals(CashierReconciliationRow row)
    {
        var totalIngreso =
            row.AperturaCaja
            + row.CambioMonedaIngreso
            + row.EnvioGiro
            + row.EnvioRemesa
            + row.EntradaTransferenciaBoveda
            + row.TraspasoDeCaja
            + row.CobroCheque
            + row.IngresoControlado;

        var totalEgreso =
            row.CambioMonedaEgreso
            + row.PagoGiro
            + row.DevolucionGiro
            + row.PagoRemesa
            + row.DevolucionRemesa
            + row.SalidaTransferenciaBoveda
            + row.TraspasoACaja
            + row.DepositoBancario
            + row.EgresoControlado;

        return (totalIngreso, totalEgreso);
    }

    // ═══════════════════════════════════════════════════════════════
    // Section E1 — Consolidated movement summary per currency
    // ═══════════════════════════════════════════════════════════════

    public async Task<MovementSummaryReport> GetMovementSummaryAsync(DateTime fecha)
    {
        var rowsTask = _cashierRepo.GetReconciliationAsync(fecha);
        var fallasTask = _cashierRepo.GetFallasCajaAsync(fecha);
        await Task.WhenAll(rowsTask, fallasTask);

        var rows = rowsTask.Result;
        var fallas = fallasTask.Result;

        var fallasByCaja = fallas.ToDictionary(f => f.IdCaja, f => f.Diferencia);

        var currencies = rows
            .GroupBy(r => r.MonedaAbreviatura)
            .Select(g =>
            {
                var items = g.ToList();
                return new CurrencyMovementSummary
                {
                    Moneda = g.Key,
                    AperturaCaja = items.Sum(r => r.AperturaCaja),
                    CambioMoneda = items.Sum(r => r.CambioMonedaIngreso - r.CambioMonedaEgreso),
                    EnvioGiro = items.Sum(r => r.EnvioGiro),
                    PagoGiro = items.Sum(r => -(r.PagoGiro + r.DevolucionGiro)),
                    EnvioRemesa = items.Sum(r => r.EnvioRemesa),
                    PagoRemesa = items.Sum(r => -(r.PagoRemesa + r.DevolucionRemesa)),
                    Egresos = items.Sum(r => -(r.DepositoBancario + r.EgresoControlado) + r.IngresoControlado),
                    Transferencias = items.Sum(r => r.EntradaTransferenciaBoveda - r.SalidaTransferenciaBoveda
                                                   + r.TraspasoDeCaja - r.TraspasoACaja + r.CobroCheque),
                    FallasCaja = items.Sum(r => fallasByCaja.GetValueOrDefault(r.IdCaja)),
                    CierreCaja = items.Sum(r => r.CierreCaja)
                };
            })
            .OrderBy(c => c.Moneda)
            .ToList();

        return new MovementSummaryReport { Fecha = fecha, Currencies = currencies };
    }

    // ═══════════════════════════════════════════════════════════════
    // Section E2 — Agency closing balances matrix
    // ═══════════════════════════════════════════════════════════════

    public async Task<AgencyClosingReport> GetAgencyClosingsAsync(DateTime fecha)
    {
        var rows = await _cashierRepo.GetAgencyClosingsAsync(fecha);
        var rowList = rows.ToList();

        var agencies = rowList
            .GroupBy(r => (r.IdAgencia, r.Agencia))
            .Select(g => new AgencyClosingEntry
            {
                IdAgencia = g.Key.IdAgencia,
                Agencia = g.Key.Agencia,
                CurrencyClosings = g.ToDictionary(r => r.MonedaAbreviatura, r => r.CierreCaja)
            })
            .OrderBy(a => a.Agencia)
            .ToList();

        // Totals per currency across all agencies
        var totals = rowList
            .GroupBy(r => r.MonedaAbreviatura)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.CierreCaja));

        return new AgencyClosingReport
        {
            Fecha = fecha,
            Agencies = agencies,
            Totals = totals
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Section C — Daily summary (caja + boveda totals per currency)
    // ═══════════════════════════════════════════════════════════════

    public async Task<DailySummaryReport> GetDailySummaryAsync(DateTime fecha)
    {
        var cajaTotalsTask = _cashierRepo.GetCurrencyTotalsAsync(fecha);
        var bovedaTotalsTask = _vaultRepo.GetCurrencyTotalsAsync(fecha);
        await Task.WhenAll(cajaTotalsTask, bovedaTotalsTask);

        var cajaTotals = cajaTotalsTask.Result.ToDictionary(r => r.MonedaAbreviatura);
        var bovedaTotals = bovedaTotalsTask.Result.ToDictionary(r => r.MonedaAbreviatura);

        var allCurrencies = cajaTotals.Keys.Union(bovedaTotals.Keys).OrderBy(c => c);
        var currencies = new List<CurrencyTotal>();

        foreach (var moneda in allCurrencies)
        {
            cajaTotals.TryGetValue(moneda, out var caja);
            bovedaTotals.TryGetValue(moneda, out var boveda);

            var totalCajas = caja?.TotalCajas ?? 0;
            var totalBoveda = boveda?.TotalBoveda - Math.Abs(totalCajas) ?? 0;
            var tcOficial = caja?.TipoCambioOficial ?? boveda?.TipoCambioOficial ?? 1;
            var totalCajasBoveda = totalCajas + totalBoveda;
            var monedasEnBob = totalCajasBoveda * tcOficial;

            currencies.Add(new CurrencyTotal
            {
                Moneda = moneda,
                TotalCajas = totalCajas,
                TotalBoveda = totalBoveda,
                TotalCajasBoveda = totalCajasBoveda,
                TipoCambioOficial = tcOficial,
                MonedasEnBob = monedasEnBob,
                Contabilidad = null,  // External source — not in CGR database
                Diferencia = null
            });
        }

        return new DailySummaryReport
        {
            Fecha = fecha,
            Currencies = currencies,
            GrandTotalBob = currencies.Sum(c => c.MonedasEnBob)
        };
    }
}
