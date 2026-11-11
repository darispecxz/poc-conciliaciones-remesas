using System.Reflection;
using ClosedXML.Excel;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;

namespace Reconciliation.Application.Export;

public class ExcelExportService : IExcelExportService
{
    // ── Corporate palette ───────────────────────────────────────
    private static readonly XLColor Orange = XLColor.FromHtml("#E87722");
    private static readonly XLColor JetBlack = XLColor.FromHtml("#000000");
    private static readonly XLColor MediumGray = XLColor.FromHtml("#808184");
    private static readonly XLColor White = XLColor.FromHtml("#FFFFFF");
    private static readonly XLColor ZebraLight = XLColor.FromHtml("#FFF3E8");   // very soft orange tint
    private static readonly XLColor MissingBg = XLColor.FromHtml("#FDE8D0");    // warm peach for missing data
    private static readonly XLColor TotalRowBg = XLColor.FromHtml("#F2F2F2");

    private const int HeaderRowOffset = 5; // data headers start at row 5 (rows 1-4 = marquee)

    public byte[] GenerateCashierReconciliation(CashierReconciliationReport report)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Arqueo de Caja");

        var headers = new[]
        {
            "Cajero", "Agencia", "Moneda", "Num Caja",
            "Apertura Caja",
            "# Cambio Ing", "Cambio Moneda Ing",
            "# Envio Giro", "Envio Giro",
            "# Envio Remesa", "Envio Remesa",
            "# Traspaso De Caja", "Traspaso De Caja",
            "# Cobro Cheque", "Cobro Cheque",
            "# Ingreso Controlado", "Ingreso Controlado",
            "# Trans Boveda Ing", "Trans Boveda Ing",
            "# Cambio Egr", "Cambio Moneda Egr",
            "# Pago Giro", "Pago Giro",
            "Devolucion Giro",
            "# Pago Remesa", "Pago Remesa",
            "Devolucion Remesa",
            "# Traspaso A Caja", "Traspaso A Caja",
            "# Deposito Bancario", "Deposito Bancario",
            "# Egreso Controlado", "Egreso Controlado",
            "# Trans Boveda Egr", "Trans Boveda Egr",
            "Total Ingreso", "Total Egreso", "Total Saldo",
            "Cierre Caja", "Diferencia"
        };

        int colCount = headers.Length;
        WriteMarquee(ws, "Reporte de Arqueos de Caja Consolidado Diario", report.Fecha, colCount);
        WriteHeaders(ws, headers);

        int row = HeaderRowOffset + 1;
        foreach (var cashier in report.Cashiers)
        {
            foreach (var c in cashier.Currencies)
            {
                ws.Cell(row, 1).Value = cashier.Cajero;
                ws.Cell(row, 2).Value = cashier.Agencia;
                ws.Cell(row, 3).Value = c.Moneda;
                ws.Cell(row, 4).Value = c.NumCaja;
                ws.Cell(row, 5).Value = c.AperturaCaja;
                ws.Cell(row, 6).Value = c.CambioMonedaIngreso.Count;
                ws.Cell(row, 7).Value = c.CambioMonedaIngreso.Amount;
                ws.Cell(row, 8).Value = c.EnvioGiro.Count;
                ws.Cell(row, 9).Value = c.EnvioGiro.Amount;
                ws.Cell(row, 10).Value = c.EnvioRemesa.Count;
                ws.Cell(row, 11).Value = c.EnvioRemesa.Amount;
                ws.Cell(row, 12).Value = c.TraspasoDeCaja.Count;
                ws.Cell(row, 13).Value = c.TraspasoDeCaja.Amount;
                ws.Cell(row, 14).Value = c.CobroCheque.Count;
                ws.Cell(row, 15).Value = c.CobroCheque.Amount;
                ws.Cell(row, 16).Value = c.IngresoControlado.Count;
                ws.Cell(row, 17).Value = c.IngresoControlado.Amount;
                ws.Cell(row, 18).Value = c.TransferenciaBovedaIngreso.Count;
                ws.Cell(row, 19).Value = c.TransferenciaBovedaIngreso.Amount;
                ws.Cell(row, 20).Value = c.CambioMonedaEgreso.Count;
                ws.Cell(row, 21).Value = c.CambioMonedaEgreso.Amount;
                ws.Cell(row, 22).Value = c.PagoGiro.Count;
                ws.Cell(row, 23).Value = c.PagoGiro.Amount;
                ws.Cell(row, 24).Value = c.DevolucionGiro;
                ws.Cell(row, 25).Value = c.PagoRemesa.Count;
                ws.Cell(row, 26).Value = c.PagoRemesa.Amount;
                ws.Cell(row, 27).Value = c.DevolucionRemesa;
                ws.Cell(row, 28).Value = c.TraspasoACaja.Count;
                ws.Cell(row, 29).Value = c.TraspasoACaja.Amount;
                ws.Cell(row, 30).Value = c.DepositoBancario.Count;
                ws.Cell(row, 31).Value = c.DepositoBancario.Amount;
                ws.Cell(row, 32).Value = c.EgresoControlado.Count;
                ws.Cell(row, 33).Value = c.EgresoControlado.Amount;
                ws.Cell(row, 34).Value = c.TransferenciaBovedaEgreso.Count;
                ws.Cell(row, 35).Value = c.TransferenciaBovedaEgreso.Amount;
                ws.Cell(row, 36).Value = c.TotalIngreso;
                ws.Cell(row, 37).Value = c.TotalEgreso;
                ws.Cell(row, 38).Value = c.TotalSaldo;
                ws.Cell(row, 39).Value = c.CierreCaja;
                ws.Cell(row, 40).Value = c.Diferencia;
                row++;
            }
        }

