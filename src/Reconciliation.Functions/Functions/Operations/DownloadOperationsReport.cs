using System.Globalization;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.Operations;

namespace Reconciliation.Functions.Functions.Operations;

public class DownloadOperationsReport
{
    private readonly IOperationsService _service;
    private readonly IOperationsExcelExportService _excelExport;
    private readonly ILogger<DownloadOperationsReport> _logger;

    public DownloadOperationsReport(
        IOperationsService service,
        IOperationsExcelExportService excelExport,
        ILogger<DownloadOperationsReport> logger)
    {
        _service = service;
        _excelExport = excelExport;
        _logger = logger;
    }

    [Function("DownloadOperationsReport")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "operations/report/download")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var fromStr = query["from"];
        var toStr = query["to"];

        if (!DateTime.TryParseExact(fromStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            || !DateTime.TryParseExact(toStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Both 'from' and 'to' query parameters are required (yyyy-MM-dd).");
            return bad;
        }

        if (from > to)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("'from' must be less than or equal to 'to'.");
            return bad;
        }

        _logger.LogInformation("[OperationsReportDownload] Request from {From} to {To}",
            from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));

        var report = await _service.GetReportAsync(from, to);
        var fileBytes = _excelExport.GenerateOperationsReport(report);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Headers.Add("Content-Disposition",
            $"attachment; filename=\"Reporte_Operaciones_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx\"");
        response.Body.Write(fileBytes);
        return response;
    }
}
