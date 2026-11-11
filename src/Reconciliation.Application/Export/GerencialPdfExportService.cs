using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;

namespace Reconciliation.Application.Export;

public class GerencialPdfExportService : IGerencialPdfExportService
{
    // ── Corporate palette ───────────────────────────────────────
    private static readonly string Orange = "#E87722";
    private static readonly string MainGreen = "#0F6E56";
    private static readonly string JetBlack = "#000000";
    private static readonly string MediumGray = "#808184";
    private static readonly string ZebraLight = "#FFF3E8";
    private static readonly string HeaderBg = "#1A1A1A";
    private static readonly string TotalRowBg = "#F2F2F2";
    private static readonly string White = "#FFFFFF";
    private static readonly string KpiBg = "#F8F9FA";

    public byte[] GenerateGerencialReport(GerencialReport report, GerencialChartImages charts)
    {
        string periodLabel = FormatPeriodLabel(report);

        return Document.Create(container =>
        {
            // ═══ Cover page ════════════════════════════════════════
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Content().AlignCenter().AlignMiddle().Column(col =>
                {
                    var logoStream = GetLogoStream();
                    if (logoStream != null)
                    {
                        col.Item().AlignCenter().Width(120).Image(logoStream).FitArea();
                        logoStream.Dispose();
                    }

                    col.Item().PaddingTop(24).AlignCenter()
                        .Text("GAMBARTE S.R.L.")
                        .Bold().FontSize(28).FontColor(MainGreen);

                    col.Item().PaddingTop(4).AlignCenter()
                        .Text("Cambio de Divisas, Giros y Remesas")
                        .FontSize(12).FontColor(MediumGray).Italic();

                    col.Item().PaddingTop(40).AlignCenter()
                        .Background(Orange).Padding(12)
                        .Text("REPORTE GERENCIAL")
                        .Bold().FontSize(20).FontColor(White);

                    col.Item().PaddingTop(20).AlignCenter()
                        .Text(periodLabel)
                        .FontSize(14).FontColor(JetBlack);

                    col.Item().PaddingTop(8).AlignCenter()
                        .Text($"Tipo: {report.Tipo}")
                        .FontSize(11).FontColor(MediumGray);

                    col.Item().PaddingTop(40).AlignCenter()
                        .Text($"Generado: {report.FechaProcesamiento:yyyy-MM-dd HH:mm}")
                        .FontSize(10).FontColor(MediumGray);

                    col.Item().PaddingTop(4).AlignCenter()
                        .Text("Confidencial — Uso interno")
                        .FontSize(9).FontColor(MediumGray).Italic();
                });
            });

            // ═══ Executive summary ═════════════════════════════════
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeHeader(h,
                    "Resumen Ejecutivo", periodLabel));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // ── Remittance KPIs ──
                    col.Item().Text("Remesas").Bold().FontSize(12).FontColor(MainGreen);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1); c.RelativeColumn(1);
                            c.RelativeColumn(1); c.RelativeColumn(1);
                        });

                        KpiCell(table, "Total BOB", report.Remesas.TotalBob);
                        KpiCell(table, "Total USD", report.Remesas.TotalUsd);
                        KpiCell(table, "Transacciones", report.Remesas.TotalTransacciones);
                        KpiCell(table, "Comisiones BOB", report.Remesas.TotalComisiones);
                        KpiCell(table, "Ticket Prom. BOB", report.Remesas.TicketPromedioBob);
                        KpiCell(table, "Ticket Prom. USD", report.Remesas.TicketPromedioUsd);
                        KpiCell(table, "Variacion %", report.Remesas.VariacionPorcentual);
                    });

                    // ── Exchange KPIs ──
                    col.Item().PaddingTop(16).Text("Cambio de Moneda").Bold().FontSize(12).FontColor(MainGreen);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1); c.RelativeColumn(1);
                            c.RelativeColumn(1); c.RelativeColumn(1);
                        });

                        KpiCell(table, "Total Operaciones", report.CambioMoneda.TotalOperaciones);
                        KpiCell(table, "BOB Recibido", report.CambioMoneda.BobRecibido);
                        KpiCell(table, "BOB Entregado", report.CambioMoneda.BobEntregado);
                        KpiCell(table, "Ganancia BOB", report.CambioMoneda.GananciaBob);
                        KpiCell(table, "Variacion %", report.CambioMoneda.VariacionPorcentual);
                    });

                    // ── Consolidated KPIs ──
                    col.Item().PaddingTop(16).Text("Consolidado").Bold().FontSize(12).FontColor(MainGreen);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1); c.RelativeColumn(1);
                            c.RelativeColumn(1); c.RelativeColumn(1);
                        });

                        KpiCell(table, "Ingreso Remesas BOB", report.Consolidado.IngresoRemesasBob);
                        KpiCell(table, "Ingreso Cambio BOB", report.Consolidado.IngresoCambioBob);
                        KpiCell(table, "Ingreso Total BOB", report.Consolidado.IngresoTotalBob);
                        KpiCell(table, "Total Operaciones", report.Consolidado.TotalOperaciones);
                        KpiCell(table, "Agencias Activas", report.Consolidado.AgenciasActivas);
                        KpiCell(table, "Agencias Totales", report.Consolidado.AgenciasTotales);
                        KpiCell(table, "Variacion Ingreso %", report.Consolidado.VariacionIngresoTotal);
                    });

                    // ── Alerts ──
                    if (report.Consolidado.Alertas.Count > 0)
                    {
                        col.Item().PaddingTop(16).Text("Alertas Operacionales").Bold().FontSize(11).FontColor(Orange);
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1.5f);
                            });

                            HeaderCell(table, "Tipo Alerta");
                            HeaderCell(table, "Cantidad");
                            HeaderCell(table, "Monto Total");

                            int ri = 0;
                            foreach (var a in report.Consolidado.Alertas)
                            {
                                bool z = ri++ % 2 == 1;
                                TextCell(table, a.TipoAlerta, z);
                                NumberCell(table, a.Cantidad, z, "N0");
                                NumberCell(table, a.MontoTotal, z);
                            }
                        });
                    }
                });

                page.Footer().Element(f => ComposeFooter(f, periodLabel));
            });

            // ═══ Remittances section ═══════════════════════════════
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Element(h => ComposeHeader(h,
                    "Remesas — Detalle", periodLabel));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // ── Payment type breakdown ──
                    col.Item().Text("Distribucion por Tipo de Pago").Bold().FontSize(10);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1.5f);
                        });

                        HeaderCell(table, "Tipo");
                        HeaderCell(table, "Transacciones");
                        HeaderCell(table, "Monto");

                        PaymentTypeRow(table, "Ventanilla BOB", report.Remesas.PorTipoPago.VentanillaBob, 0);
                        PaymentTypeRow(table, "Ventanilla USD", report.Remesas.PorTipoPago.VentanillaUsd, 1);
                        PaymentTypeRow(table, "Transferencia BOB", report.Remesas.PorTipoPago.TransferenciaBob, 2);
                        PaymentTypeRow(table, "Transferencia USD", report.Remesas.PorTipoPago.TransferenciaUsd, 3);
                    });

                    // ── Department breakdown ──
                    if (report.Remesas.PorDepartamento.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("Remesas por Departamento").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.5f); c.RelativeColumn(0.8f); c.RelativeColumn(0.8f);
                                c.RelativeColumn(0.8f); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.2f);
                            });

                            HeaderCell(table, "Departamento");
                            HeaderCell(table, "Ag. Activas");
                            HeaderCell(table, "Ag. Totales");
                            HeaderCell(table, "Trans.");
                            HeaderCell(table, "Monto BOB");
                            HeaderCell(table, "Monto USD");
                            HeaderCell(table, "Comisiones");

                            int ri = 0;
                            foreach (var d in report.Remesas.PorDepartamento)
                            {
                                bool z = ri++ % 2 == 1;
                                TextCell(table, d.Departamento, z);
                                NumberCell(table, d.AgenciasActivas, z, "N0");
                                NumberCell(table, d.AgenciasTotales, z, "N0");
                                NumberCell(table, d.Transacciones, z, "N0");
                                NumberCell(table, d.MontoBob, z);
                                NumberCell(table, d.MontoUsd, z);
                                NumberCell(table, d.Comisiones, z);
                            }
                        });
                    }

                    // ── Sent vs Paid comparison ──
                    col.Item().PaddingTop(12).Text("Comparativa: Enviadas vs Pagadas").Bold().FontSize(10);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.5f); c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.2f); c.RelativeColumn(1);
                        });

                        HeaderCell(table, "Concepto");
                        HeaderCell(table, "BOB");
                        HeaderCell(table, "USD");
                        HeaderCell(table, "Transacciones");

                        TextCell(table, "Enviadas", false);
                        NumberCell(table, report.Remesas.TotalBob, false);
                        NumberCell(table, report.Remesas.TotalUsd, false);
                        NumberCell(table, report.Remesas.TotalTransacciones, false, "N0");

                        TextCell(table, "Pagadas", true);
                        NumberCell(table, report.Remesas.PagadasBob, true);
                        NumberCell(table, report.Remesas.PagadasUsd, true);
                        NumberCell(table, report.Remesas.PagadasTransacciones, true, "N0");

                        TotalLabelCell(table, "Diferencia", 1);
                        TotalNumberCell(table, report.Remesas.TotalBob - report.Remesas.PagadasBob);
                        TotalNumberCell(table, report.Remesas.TotalUsd - report.Remesas.PagadasUsd);
                        TotalNumberCell(table, report.Remesas.TotalTransacciones - report.Remesas.PagadasTransacciones, "N0");
                    });

                    // ── Charts ──
                    TryInsertChart(col, charts.RemittanceVolume, "Remesas: Enviadas vs Pagadas");
                    TryInsertChart(col, charts.PaymentTypeDistribution, "Distribucion por Tipo de Pago");
                });

                page.Footer().Element(f => ComposeFooter(f, periodLabel));
            });

            // ═══ Exchange section ══════════════════════════════════
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Element(h => ComposeHeader(h,
                    "Cambio de Moneda — Detalle", periodLabel));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // ── Department breakdown ──
                    if (report.CambioMoneda.PorDepartamento.Count > 0)
                    {
                        col.Item().Text("Cambio por Departamento").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.5f); c.RelativeColumn(1);
                                c.RelativeColumn(1.2f); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                            });

                            HeaderCell(table, "Departamento");
                            HeaderCell(table, "Operaciones");
                            HeaderCell(table, "BOB Recibido");
                            HeaderCell(table, "BOB Entregado");
                            HeaderCell(table, "Ganancia BOB");

                            int ri = 0;
                            foreach (var d in report.CambioMoneda.PorDepartamento)
                            {
                                bool z = ri++ % 2 == 1;
                                TextCell(table, d.Departamento, z);
                                NumberCell(table, d.TotalOperaciones, z, "N0");
                                NumberCell(table, d.BobRecibido, z);
                                NumberCell(table, d.BobEntregado, z);
                                NumberCell(table, d.GananciaBob, z);
                            }
                        });
                    }

                    // ── Time series table ──
                    if (report.CambioMoneda.SerieTemporal.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("Serie Temporal de Cambio").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.5f); c.RelativeColumn(1);
                                c.RelativeColumn(1.2f); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                            });

                            HeaderCell(table, "Periodo");
                            HeaderCell(table, "Operaciones");
                            HeaderCell(table, "BOB Recibido");
                            HeaderCell(table, "BOB Entregado");
                            HeaderCell(table, "Ganancia BOB");

                            int ri = 0;
                            foreach (var s in report.CambioMoneda.SerieTemporal)
                            {
                                bool z = ri++ % 2 == 1;
                                TextCell(table, s.Periodo, z);
                                NumberCell(table, s.TotalOperaciones, z, "N0");
                                NumberCell(table, s.BobRecibido, z);
                                NumberCell(table, s.BobEntregado, z);
                                NumberCell(table, s.GananciaBob, z);
                            }
                        });
                    }

                    // ── Charts ──
                    TryInsertChart(col, charts.ExchangeOperations, "Flujo BOB por Periodo");
                    TryInsertChart(col, charts.ExchangeDifferential, "Ganancia/Perdida por Periodo");
                });

                page.Footer().Element(f => ComposeFooter(f, periodLabel));
            });

            // ═══ Consolidated section ══════════════════════════════
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Element(h => ComposeHeader(h,
                    "Consolidado", periodLabel));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // ── Department consolidation ──
                    if (report.Consolidado.PorDepartamento.Count > 0)
                    {
                        col.Item().Text("Consolidado por Departamento").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.5f); c.RelativeColumn(1); c.RelativeColumn(1);
                                c.RelativeColumn(1); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.2f);
                            });

                            HeaderCell(table, "Departamento");
                            HeaderCell(table, "Trans. Rem.");
                            HeaderCell(table, "Op. Cambio");
                            HeaderCell(table, "Total Op.");
                            HeaderCell(table, "Ing. Rem. BOB");
                            HeaderCell(table, "Ing. Cambio BOB");
                            HeaderCell(table, "Ing. Total BOB");

                            int ri = 0;
                            foreach (var d in report.Consolidado.PorDepartamento)
                            {
                                bool z = ri++ % 2 == 1;
                                TextCell(table, d.Departamento, z);
                                NumberCell(table, d.TransaccionesRemesas, z, "N0");
                                NumberCell(table, d.OperacionesCambio, z, "N0");
                                NumberCell(table, d.TotalOperaciones, z, "N0");
                                NumberCell(table, d.IngresoRemesasBob, z);
                                NumberCell(table, d.IngresoCambioBob, z);
                                NumberCell(table, d.IngresoTotalBob, z);
                            }

                            // Total row
                            TotalLabelCell(table, "TOTAL", 3);
                            TotalNumberCell(table, report.Consolidado.TotalOperaciones, "N0");
                            TotalNumberCell(table, report.Consolidado.IngresoRemesasBob);
                            TotalNumberCell(table, report.Consolidado.IngresoCambioBob);
                            TotalNumberCell(table, report.Consolidado.IngresoTotalBob);
                        });
                    }

                    // ── Income series table ──
                    if (report.Consolidado.SerieIngresos.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("Serie de Ingresos").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.5f); c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                            });

                            HeaderCell(table, "Periodo");
                            HeaderCell(table, "Remesas BOB");
                            HeaderCell(table, "Cambio BOB");
                            HeaderCell(table, "Total BOB");

                            int ri = 0;
                            foreach (var s in report.Consolidado.SerieIngresos)
                            {
                                bool z = ri++ % 2 == 1;
                                TextCell(table, s.Periodo, z);
                                NumberCell(table, s.RemesasBob, z);
                                NumberCell(table, s.CambioBob, z);
                                NumberCell(table, s.TotalBob, z);
                            }
                        });
                    }

                    // ── Charts ──
                    TryInsertChart(col, charts.ConsolidatedIncome, "Ingresos Consolidados por Periodo");
                    TryInsertChart(col, charts.OperationsByDepartment, "Operaciones por Departamento");
                });

                page.Footer().Element(f => ComposeFooter(f, periodLabel));
            });
        }).GeneratePdf();
    }

    // ═══ Helpers ═══════════════════════════════════════════════════

    private static string FormatPeriodLabel(GerencialReport report)
    {
        if (report.FechaDesde.Date == report.FechaHasta.Date)
            return $"{report.FechaDesde:yyyy-MM-dd}";

        return $"{report.FechaDesde:yyyy-MM-dd} — {report.FechaHasta:yyyy-MM-dd}";
    }

    private static void TryInsertChart(ColumnDescriptor col, byte[] chartBytes, string fallbackTitle)
    {
        if (chartBytes.Length == 0) return;

        try
        {
            col.Item().PaddingTop(12).AlignCenter()
                .Image(chartBytes).FitArea();
        }
        catch
        {
            col.Item().PaddingTop(12).AlignCenter()
                .Text($"[No se pudo renderizar: {fallbackTitle}]")
                .FontSize(8).FontColor(MediumGray).Italic();
        }
    }

    // ── Header / Footer ──────────────────────────────────────────

    private static void ComposeHeader(IContainer container, string title, string periodLabel)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                var logoStream = GetLogoStream();
                if (logoStream != null)
                {
                    row.ConstantItem(60).AlignMiddle().Image(logoStream).FitArea();
                    logoStream.Dispose();
                }

                row.RelativeItem().PaddingLeft(8).Column(inner =>
                {
                    inner.Item().Text("GAMBARTE S.R.L.")
                        .Bold().FontSize(13).FontColor(JetBlack);
                    inner.Item().Text("Cambio de Divisas, Giros y Remesas")
                        .FontSize(7).FontColor(MediumGray).Italic();
                });

                row.ConstantItem(160).AlignRight().AlignMiddle().Text(text =>
                {
                    text.Span($"Periodo: {periodLabel}").FontSize(8).FontColor(MediumGray);
                    text.EmptyLine();
                    text.Span($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(7).FontColor(MediumGray);
                });
            });

            // Title bar
            col.Item().PaddingTop(6).Background(Orange).Padding(6).AlignCenter()
                .Text(title).Bold().FontSize(11).FontColor(White);

            // Thin separator
            col.Item().PaddingBottom(4).LineHorizontal(1).LineColor(MediumGray);
        });
    }

    private static void ComposeFooter(IContainer container, string periodLabel)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Text("GAMBARTE S.R.L.")
                .FontSize(7).FontColor(MediumGray);
            row.RelativeItem().AlignCenter().Text(text =>
            {
                text.Span($"{periodLabel}  |  ").FontSize(7).FontColor(MediumGray);
                text.Span("Confidencial — Uso interno").FontSize(7).FontColor(MediumGray).Italic();
            });
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Pagina ").FontSize(7).FontColor(MediumGray);
                text.CurrentPageNumber().FontSize(7).FontColor(JetBlack).Bold();
                text.Span(" de ").FontSize(7).FontColor(MediumGray);
                text.TotalPages().FontSize(7).FontColor(JetBlack).Bold();
            });
        });
    }

    // ── Cell helpers ─────────────────────────────────────────────

    private static void HeaderCell(TableDescriptor table, string text)
    {
        table.Cell().Background(HeaderBg)
            .BorderBottom(2).BorderColor(Orange)
            .Padding(3).AlignCenter()
            .Text(text).Bold().FontColor(White).FontSize(7);
    }

    private static void TextCell(TableDescriptor table, string text, bool zebra)
    {
        table.Cell().Background(zebra ? ZebraLight : White)
            .BorderBottom(0.5f).BorderColor("#E0E0E0")
            .Padding(3).Text(text);
    }

    private static void NumberCell(TableDescriptor table, decimal value, bool zebra, string format = "N2")
    {
        table.Cell().Background(zebra ? ZebraLight : White)
            .BorderBottom(0.5f).BorderColor("#E0E0E0")
            .Padding(3).AlignRight().Text(value.ToString(format));
    }

    private static void NumberCell(TableDescriptor table, int value, bool zebra, string format = "N0")
    {
        NumberCell(table, (decimal)value, zebra, format);
    }

    private static void KpiCell(TableDescriptor table, string label, decimal value, string format = "N2")
    {
        table.Cell().Background(KpiBg)
            .Border(0.5f).BorderColor("#E0E0E0")
            .Padding(6).Column(col =>
            {
                col.Item().Text(label).FontSize(7).FontColor(MediumGray);
                col.Item().PaddingTop(2).Text(value.ToString(format))
                    .Bold().FontSize(11).FontColor(JetBlack);
            });
    }

    private static void KpiCell(TableDescriptor table, string label, int value)
    {
        KpiCell(table, label, (decimal)value, "N0");
    }

    private static void KpiCell(TableDescriptor table, string label, decimal? value)
    {
        table.Cell().Background(KpiBg)
            .Border(0.5f).BorderColor("#E0E0E0")
            .Padding(6).Column(col =>
            {
                col.Item().Text(label).FontSize(7).FontColor(MediumGray);
                col.Item().PaddingTop(2).Text(value.HasValue ? $"{value.Value:N2}%" : "—")
                    .Bold().FontSize(11).FontColor(value.HasValue ? JetBlack : MediumGray);
            });
    }

    private static void TotalLabelCell(TableDescriptor table, string label, uint colspan)
    {
        table.Cell().ColumnSpan(colspan).Background(TotalRowBg)
            .BorderTop(2).BorderColor(Orange)
            .Padding(3).AlignRight()
            .Text(label).Bold().FontColor(JetBlack);
    }

    private static void TotalNumberCell(TableDescriptor table, decimal value, string format = "N2")
    {
        table.Cell().Background(TotalRowBg)
            .BorderTop(2).BorderColor(Orange)
            .Padding(3).AlignRight()
            .Text(value.ToString(format)).Bold().FontColor(JetBlack);
    }

    private static void TotalNumberCell(TableDescriptor table, int value, string format = "N0")
    {
        TotalNumberCell(table, (decimal)value, format);
    }

    private static void PaymentTypeRow(TableDescriptor table, string label, TipoPagoDetalle detail, int index)
    {
        bool z = index % 2 == 1;
        TextCell(table, label, z);
        NumberCell(table, detail.Transacciones, z, "N0");
        NumberCell(table, detail.Monto, z);
    }

    // ── Logo ─────────────────────────────────────────────────────

    private static Stream? GetLogoStream()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));
        return name != null ? assembly.GetManifestResourceStream(name) : null;
    }
}