        int lastDataRow = row - 1;
        var decimalCols = new[] { 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 24, 26, 27, 29, 31, 33, 35, 36, 37, 38, 39, 40 };
        ApplyDecimalFormat(ws, lastDataRow, decimalCols);
        ApplyZebraStripes(ws, lastDataRow, colCount);
        HighlightMissingCells(ws, lastDataRow, colCount);
        ApplyDataBorders(ws, lastDataRow, colCount);
        ws.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public byte[] GenerateVaultReconciliation(VaultReconciliationReport report)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Boveda");

        var headers = new[]
        {
            "Agencia", "Boveda", "Moneda",
            "Saldo Apertura",
            "Salida Apertura Caja", "Salida Traspaso Boveda", "Salida Traspaso Caja", "Total Egresos",
            "Entrada Cierre Caja", "Entrada Traspaso Boveda", "Entrada Traspaso Casa", "Total Ingresos",
            "Saldo Final"
        };

        int colCount = headers.Length;
        WriteMarquee(ws, "Conciliación de Bóveda", report.Fecha, colCount);
        WriteHeaders(ws, headers);

        int row = HeaderRowOffset + 1;
        foreach (var agency in report.Agencies)
        {
            foreach (var m in agency.Movimientos)
            {
                ws.Cell(row, 1).Value = agency.Agencia;
                ws.Cell(row, 2).Value = m.NumBoveda;
                ws.Cell(row, 3).Value = m.Moneda;
                ws.Cell(row, 4).Value = m.SaldoApertura;
                ws.Cell(row, 5).Value = m.SalidaAperturaCaja;
                ws.Cell(row, 6).Value = m.SalidaTraspasoBoveda;
                ws.Cell(row, 7).Value = m.SalidaTraspasoCaja;
                ws.Cell(row, 8).Value = m.TotalEgresos;
                ws.Cell(row, 9).Value = m.EntradaCierreCaja;
                ws.Cell(row, 10).Value = m.EntradaTraspasoBoveda;
                ws.Cell(row, 11).Value = m.EntradaTraspasoCasa;
                ws.Cell(row, 12).Value = m.TotalIngresos;
                ws.Cell(row, 13).Value = m.SaldoFinal;
                row++;
            }
        }

        int lastDataRow = row - 1;
        ApplyDecimalFormat(ws, lastDataRow, new[] { 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 });
        ApplyZebraStripes(ws, lastDataRow, colCount);
        HighlightMissingCells(ws, lastDataRow, colCount);
        ApplyDataBorders(ws, lastDataRow, colCount);
        ws.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public byte[] GenerateVaultAvailability(VaultAvailabilityReport report)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Disponibilidad Boveda");

        var headers = new[] { "Agencia", "Moneda", "Saldo", "Tipo Cambio", "Saldo BOB" };
        int colCount = headers.Length;
        WriteMarquee(ws, "Reporte de Disponibilidad en Bóveda", report.Fecha, colCount);
        WriteHeaders(ws, headers);

        int row = HeaderRowOffset + 1;
        foreach (var e in report.Entries)
        {
            ws.Cell(row, 1).Value = e.Agencia;
            ws.Cell(row, 2).Value = e.Moneda;
            ws.Cell(row, 3).Value = e.Saldo;
            ws.Cell(row, 4).Value = e.TipoCambioOficial;
            ws.Cell(row, 5).Value = e.SaldoBob;
            row++;
        }

        int lastDataRow = row - 1;

        // Total row
        WriteTotalRow(ws, row, 1, colCount, "TOTAL");
        ws.Cell(row, 5).Value = report.TotalSaldoBob;
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
        int lastRow = row;

        ApplyDecimalFormat(ws, lastDataRow, new[] { 3, 4, 5 });
        ApplyZebraStripes(ws, lastDataRow, colCount);
        HighlightMissingCells(ws, lastDataRow, colCount);
        ApplyDataBorders(ws, lastRow, colCount);
        ws.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public byte[] GenerateDailySummary(DailySummaryReport report)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Cuadre Diario");

        var headers = new[]
        {
            "Moneda", "Total Cajas", "Total Boveda", "Total Cajas+Boveda",
            "Tipo Cambio", "Monedas en BOB", "Contabilidad", "Diferencia"
        };
        int colCount = headers.Length;
        WriteMarquee(ws, "Cuadre Diario de Cajas", report.Fecha, colCount);
        WriteHeaders(ws, headers);

        int row = HeaderRowOffset + 1;
        foreach (var c in report.Currencies)
        {
            ws.Cell(row, 1).Value = c.Moneda;
            ws.Cell(row, 2).Value = c.TotalCajas;
            ws.Cell(row, 3).Value = c.TotalBoveda;
            ws.Cell(row, 4).Value = c.TotalCajasBoveda;
            ws.Cell(row, 5).Value = c.TipoCambioOficial;
            ws.Cell(row, 6).Value = c.MonedasEnBob;
            if (c.Contabilidad.HasValue)
                ws.Cell(row, 7).Value = c.Contabilidad.Value;
            if (c.Diferencia.HasValue)
                ws.Cell(row, 8).Value = c.Diferencia.Value;
            row++;
        }

        int lastDataRow = row - 1;

        // Grand total row
        WriteTotalRow(ws, row, 1, colCount, "TOTAL");
        ws.Cell(row, 6).Value = report.GrandTotalBob;
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        int lastRow = row;

        ApplyDecimalFormat(ws, lastDataRow, new[] { 2, 3, 4, 5, 6, 7, 8 });
        ApplyZebraStripes(ws, lastDataRow, colCount);
        HighlightMissingCells(ws, lastDataRow, colCount);
        ApplyDataBorders(ws, lastRow, colCount);
        ws.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public byte[] GenerateAgencyClosings(AgencyClosingReport report)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Cierres por Agencia");

