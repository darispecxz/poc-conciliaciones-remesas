using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.Operations;

namespace Reconciliation.Functions.Functions.Operations;

public class GetOperationsReport
{
    private readonly IOperationsService _service;
    private readonly ILogger<GetOperationsReport> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public GetOperationsReport(IOperationsService service, ILogger<GetOperationsReport> logger)
    {
        _service = service;
        _logger = logger;
    }

    [Function("GetOperationsReport")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "operations/report")] HttpRequestData req)
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

        _logger.LogInformation("[OperationsReport] Request from {From} to {To}",
            from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));

        var result = await _service.GetReportAsync(from, to);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions));
        return response;
    }
}
