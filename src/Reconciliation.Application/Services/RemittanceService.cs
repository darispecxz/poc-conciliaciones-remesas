using DocumentFormat.OpenXml.Drawing;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;
using Reconciliation.Domain;
using Reconciliation.Domain.Interfaces;
using Reconciliation.Domain.Utils;

namespace Reconciliation.Application.Services;

public class RemittanceService : IRemittanceService
{
    private readonly IRemittanceRepository _boliviaRepo;
    private readonly IChileRemittanceRepository _chileRepo;
    private readonly IAccountingRepository _accountingRepo;
    private readonly ILogger<RemittanceService> _logger;

    public RemittanceService(
        IRemittanceRepository boliviaRepo,
        IChileRemittanceRepository chileRepo,
        IAccountingRepository accountingRepo,
        ILogger<RemittanceService> logger)
    {
        _boliviaRepo = boliviaRepo;
        _chileRepo = chileRepo;
        _accountingRepo = accountingRepo;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    // Shared data context
    // ═══════════════════════════════════════════════════════════════

    private class RemittanceDataContext
    {
        public required DateTime Fecha;
        public required DateTime MonthStart;
        public required DateTime MonthEnd;

        public required List<BoliviaRemittanceRow> BolToChlRemittances;
        public required List<ChileRemittanceRow> ChlToBolRemittances;
        public required List<AccountingEntryRow> AccountingEntries;

        public required Dictionary<long, List<BoliviaPaymentRow>> BolPaymentsByCodigo;
        public required Dictionary<int, List<ChilePaymentRow>> ChlPaymentsByCodigo;

        public required HashSet<int> BolToChlPendingOverrides;
        public required HashSet<int> ChlToBolPendingOverrides;
    }

    private async Task<RemittanceDataContext> FetchMonthDataAsync(DateTime fecha)
    {
        var monthStart = new DateTime(fecha.Year, fecha.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        // Phase 1: Query remittances + accounting in parallel
        var boliviaTask = _boliviaRepo.GetRemittancesAsync(monthStart, monthEnd);
        var chileTask = _chileRepo.GetRemittancesAsync(monthStart, monthEnd);
        var accountingTask = _accountingRepo.GetRemittanceEntriesAsync(monthStart, monthEnd);
        await Task.WhenAll(boliviaTask, chileTask, accountingTask);

        var bolToChlRemittances = boliviaTask.Result.ToList();
        var chlToBolRemittances = chileTask.Result.ToList();
        var accountingEntries = accountingTask.Result.ToList();

        // Canceled override (Chile): negative codigo → Anulado
        foreach (var rem in chlToBolRemittances.Where(r => r.Codigo < 0))
            rem.Estado = RemittanceStatusChile.Anulado;

        // Phase 2: Query payments by remittance codes (no date restriction)
        var bolCodes = bolToChlRemittances.Select(r => r.Codigo).Distinct().ToList();
        var chlCodes = chlToBolRemittances
            .Where(r => r.Codigo > 0)
            .Select(r => r.Codigo).Distinct().ToList();

        var bolPaymentsTask = _boliviaRepo.GetPaymentsByCodigosAsync(monthStart, monthEnd);
        var chlPaymentsTask = _chileRepo.GetPaymentsByCodigosAsync(monthStart, monthEnd);
        await Task.WhenAll(bolPaymentsTask, chlPaymentsTask);

        var bolPaymentsByCodigo = bolPaymentsTask.Result
            .GroupBy(p => p.Codigo)
            .ToDictionary(g => g.Key, g => g.ToList());

        var chlPaymentsByCodigo = chlPaymentsTask.Result
            .GroupBy(p => p.Codigo)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Pending override: classify paid remittances as pending when
        // the payment date falls after the report cutoff date.
        var bolToChlPendingOverrides = new HashSet<int>();
        foreach (var rem in bolToChlRemittances
            .Where(r => r.Estado == RemittanceStatusBolivia.Pagado))
        {
            if (chlPaymentsByCodigo.TryGetValue(rem.Codigo, out var pmts)
                && pmts.All(p => p.FecCreaPago.Date > monthEnd.Date))
            {
                bolToChlPendingOverrides.Add(rem.Codigo);
                rem.Estado = RemittanceStatusBolivia.Pendiente;
            }
        }

        var chlToBolPendingOverrides = new HashSet<int>();
        foreach (var rem in chlToBolRemittances
            .Where(r => r.Estado == RemittanceStatusChile.Pagado))
        {
            if (bolPaymentsByCodigo.TryGetValue(rem.Codigo, out var pmts)
                && pmts.All(p => p.FecCreaPago.Date > monthEnd.Date))
            {
                chlToBolPendingOverrides.Add(rem.Codigo);
                rem.Estado = RemittanceStatusChile.Pendiente;
            }
        }

        return new RemittanceDataContext
        {
            Fecha = fecha,
            MonthStart = monthStart,
            MonthEnd = monthEnd,
            BolToChlRemittances = bolToChlRemittances,
            ChlToBolRemittances = chlToBolRemittances,
            AccountingEntries = accountingEntries,
            BolPaymentsByCodigo = bolPaymentsByCodigo,
            ChlPaymentsByCodigo = chlPaymentsByCodigo,
            BolToChlPendingOverrides = bolToChlPendingOverrides,
            ChlToBolPendingOverrides = chlToBolPendingOverrides
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Endpoint 1: Daily Operations
    // ═══════════════════════════════════════════════════════════════

    public async Task<RemittanceDailyOperationsReport> GetDailyOperationsAsync(DateTime fecha)
    {
        var ctx = await FetchMonthDataAsync(fecha);

        var todayChlToBol = ctx.ChlToBolRemittances.Where(r => r.FecCreRemesa.Date == fecha.Date).ToList();
        var todayBolToChl = ctx.BolToChlRemittances.Where(r => r.FecCreRemesa.Date == fecha.Date).ToList();

        // Sent today: non-cancelled remittances created today
        var sentToday = todayChlToBol
            .Where(r => r.Estado != RemittanceStatusChile.Anulado)
            .GroupBy(r => r.Moneda)
            .Select(g => new DirectionalSummary
            {
                Direction = "CHL->BOL",
                Moneda = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(r => r.Envio),
                Status = "ENVIADA"
            })
            .Concat(todayBolToChl
                .Where(r => r.Estado != RemittanceStatusBolivia.Anulado)
                .GroupBy(r => r.MonedaRecAbreviatura)
                .Select(g => new DirectionalSummary
                {
                    Direction = "BOL->CHL",
                    Moneda = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(r =>
                        r.MonedaRecAbreviatura == "USD"
                            ? r.Monto * BoliviaConstants.USD
                            : r.Monto
                    ),
                    Status = "ENVIADA"
                }))
            .OrderBy(s => s.Direction).ThenBy(s => s.Moneda)
            .ToList();

        // Cancelled today: cancelled remittances created today
        var cancelledToday = todayChlToBol
            .Where(r => r.Estado == RemittanceStatusChile.Anulado)
            .GroupBy(r => r.Moneda)
            .Select(g => new DirectionalSummary
            {
                Direction = "CHL->BOL",
                Moneda = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(r => r.Envio),
                Status = "ANULADA"
            })
            .Concat(todayBolToChl
                .Where(r => r.Estado == RemittanceStatusBolivia.Anulado)
                .GroupBy(r => r.MonedaRecAbreviatura)
                .Select(g => new DirectionalSummary
                {
                    Direction = "BOL->CHL",
                    Moneda = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(r =>
                        r.MonedaRecAbreviatura == "USD"
                            ? r.Monto * BoliviaConstants.USD
                            : r.Monto
                    ),
                    Status = "ANULADA"
                }))
            .OrderBy(s => s.Direction).ThenBy(s => s.Moneda)
            .ToList();

        // Paid today: remittances with payment date == fecha
        var paidToday = new List<DirectionalSummary>();

        // CHL→BOL: payments in Bolivia pago table made today
        var chlToBolPaidToday = ctx.BolPaymentsByCodigo
            .SelectMany(kvp => kvp.Value)
            .Where(p => p.FecCreaPago.Date == fecha.Date)
            .GroupBy(p => p.MonedaAbreviatura)
            .Select(g => new DirectionalSummary
            {
                Direction = "CHL->BOL",
                Moneda = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(p => p.Monto),
                Status = "PAGADA"
            });
        paidToday.AddRange(chlToBolPaidToday);

        // BOL→CHL: payments in Chile pago table made today
        var bolToChlPaidToday = ctx.ChlPaymentsByCodigo
            .SelectMany(kvp => kvp.Value)
            .Where(p => p.FecCreaPago.Date == fecha.Date)
            .GroupBy(p => "USD") // Chile payments don't carry currency abbreviation; use direction context
            .Select(g => new DirectionalSummary
            {
                Direction = "BOL->CHL",
                Moneda = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(p => p.Monto),
                Status = "PAGADA"
            });
        paidToday.AddRange(bolToChlPaidToday);

        // Pending accumulated (across month)
        var fechaCorte = fecha.Date;
        var pendingInBolivia = ctx.ChlToBolRemittances
            .Where(r =>
                r.FecCreRemesa.Date <= fechaCorte &&
                (
                    r.Estado == RemittanceStatusChile.Pendiente
                     || r.Estado == RemittanceStatusChile.EnProceso ||
                    (r.Estado == RemittanceStatusBolivia.Pagado && r.FecModRemesa?.Date > fechaCorte)
                )
            )
            .GroupBy(r => r.Moneda)
            .Select(g => new PendingSummary
            {
                Moneda = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(r => r.Envio)
            })
            .OrderBy(s => s.Moneda)
            .ToList();

        var pendingInChile = ctx.BolToChlRemittances
            .Where(r =>
                r.FecCreRemesa.Date <= fechaCorte &&
                (
                    r.Estado == RemittanceStatusBolivia.Pendiente ||
                    (r.Estado == RemittanceStatusBolivia.Pagado && r.FecModRemesa?.Date > fechaCorte)
                )
            )
            .GroupBy(r => r.MonedaRecAbreviatura)
            .Select(g => new PendingSummary
            {
                Moneda = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(r =>
                        r.MonedaRecAbreviatura == "USD"
                            ? r.Monto * BoliviaConstants.USD
                            : r.Monto
                    )
            })
            .OrderBy(s => s.Moneda)
            .ToList();

        var summary = new DailyOperationsSummary
        {
            TotalSentToday = sentToday.Sum(s => s.Count),
            TotalSentAmountToday = sentToday.Sum(s => s.TotalAmount),
            TotalCancelledToday = cancelledToday.Sum(s => s.Count),
            TotalCancelledAmountToday = cancelledToday.Sum(s => s.TotalAmount),
            TotalPaidToday = paidToday.Sum(s => s.Count),
            TotalPaidAmountToday = paidToday.Sum(s => s.TotalAmount),
            TotalPendingBolivia = pendingInBolivia.Sum(s => s.Count),
            TotalPendingAmountBolivia = pendingInBolivia.Sum(s => s.TotalAmount),
            TotalPendingChile = pendingInChile.Sum(s => s.Count),
            TotalPendingAmountChile = pendingInChile.Sum(s => s.TotalAmount)
        };

        _logger.LogInformation(
            "[RemittanceDailyOps] {Fecha}: Sent={Sent} Cancelled={Cancelled} Paid={Paid} " +
            "PendingBOL={PendBol} PendingCHL={PendChl}",
            fecha.ToString("yyyy-MM-dd"),
            summary.TotalSentToday, summary.TotalCancelledToday, summary.TotalPaidToday,
            summary.TotalPendingBolivia, summary.TotalPendingChile);

        return new RemittanceDailyOperationsReport
        {
            Fecha = fecha,
            Mes = fecha.Month,
            Anio = fecha.Year,
            FechaProcesamiento = DateTime.UtcNow,
            Summary = summary,
            SentToday = sentToday,
            CancelledToday = cancelledToday,
            PaidToday = paidToday,
            PendingInBolivia = pendingInBolivia,
            PendingInChile = pendingInChile
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Endpoint 2: Accounting Validation
    // ═══════════════════════════════════════════════════════════════

    public async Task<RemittanceAccountingValidationReport> GetAccountingValidationAsync(DateTime fecha)
    {
        var ctx = await FetchMonthDataAsync(fecha);

        var accounting = BuildAccountingValidation(
            ctx.BolToChlRemittances, ctx.ChlToBolRemittances,
            ctx.BolPaymentsByCodigo, ctx.AccountingEntries);

        _logger.LogInformation(
            "[RemittanceAcctValidation] {Fecha}: Validated={Total} Valid={Valid} Errors={Errors}",
            fecha.ToString("yyyy-MM-dd"),
            accounting.TotalValidated, accounting.TotalValid, accounting.TotalErrors);

        return new RemittanceAccountingValidationReport
        {
            Fecha = fecha,
            Mes = fecha.Month,
            Anio = fecha.Year,
            FechaProcesamiento = DateTime.UtcNow,
            AccountingValidation = accounting
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Endpoint 3: General Reconciliation
    // ═══════════════════════════════════════════════════════════════

    public async Task<RemittanceGeneralReconciliationReport> GetGeneralReconciliationAsync(DateTime fecha)
    {
        var ctx = await FetchMonthDataAsync(fecha);

        // ── Carry-forward: query historical data in parallel ──
        var bolHistPaidTask = _boliviaRepo.GetHistoricalPaidTotalAsync(ctx.MonthStart);
        var chlHistPaidTask = _chileRepo.GetHistoricalPaidTotalAsync(ctx.MonthStart);
        var bolPendingTask = _boliviaRepo.GetPendingAsOfDateAsync(ctx.MonthStart.AddDays(-1));
        var chlPendingTask = _chileRepo.GetPendingAsOfDateAsync(ctx.MonthStart.AddDays(-1));
        await Task.WhenAll(bolHistPaidTask, chlHistPaidTask, bolPendingTask, chlPendingTask);

        var bolHistPaid = bolHistPaidTask.Result;   // BOL→CHL historical paid total
        var chlHistPaid = chlHistPaidTask.Result;   // CHL→BOL historical paid total
        var bolPending = bolPendingTask.Result;     // BOL→CHL pending at month start
        var chlPending = chlPendingTask.Result;     // CHL→BOL pending at month start

        // Apply USD→BOB conversion to BOL→CHL amounts (repository returns raw USD amounts)
        decimal bolHistPaidConverted = bolHistPaid;
        decimal bolPendingAmountConverted = bolPending.Amount;
        decimal carryForwardBalance = 0m; //chlHistPaid - bolHistPaid;

        var carryForward = new MatrixCarryForwardRow
        {
            ChlToBolPendingCount = chlPending.Count,
            ChlToBolPendingAmount = chlPending.Amount,
            BolToChlPendingCount = bolPending.Count,
            BolToChlPendingAmount = bolPendingAmountConverted,
            SaldoAcumulado = carryForwardBalance
        };

        var dailyMatrix = BuildDailyMatrix(ctx, carryForward);

        var balance = BuildBalance(ctx.ChlToBolRemittances, ctx.BolToChlRemittances);
        
        var discrepancies = BuildDiscrepancies(
            ctx.BolToChlRemittances, ctx.ChlToBolRemittances,
            ctx.BolPaymentsByCodigo, ctx.ChlPaymentsByCodigo, ctx.AccountingEntries,
            ctx.BolToChlPendingOverrides, ctx.ChlToBolPendingOverrides, ctx.MonthEnd);
        var paymentValidation = BuildCombinedPaymentValidation(
            ctx.ChlToBolRemittances, ctx.BolToChlRemittances,
            ctx.BolPaymentsByCodigo, ctx.ChlPaymentsByCodigo);

        _logger.LogInformation(
            "[RemittanceGeneralRecon] {Fecha}: DailyRows={Rows} Discrepancies={Disc} " +
            "PaymentMissing={PmtMissing} PaymentErrors={PmtErr}",
            fecha.ToString("yyyy-MM-dd"), dailyMatrix.DailyRows.Count,
            discrepancies.MissingAccountingEntries.Count,
            paymentValidation.TotalMissing, paymentValidation.TotalFieldErrors);

        return new RemittanceGeneralReconciliationReport
        {
            Fecha = fecha,
            Mes = fecha.Month,
            Anio = fecha.Year,
            FechaProcesamiento = DateTime.UtcNow,
            DailyMatrix = dailyMatrix,
            PaymentValidation = paymentValidation,
            Discrepancies = discrepancies,
            Balance = balance
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Daily Matrix Builder
    // ═══════════════════════════════════════════════════════════════

    private static GeneralReconciliationMatrix BuildDailyMatrix(
        RemittanceDataContext ctx, MatrixCarryForwardRow carryForward)
    {
        var reportDate = ctx.Fecha.Date;
        var dailyRows = new List<MatrixDailyRow>();

        // BOL→CHL amount helper: apply USD→BOB conversion (matches DailyOps logic)
        static decimal BolAmount(BoliviaRemittanceRow r) =>
            r.MonedaRecAbreviatura == "USD" ? r.Monto * BoliviaConstants.USD : r.Monto;

        // Pre-compute payments indexed by date (for Pagadas — matches DailyOps logic)
        var bolPaymentsByDate = ctx.BolPaymentsByCodigo
            .SelectMany(kvp => kvp.Value)
            .GroupBy(p => p.FecCreaPago.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
        var chlPaymentsByDate = ctx.ChlPaymentsByCodigo
            .SelectMany(kvp => kvp.Value)
            .GroupBy(p => p.FecCreaPago.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        decimal runAccumulated = carryForward.SaldoAcumulado;

        // Mutable accumulators for totals
        int tChlRecCnt = 0; decimal tChlRecAmt = 0;
        int tChlAnuCnt = 0; decimal tChlAnuAmt = 0;
        int tChlPagCnt = 0; decimal tChlPagAmt = 0;
        int tBolEnvCnt = 0; decimal tBolEnvAmt = 0;
        int tBolAnuCnt = 0; decimal tBolAnuAmt = 0;
        int tBolPagCnt = 0; decimal tBolPagAmt = 0;
        decimal totSaldoFavorContra = 0;

        for (var day = ctx.MonthStart; day <= reportDate; day = day.AddDays(1))
        {
            var dayChlToBol = ctx.ChlToBolRemittances.Where(r => r.FecCreRemesa.Date == day).ToList();
            var dayBolToChl = ctx.BolToChlRemittances.Where(r => r.FecCreRemesa.Date == day).ToList();

            // CHL→BOL Recibidas/Anuladas for this day (created today)
            var cRecibidas = dayChlToBol.Where(r => r.Estado != RemittanceStatusChile.Anulado).ToList();
            var cAnuladas = dayChlToBol.Where(r => r.Estado == RemittanceStatusChile.Anulado).ToList();

            int cRecCnt = cRecibidas.Count; decimal cRecAmt = cRecibidas.Sum(r => r.Envio);
            int cAnuCnt = cAnuladas.Count; decimal cAnuAmt = cAnuladas.Sum(r => r.Envio);

            // CHL→BOL Pagadas: payments made today (not remittances created today)
            var dayBolPayments = bolPaymentsByDate.TryGetValue(day, out var bp) ? bp : [];
            int cPagCnt = dayBolPayments.Count;
            decimal cPagAmt = dayBolPayments.Sum(p => p.Monto);

            // BOL→CHL Enviadas/Anuladas for this day (created today, with USD conversion)
            var bEnviadas = dayBolToChl.Where(r => r.Estado != RemittanceStatusBolivia.Anulado).ToList();
            var bAnuladas = dayBolToChl.Where(r => r.Estado == RemittanceStatusBolivia.Anulado).ToList();

            int bEnvCnt = bEnviadas.Count; decimal bEnvAmt = bEnviadas.Sum(BolAmount);
            int bAnuCnt = bAnuladas.Count; decimal bAnuAmt = bAnuladas.Sum(BolAmount);

            // BOL→CHL Pagadas: payments made today (not remittances created today)
            var dayChlPayments = chlPaymentsByDate.TryGetValue(day, out var cp) ? cp : [];
            int bPagCnt = dayChlPayments.Count;
            decimal bPagAmt = dayChlPayments.Sum(p => p.Monto);

            // Pending snapshot: all remittances created up to this day that are still pending
            // CHL→BOL: Pendiente OR EnProceso OR (Pagado with payment after cutoff)
            var chlToBolPending = ctx.ChlToBolRemittances
                .Where(r =>
                    r.FecCreRemesa.Date <= day &&
                    (
                        r.Estado == RemittanceStatusChile.Pendiente
                        || r.Estado == RemittanceStatusChile.EnProceso
                        || (r.Estado == RemittanceStatusChile.Pagado && r.FecModRemesa?.Date > day)
                    ))
                .ToList();
            int cPendCnt = chlToBolPending.Count;
            decimal cPendAmt = chlToBolPending.Sum(r => r.Envio);

            // BOL→CHL: Pendiente OR (Pagado with payment after cutoff)
            var bolToChlPending = ctx.BolToChlRemittances
                .Where(r =>
                    r.FecCreRemesa.Date <= day &&
                    (
                        r.Estado == RemittanceStatusBolivia.Pendiente
                        || (r.Estado == RemittanceStatusBolivia.Pagado && r.FecModRemesa?.Date > day)
                    ))
                .ToList();
            int bPendCnt = bolToChlPending.Count;
            decimal bPendAmt = bolToChlPending.Sum(BolAmount);

            // SaldoFavorContra = CHL→BOL pagadas - BOL→CHL pagadas
            var saldoFavorContra = cPagAmt - bPagAmt;
            runAccumulated += saldoFavorContra;

            dailyRows.Add(new MatrixDailyRow
            {
                Fecha = day,
                ChlToBolRecibidas = new StatusBreakdown { Count = cRecCnt, TotalAmount = cRecAmt },
                ChlToBolAnuladas = new StatusBreakdown { Count = cAnuCnt, TotalAmount = cAnuAmt },
                ChlToBolPendientes = new StatusBreakdown { Count = cPendCnt, TotalAmount = cPendAmt },
                ChlToBolPagadas = new StatusBreakdown { Count = cPagCnt, TotalAmount = cPagAmt },
                BolToChlEnviadas = new StatusBreakdown { Count = bEnvCnt, TotalAmount = bEnvAmt },
                BolToChlAnuladas = new StatusBreakdown { Count = bAnuCnt, TotalAmount = bAnuAmt },
                BolToChlPendientes = new StatusBreakdown { Count = bPendCnt, TotalAmount = bPendAmt },
                BolToChlPagadas = new StatusBreakdown { Count = bPagCnt, TotalAmount = bPagAmt },
                SaldoFavorContra = saldoFavorContra,
                SaldoAcumulado = runAccumulated
            });

            // Accumulate totals
            tChlRecCnt += cRecCnt; tChlRecAmt += cRecAmt;
            tChlAnuCnt += cAnuCnt; tChlAnuAmt += cAnuAmt;
            tChlPagCnt += cPagCnt; tChlPagAmt += cPagAmt;
            tBolEnvCnt += bEnvCnt; tBolEnvAmt += bEnvAmt;
            tBolAnuCnt += bAnuCnt; tBolAnuAmt += bAnuAmt;
            tBolPagCnt += bPagCnt; tBolPagAmt += bPagAmt;
            totSaldoFavorContra += saldoFavorContra;
        }

        return new GeneralReconciliationMatrix
        {
            CarryForward = carryForward,
            DailyRows = dailyRows,
            Totals = new MatrixTotalsRow
            {
                ChlToBolRecibidas = new StatusBreakdown { Count = tChlRecCnt, TotalAmount = tChlRecAmt },
                ChlToBolAnuladas = new StatusBreakdown { Count = tChlAnuCnt, TotalAmount = tChlAnuAmt },
                ChlToBolPagadas = new StatusBreakdown { Count = tChlPagCnt, TotalAmount = tChlPagAmt },
                BolToChlEnviadas = new StatusBreakdown { Count = tBolEnvCnt, TotalAmount = tBolEnvAmt },
                BolToChlAnuladas = new StatusBreakdown { Count = tBolAnuCnt, TotalAmount = tBolAnuAmt },
                BolToChlPagadas = new StatusBreakdown { Count = tBolPagCnt, TotalAmount = tBolPagAmt },
                SaldoFavorContraTotal = totSaldoFavorContra
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Balance Calculation (reused by Endpoint 3)
    // ═══════════════════════════════════════════════════════════════

    private static BalanceReport BuildBalance(
        List<ChileRemittanceRow> chlToBolRemittances,
        List<BoliviaRemittanceRow> bolToChlRemittances)
    {
        var paidChlToBol = chlToBolRemittances
            .Where(r => r.Estado == RemittanceStatusChile.Pagado).ToList();
        var paidBolToChl = bolToChlRemittances
            .Where(r => r.Estado == RemittanceStatusBolivia.Pagado).ToList();

        var balancePositivo = paidChlToBol.Sum(r => r.Envio);
        var balanceNegativo = paidBolToChl.Sum(r => r.Monto);
        var saldoNeto = balancePositivo - balanceNegativo;

        var allDates = paidChlToBol.Select(r => r.FecCreRemesa.Date)
            .Union(paidBolToChl.Select(r => r.FecCreRemesa.Date))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var dailyBalances = new List<DailyBalance>();
        decimal accumulated = 0;

        foreach (var date in allDates)
        {
            var dayChlToBol = paidChlToBol
                .Where(r => r.FecCreRemesa.Date == date).Sum(r => r.Envio);
            var dayBolToChl = paidBolToChl
                .Where(r => r.FecCreRemesa.Date == date).Sum(r => r.Monto);
            var netDaily = dayChlToBol - dayBolToChl;
            accumulated += netDaily;

            dailyBalances.Add(new DailyBalance
            {
                Fecha = date,
                ChileToBoliviaAmount = dayChlToBol,
                BoliviaToChileAmount = dayBolToChl,
                NetDaily = netDaily,
                Accumulated = accumulated
            });
        }

        return new BalanceReport
        {
            BalancePositivo = balancePositivo,
            BalanceNegativo = balanceNegativo,
            SaldoNeto = saldoNeto,
            SaldoAcumulado = dailyBalances
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Accounting Integration Validation (reused by Endpoint 2)
    // ═══════════════════════════════════════════════════════════════

    private static AccountingValidationReport BuildAccountingValidation(
        List<BoliviaRemittanceRow> bolToChlRemittances,
        List<ChileRemittanceRow> chlToBolRemittances,
        Dictionary<long, List<BoliviaPaymentRow>> bolPaymentsByCodigo,
        List<AccountingEntryRow> accountingEntries)
    {
        var validationEntries = new List<AccountingValidationEntry>();

        // Rule 1: BOL→CHL pending — transfer entry (concepto LIKE '%ENVIO DE%')
        foreach (var rem in bolToChlRemittances.Where(r => r.Estado == RemittanceStatusBolivia.Pendiente))
        {
            var expectedAmount = rem.MonedaRecAbreviatura == "BOB" 
                                    ? rem.TotalBob + rem.ItfBob 
                                    : Math.Round(rem.Monto * BoliviaConstants.USD, 2, MidpointRounding.AwayFromZero) +
                                        Math.Round(rem.ComisionBob * 0.03m, 2, MidpointRounding.AwayFromZero) +
                                            Math.Round(rem.ComisionBob + rem.ItfBob, 2, MidpointRounding.AwayFromZero);
            var matchingEntries = accountingEntries
                .Where(e => e.Concepto.Contains("ENVIO DE", StringComparison.OrdinalIgnoreCase)
                         && e.Concepto.Contains(rem.Codigo.ToString(), StringComparison.OrdinalIgnoreCase) && e.TipoDocumento == "EC-ER")
                .ToList();

            if (matchingEntries.Count == 0)
            {
                validationEntries.Add(new AccountingValidationEntry
                {
                    Codigo = rem.Codigo,
                    Direction = "BOL->CHL",
                    RemittanceStatus = RemittanceStatusBolivia.ToLabel(rem.Estado),
                    ExpectedAmount = expectedAmount,
                    ExpectedConcepto = "ENVIO DE...",
                    IsValid = false,
                    ErrorMessage = "Asiento contable no encontrado",
                    ValidationType = AccountingValidationType.BoliviaToChilePending
                });
            }
            else
            {
                foreach (var entry in matchingEntries)
                {
                    var amountMatch = Math.Abs(entry.Importe - expectedAmount) < 1m;
                    validationEntries.Add(new AccountingValidationEntry
                    {
                        Codigo = rem.Codigo,
                        Direction = "BOL->CHL",
                        RemittanceStatus = RemittanceStatusBolivia.ToLabel(rem.Estado),
                        ExpectedAmount = expectedAmount,
                        AccountingAmount = entry.Importe,
                        ExpectedConcepto = "ENVIO DE...",
                        ActualConcepto = entry.Concepto,
                        IsValid = amountMatch,
                        ErrorMessage = amountMatch ? null : $"Monto esperado {expectedAmount:N2}, encontrado {entry.Importe:N2}",
                        ValidationType = AccountingValidationType.BoliviaToChilePending
                    });
                }

                if (matchingEntries.Count > 1)
                {
                    var last = validationEntries[^1];
                    validationEntries[^1] = new AccountingValidationEntry
                    {
                        Codigo = last.Codigo,
                        Direction = last.Direction,
                        RemittanceStatus = last.RemittanceStatus,
                        ExpectedAmount = last.ExpectedAmount,
                        AccountingAmount = last.AccountingAmount,
                        ExpectedConcepto = last.ExpectedConcepto,
                        ActualConcepto = last.ActualConcepto,
                        IsValid = false,
                        ErrorMessage = $"Duplicado: {matchingEntries.Count} asientos encontrados",
                        ValidationType = last.ValidationType
                    };
                }
            }
        }

        // Rule 2: BOL→CHL paid — payment entry (concepto LIKE 'PAGO EN DESTINO')
        foreach (var rem in bolToChlRemittances.Where(r => r.Estado == RemittanceStatusBolivia.Pagado))
        {
            decimal expectedAmount = rem.MonedaRecAbreviatura == "USD" ? rem.Monto * BoliviaConstants.USD : rem.Monto;
            if (bolPaymentsByCodigo.TryGetValue(rem.Codigo, out var payments) && payments.Count > 0)
                expectedAmount = payments.Sum(p => p.TotalBob);

            var matchingEntries = accountingEntries
                .Where(e => e.Concepto.Contains("PAGO EN DESTINO", StringComparison.OrdinalIgnoreCase)
                         && e.Concepto.Contains(rem.Codigo.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingEntries.Count == 0)
            {
                validationEntries.Add(new AccountingValidationEntry
                {
                    Codigo = rem.Codigo,
                    Direction = "BOL->CHL",
                    RemittanceStatus = RemittanceStatusBolivia.ToLabel(rem.Estado),
                    ExpectedAmount = expectedAmount,
                    ExpectedConcepto = "PAGO EN DESTINO",
                    IsValid = false,
                    ErrorMessage = "Asiento contable de pago no encontrado",
                    ValidationType = AccountingValidationType.BoliviaToChilePaid
                });
            }
            else
            {
                foreach (var entry in matchingEntries)
                {
                    var amountMatch = entry.Importe == expectedAmount;
                    validationEntries.Add(new AccountingValidationEntry
                    {
                        Codigo = rem.Codigo,
                        Direction = "BOL->CHL",
                        RemittanceStatus = RemittanceStatusBolivia.ToLabel(rem.Estado),
                        ExpectedAmount = expectedAmount,
                        AccountingAmount = entry.Importe,
                        ExpectedConcepto = "PAGO EN DESTINO",
                        ActualConcepto = entry.Concepto,
                        IsValid = amountMatch,
                        ErrorMessage = amountMatch ? null : $"Monto esperado {expectedAmount:N2}, encontrado {entry.Importe:N2}",
                        ValidationType = AccountingValidationType.BoliviaToChilePaid
                    });
                }
            }
        }

        // Rule 3: CHL→BOL paid — two entries with same amount
        foreach (var rem in chlToBolRemittances.Where(r => r.Estado == RemittanceStatusChile.Pagado))
        {
            var codeStr = rem.Codigo.ToString();

            var pendienteEntry = accountingEntries
                .FirstOrDefault(e => e.Concepto.Contains("PENDIENTES DE COBRO DEL PAGO DE REMESA",
                    StringComparison.OrdinalIgnoreCase)
                    && e.Concepto.Contains(codeStr, StringComparison.OrdinalIgnoreCase));

            var pagoEntry = accountingEntries
                .FirstOrDefault(e => e.Concepto.Contains("PAGO DE REMESA CGR",
                    StringComparison.OrdinalIgnoreCase)
                    && e.Concepto.Contains(codeStr, StringComparison.OrdinalIgnoreCase));

            var expectedAmount = rem.Envio;

            if (pendienteEntry == null && pagoEntry == null)
            {
                validationEntries.Add(new AccountingValidationEntry
                {
                    Codigo = rem.Codigo,
                    Direction = "CHL->BOL",
                    RemittanceStatus = RemittanceStatusChile.ToLabel(rem.Estado),
                    ExpectedAmount = expectedAmount,
                    ExpectedConcepto = "PENDIENTES DE COBRO + PAGO DE REMESA CGR",
                    IsValid = false,
                    ErrorMessage = "Ambos asientos contables no encontrados",
                    ValidationType = AccountingValidationType.ChileToBoliviaPaid
                });
            }
            else if (pendienteEntry == null)
            {
                validationEntries.Add(new AccountingValidationEntry
                {
                    Codigo = rem.Codigo,
                    Direction = "CHL->BOL",
                    RemittanceStatus = RemittanceStatusChile.ToLabel(rem.Estado),
                    ExpectedAmount = expectedAmount,
                    AccountingAmount = pagoEntry!.Importe,
                    ExpectedConcepto = "PENDIENTES DE COBRO DEL PAGO DE REMESA",
                    ActualConcepto = pagoEntry.Concepto,
                    IsValid = false,
                    ErrorMessage = "Asiento de PENDIENTES DE COBRO no encontrado",
                    ValidationType = AccountingValidationType.ChileToBoliviaPaid
                });
            }
            else if (pagoEntry == null)
            {
                validationEntries.Add(new AccountingValidationEntry
                {
                    Codigo = rem.Codigo,
                    Direction = "CHL->BOL",
                    RemittanceStatus = RemittanceStatusChile.ToLabel(rem.Estado),
                    ExpectedAmount = expectedAmount,
                    AccountingAmount = pendienteEntry.Importe,
                    ExpectedConcepto = "PAGO DE REMESA CGR",
                    ActualConcepto = pendienteEntry.Concepto,
                    IsValid = false,
                    ErrorMessage = "Asiento de PAGO DE REMESA CGR no encontrado",
                    ValidationType = AccountingValidationType.ChileToBoliviaPaid
                });
            }
            else
            {
                var amountsMatch = pendienteEntry.Importe == pagoEntry.Importe;
                string? errorMsg = amountsMatch
                    ? null
                    : $"Montos no coinciden: PENDIENTES={pendienteEntry.Importe:N2}, PAGO={pagoEntry.Importe:N2}";

                validationEntries.Add(new AccountingValidationEntry
                {
                    Codigo = rem.Codigo,
                    Direction = "CHL->BOL",
                    RemittanceStatus = RemittanceStatusChile.ToLabel(rem.Estado),
                    ExpectedAmount = expectedAmount,
                    AccountingAmount = pagoEntry.Importe,
                    ExpectedConcepto = "PENDIENTES DE COBRO + PAGO DE REMESA CGR",
                    ActualConcepto = $"{pendienteEntry.Concepto} | {pagoEntry.Concepto}",
                    IsValid = amountsMatch,
                    ErrorMessage = errorMsg,
                    ValidationType = AccountingValidationType.ChileToBoliviaPaid
                });
            }
        }

        var totalValid = validationEntries.Count(e => e.IsValid);
        var totalErrors = validationEntries.Count(e => !e.IsValid);

        return new AccountingValidationReport
        {
            TotalValidated = validationEntries.Count,
            TotalValid = totalValid,
            TotalErrors = totalErrors,
            Entries = validationEntries
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Cross-Database Discrepancy Detection (reused by Endpoint 3)
    // ═══════════════════════════════════════════════════════════════

    private static DiscrepancyReport BuildDiscrepancies(
        List<BoliviaRemittanceRow> bolToChlRemittances,
        List<ChileRemittanceRow> chlToBolRemittances,
        Dictionary<long, List<BoliviaPaymentRow>> bolPaymentsByCodigo,
        Dictionary<int, List<ChilePaymentRow>> chlPaymentsByCodigo,
        List<AccountingEntryRow> accountingEntries,
        HashSet<int> bolToChlPendingOverrides,
        HashSet<int> chlToBolPendingOverrides,
        DateTime beforeDate)
    {
        var bolPaidNoChlPayment = bolToChlRemittances
            .Where(r =>
            {
                var hasModDate = r.FecModRemesa.HasValue;
                var isBeforeCutoff = hasModDate && r.FecModRemesa.Value.Date <= beforeDate;
                var isPaid = r.Estado == RemittanceStatusBolivia.Pagado;
                var hasNoPayment = !chlPaymentsByCodigo.ContainsKey(r.Codigo);

                return isBeforeCutoff && isPaid && hasNoPayment;
            })
            .Select(r => new DiscrepancyEntry
            {
                Codigo = r.Codigo,
                Source = "Bolivia (remesa)",
                Direction = "BOL->CHL",
                Amount = r.Monto,
                Status = RemittanceStatusBolivia.ToLabel(r.Estado),
                Fecha = r.FecCreRemesa
            })
            .ToList();

        var chlPaidNoBolPayment = chlToBolRemittances
            .Where(r =>
            {
                var hasModDate = r.FecModRemesa.HasValue;
                var isBeforeCutoff = hasModDate && r.FecModRemesa <= beforeDate;
                var isPaid = r.Estado == RemittanceStatusChile.Pagado;
                var hasNoPayment = !bolPaymentsByCodigo.ContainsKey(r.Codigo);

                return isBeforeCutoff && isPaid && hasNoPayment;
            })
            .Select(r => new DiscrepancyEntry
            {
                Codigo = r.Codigo,
                Source = "Chile (remesa)",
                Direction = "CHL->BOL",
                Amount = r.Envio,
                Status = RemittanceStatusChile.ToLabel(r.Estado),
                Fecha = r.FecCreRemesa
            })
            .ToList();

        var allCodes = new HashSet<int>(
            bolToChlRemittances.Select(r => r.Codigo)
                .Concat(chlToBolRemittances.Select(r => r.Codigo)));

        var missingAccounting = new List<DiscrepancyEntry>();

        foreach (var r in bolToChlRemittances.Where(r =>
            (r.Estado == RemittanceStatusBolivia.Pagado || r.Estado == RemittanceStatusBolivia.Pendiente)
            && !bolToChlPendingOverrides.Contains(r.Codigo)))
        {
            if (!accountingEntries.Any(e =>
                e.Concepto.Contains(r.Codigo.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                missingAccounting.Add(new DiscrepancyEntry
                {
                    Codigo = r.Codigo,
                    Source = "Bolivia (remesa)",
                    Direction = "BOL->CHL",
                    Amount = r.Monto,
                    Status = RemittanceStatusBolivia.ToLabel(r.Estado),
                    Fecha = r.FecCreRemesa
                });
            }
        }

        foreach (var r in chlToBolRemittances.Where(r =>
            (r.Estado == RemittanceStatusChile.Pagado
            || r.Estado == RemittanceStatusChile.Pendiente
            || r.Estado == RemittanceStatusChile.EnProceso)
            && !chlToBolPendingOverrides.Contains(r.Codigo)))
        {
            if (!accountingEntries.Any(e =>
                e.Concepto.Contains(r.Codigo.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                missingAccounting.Add(new DiscrepancyEntry
                {
                    Codigo = r.Codigo,
                    Source = "Chile (remesa)",
                    Direction = "CHL->BOL",
                    Amount = r.Envio,
                    Status = RemittanceStatusChile.ToLabel(r.Estado),
                    Fecha = r.FecCreRemesa
                });
            }
        }

        var orphanEntries = accountingEntries
            .Where(e =>
            {
                var codeStr = ExtractCgrCode(e.Concepto);
                return codeStr != null && int.TryParse(codeStr, out var code) && !allCodes.Contains(code);
            })
            .Select(e => new OrphanAccountingEntry
            {
                IdAsiento = e.IdAsiento,
                Fecha = e.Fecha,
                Concepto = e.Concepto,
                Importe = e.Importe
            })
            .ToList();

        var amountMismatches = new List<AmountMismatch>();

        foreach (var bol in bolToChlRemittances
            .Where(r => !bolToChlPendingOverrides.Contains(r.Codigo)))
        {
            if (chlPaymentsByCodigo.TryGetValue(bol.Codigo, out var chlPayments) && chlPayments.Count == 1)
            {
                var chlPago = chlPayments[0];
                if (Math.Abs(bol.Monto - chlPago.Monto) > 0.01m)
                {
                    amountMismatches.Add(new AmountMismatch
                    {
                        Codigo = bol.Codigo,
                        BoliviaAmount = bol.Monto,
                        ChileAmount = chlPago.Monto,
                        Difference = bol.Monto - chlPago.Monto
                    });
                }
            }
        }

        foreach (var chl in chlToBolRemittances
            .Where(r => !chlToBolPendingOverrides.Contains(r.Codigo)))
        {
            if (bolPaymentsByCodigo.TryGetValue(chl.Codigo, out var bolPayments) && bolPayments.Count == 1)
            {
                var bolPago = bolPayments[0];
                if (Math.Abs(chl.Envio - bolPago.Monto) > 0.01m)
                {
                    amountMismatches.Add(new AmountMismatch
                    {
                        Codigo = chl.Codigo,
                        BoliviaAmount = bolPago.Monto,
                        ChileAmount = chl.Envio,
                        Difference = chl.Envio - bolPago.Monto
                    });
                }
            }
        }

        return new DiscrepancyReport
        {
            InBoliviaNotInChile = bolPaidNoChlPayment,
            InChileNotInBolivia = chlPaidNoBolPayment,
            MissingAccountingEntries = missingAccounting,
            OrphanAccountingEntries = orphanEntries,
            AmountMismatches = amountMismatches
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Payment Validation (reused by Endpoint 3)
    // ═══════════════════════════════════════════════════════════════

    private static PaymentValidationReport BuildCombinedPaymentValidation(
        List<ChileRemittanceRow> chlToBolRemittances,
        List<BoliviaRemittanceRow> bolToChlRemittances,
        Dictionary<long, List<BoliviaPaymentRow>> bolPaymentsByCodigo,
        Dictionary<int, List<ChilePaymentRow>> chlPaymentsByCodigo)
    {
        var entries = new List<PaymentValidationEntry>();

        BuildBoliviaPaymentEntries(chlToBolRemittances, bolPaymentsByCodigo, entries);
        BuildChilePaymentEntries(bolToChlRemittances, chlPaymentsByCodigo, entries);

        return new PaymentValidationReport
        {
            TotalRemittances = entries.Count,
            TotalWithPayment = entries.Count(e => e.Status != PaymentValidationStatus.Missing),
            TotalMissing = entries.Count(e => e.Status == PaymentValidationStatus.Missing),
            TotalDuplicates = entries.Count(e => e.Status == PaymentValidationStatus.Duplicate),
            TotalFieldErrors = entries.Count(e => e.Status == PaymentValidationStatus.FieldMismatch),
            TotalValid = entries.Count(e => e.IsValid),
            Entries = entries
        };
    }

    private static void BuildBoliviaPaymentEntries(
        List<ChileRemittanceRow> chlToBolRemittances,
        Dictionary<long, List<BoliviaPaymentRow>> bolPaymentsByCodigo,
        List<PaymentValidationEntry> entries)
    {
        var paidRemittances = chlToBolRemittances
            .Where(r => r.Estado == RemittanceStatusChile.Pagado)
            .ToList();

        foreach (var rem in paidRemittances)
        {
            bolPaymentsByCodigo.TryGetValue(rem.Codigo, out var payments);
            var paymentCount = payments?.Count ?? 0;

            if (paymentCount == 0)
            {
                var isSameMonth = rem.FecModRemesa.HasValue &&
                  rem.FecCreRemesa.Month == rem.FecModRemesa.Value.Month;

                var errorMessage = isSameMonth
                    ? "Sin registro de pago en tabla pago de Bolivia"
                    : "Pendiente de Pago";

                entries.Add(new PaymentValidationEntry
                {
                    Direction = "CHL->BOL",
                    Codigo = rem.Codigo,
                    Moneda = rem.Moneda,
                    RemittanceEnvio = rem.Envio,
                    RemittanceEstadoLabel = RemittanceStatusChile.ToLabel(rem.Estado),
                    RemittanceFecha = rem.FecCreRemesa,
                    Status = isSameMonth ? PaymentValidationStatus.Missing : PaymentValidationStatus.Valid,
                    IsValid = false,
                    ErrorMessage = errorMessage
                });
            }
            else if (paymentCount > 1)
            {
                entries.Add(new PaymentValidationEntry
                {
                    Direction = "CHL->BOL",
                    Codigo = rem.Codigo,
                    Moneda = rem.Moneda,
                    RemittanceEnvio = rem.Envio,
                    RemittanceEstadoLabel = RemittanceStatusChile.ToLabel(rem.Estado),
                    RemittanceFecha = rem.FecCreRemesa,
                    PagoMonto = payments![0].Monto,
                    PagoTipoCambio = payments[0].TipoCambio,
                    PagoTotal = payments[0].TotalBob,
                    PagoComisionBob = payments[0].ComisionBob,
                    PagoFecCrea = payments[0].FecCreaPago,
                    Status = PaymentValidationStatus.Duplicate,
                    IsValid = false,
                    ErrorMessage = $"Pago duplicado: {paymentCount} registros encontrados"
                });
            }
            else
            {
                var pago = payments![0];
                var montoMatch = Math.Abs(rem.Envio - pago.Monto) <= 0.01m;
                var comisionMatch = Math.Abs(rem.ComisionBob - pago.ComisionBob) <= 0.01m;
                var totalMatch = Math.Abs(pago.Monto * pago.TipoCambio - pago.TotalBob) <= 0.50m;
                var allMatch = montoMatch && totalMatch && comisionMatch;

                var errors = new List<string>();
                if (!montoMatch) errors.Add($"Monto: remesa={rem.Envio:N2} pago={pago.Monto:N2}");
                if (!totalMatch) errors.Add($"TotalBob: pago={pago.TotalBob:N2} esperado≈{pago.Monto * pago.TipoCambio:N2}");
                if (!comisionMatch) errors.Add($"ComisionBob: remesa={rem.ComisionBob:N2} pago={pago.ComisionBob:N2}");

                entries.Add(new PaymentValidationEntry
                {
                    Direction = "CHL->BOL",
                    Codigo = rem.Codigo,
                    Moneda = rem.Moneda,
                    RemittanceEnvio = rem.Envio,
                    RemittanceEstadoLabel = RemittanceStatusChile.ToLabel(rem.Estado),
                    RemittanceFecha = rem.FecCreRemesa,
                    PagoMonto = pago.Monto,
                    PagoTipoCambio = pago.TipoCambio,
                    PagoTotal = pago.TotalBob,
                    PagoComisionBob = pago.ComisionBob,
                    PagoFecCrea = pago.FecCreaPago,
                    MontoMatch = montoMatch,
                    TipoCambioMatch = true,
                    TotalMatch = totalMatch,
                    ComisionMatch = comisionMatch,
                    Status = allMatch ? PaymentValidationStatus.Valid : PaymentValidationStatus.FieldMismatch,
                    IsValid = allMatch,
                    ErrorMessage = allMatch ? null : string.Join("; ", errors)
                });
            }
        }
    }

    private static void BuildChilePaymentEntries(
        List<BoliviaRemittanceRow> bolToChlRemittances,
        Dictionary<int, List<ChilePaymentRow>> chlPaymentsByCodigo,
        List<PaymentValidationEntry> entries)
    {
        var paidRemittances = bolToChlRemittances
            .Where(r => r.Estado == RemittanceStatusBolivia.Pagado)
            .ToList();

        foreach (var rem in paidRemittances)
        {
            chlPaymentsByCodigo.TryGetValue(rem.Codigo, out var payments);
            var paymentCount = payments?.Count ?? 0;

            if (paymentCount == 0)
            {
                var isSameMonth = rem.FecModRemesa.HasValue &&
                  rem.FecCreRemesa.Month == rem.FecModRemesa.Value.Month;

                var errorMessage = isSameMonth
                    ? "Sin registro de pago en tabla pago de Chile"
                    : "Pendiente de Pago";

                entries.Add(new PaymentValidationEntry
                {
                    Direction = "BOL->CHL",
                    Codigo = rem.Codigo,
                    Moneda = rem.MonedaRecAbreviatura,
                    RemittanceEnvio = rem.Monto,
                    RemittanceEstadoLabel = RemittanceStatusBolivia.ToLabel(rem.Estado),
                    RemittanceFecha = rem.FecCreRemesa,
                    Status = isSameMonth ? PaymentValidationStatus.Missing : PaymentValidationStatus.Valid,
                    IsValid = false,
                    ErrorMessage = errorMessage
                });
            }
            else if (paymentCount > 1)
            {
                entries.Add(new PaymentValidationEntry
                {
                    Direction = "BOL->CHL",
                    Codigo = rem.Codigo,
                    Moneda = rem.MonedaRecAbreviatura,
                    RemittanceEnvio = rem.Monto,
                    RemittanceEstadoLabel = RemittanceStatusBolivia.ToLabel(rem.Estado),
                    RemittanceFecha = rem.FecCreRemesa,
                    PagoMonto = payments![0].Monto,
                    PagoTipoCambio = payments[0].TipoCambio,
                    PagoTotal = payments[0].Total,
                    PagoFecCrea = payments[0].FecCreaPago,
                    Status = PaymentValidationStatus.Duplicate,
                    IsValid = false,
                    ErrorMessage = $"Pago duplicado: {paymentCount} registros encontrados"
                });
            }
            else
            {
                var pago = payments![0];
                var montoMatch = Math.Abs(rem.TotalClp - pago.Total) <= 10.0m;
                var tipoCambioMatch = Math.Abs(rem.TipoCambioClp - pago.TipoCambio) <= 0.0001m;
                var allMatch = montoMatch && tipoCambioMatch;

                var errors = new List<string>();
                if (!montoMatch) errors.Add($"Monto: remesa={rem.TotalClp:N2} pago={pago.Total:N2}");
                if (!tipoCambioMatch) errors.Add($"T/C: remesa={rem.TipoCambioClp:N4} pago={pago.TipoCambio:N4}");

                entries.Add(new PaymentValidationEntry
                {
                    Direction = "BOL->CHL",
                    Codigo = rem.Codigo,
                    Moneda = rem.MonedaRecAbreviatura,
                    RemittanceEnvio = rem.Monto,
                    RemittanceEstadoLabel = RemittanceStatusBolivia.ToLabel(rem.Estado),
                    RemittanceFecha = rem.FecCreRemesa,
                    PagoMonto = pago.Monto,
                    PagoTipoCambio = pago.TipoCambio,
                    PagoTotal = pago.Total,
                    PagoFecCrea = pago.FecCreaPago,
                    MontoMatch = montoMatch,
                    TipoCambioMatch = tipoCambioMatch,
                    TotalMatch = true,
                    Status = allMatch ? PaymentValidationStatus.Valid : PaymentValidationStatus.FieldMismatch,
                    IsValid = allMatch,
                    ErrorMessage = allMatch ? null : string.Join("; ", errors)
                });
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private static string? ExtractCgrCode(string concepto)
    {
        var idx = concepto.IndexOf("CGR", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var start = idx + 3;
        while (start < concepto.Length && concepto[start] == ' ')
            start++;

        if (start >= concepto.Length || !char.IsDigit(concepto[start]))
            return null;

        var end = start;
        while (end < concepto.Length && char.IsDigit(concepto[end]))
            end++;

        return concepto[start..end];
    }
}
