using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;
using Reconciliation.Domain;
using Reconciliation.Domain.Interfaces;

namespace Reconciliation.Application.Services;

public class GerencialService : IGerencialService
{
    private readonly IGerencialRepository _repo;
    private readonly ILogger<GerencialService> _logger;

    public GerencialService(IGerencialRepository repo, ILogger<GerencialService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<GerencialReport> GetReportAsync(
        DateTime fechaDesde, DateTime fechaHasta, string tipo, int? idAgencia = null)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation(
                "[Gerencial] Building report tipo={Tipo} desde={Desde} hasta={Hasta} depto={Depto}",
                tipo, fechaDesde.ToString("yyyy-MM-dd"), fechaHasta.ToString("yyyy-MM-dd"), idAgencia);

            // ── Previous period for variation ───────────────────────────
            var (prevDesde, prevHasta) = ComputePreviousPeriod(fechaDesde, fechaHasta);

            // ── Parallel data fetch — Bolivia + Accounting ─────────────
            var swDb = Stopwatch.StartNew();

            var agenciesTask = _repo.GetAgenciesAsync();
            var remittancesTask = _repo.GetRemittanceSummaryAsync(fechaDesde, fechaHasta, idAgencia);
            var paidTask = _repo.GetRemittancesPaidAsync(fechaDesde, fechaHasta, idAgencia);
            var exchangeTask = _repo.GetExchangeSummaryAsync(fechaDesde, fechaHasta, idAgencia);
            var alertsTask = _repo.GetAccountingAlertsAsync(fechaDesde, fechaHasta);

            // Previous period (Bolivia only — no alerts for previous period)
            var prevRemittancesTask = _repo.GetRemittanceSummaryAsync(prevDesde, prevHasta, idAgencia);
            var prevExchangeTask = _repo.GetExchangeSummaryAsync(prevDesde, prevHasta, idAgencia);

            await Task.WhenAll(
                agenciesTask, remittancesTask, paidTask, exchangeTask, alertsTask,
                prevRemittancesTask, prevExchangeTask);

            swDb.Stop();
            _logger.LogInformation("[Gerencial] DB queries completed in {Ms}ms", swDb.ElapsedMilliseconds);

            var agencies = agenciesTask.Result.ToList();
            var remittances = remittancesTask.Result.ToList();
            var paidRemittances = paidTask.Result.ToList();
            var exchanges = exchangeTask.Result.ToList();
            var alerts = alertsTask.Result.ToList();
            var prevRemittances = prevRemittancesTask.Result.ToList();
            var prevExchanges = prevExchangeTask.Result.ToList();

            // ── Build sections ──────────────────────────────────────────
            var remesasResumen = BuildRemesasResumen(remittances, paidRemittances, prevRemittances, agencies);
            var cambioResumen = BuildCambioResumen(exchanges, prevExchanges, agencies);
            var consolidado = BuildConsolidado(remesasResumen, cambioResumen, agencies, alerts, remittances, exchanges);

            sw.Stop();
            _logger.LogInformation("[Gerencial] Report built in {Ms}ms total", sw.ElapsedMilliseconds);

            return new GerencialReport
            {
                Tipo = tipo,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                FechaProcesamiento = DateTime.UtcNow,
                Remesas = remesasResumen,
                CambioMoneda = cambioResumen,
                Consolidado = consolidado
            };
        }
        catch (Exception ex) 
        {
            _logger.LogWarning($"Error {ex.Message}");
        }
        return new GerencialReport();
    }

    // ═══ Remittance section ═════════════════════════════════════════

    private RemesasResumen BuildRemesasResumen(
        List<GerencialRemittanceRow> rows,
        List<GerencialRemittanceRow> paidRows,
        List<GerencialRemittanceRow> prevRows,
        List<GerencialAgencyRow> agencies)
    {
        var totalBob = rows.Where(r => r.MonedaAbreviatura == "BOB").Sum(r => r.TotalBob);
        var totalUsd = rows.Where(r => r.MonedaAbreviatura == "USD").Sum(r => r.MontoTotal);
        var totalTxn = rows.Sum(r => r.Transacciones);
        var totalComm = rows.Sum(r => r.ComisionBob);

        var bobTxn = rows.Where(r => r.MonedaAbreviatura == "BOB").Sum(r => r.Transacciones);
        var usdTxn = rows.Where(r => r.MonedaAbreviatura == "USD").Sum(r => r.Transacciones);

        // Paid totals
        var pagadasBob = paidRows.Where(r => r.MonedaAbreviatura == "BOB").Sum(r => r.TotalBob);
        var pagadasUsd = paidRows.Where(r => r.MonedaAbreviatura == "USD").Sum(r => r.MontoTotal);
        var pagadasTxn = paidRows.Sum(r => r.Transacciones);

        // Previous period totals for variation
        var prevTotalBob = prevRows.Sum(r => r.TotalBob);
        decimal? variacion = prevTotalBob != 0
            ? Math.Round((totalBob - prevTotalBob) / prevTotalBob * 100, 2)
            : null;

        // Payment type breakdown — derived from remittance summary by currency
        // (Bolivia remesa table has no forma_pago; pago table is cross-border only)
        var porTipoPago = BuildPaymentTypeBreakdown(rows);

        // By department
        var porDepto = BuildRemittanceDepartments(rows, agencies);

        // Time series — merge sent and paid by period
        var paidByDate = paidRows
            .GroupBy(r => r.Fecha.ToString("yyyy-MM-dd"))
            .ToDictionary(g => g.Key, g => g.ToList());

        var serie = rows
            .GroupBy(r => r.Fecha.ToString("yyyy-MM-dd"))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var paidInPeriod = paidByDate.TryGetValue(g.Key, out var pr) ? pr : new();
                return new RemesasSerieTemporal
                {
                    Periodo = g.Key,
                    MontoBob = g.Where(r => r.MonedaAbreviatura == "BOB").Sum(r => r.TotalBob),
                    MontoUsd = g.Where(r => r.MonedaAbreviatura == "USD").Sum(r => r.MontoTotal),
                    Transacciones = g.Sum(r => r.Transacciones),
                    PagadasBob = paidInPeriod.Where(r => r.MonedaAbreviatura == "BOB").Sum(r => r.TotalBob),
                    PagadasUsd = paidInPeriod.Where(r => r.MonedaAbreviatura == "USD").Sum(r => r.MontoTotal),
                    PagadasTransacciones = paidInPeriod.Sum(r => r.Transacciones)
                };
            })
            .ToList();

        // Add paid-only dates not in sent series
        var sentDates = serie.Select(s => s.Periodo).ToHashSet();
        var paidOnlyEntries = paidByDate
            .Where(kv => !sentDates.Contains(kv.Key))
            .OrderBy(kv => kv.Key)
            .Select(kv => new RemesasSerieTemporal
            {
                Periodo = kv.Key,
                PagadasBob = kv.Value.Where(r => r.MonedaAbreviatura == "BOB").Sum(r => r.TotalBob),
                PagadasUsd = kv.Value.Where(r => r.MonedaAbreviatura == "USD").Sum(r => r.MontoTotal),
                PagadasTransacciones = kv.Value.Sum(r => r.Transacciones)
            });
        serie.AddRange(paidOnlyEntries);
        serie.Sort((a, b) => string.Compare(a.Periodo, b.Periodo, StringComparison.Ordinal));

        return new RemesasResumen
        {
            TotalBob = totalBob,
            TotalUsd = totalUsd,
            TotalTransacciones = totalTxn,
            TotalComisiones = totalComm,
            TicketPromedioBob = bobTxn > 0 ? Math.Round(totalBob / bobTxn, 2) : 0,
            TicketPromedioUsd = usdTxn > 0 ? Math.Round(totalUsd / usdTxn, 2) : 0,
            VariacionPorcentual = variacion,
            PagadasBob = pagadasBob,
            PagadasUsd = pagadasUsd,
            PagadasTransacciones = pagadasTxn,
            PorTipoPago = porTipoPago,
            PorDepartamento = porDepto,
            SerieTemporal = serie
        };
    }

    private static RemesasPorTipoPago BuildPaymentTypeBreakdown(List<GerencialRemittanceRow> rows)
    {
        // Bolivia remesa table has no forma_pago column — remesa and pago are
        // cross-border tables (Bolivia remittances are paid in Chile and vice-versa).
        // We report total by currency without ventanilla/transferencia split.
        var bobMonto = rows.Where(r => r.MonedaAbreviatura == "BOB").Sum(r => r.TotalBob);
        var bobTxn = rows.Where(r => r.MonedaAbreviatura == "BOB").Sum(r => r.Transacciones);
        var usdMonto = rows.Where(r => r.MonedaAbreviatura == "USD").Sum(r => r.MontoTotal);
        var usdTxn = rows.Where(r => r.MonedaAbreviatura == "USD").Sum(r => r.Transacciones);

        return new RemesasPorTipoPago
        {
            VentanillaBob = new TipoPagoDetalle { Monto = bobMonto, Transacciones = bobTxn },
            VentanillaUsd = new TipoPagoDetalle { Monto = usdMonto, Transacciones = usdTxn },
            TransferenciaBob = new TipoPagoDetalle(),
            TransferenciaUsd = new TipoPagoDetalle()
        };
    }

    private static List<RemesasPorDepartamento> BuildRemittanceDepartments(
        List<GerencialRemittanceRow> rows,
        List<GerencialAgencyRow> agencies)
    {
        var deptAgencies = agencies.GroupBy(a => a.Departamento);

        return deptAgencies.Select(dg =>
        {
            var agencyIds = dg.Select(a => a.IdAgencia).ToHashSet();
            var deptRows = rows.Where(r => agencyIds.Contains(r.IdAgencia)).ToList();

            var txn = deptRows.Sum(r => r.Transacciones);
            return new RemesasPorDepartamento
            {
                Departamento = dg.Key,
                AgenciasTotales = dg.Count(),
                AgenciasActivas = deptRows.Select(r => r.IdAgencia).Distinct().Count(),
                Transacciones = txn,
                MontoBob = deptRows.Where(r => r.MonedaAbreviatura == "BOB").Sum(r => r.TotalBob),
                MontoUsd = deptRows.Where(r => r.MonedaAbreviatura == "USD").Sum(r => r.MontoTotal),
                TransaccionesVentanilla = 0,
                TransaccionesTransferencia = 0,
                Comisiones = deptRows.Sum(r => r.ComisionBob),
                Estado = txn > 0 ? "Activo" : "Sin actividad"
            };
        })
        .OrderByDescending(d => d.Transacciones)
        .ToList();
    }

    // ═══ Exchange section ═══════════════════════════════════════════

    private static CambioResumen BuildCambioResumen(
        List<GerencialExchangeRow> rows,
        List<GerencialExchangeRow> prevRows,
        List<GerencialAgencyRow> agencies)
    {
        var totalOps = rows.Sum(r => r.TotalOperaciones);
        var totalBobRecibido = rows.Sum(r => r.BobRecibido);
        var totalBobEntregado = rows.Sum(r => r.BobEntregado);
        var ganancia = totalBobRecibido - totalBobEntregado;

        // Previous period for variation
        var prevGanancia = prevRows.Sum(r => r.BobRecibido) - prevRows.Sum(r => r.BobEntregado);
        decimal? variacion = prevGanancia != 0
            ? Math.Round((ganancia - prevGanancia) / Math.Abs(prevGanancia) * 100, 2)
            : null;

        // By department
        var deptAgencies = agencies.GroupBy(a => a.Departamento);
        var porDepto = deptAgencies.Select(dg =>
        {
            var agencyIds = dg.Select(a => a.IdAgencia).ToHashSet();
            var deptRows = rows.Where(r => agencyIds.Contains(r.IdAgencia)).ToList();
            var deptBobRec = deptRows.Sum(r => r.BobRecibido);
            var deptBobEnt = deptRows.Sum(r => r.BobEntregado);
            return new CambioPorDepartamento
            {
                Departamento = dg.Key,
                TotalOperaciones = deptRows.Sum(r => r.TotalOperaciones),
                BobRecibido = deptBobRec,
                BobEntregado = deptBobEnt,
                GananciaBob = deptBobRec - deptBobEnt
            };
        })
        .OrderByDescending(d => d.TotalOperaciones)
        .ToList();

        // Time series
        var serie = rows
            .GroupBy(r => r.Fecha.ToString("yyyy-MM-dd"))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var bobRec = g.Sum(r => r.BobRecibido);
                var bobEnt = g.Sum(r => r.BobEntregado);
                return new CambioSerieTemporal
                {
                    Periodo = g.Key,
                    TotalOperaciones = g.Sum(r => r.TotalOperaciones),
                    BobRecibido = bobRec,
                    BobEntregado = bobEnt,
                    GananciaBob = bobRec - bobEnt
                };
            })
            .ToList();

        return new CambioResumen
        {
            TotalOperaciones = totalOps,
            BobRecibido = totalBobRecibido,
            BobEntregado = totalBobEntregado,
            GananciaBob = ganancia,
            VariacionPorcentual = variacion,
            PorDepartamento = porDepto,
            SerieTemporal = serie
        };
    }

    // ═══ Consolidated section ═══════════════════════════════════════

    private static ConsolidadoResumen BuildConsolidado(
        RemesasResumen remesas,
        CambioResumen cambio,
        List<GerencialAgencyRow> agencies,
        List<GerencialAlertRow> alerts,
        List<GerencialRemittanceRow> remittanceRows,
        List<GerencialExchangeRow> exchangeRows)
    {
        var ingresoRemesas = remesas.TotalComisiones;
        var ingresoCambio = cambio.GananciaBob;
        var ingresoTotal = ingresoRemesas + ingresoCambio;

        var activeAgencyIds = remittanceRows.Select(r => r.IdAgencia)
            .Union(exchangeRows.Select(r => r.IdAgencia))
            .Distinct()
            .Count();

        // Previous period variation is already handled in individual sections
        var prevRemIngreso = remesas.VariacionPorcentual.HasValue ? ingresoTotal : 0;
        decimal? variacionTotal = (remesas.VariacionPorcentual.HasValue || cambio.VariacionPorcentual.HasValue)
            ? Math.Round(
                ((remesas.VariacionPorcentual ?? 0) * ingresoRemesas +
                 (cambio.VariacionPorcentual ?? 0) * ingresoCambio) /
                (ingresoTotal != 0 ? ingresoTotal : 1), 2)
            : null;

        // Alerts
        var alertasList = alerts
            .GroupBy(a => a.TipoAlerta)
            .Select(g => new AlertaOperacional
            {
                TipoAlerta = g.Key,
                Cantidad = g.Count(),
                MontoTotal = g.Sum(a => a.Importe)
            })
            .OrderByDescending(a => a.Cantidad)
            .ToList();

        // By department
        var deptAgencies = agencies.GroupBy(a => a.Departamento);
        var porDepto = deptAgencies.Select(dg =>
        {
            var agencyIds = dg.Select(a => a.IdAgencia).ToHashSet();
            var deptRemTxn = remittanceRows.Where(r => agencyIds.Contains(r.IdAgencia)).Sum(r => r.Transacciones);
            var deptExOps = exchangeRows.Where(r => agencyIds.Contains(r.IdAgencia))
                .Sum(r => r.TotalOperaciones);
            var deptRemIncome = remittanceRows.Where(r => agencyIds.Contains(r.IdAgencia)).Sum(r => r.ComisionBob);
            var deptExIncome = exchangeRows.Where(r => agencyIds.Contains(r.IdAgencia))
                .Sum(r => r.BobRecibido - r.BobEntregado);

            return new ConsolidadoPorDepartamento
            {
                Departamento = dg.Key,
                TransaccionesRemesas = deptRemTxn,
                OperacionesCambio = deptExOps,
                TotalOperaciones = deptRemTxn + deptExOps,
                IngresoRemesasBob = deptRemIncome,
                IngresoCambioBob = deptExIncome,
                IngresoTotalBob = deptRemIncome + deptExIncome
            };
        })
        .OrderByDescending(d => d.TotalOperaciones)
        .ToList();

        // Income series — merge remittance and exchange time series
        var remSerieDict = remesas.SerieTemporal.ToDictionary(s => s.Periodo);
        var exSerieDict = cambio.SerieTemporal.ToDictionary(s => s.Periodo);
        var allPeriods = remSerieDict.Keys.Union(exSerieDict.Keys).OrderBy(p => p);

        var serieIngresos = allPeriods.Select(p =>
        {
            var remBob = remSerieDict.TryGetValue(p, out var rv) ? rv.MontoBob : 0;
            var exBob = exSerieDict.TryGetValue(p, out var ev) ? ev.GananciaBob : 0;
            return new SerieIngresos
            {
                Periodo = p,
                RemesasBob = remBob,
                CambioBob = exBob,
                TotalBob = remBob + exBob
            };
        }).ToList();

        return new ConsolidadoResumen
        {
            IngresoRemesasBob = ingresoRemesas,
            IngresoCambioBob = ingresoCambio,
            IngresoTotalBob = ingresoTotal,
            TotalOperaciones = remesas.TotalTransacciones + cambio.TotalOperaciones,
            AgenciasActivas = activeAgencyIds,
            AgenciasTotales = agencies.Count,
            VariacionIngresoTotal = variacionTotal,
            Alertas = alertasList,
            PorDepartamento = porDepto,
            SerieIngresos = serieIngresos
        };
    }

    // ═══ Period helpers ═════════════════════════════════════════════

    private static (DateTime Desde, DateTime Hasta) ComputePreviousPeriod(
        DateTime fechaDesde, DateTime fechaHasta)
    {
        var duration = fechaHasta - fechaDesde;

        if (duration.TotalDays <= 1)
        {
            // Daily: previous day
            return (fechaDesde.AddDays(-1), fechaHasta.AddDays(-1));
        }

        if (fechaDesde.Day == 1 && fechaHasta == new DateTime(fechaDesde.Year, fechaDesde.Month, 1)
                .AddMonths(1).AddDays(-1))
        {
            // Full month: previous month
            var prevMonth = fechaDesde.AddMonths(-1);
            var prevEnd = new DateTime(prevMonth.Year, prevMonth.Month, 1).AddMonths(1).AddDays(-1);
            return (prevMonth, prevEnd);
        }

        // Range: shift back by the same duration
        var days = (int)duration.TotalDays + 1;
        return (fechaDesde.AddDays(-days), fechaDesde.AddDays(-1));
    }
}
