using System.Globalization;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.Interfaces;

namespace Reconciliation.Functions.Functions;

public class RemittanceDownloadFunction
{
    private readonly IRemittanceService _remittanceService;
    private readonly IExcelExportService _excelExport;
    private readonly IPdfExportService _pdfExport;
    private readonly ILogger<RemittanceDownloadFunction> _logger;

    public RemittanceDownloadFunction(
        IRemittanceService remittanceService,
        IExcelExportService excelExport,
        IPdfExportService pdfExport,
        ILogger<RemittanceDownloadFunction> logger)
    {
        _remittanceService = remittanceService;
        _excelExport = excelExport;
        _pdfExport = pdfExport;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/reconciliation/remittances/daily-operations/download?date=2026-01-15&amp;format=xlsx|pdf
    /// </summary>
    [Function("DownloadRemittanceDailyOperations")]
    public async Task<HttpResponseData> DownloadRemittanceDailyOperationsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "reconciliation/remittances/daily-operations/download")] HttpRequestData req)
    {
        var fecha = ParseDate(req);
        var format = ParseFormat(req);

        _logger.LogInformation("[RemittanceDailyOpsDownload] Request for {Fecha} format={Format}",
            fecha.ToString("yyyy-MM-dd"), format);

        var report = await _remittanceService.GetDailyOperationsAsync(fecha);

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateRemittanceDailyOperations(report)
            : _excelExport.GenerateRemittanceDailyOperations(report);

        return WriteFileResponse(req, fileBytes, "RemittanceDailyOperations", fecha, format);
    }

    /// <summary>
    /// GET /api/reconciliation/remittances/accounting-validation/download?date=2026-01-15&amp;format=xlsx|pdf
    /// </summary>
    [Function("DownloadRemittanceAccountingValidation")]
    public async Task<HttpResponseData> DownloadRemittanceAccountingValidationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "reconciliation/remittances/accounting-validation/download")] HttpRequestData req)
    {
        var fecha = ParseDate(req);
        var format = ParseFormat(req);

        _logger.LogInformation("[RemittanceAcctValidDownload] Request for {Fecha} format={Format}",
            fecha.ToString("yyyy-MM-dd"), format);

        var report = await _remittanceService.GetAccountingValidationAsync(fecha);

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateRemittanceAccountingValidation(report)
            : _excelExport.GenerateRemittanceAccountingValidation(report);

        return WriteFileResponse(req, fileBytes, "RemittanceAccountingValidation", fecha, format);
    }

    /// <summary>
    /// GET /api/reconciliation/remittances/general-reconciliation/download?date=2026-01-15&amp;format=xlsx|pdf
    /// </summary>
    [Function("DownloadRemittanceGeneralReconciliation")]
    public async Task<HttpResponseData> DownloadRemittanceGeneralReconciliationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "reconciliation/remittances/general-reconciliation/download")] HttpRequestData req)
    {
        var fecha = ParseDate(req);
        var format = ParseFormat(req);

        _logger.LogInformation("[RemittanceGenReconDownload] Request for {Fecha} format={Format}",
            fecha.ToString("yyyy-MM-dd"), format);

        var report = await _remittanceService.GetGeneralReconciliationAsync(fecha);

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateRemittanceGeneralReconciliation(report)
            : _excelExport.GenerateRemittanceGeneralReconciliation(report);

        return WriteFileResponse(req, fileBytes, "RemittanceGeneralReconciliation", fecha, format);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static DateTime ParseDate(HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var dateStr = query["date"];
        return !string.IsNullOrEmpty(dateStr)
            && DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DateTime.UtcNow.Date;
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
            $"attachment; filename=\"Report_{reportName}_{fecha:yyyy-MM-dd}.{ext}\"");
        response.Body.Write(bytes);
        return response;
    }
}
