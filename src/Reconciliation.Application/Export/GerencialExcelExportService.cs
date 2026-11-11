using System.Reflection;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;

namespace Reconciliation.Application.Export;

public class GerencialExcelExportService : IGerencialExcelExportService
{
    private readonly ILogger<GerencialExcelExportService> _logger;

    // ── Corporate palette (same as ExcelExportService) ──────────────
    private static readonly XLColor Orange = XLColor.FromHtml("#E87722");
    private static readonly XLColor JetBlack = XLColor.FromHtml("#000000");
    private static readonly XLColor MediumGray = XLColor.FromHtml("#808184");
    private static readonly XLColor White = XLColor.FromHtml("#FFFFFF");
    private static readonly XLColor ZebraLight = XLColor.FromHtml("#FFF3E8");
    private static readonly XLColor TotalRowBg = XLColor.FromHtml("#F2F2F2");

    // Gambarte gerencial palette
    private static readonly XLColor GreenBg = XLColor.FromHtml("#E1F5EE");

    private const int HeaderRowOffset = 5;

    public GerencialExcelExportService(ILogger<GerencialExcelExportService> logger)
    {
        _logger = logger;
    }

    public byte[] GenerateGerencialReport(GerencialReport report, GerencialChartImages charts)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var workbook = new XLWorkbook();

        BuildExecutiveSummarySheet(workbook, report);
        BuildRemittancesSheet(workbook, report, charts);
        BuildExchangeSheet(workbook, report, charts);
        BuildConsolidatedSheet(workbook, report, charts);

