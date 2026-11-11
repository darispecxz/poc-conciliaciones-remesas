using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.Interfaces;

namespace Reconciliation.Functions.Functions;

public class RemittanceReportFunction
{
    private readonly IRemittanceService _remittanceService;
    private readonly ILogger<RemittanceReportFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public RemittanceReportFunction(
        IRemittanceService remittanceService,
        ILogger<RemittanceReportFunction> logger)
    {
        _remittanceService = remittanceService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/reconciliation/remittances/daily-operations?date=2026-01-15
    /// Daily operations: sent/cancelled/paid for the date, pending accumulated.
    /// </summary>
    [Function("GetRemittanceDailyOperations")]
    public async Task<HttpResponseData> GetRemittanceDailyOperationsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "reconciliation/remittances/daily-operations")] HttpRequestData req)
    {
        var fecha = ParseDate(req);
        _logger.LogInformation("[RemittanceDailyOps] Request for {Fecha}", fecha.ToString("yyyy-MM-dd"));
        var result = await _remittanceService.GetDailyOperationsAsync(fecha);
        return await WriteJsonResponse(req, result);
    }

    /// <summary>
    /// GET /api/reconciliation/remittances/accounting-validation?date=2026-01-15
    /// Full-month accounting validation (remittance vs co_asientos).
    /// </summary>
    [Function("GetRemittanceAccountingValidation")]
    public async Task<HttpResponseData> GetRemittanceAccountingValidationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "reconciliation/remittances/accounting-validation")] HttpRequestData req)
    {
        var fecha = ParseDate(req);
        _logger.LogInformation("[RemittanceAcctValid] Request for {Fecha}", fecha.ToString("yyyy-MM-dd"));
        var result = await _remittanceService.GetAccountingValidationAsync(fecha);
        return await WriteJsonResponse(req, result);
    }

    /// <summary>
    /// GET /api/reconciliation/remittances/general-reconciliation?date=2026-01-15
    /// Daily matrix, payment validation, discrepancies, and balance.
    /// </summary>
    [Function("GetRemittanceGeneralReconciliation")]
    public async Task<HttpResponseData> GetRemittanceGeneralReconciliationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "reconciliation/remittances/general-reconciliation")] HttpRequestData req)
    {
        var fecha = ParseDate(req);
        _logger.LogInformation("[RemittanceGenRecon] Request for {Fecha}", fecha.ToString("yyyy-MM-dd"));
        var result = await _remittanceService.GetGeneralReconciliationAsync(fecha);
        return await WriteJsonResponse(req, result);
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

    private static async Task<HttpResponseData> WriteJsonResponse<T>(
        HttpRequestData req, T data)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(data, JsonOptions));
        return response;
    }
}