        var currencies = report.Agencies
            .SelectMany(a => a.CurrencyClosings.Keys)
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        int colCount = 1 + currencies.Count;
        WriteMarquee(ws, "Cierres por Agencia", report.Fecha, colCount);

        // Custom header row (dynamic columns)
        var headerRow = ws.Row(HeaderRowOffset);
        var headerCell = ws.Cell(HeaderRowOffset, 1);
        headerCell.Value = "Agencia";
        for (int i = 0; i < currencies.Count; i++)
            ws.Cell(HeaderRowOffset, i + 2).Value = currencies[i];
        StyleHeaderRow(ws, colCount);

        int row = HeaderRowOffset + 1;
        foreach (var agency in report.Agencies)
        {
            ws.Cell(row, 1).Value = agency.Agencia;
            for (int i = 0; i < currencies.Count; i++)
            {
                if (agency.CurrencyClosings.TryGetValue(currencies[i], out var val))
                    ws.Cell(row, i + 2).Value = val;
            }
            row++;
        }

        int lastDataRow = row - 1;

        // Totals row
        WriteTotalRow(ws, row, 1, colCount, "TOTAL");
        for (int i = 0; i < currencies.Count; i++)
        {
            if (report.Totals.TryGetValue(currencies[i], out var total))
            {
                ws.Cell(row, i + 2).Value = total;
                ws.Cell(row, i + 2).Style.Font.Bold = true;
                ws.Cell(row, i + 2).Style.NumberFormat.Format = "#,##0.00";
            }
        }
        int lastRow = row;

        var decimalCols = Enumerable.Range(2, currencies.Count).ToArray();
        ApplyDecimalFormat(ws, lastDataRow, decimalCols);
        ApplyZebraStripes(ws, lastDataRow, colCount);
        HighlightMissingCells(ws, lastDataRow, colCount);
        ApplyDataBorders(ws, lastRow, colCount);
        ws.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public byte[] GenerateMovementSummary(MovementSummaryReport report)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Resumen Movimientos");

        var headers = new[]
        {
            "Moneda", "Apertura Caja", "Cambio Moneda", "Envio Giro", "Pago Giro",
            "Envio Remesa", "Pago Remesa", "Egresos", "Transferencias",
            "Fallas Caja", "Cierre Caja"
        };
        int colCount = headers.Length;
        WriteMarquee(ws, "Resumen de Movimientos de Caja", report.Fecha, colCount);
        WriteHeaders(ws, headers);

        int row = HeaderRowOffset + 1;
        foreach (var c in report.Currencies)
        {
            ws.Cell(row, 1).Value = c.Moneda;
            ws.Cell(row, 2).Value = c.AperturaCaja;
            ws.Cell(row, 3).Value = c.CambioMoneda;
            ws.Cell(row, 4).Value = c.EnvioGiro;
            ws.Cell(row, 5).Value = c.PagoGiro;
            ws.Cell(row, 6).Value = c.EnvioRemesa;
            ws.Cell(row, 7).Value = c.PagoRemesa;
            ws.Cell(row, 8).Value = c.Egresos;
            ws.Cell(row, 9).Value = c.Transferencias;
            ws.Cell(row, 10).Value = c.FallasCaja;
            ws.Cell(row, 11).Value = c.CierreCaja;
            row++;
        }