        sw.Stop();
        _logger.LogInformation("[GerencialExcel] Generated in {Ms}ms", sw.ElapsedMilliseconds);
        return ToBytes(workbook);
    }

    // ═══ Sheet 1: Executive Summary ═════════════════════════════════

    private void BuildExecutiveSummarySheet(XLWorkbook wb, GerencialReport report)
    {
        var ws = wb.Worksheets.Add("Resumen Ejecutivo");
        int colCount = 6;
        var period = $"{report.FechaDesde:yyyy-MM-dd} a {report.FechaHasta:yyyy-MM-dd}";
        WriteMarquee(ws, $"Reporte Gerencial — {period}", report.FechaProcesamiento, colCount);

        // KPIs
        var kpiHeaders = new[] { "Indicador", "Remesas", "Cambio", "Total", "Var. %", "Periodo Ant." };
        WriteHeaders(ws, kpiHeaders);

        int row = HeaderRowOffset + 1;
        ws.Cell(row, 1).Value = "Ingresos BOB";
        ws.Cell(row, 2).Value = report.Remesas.TotalComisiones;
        ws.Cell(row, 3).Value = report.CambioMoneda.GananciaBob;
        ws.Cell(row, 4).Value = report.Consolidado.IngresoTotalBob;
        if (report.Consolidado.VariacionIngresoTotal.HasValue)
            ws.Cell(row, 5).Value = report.Consolidado.VariacionIngresoTotal.Value;
        row++;

        ws.Cell(row, 1).Value = "Operaciones";
        ws.Cell(row, 2).Value = report.Remesas.TotalTransacciones;
        ws.Cell(row, 3).Value = report.CambioMoneda.TotalOperaciones;
        ws.Cell(row, 4).Value = report.Consolidado.TotalOperaciones;
        row++;

        ws.Cell(row, 1).Value = "Agencias activas / total";
        ws.Cell(row, 4).Value = $"{report.Consolidado.AgenciasActivas} / {report.Consolidado.AgenciasTotales}";
        row += 2;

        // Alerts table
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Alertas Operacionales";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var alertHeaders = new[] { "Tipo de Alerta", "Cantidad", "Monto Total" };
        for (int i = 0; i < alertHeaders.Length; i++)
            ws.Cell(row, i + 1).Value = alertHeaders[i];
        StyleSubHeaderRow(ws, row, alertHeaders.Length);
        row++;

        foreach (var alert in report.Consolidado.Alertas)
        {
            ws.Cell(row, 1).Value = alert.TipoAlerta;
            ws.Cell(row, 2).Value = alert.Cantidad;
            ws.Cell(row, 3).Value = alert.MontoTotal;
            row++;
        }
        row++;

        // Active agencies by department
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Agencias Activas por Departamento";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var deptHeaders = new[] { "Departamento", "Agencias Totales", "Agencias Activas", "Total Operaciones", "Ingreso Total BOB" };
        for (int i = 0; i < deptHeaders.Length; i++)
            ws.Cell(row, i + 1).Value = deptHeaders[i];
        StyleSubHeaderRow(ws, row, deptHeaders.Length);
        row++;

        foreach (var dept in report.Consolidado.PorDepartamento)
        {
            ws.Cell(row, 1).Value = dept.Departamento;
            ws.Cell(row, 2).Value = report.Remesas.PorDepartamento
                .FirstOrDefault(d => d.Departamento == dept.Departamento)?.AgenciasTotales ?? 0;
            ws.Cell(row, 3).Value = report.Remesas.PorDepartamento
                .FirstOrDefault(d => d.Departamento == dept.Departamento)?.AgenciasActivas ?? 0;
            ws.Cell(row, 4).Value = dept.TotalOperaciones;
            ws.Cell(row, 5).Value = dept.IngresoTotalBob;
            row++;
        }

        int lastDataRow = row - 1;
        ApplyDecimalFormat(ws, lastDataRow, new[] { 2, 3, 4, 5 });
        ApplyZebraStripes(ws, lastDataRow, colCount);
        ApplyDataBorders(ws, lastDataRow, colCount);
        ws.Columns().AdjustToContents();
    }

    // ═══ Sheet 2: Remittances ═══════════════════════════════════════

    private void BuildRemittancesSheet(XLWorkbook wb, GerencialReport report, GerencialChartImages charts)
    {
        var ws = wb.Worksheets.Add("Remesas");
        int colCount = 10;
        WriteMarquee(ws, "Remesas — Detalle", report.FechaProcesamiento, colCount);

        int row = HeaderRowOffset;

        // General totals block
        ws.Cell(row, 1).Value = "Total BOB"; ws.Cell(row, 2).Value = report.Remesas.TotalBob;
        ws.Cell(row, 3).Value = "Total USD"; ws.Cell(row, 4).Value = report.Remesas.TotalUsd;
        ws.Cell(row, 5).Value = "Transacciones"; ws.Cell(row, 6).Value = report.Remesas.TotalTransacciones;
        ws.Cell(row, 7).Value = "Comisiones"; ws.Cell(row, 8).Value = report.Remesas.TotalComisiones;
        for (int c = 1; c <= 8; c++) ws.Cell(row, c).Style.Font.Bold = c % 2 == 1;
        row++;

        ws.Cell(row, 1).Value = "Ticket Prom. BOB"; ws.Cell(row, 2).Value = report.Remesas.TicketPromedioBob;
        ws.Cell(row, 3).Value = "Ticket Prom. USD"; ws.Cell(row, 4).Value = report.Remesas.TicketPromedioUsd;
        ws.Cell(row, 5).Value = "Var. %";
        if (report.Remesas.VariacionPorcentual.HasValue)
            ws.Cell(row, 6).Value = report.Remesas.VariacionPorcentual.Value;
        for (int c = 1; c <= 6; c++) ws.Cell(row, c).Style.Font.Bold = c % 2 == 1;
        row += 2;

        // Payment type breakdown
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Desglose por Tipo de Pago";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var ptHeaders = new[] { "Tipo", "Monto", "Transacciones" };
        for (int i = 0; i < ptHeaders.Length; i++) ws.Cell(row, i + 1).Value = ptHeaders[i];
        StyleSubHeaderRow(ws, row, ptHeaders.Length);
        row++;

        WritePaymentTypeRow(ws, row++, "Ventanilla BOB", report.Remesas.PorTipoPago.VentanillaBob);
        WritePaymentTypeRow(ws, row++, "Ventanilla USD", report.Remesas.PorTipoPago.VentanillaUsd);
        WritePaymentTypeRow(ws, row++, "Transferencia BOB", report.Remesas.PorTipoPago.TransferenciaBob);
        WritePaymentTypeRow(ws, row++, "Transferencia USD", report.Remesas.PorTipoPago.TransferenciaUsd);
        row++;

        // Sent vs Paid comparison
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Comparativa: Enviadas vs Pagadas";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var compHeaders = new[] { "Concepto", "BOB", "USD", "Transacciones" };
        for (int i = 0; i < compHeaders.Length; i++) ws.Cell(row, i + 1).Value = compHeaders[i];
        StyleSubHeaderRow(ws, row, compHeaders.Length);
        row++;

        ws.Cell(row, 1).Value = "Enviadas"; ws.Cell(row, 2).Value = report.Remesas.TotalBob;
        ws.Cell(row, 3).Value = report.Remesas.TotalUsd; ws.Cell(row, 4).Value = report.Remesas.TotalTransacciones;
        row++;
        ws.Cell(row, 1).Value = "Pagadas"; ws.Cell(row, 2).Value = report.Remesas.PagadasBob;
        ws.Cell(row, 3).Value = report.Remesas.PagadasUsd; ws.Cell(row, 4).Value = report.Remesas.PagadasTransacciones;
        row++;
        ws.Cell(row, 1).Value = "Diferencia"; ws.Cell(row, 2).Value = report.Remesas.TotalBob - report.Remesas.PagadasBob;
        ws.Cell(row, 3).Value = report.Remesas.TotalUsd - report.Remesas.PagadasUsd;
        ws.Cell(row, 4).Value = report.Remesas.TotalTransacciones - report.Remesas.PagadasTransacciones;
        ws.Cell(row, 1).Style.Font.Bold = true;
        row += 2;

        // Chart images
        row = TryInsertImage(ws, charts.RemittanceVolume, row, "Grafico: Remesas Enviadas vs Pagadas");
        row = TryInsertImage(ws, charts.PaymentTypeDistribution, row, "Grafico: Distribucion por Tipo de Pago");

        // Department detail table
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Detalle por Departamento";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var deptHeaders = new[] { "Departamento", "Ag. Total", "Ag. Activas", "Transacciones",
            "Monto BOB", "Monto USD", "Ventanilla", "Transferencia", "Comisiones", "Estado" };
        for (int i = 0; i < deptHeaders.Length; i++) ws.Cell(row, i + 1).Value = deptHeaders[i];
        StyleSubHeaderRow(ws, row, deptHeaders.Length);
        row++;

        foreach (var d in report.Remesas.PorDepartamento)
        {
            ws.Cell(row, 1).Value = d.Departamento;
            ws.Cell(row, 2).Value = d.AgenciasTotales;
            ws.Cell(row, 3).Value = d.AgenciasActivas;
            ws.Cell(row, 4).Value = d.Transacciones;
            ws.Cell(row, 5).Value = d.MontoBob;
            ws.Cell(row, 6).Value = d.MontoUsd;
            ws.Cell(row, 7).Value = d.TransaccionesVentanilla;
            ws.Cell(row, 8).Value = d.TransaccionesTransferencia;
            ws.Cell(row, 9).Value = d.Comisiones;
            ws.Cell(row, 10).Value = d.Estado;
            row++;
        }

        // Totals row
        WriteTotalRow(ws, row, 1, colCount, "TOTAL");
        ws.Cell(row, 4).Value = report.Remesas.TotalTransacciones;
        ws.Cell(row, 5).Value = report.Remesas.TotalBob;
        ws.Cell(row, 6).Value = report.Remesas.TotalUsd;
        ws.Cell(row, 9).Value = report.Remesas.TotalComisiones;
        row += 2;

        // Time series
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Serie Temporal";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var tsHeaders = new[] { "Periodo", "Enviadas BOB", "Enviadas USD", "Env. Txn",
            "Pagadas BOB", "Pagadas USD", "Pag. Txn" };
        for (int i = 0; i < tsHeaders.Length; i++) ws.Cell(row, i + 1).Value = tsHeaders[i];
        StyleSubHeaderRow(ws, row, tsHeaders.Length);
        row++;

        foreach (var ts in report.Remesas.SerieTemporal)
        {
            ws.Cell(row, 1).Value = ts.Periodo;
            ws.Cell(row, 2).Value = ts.MontoBob;
            ws.Cell(row, 3).Value = ts.MontoUsd;
            ws.Cell(row, 4).Value = ts.Transacciones;
            ws.Cell(row, 5).Value = ts.PagadasBob;
            ws.Cell(row, 6).Value = ts.PagadasUsd;
            ws.Cell(row, 7).Value = ts.PagadasTransacciones;
            row++;
        }

        ApplyDecimalFormat(ws, row - 1, new[] { 2, 5, 6, 8, 9 });
        ws.Columns().AdjustToContents();
    }

    // ═══ Sheet 3: Currency Exchange ═════════════════════════════════

    private void BuildExchangeSheet(XLWorkbook wb, GerencialReport report, GerencialChartImages charts)
    {
        var ws = wb.Worksheets.Add("Cambio de Moneda");
        int colCount = 6;
        WriteMarquee(ws, "Cambio de Moneda — Detalle", report.FechaProcesamiento, colCount);

        int row = HeaderRowOffset;

        // General totals
        ws.Cell(row, 1).Value = "Total Operaciones"; ws.Cell(row, 2).Value = report.CambioMoneda.TotalOperaciones;
        ws.Cell(row, 3).Value = "BOB Recibido"; ws.Cell(row, 4).Value = report.CambioMoneda.BobRecibido;
        ws.Cell(row, 5).Value = "BOB Entregado"; ws.Cell(row, 6).Value = report.CambioMoneda.BobEntregado;
        for (int c = 1; c <= 6; c++) ws.Cell(row, c).Style.Font.Bold = c % 2 == 1;
        row++;

        ws.Cell(row, 1).Value = "Ganancia BOB"; ws.Cell(row, 2).Value = report.CambioMoneda.GananciaBob;
        ws.Cell(row, 3).Value = "Var. %";
        if (report.CambioMoneda.VariacionPorcentual.HasValue)
            ws.Cell(row, 4).Value = report.CambioMoneda.VariacionPorcentual.Value;
        for (int c = 1; c <= 4; c++) ws.Cell(row, c).Style.Font.Bold = c % 2 == 1;
        // Highlight profit/loss
        ws.Cell(row, 2).Style.Font.FontColor = report.CambioMoneda.GananciaBob >= 0
            ? XLColor.FromHtml("#0F6E56") : XLColor.FromHtml("#CC0000");
        row += 2;

        // Chart images
        row = TryInsertImage(ws, charts.ExchangeOperations, row, "Grafico: Flujo BOB por Periodo");
        row = TryInsertImage(ws, charts.ExchangeDifferential, row, "Grafico: Ganancia/Perdida por Periodo");

        // Department detail
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Detalle por Departamento";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var deptHeaders = new[] { "Departamento", "Operaciones", "BOB Recibido",
            "BOB Entregado", "Ganancia BOB" };
        for (int i = 0; i < deptHeaders.Length; i++) ws.Cell(row, i + 1).Value = deptHeaders[i];
        StyleSubHeaderRow(ws, row, deptHeaders.Length);
        row++;

        foreach (var d in report.CambioMoneda.PorDepartamento)
        {
            ws.Cell(row, 1).Value = d.Departamento;
            ws.Cell(row, 2).Value = d.TotalOperaciones;
            ws.Cell(row, 3).Value = d.BobRecibido;
            ws.Cell(row, 4).Value = d.BobEntregado;
            ws.Cell(row, 5).Value = d.GananciaBob;
            row++;
        }

        WriteTotalRow(ws, row, 1, colCount, "TOTAL");
        ws.Cell(row, 2).Value = report.CambioMoneda.TotalOperaciones;
        ws.Cell(row, 3).Value = report.CambioMoneda.BobRecibido;
        ws.Cell(row, 4).Value = report.CambioMoneda.BobEntregado;
        ws.Cell(row, 5).Value = report.CambioMoneda.GananciaBob;
        row += 2;

        // Time series
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Serie Temporal";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var tsHeaders = new[] { "Periodo", "Operaciones", "BOB Recibido",
            "BOB Entregado", "Ganancia BOB" };
        for (int i = 0; i < tsHeaders.Length; i++) ws.Cell(row, i + 1).Value = tsHeaders[i];
        StyleSubHeaderRow(ws, row, tsHeaders.Length);
        row++;

        foreach (var ts in report.CambioMoneda.SerieTemporal)
        {
            ws.Cell(row, 1).Value = ts.Periodo;
            ws.Cell(row, 2).Value = ts.TotalOperaciones;
            ws.Cell(row, 3).Value = ts.BobRecibido;
            ws.Cell(row, 4).Value = ts.BobEntregado;
            ws.Cell(row, 5).Value = ts.GananciaBob;
            row++;
        }

        ApplyDecimalFormat(ws, row - 1, new[] { 3, 4, 5 });
        ws.Columns().AdjustToContents();
    }

    // ═══ Sheet 4: Consolidated ══════════════════════════════════════

    private void BuildConsolidatedSheet(XLWorkbook wb, GerencialReport report, GerencialChartImages charts)
    {
        var ws = wb.Worksheets.Add("Consolidado");
        int colCount = 8;
        WriteMarquee(ws, "Consolidado", report.FechaProcesamiento, colCount);

        int row = HeaderRowOffset;

        // Chart images
        row = TryInsertImage(ws, charts.ConsolidatedIncome, row, "Grafico: Ingresos Consolidados por Periodo");
        row = TryInsertImage(ws, charts.OperationsByDepartment, row, "Grafico: Operaciones por Departamento");

        // Consolidated department table
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Consolidado por Departamento";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var deptHeaders = new[] { "Departamento", "Txn Remesas", "Ops Cambio", "Total Ops",
            "Ingreso Remesas BOB", "Ingreso Cambio BOB", "Ingreso Total BOB" };
        for (int i = 0; i < deptHeaders.Length; i++) ws.Cell(row, i + 1).Value = deptHeaders[i];
        StyleSubHeaderRow(ws, row, deptHeaders.Length);
        row++;

        foreach (var d in report.Consolidado.PorDepartamento)
        {
            ws.Cell(row, 1).Value = d.Departamento;
            ws.Cell(row, 2).Value = d.TransaccionesRemesas;
            ws.Cell(row, 3).Value = d.OperacionesCambio;
            ws.Cell(row, 4).Value = d.TotalOperaciones;
            ws.Cell(row, 5).Value = d.IngresoRemesasBob;
            ws.Cell(row, 6).Value = d.IngresoCambioBob;
            ws.Cell(row, 7).Value = d.IngresoTotalBob;
            row++;
        }

        WriteTotalRow(ws, row, 1, colCount, "TOTAL");
        ws.Cell(row, 2).Value = report.Remesas.TotalTransacciones;
        ws.Cell(row, 3).Value = report.CambioMoneda.TotalOperaciones;
        ws.Cell(row, 4).Value = report.Consolidado.TotalOperaciones;
        ws.Cell(row, 5).Value = report.Consolidado.IngresoRemesasBob;
        ws.Cell(row, 6).Value = report.Consolidado.IngresoCambioBob;
        ws.Cell(row, 7).Value = report.Consolidado.IngresoTotalBob;
        row += 2;

        // Income series
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Serie de Ingresos por Periodo";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;

        var isHeaders = new[] { "Periodo", "Remesas BOB", "Cambio BOB", "Total BOB" };
        for (int i = 0; i < isHeaders.Length; i++) ws.Cell(row, i + 1).Value = isHeaders[i];
        StyleSubHeaderRow(ws, row, isHeaders.Length);
        row++;

        foreach (var s in report.Consolidado.SerieIngresos)
        {
            ws.Cell(row, 1).Value = s.Periodo;
            ws.Cell(row, 2).Value = s.RemesasBob;
            ws.Cell(row, 3).Value = s.CambioBob;
            ws.Cell(row, 4).Value = s.TotalBob;
            row++;
        }

        ApplyDecimalFormat(ws, row - 1, new[] { 2, 3, 4, 5, 6, 7 });
        ws.Columns().AdjustToContents();
    }

    // ═══ Helper methods (same patterns as ExcelExportService) ═══════

    private int TryInsertImage(IXLWorksheet ws, byte[] imageBytes, int row, string caption)
    {
        if (imageBytes.Length == 0) return row;

        try
        {
            ws.Cell(row, 1).Value = caption;
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Cell(row, 1).Style.Font.FontColor = MediumGray;
            row++;

            using var ms = new MemoryStream(imageBytes);
            ws.AddPicture(ms).MoveTo(ws.Cell(row, 1)).WithSize(680, 290);
            row += 16; // approximate rows for image height
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GerencialExcel] Failed to insert chart image, continuing without it");
            ws.Cell(row, 1).Value = "(Grafico no disponible)";
            row++;
        }

        return row;
    }

    private static void WritePaymentTypeRow(IXLWorksheet ws, int row, string label, TipoPagoDetalle detail)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 2).Value = detail.Monto;
        ws.Cell(row, 3).Value = detail.Transacciones;
    }

    private static void WriteMarquee(IXLWorksheet ws, string title, DateTime fecha, int colCount)
    {
        ws.Row(1).Height = 40;
        var logoStream = GetLogoStream();
        if (logoStream != null)
        {
            using (logoStream)
                ws.AddPicture(logoStream).MoveTo(ws.Cell(1, 1)).WithSize(120, 48);
        }

        ws.Range(1, 2, 1, colCount).Merge();
        var nameCell = ws.Cell(1, 2);
        nameCell.Value = "GAMBARTE S.R.L.";
        nameCell.Style.Font.Bold = true;
        nameCell.Style.Font.FontSize = 16;
        nameCell.Style.Font.FontColor = JetBlack;
        nameCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        nameCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        ws.Range(2, 1, 2, colCount).Merge();
        var sloganCell = ws.Cell(2, 1);
        sloganCell.Value = "Cambio de Divisas, Giros y Remesas";
        sloganCell.Style.Font.FontSize = 9;
        sloganCell.Style.Font.FontColor = MediumGray;
        sloganCell.Style.Font.Italic = true;
        sloganCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        ws.Range(3, 1, 3, colCount).Merge();
        var titleCell = ws.Cell(3, 1);
        titleCell.Value = title;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 13;
        titleCell.Style.Font.FontColor = White;
        titleCell.Style.Fill.BackgroundColor = Orange;
        titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(3).Height = 28;

        ws.Range(4, 1, 4, colCount).Merge();
        var dateCell = ws.Cell(4, 1);
        dateCell.Value = $"Fecha del reporte: {fecha:yyyy-MM-dd}    Generado: {DateTime.Now:yyyy-MM-dd HH:mm}";
        dateCell.Style.Font.FontSize = 9;
        dateCell.Style.Font.FontColor = MediumGray;
        dateCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void WriteHeaders(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(HeaderRowOffset, i + 1).Value = headers[i];
        StyleHeaderRow(ws, headers.Length);
    }

    private static void StyleHeaderRow(IXLWorksheet ws, int colCount)
    {
        var range = ws.Range(HeaderRowOffset, 1, HeaderRowOffset, colCount);
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 10;
        range.Style.Font.FontColor = White;
        range.Style.Fill.BackgroundColor = JetBlack;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        ws.Row(HeaderRowOffset).Height = 24;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        range.Style.Border.BottomBorderColor = Orange;
    }

    private static void StyleSubHeaderRow(IXLWorksheet ws, int row, int colCount)
    {
        var range = ws.Range(row, 1, row, colCount);
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 10;
        range.Style.Font.FontColor = White;
        range.Style.Fill.BackgroundColor = JetBlack;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        range.Style.Border.BottomBorderColor = Orange;
    }

    private static void ApplyDecimalFormat(IXLWorksheet ws, int lastRow, int[] columns)
    {
        if (lastRow < HeaderRowOffset + 1) return;
        foreach (var col in columns)
            ws.Range(HeaderRowOffset + 1, col, lastRow, col).Style.NumberFormat.Format = "#,##0.00";
    }

    private static void ApplyZebraStripes(IXLWorksheet ws, int lastRow, int colCount)
    {
        for (int r = HeaderRowOffset + 1; r <= lastRow; r++)
        {
            if ((r - HeaderRowOffset) % 2 == 0)
                ws.Range(r, 1, r, colCount).Style.Fill.BackgroundColor = ZebraLight;
        }
    }

    private static void ApplyDataBorders(IXLWorksheet ws, int lastRow, int colCount)
    {
        if (lastRow < HeaderRowOffset) return;
        var range = ws.Range(HeaderRowOffset, 1, lastRow, colCount);
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#D0D0D0");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        range.Style.Border.OutsideBorderColor = MediumGray;
    }

    private static void WriteTotalRow(IXLWorksheet ws, int row, int startCol, int colCount, string label)
    {
        var range = ws.Range(row, startCol, row, colCount);
        range.Style.Fill.BackgroundColor = TotalRowBg;
        range.Style.Font.Bold = true;
        range.Style.Border.TopBorder = XLBorderStyleValues.Double;
        range.Style.Border.TopBorderColor = Orange;
        ws.Cell(row, startCol).Value = label;
        ws.Cell(row, startCol).Style.Font.FontColor = JetBlack;
    }

    private static Stream? GetLogoStream()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));
        return name != null ? assembly.GetManifestResourceStream(name) : null;
    }

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
