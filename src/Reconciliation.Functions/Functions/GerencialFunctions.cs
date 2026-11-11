using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Functions.Functions;

public class GerencialFunctions
{
    private readonly IGerencialService _gerencialService;
    private readonly IGerencialChartGenerator _chartGenerator;
    private readonly IGerencialExcelExportService _excelExport;
    private readonly IGerencialPdfExportService _pdfExport;
    private readonly ILogger<GerencialFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public GerencialFunctions(
        IGerencialService gerencialService,
        IGerencialChartGenerator chartGenerator,
        IGerencialExcelExportService excelExport,
        IGerencialPdfExportService pdfExport,
        ILogger<GerencialFunctions> logger)
    {
        _gerencialService = gerencialService;
        _chartGenerator = chartGenerator;
        _excelExport = excelExport;
        _pdfExport = pdfExport;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/reconciliation/gerencial?tipo=diario&amp;fecha=2026-04-12
    /// GET /api/reconciliation/gerencial?tipo=mensual&amp;mes=4&amp;anio=2026
    /// GET /api/reconciliation/gerencial?tipo=rango&amp;fecha_desde=2026-04-01&amp;fecha_hasta=2026-04-12
    /// Optional: &amp;departamento=La Paz
    /// </summary>
    [Function("GetGerencialReport")]
    public async Task<HttpResponseData> GetGerencialReportAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "reconciliation/gerencial")] HttpRequestData req)
    {
        var (fechaDesde, fechaHasta, tipo, idAgencia) = ParseGerencialParams(req);

        _logger.LogInformation(
            "[Gerencial] JSON request tipo={Tipo} desde={Desde} hasta={Hasta} agencia={Depto}",
            tipo, fechaDesde.ToString("yyyy-MM-dd"), fechaHasta.ToString("yyyy-MM-dd"),
        idAgencia);

        var report = await _gerencialService.GetReportAsync(fechaDesde, fechaHasta, tipo, idAgencia);
        return await WriteJsonResponse(req, report);
    }

    /// <summary>
    /// GET /api/reconciliation/gerencial/download?tipo=diario&amp;fecha=2026-04-12&amp;format=xlsx|pdf
    /// GET /api/reconciliation/gerencial/download?tipo=mensual&amp;mes=4&amp;anio=2026&amp;format=pdf
    /// GET /api/reconciliation/gerencial/download?tipo=rango&amp;fecha_desde=2026-04-01&amp;fecha_hasta=2026-04-12
    /// Optional: &amp;departamento=La Paz
    /// </summary>
    [Function("DownloadGerencialReport")]
    public async Task<HttpResponseData> DownloadGerencialReportAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "reconciliation/gerencial/download")] HttpRequestData req)
    {
        var (fechaDesde, fechaHasta, tipo, idAgencia) = ParseGerencialParams(req);
        var format = ParseFormat(req);

        _logger.LogInformation(
            "[Gerencial] Download request tipo={Tipo} desde={Desde} hasta={Hasta} depto={Depto} format={Format}",
            tipo, fechaDesde.ToString("yyyy-MM-dd"), fechaHasta.ToString("yyyy-MM-dd"),
            idAgencia, format);

        var report = await _gerencialService.GetReportAsync(fechaDesde, fechaHasta, tipo, idAgencia);

        // Generate chart images
        _logger.LogInformation("[Gerencial] Generating chart images...");
        var charts = new GerencialChartImages
        {
            RemittanceVolume = _chartGenerator.GenerateRemittanceVolume(report.Remesas.SerieTemporal),
            PaymentTypeDistribution = _chartGenerator.GeneratePaymentTypeDistribution(report.Remesas.PorTipoPago),
            ExchangeOperations = _chartGenerator.GenerateExchangeOperations(report.CambioMoneda.SerieTemporal),
            ExchangeDifferential = _chartGenerator.GenerateExchangeDifferential(report.CambioMoneda.SerieTemporal),
            ConsolidatedIncome = _chartGenerator.GenerateConsolidatedIncome(report.Consolidado.SerieIngresos),
            OperationsByDepartment = _chartGenerator.GenerateOperationsByDepartment(report.Consolidado.PorDepartamento)
        };

        byte[] fileBytes = format == "pdf"
            ? _pdfExport.GenerateGerencialReport(report, charts)
            : _excelExport.GenerateGerencialReport(report, charts);

        return WriteFileResponse(req, fileBytes, "Gerencial", fechaDesde, format);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static (DateTime fechaDesde, DateTime fechaHasta, string tipo, int? idAgencia)
        ParseGerencialParams(HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        var tipo = (query["tipo"] ?? "diario").ToLowerInvariant();
        var idAgencia = query["idAgencia"];

        DateTime fechaDesde, fechaHasta;

        switch (tipo)
        {
            case "mensual":
            {
                int anio = int.TryParse(query["anio"], out var a) ? a : DateTime.UtcNow.Year;
                int mes = int.TryParse(query["mes"], out var m) ? m : DateTime.UtcNow.Month;
                fechaDesde = new DateTime(anio, mes, 1);
                fechaHasta = fechaDesde.AddMonths(1).AddDays(-1);
                break;
            }
            case "rango":
            {
                fechaDesde = TryParseDate(query["fecha_desde"]) ?? DateTime.UtcNow.Date;
                fechaHasta = TryParseDate(query["fecha_hasta"]) ?? DateTime.UtcNow.Date;
                if (fechaHasta < fechaDesde) fechaHasta = fechaDesde;
                break;
            }
            default: // diario
            {
                tipo = "diario";
                var fecha = TryParseDate(query["date"]) ?? DateTime.UtcNow.Date;
                fechaDesde = fecha;
                fechaHasta = fecha;
                break;
            }
        }

        return (fechaDesde, fechaHasta, tipo, int.Parse(idAgencia ?? "0"));
    }

    private static DateTime? TryParseDate(string? value)
    {
        if (!string.IsNullOrEmpty(value)
            && DateTime.TryParseExact(value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;
        return null;
    }

    private static string ParseFormat(HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var format = query["format"]?.ToLowerInvariant();
        return format == "pdf" ? "pdf" : "xlsx";
    }

    private static async Task<HttpResponseData> WriteJsonResponse<T>(
        HttpRequestData req, T data)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(data, JsonOptions));
        return response;
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
