using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.Interfaces;

namespace Reconciliation.Functions.Functions;

public class ReconciliationReportFunction
{
    private readonly ICashierService _cashierService;
    private readonly IVaultService _vaultService;
    private readonly ILogger<ReconciliationReportFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public ReconciliationReportFunction(
        ICashierService cashierService,
        IVaultService vaultService,
        ILogger<ReconciliationReportFunction> logger)
    {
        _cashierService = cashierService;
        _vaultService = vaultService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/reconciliation/cashiers?date=2026-01-15&agencia=1
    /// Full per-cashier reconciliation (Section D).
    /// </summary>
    [Function("GetCashierReconciliation")]
    public async Task<HttpResponseData> GetCashierReconciliationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/cashiers")] HttpRequestData req)
    {
        var (fecha, idAgencia) = ParseQueryParams(req);
        var result = await _cashierService.GetReconciliationAsync(fecha, idAgencia);
        return await WriteJsonResponse(req, result);
    }

    /// <summary>
    /// GET /api/reconciliation/vaults?date=2026-01-15
    /// Vault reconciliation with movement breakdown.
    /// </summary>
    [Function("GetVaultReconciliation")]
    public async Task<HttpResponseData> GetVaultReconciliationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/vaults")] HttpRequestData req)
    {
        var (fecha, idAgencia) = ParseQueryParams(req);
        var result = await _vaultService.GetReconciliationAsync(fecha, idAgencia);
        return await WriteJsonResponse(req, result);
    }

    /// <summary>
    /// GET /api/reconciliation/availability?date=2026-01-15
    /// Vault availability with exchange rate conversion (Section B).
    /// </summary>
    [Function("GetVaultAvailability")]
    public async Task<HttpResponseData> GetVaultAvailabilityAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/availability")] HttpRequestData req)
    {
        var (fecha, _) = ParseQueryParams(req);
        var result = await _vaultService.GetAvailabilityAsync(fecha);
        return await WriteJsonResponse(req, result);
    }

    /// <summary>
    /// GET /api/reconciliation/summary?date=2026-01-15
    /// Daily summary: caja + boveda totals per currency (Section C).
    /// </summary>
    [Function("GetDailySummary")]
    public async Task<HttpResponseData> GetDailySummaryAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/summary")] HttpRequestData req)
    {
        var (fecha, _) = ParseQueryParams(req);
        var result = await _cashierService.GetDailySummaryAsync(fecha);
        return await WriteJsonResponse(req, result);
    }

    /// <summary>
    /// GET /api/reconciliation/closings?date=2026-01-15
    /// Agency closing balances matrix (Section E2).
    /// </summary>
    [Function("GetAgencyClosings")]
    public async Task<HttpResponseData> GetAgencyClosingsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/closings")] HttpRequestData req)
    {
        var (fecha, _) = ParseQueryParams(req);
        var result = await _cashierService.GetAgencyClosingsAsync(fecha);
        return await WriteJsonResponse(req, result);
    }

    /// <summary>
    /// GET /api/reconciliation/movements?date=2026-01-15
    /// Consolidated movement summary per currency (Section E1).
    /// </summary>
    [Function("GetMovementSummary")]
    public async Task<HttpResponseData> GetMovementSummaryAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "reconciliation/movements")] HttpRequestData req)
    {
        var (fecha, _) = ParseQueryParams(req);
        var result = await _cashierService.GetMovementSummaryAsync(fecha);
        return await WriteJsonResponse(req, result);
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

    private static async Task<HttpResponseData> WriteJsonResponse<T>(HttpRequestData req, T data)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(data, JsonOptions));
        return response;
    }
}
