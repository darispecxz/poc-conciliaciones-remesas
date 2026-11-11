using System.Globalization;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.Interfaces;

namespace Reconciliation.Functions.Functions;

public class ReconciliationDownloadFunction
{
    private readonly ICashierService _cashierService;
    private readonly IVaultService _vaultService;
    private readonly IExcelExportService _excelExport;
    private readonly IPdfExportService _pdfExport;
    private readonly ILogger<ReconciliationDownloadFunction> _logger;

    public ReconciliationDownloadFunction(
        ICashierService cashierService,
        IVaultService vaultService,
        IExcelExportService excelExport,
        IPdfExportService pdfExport,
        ILogger<ReconciliationDownloadFunction> logger)
    {
        _cashierService = cashierService;
        _vaultService = vaultService;
        _excelExport = excelExport;
        _pdfExport = pdfExport;
        _logger = logger;
    }

    [Function("DownloadCashierReconciliation")]
    public async Task<HttpResponseData> DownloadCashierReconciliationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/cashiers/download")] HttpRequestData req)
    {
        var (fecha, idAgencia) = ParseQueryParams(req);
        var format = ParseFormat(req);
        var report = await _cashierService.GetReconciliationAsync(fecha, idAgencia);

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateCashierReconciliation(report)
            : _excelExport.GenerateCashierReconciliation(report);

        return WriteFileResponse(req, fileBytes, "ArqueoCajaConsolidadoDiario", fecha, format);
    }

    [Function("DownloadVaultReconciliation")]
    public async Task<HttpResponseData> DownloadVaultReconciliationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/vaults/download")] HttpRequestData req)
    {
        var (fecha, idAgencia) = ParseQueryParams(req);
        var format = ParseFormat(req);
        var report = await _vaultService.GetReconciliationAsync(fecha, idAgencia);

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateVaultReconciliation(report)
            : _excelExport.GenerateVaultReconciliation(report);

        return WriteFileResponse(req, fileBytes, "BovedaConciliacion", fecha, format);
    }

    [Function("DownloadVaultAvailability")]
    public async Task<HttpResponseData> DownloadVaultAvailabilityAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/availability/download")] HttpRequestData req)
    {
        var (fecha, _) = ParseQueryParams(req);
        var format = ParseFormat(req);
        var report = await _vaultService.GetAvailabilityAsync(fecha);

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateVaultAvailability(report)
            : _excelExport.GenerateVaultAvailability(report);

        return WriteFileResponse(req, fileBytes, "DisponibilidadBoveda", fecha, format);
    }

    [Function("DownloadDailySummary")]
    public async Task<HttpResponseData> DownloadDailySummaryAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/summary/download")] HttpRequestData req)
    {
        var (fecha, _) = ParseQueryParams(req);
        var format = ParseFormat(req);
        var report = await _cashierService.GetDailySummaryAsync(fecha);

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateDailySummary(report)
            : _excelExport.GenerateDailySummary(report);

        return WriteFileResponse(req, fileBytes, "ResumenDiario", fecha, format);
    }

    [Function("DownloadAgencyClosings")]
    public async Task<HttpResponseData> DownloadAgencyClosingsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/closings/download")] HttpRequestData req)
    {
        var (fecha, _) = ParseQueryParams(req);
        var format = ParseFormat(req);
        var report = await _cashierService.GetAgencyClosingsAsync(fecha);

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateAgencyClosings(report)
            : _excelExport.GenerateAgencyClosings(report);

        return WriteFileResponse(req, fileBytes, "CierreAgencia", fecha, format);
    }

    [Function("DownloadMovementSummary")]
    public async Task<HttpResponseData> DownloadMovementSummaryAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/movements/download")] HttpRequestData req)
    {
        var (fecha, _) = ParseQueryParams(req);
        var format = ParseFormat(req);
        var report = await _cashierService.GetMovementSummaryAsync(fecha);

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateMovementSummary(report)
            : _excelExport.GenerateMovementSummary(report);

        return WriteFileResponse(req, fileBytes, "ResumenMovimientos", fecha, format);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static (DateTime Fecha, int? IdAgencia) ParseQueryParams(HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        var dateStr = query["date"];
        var fecha = !string.IsNullOrEmpty(dateStr)
            && DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)
            ? parsed
            : DateTime.UtcNow.Date;

        int? idAgencia = int.TryParse(query["agencia"], out var ag) ? ag : null;

        return (fecha, idAgencia);
    }

    private static string ParseFormat(HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var format = query["format"]?.ToLowerInvariant();
        return format == "pdf" ? "pdf" : "xlsx";
    }

    private static HttpResponseData WriteFileResponse(
        HttpRequestData req, byte[] bytes, string reportName, DateTime fecha, string format)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        var ext = format == "pdf" ? "pdf" : "xlsx";
        var mime = format == "pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        response.Headers.Add("Content-Type", mime);
        response.Headers.Add("Content-Disposition",
            $"attachment; filename=\"Reporte_{reportName}_{fecha:yyyy-MM-dd}.{ext}\"");
        response.Body.Write(bytes);
        return response;
    }
}