        int lastDataRow = row - 1;
        ApplyDecimalFormat(ws, lastDataRow, new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 });
        ApplyZebraStripes(ws, lastDataRow, colCount);
        HighlightMissingCells(ws, lastDataRow, colCount);
        ApplyDataBorders(ws, lastDataRow, colCount);
        ws.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public byte[] GenerateRemittanceDailyOperations(RemittanceDailyOperationsReport report)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Operaciones Diarias");

        var headers = new[] { "Direccion", "Moneda", "Cantidad", "Monto Total", "Estado" };
        int colCount = headers.Length;
        WriteMarquee(ws, $"Operaciones Diarias de Remesas — {report.Fecha:yyyy-MM-dd}", report.Fecha, colCount);
        WriteHeaders(ws, headers);

        // Summary line
        ws.Range(HeaderRowOffset - 1, 1, HeaderRowOffset - 1, colCount).Merge();
        var summaryCell = ws.Cell(HeaderRowOffset - 1, 1);
        summaryCell.Value = $"Enviadas: {report.Summary.TotalSentToday} ({report.Summary.TotalSentAmountToday:#,##0.00})  |  " +
                            $"Anuladas: {report.Summary.TotalCancelledToday} ({report.Summary.TotalCancelledAmountToday:#,##0.00})  |  " +
                            $"Pagadas: {report.Summary.TotalPaidToday} ({report.Summary.TotalPaidAmountToday:#,##0.00})  |  " +
                            $"Pend. Bolivia: {report.Summary.TotalPendingBolivia} ({report.Summary.TotalPendingAmountBolivia:#,##0.00})  |  " +
                            $"Pend. Chile: {report.Summary.TotalPendingChile} ({report.Summary.TotalPendingAmountChile:#,##0.00})";
        summaryCell.Style.Font.FontSize = 10;
        summaryCell.Style.Font.Bold = true;

        int row = HeaderRowOffset + 1;

        // Sub-section: Remesas enviadas hoy
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Remesas enviadas hoy";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;
        foreach (var s in report.SentToday)
        {
            ws.Cell(row, 1).Value = s.Direction;
            ws.Cell(row, 2).Value = s.Moneda;
            ws.Cell(row, 3).Value = s.Count;
            ws.Cell(row, 4).Value = s.TotalAmount;
            ws.Cell(row, 5).Value = s.Status;
            row++;
        }

        // Sub-section: Remesas anuladas hoy
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Remesas anuladas hoy";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;
        foreach (var s in report.CancelledToday)
        {
            ws.Cell(row, 1).Value = s.Direction;
            ws.Cell(row, 2).Value = s.Moneda;
            ws.Cell(row, 3).Value = s.Count;
            ws.Cell(row, 4).Value = s.TotalAmount;
            ws.Cell(row, 5).Value = s.Status;
            row++;
        }

        // Sub-section: Remesas pagadas hoy
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Remesas pagadas hoy";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;
        foreach (var s in report.PaidToday)
        {
            ws.Cell(row, 1).Value = s.Direction;
            ws.Cell(row, 2).Value = s.Moneda;
            ws.Cell(row, 3).Value = s.Count;
            ws.Cell(row, 4).Value = s.TotalAmount;
            ws.Cell(row, 5).Value = s.Status;
            row++;
        }

        // Sub-section: Pagos pendientes en Bolivia (acumulado)
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Pagos pendientes en Bolivia (acumulado)";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;
        foreach (var s in report.PendingInBolivia)
        {
            ws.Cell(row, 1).Value = "CHL \u2192 BOL";
            ws.Cell(row, 2).Value = s.Moneda;
            ws.Cell(row, 3).Value = s.Count;
            ws.Cell(row, 4).Value = s.TotalAmount;
            row++;
        }

        // Sub-section: Pagos pendientes en Chile (acumulado)
        ws.Range(row, 1, row, colCount).Merge();
        ws.Cell(row, 1).Value = "Pagos pendientes en Chile (acumulado)";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
        row++;
        foreach (var s in report.PendingInChile)
        {
            ws.Cell(row, 1).Value = "BOL \u2192 CHL";
            ws.Cell(row, 2).Value = s.Moneda;
            ws.Cell(row, 3).Value = s.Count;
            ws.Cell(row, 4).Value = s.TotalAmount;
            row++;
        }

        int lastDataRow = row - 1;
        ApplyDecimalFormat(ws, lastDataRow, new[] { 4 });
        ApplyDataBorders(ws, lastDataRow, colCount);
        ws.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public byte[] GenerateRemittanceAccountingValidation(RemittanceAccountingValidationReport report)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Validacion Contable");
        var validBg = XLColor.FromHtml("#E8F5E9");
        var errorBg = XLColor.FromHtml("#FFEBEE");

        var headers = new[]
        {
            "Codigo CGR", "Direccion", "Estado Remesa", "Tipo Validacion",
            "Monto Esperado", "Monto Contable", "Concepto Esperado",
            "Concepto Real", "Valido", "Error"
        };
        int colCount = headers.Length;
        WriteMarquee(ws, $"Validacion Contable de Remesas — {report.Mes:D2}/{report.Anio}", report.Fecha, colCount);
        WriteHeaders(ws, headers);

        // Summary
        ws.Range(HeaderRowOffset - 1, 1, HeaderRowOffset - 1, colCount).Merge();
        var summaryCell = ws.Cell(HeaderRowOffset - 1, 1);
        summaryCell.Value = $"Total validados: {report.AccountingValidation.TotalValidated}  |  " +
                            $"Correctos: {report.AccountingValidation.TotalValid}  |  " +
                            $"Errores: {report.AccountingValidation.TotalErrors}";
        summaryCell.Style.Font.FontSize = 10;
        summaryCell.Style.Font.Bold = true;

        int row = HeaderRowOffset + 1;
        foreach (var e in report.AccountingValidation.Entries)
        {
            ws.Cell(row, 1).Value = e.Codigo;
            ws.Cell(row, 2).Value = e.Direction;
            ws.Cell(row, 3).Value = e.RemittanceStatus;
            ws.Cell(row, 4).Value = e.ValidationType.ToString();
            ws.Cell(row, 5).Value = e.ExpectedAmount;
            if (e.AccountingAmount.HasValue)
                ws.Cell(row, 6).Value = e.AccountingAmount.Value;
            ws.Cell(row, 7).Value = e.ExpectedConcepto;
            ws.Cell(row, 8).Value = e.ActualConcepto ?? "";
            ws.Cell(row, 9).Value = e.IsValid ? "SI" : "NO";
            ws.Cell(row, 10).Value = e.ErrorMessage ?? "";

            // Color-code rows
            var rowBg = e.IsValid ? validBg : errorBg;
            ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = rowBg;
            row++;
        }

        int lastDataRow = row - 1;
        ApplyDecimalFormat(ws, lastDataRow, new[] { 5, 6 });
        ApplyDataBorders(ws, lastDataRow, colCount);
        ws.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public byte[] GenerateRemittanceGeneralReconciliation(RemittanceGeneralReconciliationReport report)
    {
        using var workbook = new XLWorkbook();

        // ── Worksheet 1: Conciliacion General ─────────────────────
        {
            var ws = workbook.Worksheets.Add("Conciliacion General");
            int colCount = 19;
            WriteMarquee(ws, $"Conciliacion General de Remesas — {report.Mes:D2}/{report.Anio}", report.Fecha, colCount);

            // Multi-level headers — Row 5 (HeaderRowOffset), Row 6, Row 7
            // Row 5: top-level groups
            ws.Range(HeaderRowOffset, 1, HeaderRowOffset + 2, 1).Merge();
            ws.Cell(HeaderRowOffset, 1).Value = "FECHA";
            ws.Cell(HeaderRowOffset, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(HeaderRowOffset, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            ws.Range(HeaderRowOffset, 2, HeaderRowOffset, 9).Merge();
            ws.Cell(HeaderRowOffset, 2).Value = "REMESAS ORIGEN CHILE - DESTINO BOLIVIA";
            ws.Cell(HeaderRowOffset, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(HeaderRowOffset, 10, HeaderRowOffset, 17).Merge();
            ws.Cell(HeaderRowOffset, 10).Value = "REMESAS ORIGEN BOLIVIA - DESTINO CHILE";
            ws.Cell(HeaderRowOffset, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(HeaderRowOffset, 18, HeaderRowOffset, 19).Merge();
            ws.Cell(HeaderRowOffset, 18).Value = "SALDO";
            ws.Cell(HeaderRowOffset, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Row 6: sub-groups
            int r6 = HeaderRowOffset + 1;
            ws.Range(r6, 2, r6, 3).Merge(); ws.Cell(r6, 2).Value = "Recibidas";
            ws.Range(r6, 4, r6, 5).Merge(); ws.Cell(r6, 4).Value = "Anuladas";
            ws.Range(r6, 6, r6, 7).Merge(); ws.Cell(r6, 6).Value = "Pendientes";
            ws.Range(r6, 8, r6, 9).Merge(); ws.Cell(r6, 8).Value = "Pagadas";
            ws.Range(r6, 10, r6, 11).Merge(); ws.Cell(r6, 10).Value = "Enviadas";
            ws.Range(r6, 12, r6, 13).Merge(); ws.Cell(r6, 12).Value = "Anuladas";
            ws.Range(r6, 14, r6, 15).Merge(); ws.Cell(r6, 14).Value = "Pendientes";
            ws.Range(r6, 16, r6, 17).Merge(); ws.Cell(r6, 16).Value = "Pagadas";
            ws.Cell(r6, 18).Value = "Favor/Contra";
            ws.Cell(r6, 19).Value = "Acumulado";
            for (int c = 2; c <= 19; c++)
                ws.Cell(r6, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Row 7: CANT. / IMPORTE alternating
            int r7 = HeaderRowOffset + 2;
            for (int c = 2; c <= 17; c++)
            {
                ws.Cell(r7, c).Value = (c % 2 == 0) ? "CANT." : "IMPORTE";
                ws.Cell(r7, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            ws.Cell(r7, 18).Value = "IMPORTE";
            ws.Cell(r7, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r7, 19).Value = "IMPORTE";
            ws.Cell(r7, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Style all 3 header rows
            for (int hr = HeaderRowOffset; hr <= HeaderRowOffset + 2; hr++)
            {
                var range = ws.Range(hr, 1, hr, colCount);
                range.Style.Font.Bold = true;
                range.Style.Font.FontSize = 10;
                range.Style.Font.FontColor = White;
                range.Style.Fill.BackgroundColor = JetBlack;
                range.Style.Alignment.WrapText = true;
                range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                range.Style.Border.BottomBorderColor = Orange;
            }

            // Row 8: Carry-forward row
            int dataStartRow = HeaderRowOffset + 3;
            var cf = report.DailyMatrix.CarryForward;
            ws.Cell(dataStartRow, 1).Value = "Arrastre";
            ws.Cell(dataStartRow, 6).Value = cf.ChlToBolPendingCount;
            ws.Cell(dataStartRow, 7).Value = cf.ChlToBolPendingAmount;
            ws.Cell(dataStartRow, 14).Value = cf.BolToChlPendingCount;
            ws.Cell(dataStartRow, 15).Value = cf.BolToChlPendingAmount;
            ws.Cell(dataStartRow, 19).Value = cf.SaldoAcumulado;
            var cfRange = ws.Range(dataStartRow, 1, dataStartRow, colCount);
            cfRange.Style.Font.Bold = true;
            cfRange.Style.Fill.BackgroundColor = TotalRowBg;

            // Row 9+: Daily data rows
            int row = dataStartRow + 1;
            foreach (var day in report.DailyMatrix.DailyRows)
            {
                ws.Cell(row, 1).Value = day.Fecha.ToString("dd");
                ws.Cell(row, 2).Value = day.ChlToBolRecibidas.Count;
                ws.Cell(row, 3).Value = day.ChlToBolRecibidas.TotalAmount;
                ws.Cell(row, 4).Value = day.ChlToBolAnuladas.Count;
                ws.Cell(row, 5).Value = day.ChlToBolAnuladas.TotalAmount;
                ws.Cell(row, 6).Value = day.ChlToBolPendientes.Count;
                ws.Cell(row, 7).Value = day.ChlToBolPendientes.TotalAmount;
                ws.Cell(row, 8).Value = day.ChlToBolPagadas.Count;
                ws.Cell(row, 9).Value = day.ChlToBolPagadas.TotalAmount;
                ws.Cell(row, 10).Value = day.BolToChlEnviadas.Count;
                ws.Cell(row, 11).Value = day.BolToChlEnviadas.TotalAmount;
                ws.Cell(row, 12).Value = day.BolToChlAnuladas.Count;
                ws.Cell(row, 13).Value = day.BolToChlAnuladas.TotalAmount;
                ws.Cell(row, 14).Value = day.BolToChlPendientes.Count;
                ws.Cell(row, 15).Value = day.BolToChlPendientes.TotalAmount;
                ws.Cell(row, 16).Value = day.BolToChlPagadas.Count;
                ws.Cell(row, 17).Value = day.BolToChlPagadas.TotalAmount;
                ws.Cell(row, 18).Value = day.SaldoFavorContra;
                ws.Cell(row, 19).Value = day.SaldoAcumulado;
                row++;
            }

            // Totals row
            var totals = report.DailyMatrix.Totals;
            WriteTotalRow(ws, row, 1, colCount, "TOTALES");
            ws.Cell(row, 2).Value = totals.ChlToBolRecibidas.Count;
            ws.Cell(row, 3).Value = totals.ChlToBolRecibidas.TotalAmount;
            ws.Cell(row, 4).Value = totals.ChlToBolAnuladas.Count;
            ws.Cell(row, 5).Value = totals.ChlToBolAnuladas.TotalAmount;
            ws.Cell(row, 8).Value = totals.ChlToBolPagadas.Count;
            ws.Cell(row, 9).Value = totals.ChlToBolPagadas.TotalAmount;
            ws.Cell(row, 10).Value = totals.BolToChlEnviadas.Count;
            ws.Cell(row, 11).Value = totals.BolToChlEnviadas.TotalAmount;
            ws.Cell(row, 12).Value = totals.BolToChlAnuladas.Count;
            ws.Cell(row, 13).Value = totals.BolToChlAnuladas.TotalAmount;
            ws.Cell(row, 16).Value = totals.BolToChlPagadas.Count;
            ws.Cell(row, 17).Value = totals.BolToChlPagadas.TotalAmount;
            ws.Cell(row, 18).Value = totals.SaldoFavorContraTotal;

            int lastDataRow = row;
            ApplyDecimalFormat(ws, lastDataRow, new[] { 3, 5, 7, 9, 11, 13, 15, 17, 18, 19 });
            ApplyZebraStripes(ws, row - 1, colCount);
            ApplyDataBorders(ws, lastDataRow, colCount);
            ws.Columns().AdjustToContents();
        }

        // ── Worksheet 2: Validacion de Pagos ───────────────────────
        {
            var ws = workbook.Worksheets.Add("Validacion de Pagos");
            var validBg = XLColor.FromHtml("#E8F5E9");
            var errorBg = XLColor.FromHtml("#FFEBEE");
            var warnBg = XLColor.FromHtml("#FFF8E1");

            var headers = new[]
            {
                "Direccion", "Codigo CGR", "Moneda", "Fecha Remesa", "Monto Envio",
                "Pago Monto", "Pago T/C", "Pago Total", "Pago Comision",
                "Fecha Pago", "Monto OK", "T/C OK", "Total OK", "Comision OK",
                "Estado", "Error"
            };
            int colCount = headers.Length;
            WriteMarquee(ws, $"Validacion de Pagos — {report.Mes:D2}/{report.Anio}", report.Fecha, colCount);
            WriteHeaders(ws, headers);

            // Summary row
            ws.Range(HeaderRowOffset - 1, 1, HeaderRowOffset - 1, colCount).Merge();
            var summaryCell = ws.Cell(HeaderRowOffset - 1, 1);
            summaryCell.Value = $"Total remesas: {report.PaymentValidation.TotalRemittances}  |  " +
                                $"Con pago: {report.PaymentValidation.TotalWithPayment}  |  " +
                                $"Sin pago: {report.PaymentValidation.TotalMissing}  |  " +
                                $"Duplicados: {report.PaymentValidation.TotalDuplicates}  |  " +
                                $"Errores campo: {report.PaymentValidation.TotalFieldErrors}  |  " +
                                $"Validos: {report.PaymentValidation.TotalValid}";
            summaryCell.Style.Font.FontSize = 10;
            summaryCell.Style.Font.Bold = true;

            int row = HeaderRowOffset + 1;
            foreach (var e in report.PaymentValidation.Entries)
            {
                ws.Cell(row, 1).Value = e.Direction;
                ws.Cell(row, 2).Value = e.Codigo;
                ws.Cell(row, 3).Value = e.Moneda;
                ws.Cell(row, 4).Value = e.RemittanceFecha.ToString("yyyy-MM-dd");
                ws.Cell(row, 5).Value = e.RemittanceEnvio;
                if (e.PagoMonto.HasValue) ws.Cell(row, 6).Value = e.PagoMonto.Value;
                if (e.PagoTipoCambio.HasValue) ws.Cell(row, 7).Value = e.PagoTipoCambio.Value;
                if (e.PagoTotal.HasValue) ws.Cell(row, 8).Value = e.PagoTotal.Value;
                if (e.PagoComisionBob.HasValue) ws.Cell(row, 9).Value = e.PagoComisionBob.Value;
                if (e.PagoFecCrea.HasValue) ws.Cell(row, 10).Value = e.PagoFecCrea.Value.ToString("yyyy-MM-dd");
                ws.Cell(row, 11).Value = e.Status == DTOs.PaymentValidationStatus.Missing ? "\u2014" : (e.MontoMatch ? "SI" : "NO");
                ws.Cell(row, 12).Value = e.Status == DTOs.PaymentValidationStatus.Missing ? "\u2014" : (e.TipoCambioMatch ? "SI" : "NO");
                ws.Cell(row, 13).Value = e.Status == DTOs.PaymentValidationStatus.Missing ? "\u2014" : (e.TotalMatch ? "SI" : "NO");
                ws.Cell(row, 14).Value = e.Status == DTOs.PaymentValidationStatus.Missing ? "\u2014" : (e.ComisionMatch ? "SI" : "NO");
                ws.Cell(row, 15).Value = e.Status switch
                {
                    DTOs.PaymentValidationStatus.Valid => "VALIDO",
                    DTOs.PaymentValidationStatus.Missing => "SIN PAGO",
                    DTOs.PaymentValidationStatus.Duplicate => "DUPLICADO",
                    DTOs.PaymentValidationStatus.FieldMismatch => "ERROR CAMPO",
                    _ => ""
                };
                ws.Cell(row, 16).Value = e.ErrorMessage ?? "";

                // Color-code rows
                var rowBg = e.Status switch
                {
                    DTOs.PaymentValidationStatus.Valid => validBg,
                    DTOs.PaymentValidationStatus.Duplicate => warnBg,
                    _ => errorBg
                };
                ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = rowBg;
                row++;
            }

            int lastDataRow = row - 1;
            ApplyDecimalFormat(ws, lastDataRow, new[] { 5, 6, 7, 8, 9 });
            ApplyDataBorders(ws, lastDataRow, colCount);
            ws.Columns().AdjustToContents();
        }

        // ── Worksheet 3: Discrepancias ──────────────────────────────
        {
            var ws = workbook.Worksheets.Add("Discrepancias");
            var errorBg = XLColor.FromHtml("#FFEBEE");
            int colCount = 6; // max columns across sub-sections
            WriteMarquee(ws, $"Discrepancias de Remesas — {report.Mes:D2}/{report.Anio}", report.Fecha, colCount);

            int row = HeaderRowOffset;

            // Sub-section 1: Remesas en Bolivia no encontradas en Chile
            ws.Range(row, 1, row, colCount).Merge();
            ws.Cell(row, 1).Value = "Remesas en Bolivia no encontradas en Chile";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
            row++;
            var disc1Headers = new[] { "Codigo", "Fuente", "Direccion", "Monto", "Estado", "Fecha" };
            for (int i = 0; i < disc1Headers.Length; i++)
                ws.Cell(row, i + 1).Value = disc1Headers[i];
            var disc1HeaderRange = ws.Range(row, 1, row, disc1Headers.Length);
            disc1HeaderRange.Style.Font.Bold = true;
            disc1HeaderRange.Style.Font.FontColor = White;
            disc1HeaderRange.Style.Fill.BackgroundColor = JetBlack;
            disc1HeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row++;
            foreach (var e in report.Discrepancies.InBoliviaNotInChile)
            {
                ws.Cell(row, 1).Value = e.Codigo;
                ws.Cell(row, 2).Value = e.Source;
                ws.Cell(row, 3).Value = e.Direction;
                ws.Cell(row, 4).Value = e.Amount;
                ws.Cell(row, 5).Value = e.Status;
                ws.Cell(row, 6).Value = e.Fecha.ToString("yyyy-MM-dd");
                ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = errorBg;
                row++;
            }
            row++;

            // Sub-section 2: Remesas en Chile no encontradas en Bolivia
            ws.Range(row, 1, row, colCount).Merge();
            ws.Cell(row, 1).Value = "Remesas en Chile no encontradas en Bolivia";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
            row++;
            for (int i = 0; i < disc1Headers.Length; i++)
                ws.Cell(row, i + 1).Value = disc1Headers[i];
            var disc2HeaderRange = ws.Range(row, 1, row, disc1Headers.Length);
            disc2HeaderRange.Style.Font.Bold = true;
            disc2HeaderRange.Style.Font.FontColor = White;
            disc2HeaderRange.Style.Fill.BackgroundColor = JetBlack;
            disc2HeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row++;
            foreach (var e in report.Discrepancies.InChileNotInBolivia)
            {
                ws.Cell(row, 1).Value = e.Codigo;
                ws.Cell(row, 2).Value = e.Source;
                ws.Cell(row, 3).Value = e.Direction;
                ws.Cell(row, 4).Value = e.Amount;
                ws.Cell(row, 5).Value = e.Status;
                ws.Cell(row, 6).Value = e.Fecha.ToString("yyyy-MM-dd");
                ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = errorBg;
                row++;
            }
            row++;

            // Sub-section 3: Remesas sin asientos contables
            ws.Range(row, 1, row, colCount).Merge();
            ws.Cell(row, 1).Value = "Remesas sin asientos contables";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
            row++;
            for (int i = 0; i < disc1Headers.Length; i++)
                ws.Cell(row, i + 1).Value = disc1Headers[i];
            var disc3HeaderRange = ws.Range(row, 1, row, disc1Headers.Length);
            disc3HeaderRange.Style.Font.Bold = true;
            disc3HeaderRange.Style.Font.FontColor = White;
            disc3HeaderRange.Style.Fill.BackgroundColor = JetBlack;
            disc3HeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row++;
            foreach (var e in report.Discrepancies.MissingAccountingEntries)
            {
                ws.Cell(row, 1).Value = e.Codigo;
                ws.Cell(row, 2).Value = e.Source;
                ws.Cell(row, 3).Value = e.Direction;
                ws.Cell(row, 4).Value = e.Amount;
                ws.Cell(row, 5).Value = e.Status;
                ws.Cell(row, 6).Value = e.Fecha.ToString("yyyy-MM-dd");
                ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = errorBg;
                row++;
            }
            row++;

            // Sub-section 4: Asientos contables huerfanos
            ws.Range(row, 1, row, colCount).Merge();
            ws.Cell(row, 1).Value = "Asientos contables huerfanos";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
            row++;
            var disc4Headers = new[] { "Id Asiento", "Fecha", "Concepto", "Importe" };
            for (int i = 0; i < disc4Headers.Length; i++)
                ws.Cell(row, i + 1).Value = disc4Headers[i];
            var disc4HeaderRange = ws.Range(row, 1, row, disc4Headers.Length);
            disc4HeaderRange.Style.Font.Bold = true;
            disc4HeaderRange.Style.Font.FontColor = White;
            disc4HeaderRange.Style.Fill.BackgroundColor = JetBlack;
            disc4HeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row++;
            foreach (var e in report.Discrepancies.OrphanAccountingEntries)
            {
                ws.Cell(row, 1).Value = e.IdAsiento;
                ws.Cell(row, 2).Value = e.Fecha.ToString("yyyy-MM-dd");
                ws.Cell(row, 3).Value = e.Concepto;
                ws.Cell(row, 4).Value = e.Importe;
                ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = errorBg;
                row++;
            }
            row++;

            // Sub-section 5: Diferencias de monto
            ws.Range(row, 1, row, colCount).Merge();
            ws.Cell(row, 1).Value = "Diferencias de monto";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = TotalRowBg;
            row++;
            var disc5Headers = new[] { "Codigo", "Monto Bolivia", "Monto Chile", "Diferencia" };
            for (int i = 0; i < disc5Headers.Length; i++)
                ws.Cell(row, i + 1).Value = disc5Headers[i];
            var disc5HeaderRange = ws.Range(row, 1, row, disc5Headers.Length);
            disc5HeaderRange.Style.Font.Bold = true;
            disc5HeaderRange.Style.Font.FontColor = White;
            disc5HeaderRange.Style.Fill.BackgroundColor = JetBlack;
            disc5HeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row++;
            foreach (var e in report.Discrepancies.AmountMismatches)
            {
                ws.Cell(row, 1).Value = e.Codigo;
                ws.Cell(row, 2).Value = e.BoliviaAmount;
                ws.Cell(row, 3).Value = e.ChileAmount;
                ws.Cell(row, 4).Value = e.Difference;
                ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = errorBg;
                row++;
            }

            int lastDataRow = row - 1;
            ApplyDataBorders(ws, lastDataRow, colCount);
            ws.Columns().AdjustToContents();
        }

        // ── Worksheet 4: Balance ──────────────────────────────────
        {
            var ws = workbook.Worksheets.Add("Balance");
            int colCount = 5;
            WriteMarquee(ws, $"Balance de Remesas Bolivia — Chile — {report.Mes:D2}/{report.Anio}", report.Fecha, colCount);

            // Summary row
            var summaryHeaders = new[] { "Balance Positivo (Favor Bolivia)", "Balance Negativo (Favor Chile)", "Saldo Neto" };
            for (int i = 0; i < summaryHeaders.Length; i++)
            {
                ws.Cell(HeaderRowOffset, i + 1).Value = summaryHeaders[i];
            }
            StyleHeaderRow(ws, 3);

            int row = HeaderRowOffset + 1;
            ws.Cell(row, 1).Value = report.Balance.BalancePositivo;
            ws.Cell(row, 2).Value = report.Balance.BalanceNegativo;
            ws.Cell(row, 3).Value = report.Balance.SaldoNeto;
            ApplyDecimalFormat(ws, row, new[] { 1, 2, 3 });
            row += 2;

            // Accumulated table
            var accHeaders = new[] { "Fecha", "CHL -> BOL", "BOL -> CHL", "Neto Diario", "Acumulado" };
            for (int i = 0; i < accHeaders.Length; i++)
                ws.Cell(row, i + 1).Value = accHeaders[i];
            var accHeaderRange = ws.Range(row, 1, row, accHeaders.Length);
            accHeaderRange.Style.Font.Bold = true;
            accHeaderRange.Style.Font.FontColor = White;
            accHeaderRange.Style.Fill.BackgroundColor = JetBlack;
            accHeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row++;

            int accDataStart = row;
            foreach (var d in report.Balance.SaldoAcumulado)
            {
                ws.Cell(row, 1).Value = d.Fecha.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value = d.ChileToBoliviaAmount;
                ws.Cell(row, 3).Value = d.BoliviaToChileAmount;
                ws.Cell(row, 4).Value = d.NetDaily;
                ws.Cell(row, 5).Value = d.Accumulated;
                row++;
            }

            int lastDataRow = row - 1;
            if (lastDataRow >= accDataStart)
            {
                ApplyDecimalFormat(ws, lastDataRow, new[] { 2, 3, 4, 5 });
                ApplyZebraStripes(ws, lastDataRow, colCount);
                ApplyDataBorders(ws, lastDataRow, colCount);
            }
            ws.Columns().AdjustToContents();
        }

        return ToBytes(workbook);
    }

    // ── Marquee / Header helpers ────────────────────────────────

    private static void WriteMarquee(IXLWorksheet ws, string title, DateTime fecha, int colCount)
    {
        // Row 1: Logo + company name
        ws.Row(1).Height = 40;
        var logoStream = GetLogoStream();
        if (logoStream != null)
        {
            using (logoStream)
            {
                var picture = ws.AddPicture(logoStream)
                    .MoveTo(ws.Cell(1, 1))
                    .WithSize(120, 48);
            }
        }

        // Company name — merged across remaining columns
        int nameStart = 2;
        ws.Range(1, nameStart, 1, colCount).Merge();
        var nameCell = ws.Cell(1, nameStart);
        nameCell.Value = "GAMBARTE S.R.L.";
        nameCell.Style.Font.Bold = true;
        nameCell.Style.Font.FontSize = 16;
        nameCell.Style.Font.FontColor = JetBlack;
        nameCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        nameCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Row 2: Slogan
        ws.Range(2, 1, 2, colCount).Merge();
        var sloganCell = ws.Cell(2, 1);
        sloganCell.Value = "Cambio de Divisas, Giros y Remesas";
        sloganCell.Style.Font.FontSize = 9;
        sloganCell.Style.Font.FontColor = MediumGray;
        sloganCell.Style.Font.Italic = true;
        sloganCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        // Row 3: Report title — centred, orange background
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

        // Row 4: Date/time stamp — right-aligned
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

    // ── Formatting helpers ──────────────────────────────────────

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

    private static void HighlightMissingCells(IXLWorksheet ws, int lastRow, int colCount)
    {
        for (int r = HeaderRowOffset + 1; r <= lastRow; r++)
        {
            for (int c = 1; c <= colCount; c++)
            {
                var cell = ws.Cell(r, c);
                if (cell.IsEmpty() || (cell.DataType == XLDataType.Text && string.IsNullOrWhiteSpace(cell.GetString())))
                    cell.Style.Fill.BackgroundColor = MissingBg;
            }
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

    // ── Logo / serialisation ────────────────────────────────────

    private static Stream? GetLogoStream()
    {
        var assembly = Assembly.GetExecutingAssembly();
        // Resource name: <RootNamespace>.<path with dots>
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
