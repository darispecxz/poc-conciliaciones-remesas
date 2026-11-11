using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;

namespace Reconciliation.Application.Export;

public class PdfExportService : IPdfExportService
{
    // ── Corporate palette ───────────────────────────────────────
    private static readonly string Orange = "#E87722";
    private static readonly string JetBlack = "#000000";
    private static readonly string MediumGray = "#808184";
    private static readonly string ZebraLight = "#FFF3E8";
    private static readonly string MissingBg = "#FDE8D0";
    private static readonly string HeaderBg = "#1A1A1A";
    private static readonly string TotalRowBg = "#F2F2F2";
    private static readonly string White = "#FFFFFF";

    public byte[] GenerateCashierReconciliation(CashierReconciliationReport report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Element(h => ComposeHeader(h,
                    "Reporte de Arqueos de Caja Consolidado Diario", report.Fecha));

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    var headerTexts = new[]
                    {
                        "Cajero", "Agencia", "Moneda", "Apertura",
                        "Cambio Ing", "Envio Giro", "Envio Rem",
                        "Trasp. De Caja", "Trans Bov Ing",
                        "Cambio Egr", "Pago Giro", "Pago Rem",
                        "Trasp. A Caja", "Trans Bov Egr",
                        "Total Ing", "Total Egr", "Total Saldo",
                        "Cierre", "Diferencia"
                    };

                    foreach (var h in headerTexts)
                        HeaderCell(table, h);

                    int rowIndex = 0;
                    foreach (var cashier in report.Cashiers)
                    {
                        foreach (var c in cashier.Currencies)
                        {
                            bool zebra = rowIndex % 2 == 1;
                            TextCell(table, cashier.Cajero, zebra);
                            TextCell(table, cashier.Agencia, zebra);
                            TextCell(table, c.Moneda, zebra);
                            NumberCell(table, c.AperturaCaja, zebra);
                            NumberCell(table, c.CambioMonedaIngreso.Amount, zebra);
                            NumberCell(table, c.EnvioGiro.Amount, zebra);
                            NumberCell(table, c.EnvioRemesa.Amount, zebra);
                            NumberCell(table, c.TraspasoDeCaja.Amount, zebra);
                            NumberCell(table, c.TransferenciaBovedaIngreso.Amount, zebra);
                            NumberCell(table, c.CambioMonedaEgreso.Amount, zebra);
                            NumberCell(table, c.PagoGiro.Amount, zebra);
                            NumberCell(table, c.PagoRemesa.Amount, zebra);
                            NumberCell(table, c.TraspasoACaja.Amount, zebra);
                            NumberCell(table, c.TransferenciaBovedaEgreso.Amount, zebra);
                            NumberCell(table, c.TotalIngreso, zebra);
                            NumberCell(table, c.TotalEgreso, zebra);
                            NumberCell(table, c.TotalSaldo, zebra);
                            NumberCell(table, c.CierreCaja, zebra);
                            NumberCell(table, c.Diferencia, zebra);
                            rowIndex++;
                        }
                    }
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateVaultReconciliation(VaultReconciliationReport report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Element(h => ComposeHeader(h,
                    "Conciliación de Bóveda", report.Fecha));

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                    });

                    var headerTexts = new[]
                    {
                        "Agencia", "Bóveda", "Moneda", "Saldo Apert.",
                        "Sal. Apert. Caja", "Sal. Trasp. Bov.", "Sal. Trasp. Caja", "Total Egresos",
                        "Ent. Cierre Caja", "Ent. Trasp. Bov.", "Ent. Trasp. Casa", "Total Ingresos",
                        "Saldo Final"
                    };

                    foreach (var h in headerTexts)
                        HeaderCell(table, h);

                    int rowIndex = 0;
                    foreach (var agency in report.Agencies)
                    {
                        foreach (var m in agency.Movimientos)
                        {
                            bool zebra = rowIndex % 2 == 1;
                            TextCell(table, agency.Agencia, zebra);
                            TextCell(table, m.NumBoveda.ToString(), zebra);
                            TextCell(table, m.Moneda, zebra);
                            NumberCell(table, m.SaldoApertura, zebra);
                            NumberCell(table, m.SalidaAperturaCaja, zebra);
                            NumberCell(table, m.SalidaTraspasoBoveda, zebra);
                            NumberCell(table, m.SalidaTraspasoCaja, zebra);
                            NumberCell(table, m.TotalEgresos, zebra);
                            NumberCell(table, m.EntradaCierreCaja, zebra);
                            NumberCell(table, m.EntradaTraspasoBoveda, zebra);
                            NumberCell(table, m.EntradaTraspasoCasa, zebra);
                            NumberCell(table, m.TotalIngresos, zebra);
                            NumberCell(table, m.SaldoFinal, zebra);
                            rowIndex++;
                        }
                    }
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateVaultAvailability(VaultAvailabilityReport report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeHeader(h,
                    "Reporte de Disponibilidad en Bóveda", report.Fecha));

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                    });

                    var headerTexts = new[] { "Agencia", "Moneda", "Saldo", "Tipo Cambio", "Saldo BOB" };
                    foreach (var h in headerTexts)
                        HeaderCell(table, h);

                    int rowIndex = 0;
                    foreach (var e in report.Entries)
                    {
                        bool zebra = rowIndex % 2 == 1;
                        TextCell(table, e.Agencia, zebra);
                        TextCell(table, e.Moneda, zebra);
                        NumberCell(table, e.Saldo, zebra);
                        NumberCell(table, e.TipoCambioOficial, zebra, "N4");
                        NumberCell(table, e.SaldoBob, zebra);
                        rowIndex++;
                    }

                    // Total row
                    TotalLabelCell(table, "TOTAL", 4);
                    TotalNumberCell(table, report.TotalSaldoBob);
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateDailySummary(DailySummaryReport report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeHeader(h,
                    "Cuadre Diario de Cajas", report.Fecha));

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                    });

                    var headerTexts = new[]
                    {
                        "Moneda", "Total Cajas", "Total Bóveda", "Total Cajas+Bóveda",
                        "Tipo Cambio", "Monedas en BOB", "Contabilidad", "Diferencia"
                    };
                    foreach (var h in headerTexts)
                        HeaderCell(table, h);

                    int rowIndex = 0;
                    foreach (var c in report.Currencies)
                    {
                        bool zebra = rowIndex % 2 == 1;
                        TextCell(table, c.Moneda, zebra);
                        NumberCell(table, c.TotalCajas, zebra);
                        NumberCell(table, c.TotalBoveda, zebra);
                        NumberCell(table, c.TotalCajasBoveda, zebra);
                        NumberCell(table, c.TipoCambioOficial, zebra, "N4");
                        NumberCell(table, c.MonedasEnBob, zebra);
                        NullableNumberCell(table, c.Contabilidad, zebra);
                        NullableNumberCell(table, c.Diferencia, zebra);
                        rowIndex++;
                    }

                    // Grand total
                    TotalLabelCell(table, "TOTAL", 5);
                    TotalNumberCell(table, report.GrandTotalBob);
                    table.Cell().Background(TotalRowBg).Padding(3).Text("");
                    table.Cell().Background(TotalRowBg).Padding(3).Text("");
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateAgencyClosings(AgencyClosingReport report)
    {
        var currencies = report.Agencies
            .SelectMany(a => a.CurrencyClosings.Keys)
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeHeader(h,
                    "Cierres por Agencia", report.Fecha));

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        foreach (var _ in currencies)
                            columns.RelativeColumn(1.5f);
                    });

                    HeaderCell(table, "Agencia");
                    foreach (var cur in currencies)
                        HeaderCell(table, cur);

                    int rowIndex = 0;
                    foreach (var agency in report.Agencies)
                    {
                        bool zebra = rowIndex % 2 == 1;
                        TextCell(table, agency.Agencia, zebra);
                        foreach (var cur in currencies)
                        {
                            if (agency.CurrencyClosings.TryGetValue(cur, out var v))
                                NumberCell(table, v, zebra);
                            else
                                MissingCell(table, zebra);
                        }
                        rowIndex++;
                    }

                    // Totals
                    TotalLabelCell(table, "TOTAL", 1);
                    foreach (var cur in currencies)
                    {
                        if (report.Totals.TryGetValue(cur, out var v))
                            TotalNumberCell(table, v);
                        else
                            table.Cell().Background(TotalRowBg).Padding(3).Text("");
                    }
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateMovementSummary(MovementSummaryReport report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeHeader(h,
                    "Resumen de Movimientos de Caja", report.Fecha));

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                    });

                    var headerTexts = new[]
                    {
                        "Moneda", "Apertura", "Cambio Mon.", "Envío Giro", "Pago Giro",
                        "Envío Remesa", "Pago Remesa", "Egresos", "Transferencias",
                        "Fallas Caja", "Cierre Caja"
                    };
                    foreach (var h in headerTexts)
                        HeaderCell(table, h);

                    int rowIndex = 0;
                    foreach (var c in report.Currencies)
                    {
                        bool zebra = rowIndex % 2 == 1;
                        TextCell(table, c.Moneda, zebra);
                        NumberCell(table, c.AperturaCaja, zebra);
                        NumberCell(table, c.CambioMoneda, zebra);
                        NumberCell(table, c.EnvioGiro, zebra);
                        NumberCell(table, c.PagoGiro, zebra);
                        NumberCell(table, c.EnvioRemesa, zebra);
                        NumberCell(table, c.PagoRemesa, zebra);
                        NumberCell(table, c.Egresos, zebra);
                        NumberCell(table, c.Transferencias, zebra);
                        NumberCell(table, c.FallasCaja, zebra);
                        NumberCell(table, c.CierreCaja, zebra);
                        rowIndex++;
                    }
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateRemittanceDailyOperations(RemittanceDailyOperationsReport report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Element(h => ComposeHeader(h,
                    $"Operaciones Diarias de Remesas — {report.Fecha:yyyy-MM-dd}", report.Fecha));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // Summary line
                    var s = report.Summary;
                    col.Item().Text(
                        $"Enviadas: {s.TotalSentToday} (${s.TotalSentAmountToday:N2}) | " +
                        $"Anuladas: {s.TotalCancelledToday} (${s.TotalCancelledAmountToday:N2}) | " +
                        $"Pagadas: {s.TotalPaidToday} (${s.TotalPaidAmountToday:N2}) | " +
                        $"Pend. BOL: {s.TotalPendingBolivia} (${s.TotalPendingAmountBolivia:N2}) | " +
                        $"Pend. CHL: {s.TotalPendingChile} (${s.TotalPendingAmountChile:N2})")
                        .Bold().FontSize(8);

                    // Remesas enviadas hoy
                    col.Item().PaddingTop(8).Text("Remesas enviadas hoy").Bold().FontSize(9);
                    col.Item().PaddingBottom(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(1.5f);
                            c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f);
                        });
                        HeaderCell(table, "Direccion"); HeaderCell(table, "Moneda");
                        HeaderCell(table, "Cantidad"); HeaderCell(table, "Monto Total"); HeaderCell(table, "Estado");

                        int ri = 0;
                        foreach (var item in report.SentToday)
                        {
                            bool z = ri++ % 2 == 1;
                            TextCell(table, item.Direction, z); TextCell(table, item.Moneda, z);
                            NumberCell(table, item.Count, z, "N0"); NumberCell(table, item.TotalAmount, z);
                            TextCell(table, item.Status, z);
                        }
                    });

                    // Remesas anuladas hoy
                    col.Item().PaddingTop(8).Text("Remesas anuladas hoy").Bold().FontSize(9);
                    col.Item().PaddingBottom(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(1.5f);
                            c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f);
                        });
                        HeaderCell(table, "Direccion"); HeaderCell(table, "Moneda");
                        HeaderCell(table, "Cantidad"); HeaderCell(table, "Monto Total"); HeaderCell(table, "Estado");

                        int ri = 0;
                        foreach (var item in report.CancelledToday)
                        {
                            bool z = ri++ % 2 == 1;
                            TextCell(table, item.Direction, z); TextCell(table, item.Moneda, z);
                            NumberCell(table, item.Count, z, "N0"); NumberCell(table, item.TotalAmount, z);
                            TextCell(table, item.Status, z);
                        }
                    });

                    // Remesas pagadas hoy
                    col.Item().PaddingTop(8).Text("Remesas pagadas hoy").Bold().FontSize(9);
                    col.Item().PaddingBottom(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(1.5f);
                            c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f);
                        });
                        HeaderCell(table, "Direccion"); HeaderCell(table, "Moneda");
                        HeaderCell(table, "Cantidad"); HeaderCell(table, "Monto Total"); HeaderCell(table, "Estado");

                        int ri = 0;
                        foreach (var item in report.PaidToday)
                        {
                            bool z = ri++ % 2 == 1;
                            TextCell(table, item.Direction, z); TextCell(table, item.Moneda, z);
                            NumberCell(table, item.Count, z, "N0"); NumberCell(table, item.TotalAmount, z);
                            TextCell(table, item.Status, z);
                        }
                    });

                    // Pagos pendientes en Bolivia (acumulado)
                    col.Item().PaddingTop(8).Text("Pagos pendientes en Bolivia (acumulado)").Bold().FontSize(9);
                    col.Item().PaddingBottom(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(1.5f);
                            c.RelativeColumn(1); c.RelativeColumn(1.5f);
                        });
                        HeaderCell(table, "Tipo"); HeaderCell(table, "Moneda");
                        HeaderCell(table, "Cantidad"); HeaderCell(table, "Monto Total");

                        int ri = 0;
                        foreach (var item in report.PendingInBolivia)
                        {
                            bool z = ri++ % 2 == 1;
                            TextCell(table, "CHL -> BOL", z); TextCell(table, item.Moneda, z);
                            NumberCell(table, item.Count, z, "N0"); NumberCell(table, item.TotalAmount, z);
                        }
                    });

                    // Pagos pendientes en Chile (acumulado)
                    col.Item().PaddingTop(8).Text("Pagos pendientes en Chile (acumulado)").Bold().FontSize(9);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(1.5f);
                            c.RelativeColumn(1); c.RelativeColumn(1.5f);
                        });
                        HeaderCell(table, "Tipo"); HeaderCell(table, "Moneda");
                        HeaderCell(table, "Cantidad"); HeaderCell(table, "Monto Total");

                        int ri = 0;
                        foreach (var item in report.PendingInChile)
                        {
                            bool z = ri++ % 2 == 1;
                            TextCell(table, "BOL -> CHL", z); TextCell(table, item.Moneda, z);
                            NumberCell(table, item.Count, z, "N0"); NumberCell(table, item.TotalAmount, z);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateRemittanceAccountingValidation(RemittanceAccountingValidationReport report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Element(h => ComposeHeader(h,
                    $"Validacion Contable de Remesas — {report.Mes:D2}/{report.Anio}", report.Fecha));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Item().Text($"Total validados: {report.AccountingValidation.TotalValidated}  |  " +
                                    $"Correctos: {report.AccountingValidation.TotalValid}  |  " +
                                    $"Errores: {report.AccountingValidation.TotalErrors}")
                        .Bold().FontSize(9);

                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1);    // Codigo
                            c.RelativeColumn(1);    // Direccion
                            c.RelativeColumn(1);    // Estado
                            c.RelativeColumn(1.5f); // Tipo
                            c.RelativeColumn(1.2f); // Esperado
                            c.RelativeColumn(1.2f); // Contable
                            c.RelativeColumn(0.5f); // Valido
                            c.RelativeColumn(2.5f); // Error
                        });

                        HeaderCell(table, "Codigo"); HeaderCell(table, "Direccion");
                        HeaderCell(table, "Estado"); HeaderCell(table, "Tipo Validacion");
                        HeaderCell(table, "Monto Esperado"); HeaderCell(table, "Monto Contable");
                        HeaderCell(table, "Valido"); HeaderCell(table, "Error");

                        foreach (var e in report.AccountingValidation.Entries)
                        {
                            var bg = e.IsValid ? "#E8F5E9" : "#FFEBEE";
                            StatusCell(table, e.Codigo.ToString(), bg);
                            StatusCell(table, e.Direction, bg);
                            StatusCell(table, e.RemittanceStatus, bg);
                            StatusCell(table, e.ValidationType.ToString(), bg);
                            StatusNumberCell(table, e.ExpectedAmount, bg);
                            StatusNullableNumberCell(table, e.AccountingAmount, bg);
                            StatusCell(table, e.IsValid ? "SI" : "NO", bg);
                            StatusCell(table, e.ErrorMessage ?? "", bg);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateRemittanceGeneralReconciliation(RemittanceGeneralReconciliationReport report)
    {
        return Document.Create(container =>
        {
            // ── Page 1: Daily Matrix ─────────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(6));

                page.Header().Element(h => ComposeHeader(h,
                    $"Conciliacion General de Remesas — {report.Mes:D2}/{report.Anio}", report.Fecha));

                page.Content().PaddingVertical(8).Table(table =>
                {
                    // 19 columns
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.2f);  // 1: Fecha
                        c.RelativeColumn(0.8f);  // 2: CHL→BOL Recibidas C
                        c.RelativeColumn(0.8f);  // 3: CHL→BOL Recibidas I
                        c.RelativeColumn(0.8f);  // 4: CHL→BOL Anuladas C
                        c.RelativeColumn(0.8f);  // 5: CHL→BOL Anuladas I
                        c.RelativeColumn(0.8f);  // 6: CHL→BOL Pendientes C
                        c.RelativeColumn(0.8f);  // 7: CHL→BOL Pendientes I
                        c.RelativeColumn(0.8f);  // 8: CHL→BOL Pagadas C
                        c.RelativeColumn(0.8f);  // 9: CHL→BOL Pagadas I
                        c.RelativeColumn(0.8f);  // 10: BOL→CHL Enviadas C
                        c.RelativeColumn(0.8f);  // 11: BOL→CHL Enviadas I
                        c.RelativeColumn(0.8f);  // 12: BOL→CHL Anuladas C
                        c.RelativeColumn(0.8f);  // 13: BOL→CHL Anuladas I
                        c.RelativeColumn(0.8f);  // 14: BOL→CHL Pendientes C
                        c.RelativeColumn(0.8f);  // 15: BOL→CHL Pendientes I
                        c.RelativeColumn(0.8f);  // 16: BOL→CHL Pagadas C
                        c.RelativeColumn(0.8f);  // 17: BOL→CHL Pagadas I
                        c.RelativeColumn(1f);    // 18: Saldo F/C
                        c.RelativeColumn(1f);    // 19: Saldo Acum.
                    });

                    // ── Header Level 1: Group headers ──
                    table.Cell().Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("FECHA").Bold().FontColor(White).FontSize(6);
                    table.Cell().ColumnSpan(8).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("CHL → BOL").Bold().FontColor(White).FontSize(6);
                    table.Cell().ColumnSpan(8).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("BOL → CHL").Bold().FontColor(White).FontSize(6);
                    table.Cell().ColumnSpan(2).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("SALDO").Bold().FontColor(White).FontSize(6);

                    // ── Header Level 2: Sub-group headers ──
                    table.Cell().Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("").FontSize(5);
                    // CHL→BOL sub-groups
                    table.Cell().ColumnSpan(2).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Recibidas").Bold().FontColor(White).FontSize(5);
                    table.Cell().ColumnSpan(2).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Anuladas").Bold().FontColor(White).FontSize(5);
                    table.Cell().ColumnSpan(2).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Pendientes").Bold().FontColor(White).FontSize(5);
                    table.Cell().ColumnSpan(2).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Pagadas").Bold().FontColor(White).FontSize(5);
                    // BOL→CHL sub-groups
                    table.Cell().ColumnSpan(2).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Enviadas").Bold().FontColor(White).FontSize(5);
                    table.Cell().ColumnSpan(2).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Anuladas").Bold().FontColor(White).FontSize(5);
                    table.Cell().ColumnSpan(2).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Pendientes").Bold().FontColor(White).FontSize(5);
                    table.Cell().ColumnSpan(2).Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Pagadas").Bold().FontColor(White).FontSize(5);
                    // Saldo sub-groups
                    table.Cell().Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("F/C").Bold().FontColor(White).FontSize(5);
                    table.Cell().Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Acum.").Bold().FontColor(White).FontSize(5);

                    // ── Header Level 3: C. / Imp. labels ──
                    table.Cell().Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("").FontSize(5);
                    // Cols 2-17: alternating C. and Imp.
                    for (int i = 0; i < 16; i++)
                    {
                        var label = i % 2 == 0 ? "C." : "Imp.";
                        table.Cell().Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                            .Padding(2).AlignCenter().Text(label).Bold().FontColor(White).FontSize(5);
                    }
                    // Cols 18-19: Imp.
                    table.Cell().Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Imp.").Bold().FontColor(White).FontSize(5);
                    table.Cell().Background(HeaderBg).BorderBottom(2).BorderColor(Orange)
                        .Padding(2).AlignCenter().Text("Imp.").Bold().FontColor(White).FontSize(5);

                    // ── Carry-forward row ──
                    var cf = report.DailyMatrix.CarryForward;
                    table.Cell().Background(TotalRowBg).Padding(2).Text("Arrastre").Bold().FontSize(6);
                    // Cols 2-5: empty (Recibidas C/I, Anuladas C/I)
                    for (int i = 0; i < 4; i++)
                        table.Cell().Background(TotalRowBg).Padding(2).Text("");
                    // Cols 6-7: CHL→BOL Pendientes
                    table.Cell().Background(TotalRowBg).Padding(2).AlignRight()
                        .Text(cf.ChlToBolPendingCount.ToString("N0")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).Padding(2).AlignRight()
                        .Text(cf.ChlToBolPendingAmount.ToString("N2")).Bold().FontSize(6);
                    // Cols 8-9: empty (Pagadas C/I)
                    table.Cell().Background(TotalRowBg).Padding(2).Text("");
                    table.Cell().Background(TotalRowBg).Padding(2).Text("");
                    // Cols 10-13: empty (Enviadas C/I, Anuladas C/I)
                    for (int i = 0; i < 4; i++)
                        table.Cell().Background(TotalRowBg).Padding(2).Text("");
                    // Cols 14-15: BOL→CHL Pendientes
                    table.Cell().Background(TotalRowBg).Padding(2).AlignRight()
                        .Text(cf.BolToChlPendingCount.ToString("N0")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).Padding(2).AlignRight()
                        .Text(cf.BolToChlPendingAmount.ToString("N2")).Bold().FontSize(6);
                    // Cols 16-17: empty (Pagadas C/I)
                    table.Cell().Background(TotalRowBg).Padding(2).Text("");
                    table.Cell().Background(TotalRowBg).Padding(2).Text("");
                    // Col 18: empty (F/C)
                    table.Cell().Background(TotalRowBg).Padding(2).Text("");
                    // Col 19: Saldo Acumulado
                    table.Cell().Background(TotalRowBg).Padding(2).AlignRight()
                        .Text(cf.SaldoAcumulado.ToString("N2")).Bold().FontSize(6);

                    // ── Data rows ──
                    int rowIndex = 0;
                    foreach (var day in report.DailyMatrix.DailyRows)
                    {
                        bool zebra = rowIndex++ % 2 == 1;
                        TextCell(table, day.Fecha.ToString("dd"), zebra);
                        NumberCell(table, day.ChlToBolRecibidas.Count, zebra, "N0");
                        NumberCell(table, day.ChlToBolRecibidas.TotalAmount, zebra);
                        NumberCell(table, day.ChlToBolAnuladas.Count, zebra, "N0");
                        NumberCell(table, day.ChlToBolAnuladas.TotalAmount, zebra);
                        NumberCell(table, day.ChlToBolPendientes.Count, zebra, "N0");
                        NumberCell(table, day.ChlToBolPendientes.TotalAmount, zebra);
                        NumberCell(table, day.ChlToBolPagadas.Count, zebra, "N0");
                        NumberCell(table, day.ChlToBolPagadas.TotalAmount, zebra);
                        NumberCell(table, day.BolToChlEnviadas.Count, zebra, "N0");
                        NumberCell(table, day.BolToChlEnviadas.TotalAmount, zebra);
                        NumberCell(table, day.BolToChlAnuladas.Count, zebra, "N0");
                        NumberCell(table, day.BolToChlAnuladas.TotalAmount, zebra);
                        NumberCell(table, day.BolToChlPendientes.Count, zebra, "N0");
                        NumberCell(table, day.BolToChlPendientes.TotalAmount, zebra);
                        NumberCell(table, day.BolToChlPagadas.Count, zebra, "N0");
                        NumberCell(table, day.BolToChlPagadas.TotalAmount, zebra);
                        NumberCell(table, day.SaldoFavorContra, zebra);
                        NumberCell(table, day.SaldoAcumulado, zebra);
                    }

                    // ── Totals row ──
                    var t = report.DailyMatrix.Totals;
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).Text("TOTALES").Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.ChlToBolRecibidas.Count.ToString("N0")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.ChlToBolRecibidas.TotalAmount.ToString("N2")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.ChlToBolAnuladas.Count.ToString("N0")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.ChlToBolAnuladas.TotalAmount.ToString("N2")).Bold().FontSize(6);
                    // Pendientes: no totals — empty cells
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange).Padding(2).Text("");
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange).Padding(2).Text("");
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.ChlToBolPagadas.Count.ToString("N0")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.ChlToBolPagadas.TotalAmount.ToString("N2")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.BolToChlEnviadas.Count.ToString("N0")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.BolToChlEnviadas.TotalAmount.ToString("N2")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.BolToChlAnuladas.Count.ToString("N0")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.BolToChlAnuladas.TotalAmount.ToString("N2")).Bold().FontSize(6);
                    // Pendientes: no totals — empty cells
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange).Padding(2).Text("");
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange).Padding(2).Text("");
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.BolToChlPagadas.Count.ToString("N0")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.BolToChlPagadas.TotalAmount.ToString("N2")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange)
                        .Padding(2).AlignRight().Text(t.SaldoFavorContraTotal.ToString("N2")).Bold().FontSize(6);
                    table.Cell().Background(TotalRowBg).BorderTop(2).BorderColor(Orange).Padding(2).Text("");
                });

                page.Footer().Element(ComposeFooter);
            });

            // ── Page 2: Payment Validation ───────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Element(h => ComposeHeader(h,
                    $"Validacion de Pagos — {report.Mes:D2}/{report.Anio}", report.Fecha));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Item().Text($"Total remesas: {report.PaymentValidation.TotalRemittances}  |  " +
                                    $"Con pago: {report.PaymentValidation.TotalWithPayment}  |  " +
                                    $"Sin pago: {report.PaymentValidation.TotalMissing}  |  " +
                                    $"Duplicados: {report.PaymentValidation.TotalDuplicates}  |  " +
                                    $"Errores campo: {report.PaymentValidation.TotalFieldErrors}  |  " +
                                    $"Validos: {report.PaymentValidation.TotalValid}")
                        .Bold().FontSize(8);

                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(0.9f);  // Direccion
                            c.RelativeColumn(0.7f);  // Codigo
                            c.RelativeColumn(0.7f);  // Moneda
                            c.RelativeColumn(1);     // Monto Envio
                            c.RelativeColumn(1);     // Pago Monto
                            c.RelativeColumn(0.8f);  // Pago T/C
                            c.RelativeColumn(1);     // Pago Total
                            c.RelativeColumn(0.9f);  // Pago Comision
                            c.RelativeColumn(0.9f);  // Fecha Pago
                            c.RelativeColumn(0.7f);  // Estado
                            c.RelativeColumn(2.2f);  // Error
                        });

                        HeaderCell(table, "Direccion"); HeaderCell(table, "Codigo");
                        HeaderCell(table, "Moneda"); HeaderCell(table, "Monto Envio");
                        HeaderCell(table, "Pago Monto"); HeaderCell(table, "Pago T/C");
                        HeaderCell(table, "Pago Total"); HeaderCell(table, "Pago Comision");
                        HeaderCell(table, "Fecha Pago"); HeaderCell(table, "Estado");
                        HeaderCell(table, "Error");

                        foreach (var e in report.PaymentValidation.Entries)
                        {
                            var bg = e.Status switch
                            {
                                DTOs.PaymentValidationStatus.Valid => "#E8F5E9",
                                DTOs.PaymentValidationStatus.Duplicate => "#FFF8E1",
                                _ => "#FFEBEE"
                            };

                            StatusCell(table, e.Direction, bg);
                            StatusCell(table, e.Codigo.ToString(), bg);
                            StatusCell(table, e.Moneda, bg);
                            StatusNumberCell(table, e.RemittanceEnvio, bg);
                            StatusNullableNumberCell(table, e.PagoMonto, bg);
                            if (e.PagoTipoCambio.HasValue)
                            {
                                table.Cell().Background(bg)
                                    .BorderBottom(0.5f).BorderColor("#E0E0E0")
                                    .Padding(2).AlignRight().Text(e.PagoTipoCambio.Value.ToString("N4")).FontSize(7);
                            }
                            else
                            {
                                table.Cell().Background(bg)
                                    .BorderBottom(0.5f).BorderColor("#E0E0E0")
                                    .Padding(2).AlignCenter().Text("—").FontSize(7).FontColor(MediumGray).Italic();
                            }
                            StatusNullableNumberCell(table, e.PagoTotal, bg);
                            StatusNullableNumberCell(table, e.PagoComisionBob, bg);
                            StatusCell(table, e.PagoFecCrea?.ToString("yyyy-MM-dd") ?? "—", bg);
                            var statusLabel = e.Status switch
                            {
                                DTOs.PaymentValidationStatus.Valid => "VALIDO",
                                DTOs.PaymentValidationStatus.Missing => "SIN PAGO",
                                DTOs.PaymentValidationStatus.Duplicate => "DUPLICADO",
                                DTOs.PaymentValidationStatus.FieldMismatch => "ERROR",
                                _ => ""
                            };
                            StatusCell(table, statusLabel, bg);
                            StatusCell(table, e.ErrorMessage ?? "", bg);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });

            // ── Page 3: Discrepancies ────────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Element(h => ComposeHeader(h,
                    $"Discrepancias de Remesas — {report.Mes:D2}/{report.Anio}", report.Fecha));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // 1. Remesas en Bolivia no encontradas en Chile
                    col.Item().Text("Remesas en Bolivia no encontradas en Chile").Bold().FontSize(9);
                    col.Item().PaddingBottom(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1);
                            c.RelativeColumn(1.2f); c.RelativeColumn(1); c.RelativeColumn(1);
                        });
                        HeaderCell(table, "Codigo"); HeaderCell(table, "Fuente");
                        HeaderCell(table, "Direccion"); HeaderCell(table, "Monto");
                        HeaderCell(table, "Estado"); HeaderCell(table, "Fecha");

                        foreach (var e in report.Discrepancies.InBoliviaNotInChile)
                        {
                            var bg = "#FFEBEE";
                            StatusCell(table, e.Codigo.ToString(), bg);
                            StatusCell(table, e.Source, bg);
                            StatusCell(table, e.Direction, bg);
                            StatusNumberCell(table, e.Amount, bg);
                            StatusCell(table, e.Status, bg);
                            StatusCell(table, e.Fecha.ToString("yyyy-MM-dd"), bg);
                        }
                    });

                    // 2. Remesas en Chile no encontradas en Bolivia
                    col.Item().PaddingTop(8).Text("Remesas en Chile no encontradas en Bolivia").Bold().FontSize(9);
                    col.Item().PaddingBottom(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1);
                            c.RelativeColumn(1.2f); c.RelativeColumn(1); c.RelativeColumn(1);
                        });
                        HeaderCell(table, "Codigo"); HeaderCell(table, "Fuente");
                        HeaderCell(table, "Direccion"); HeaderCell(table, "Monto");
                        HeaderCell(table, "Estado"); HeaderCell(table, "Fecha");

                        foreach (var e in report.Discrepancies.InChileNotInBolivia)
                        {
                            var bg = "#FFEBEE";
                            StatusCell(table, e.Codigo.ToString(), bg);
                            StatusCell(table, e.Source, bg);
                            StatusCell(table, e.Direction, bg);
                            StatusNumberCell(table, e.Amount, bg);
                            StatusCell(table, e.Status, bg);
                            StatusCell(table, e.Fecha.ToString("yyyy-MM-dd"), bg);
                        }
                    });

                    // 3. Remesas sin asientos contables
                    col.Item().PaddingTop(8).Text("Remesas sin asientos contables").Bold().FontSize(9);
                    col.Item().PaddingBottom(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1);
                            c.RelativeColumn(1.2f); c.RelativeColumn(1); c.RelativeColumn(1);
                        });
                        HeaderCell(table, "Codigo"); HeaderCell(table, "Fuente");
                        HeaderCell(table, "Direccion"); HeaderCell(table, "Monto");
                        HeaderCell(table, "Estado"); HeaderCell(table, "Fecha");

                        foreach (var e in report.Discrepancies.MissingAccountingEntries)
                        {
                            var bg = "#FFEBEE";
                            StatusCell(table, e.Codigo.ToString(), bg);
                            StatusCell(table, e.Source, bg);
                            StatusCell(table, e.Direction, bg);
                            StatusNumberCell(table, e.Amount, bg);
                            StatusCell(table, e.Status, bg);
                            StatusCell(table, e.Fecha.ToString("yyyy-MM-dd"), bg);
                        }
                    });

                    // 4. Asientos contables huerfanos
                    col.Item().PaddingTop(8).Text("Asientos contables huerfanos").Bold().FontSize(9);
                    col.Item().PaddingBottom(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1); c.RelativeColumn(1);
                            c.RelativeColumn(2); c.RelativeColumn(1.2f);
                        });
                        HeaderCell(table, "ID Asiento"); HeaderCell(table, "Fecha");
                        HeaderCell(table, "Concepto"); HeaderCell(table, "Importe");

                        foreach (var e in report.Discrepancies.OrphanAccountingEntries)
                        {
                            var bg = "#FFEBEE";
                            StatusCell(table, e.IdAsiento.ToString(), bg);
                            StatusCell(table, e.Fecha.ToString("yyyy-MM-dd"), bg);
                            StatusCell(table, e.Concepto, bg);
                            StatusNumberCell(table, e.Importe, bg);
                        }
                    });

                    // 5. Diferencias de monto
                    col.Item().PaddingTop(8).Text("Diferencias de monto").Bold().FontSize(9);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1); c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                        });
                        HeaderCell(table, "Codigo"); HeaderCell(table, "Monto Bolivia");
                        HeaderCell(table, "Monto Chile"); HeaderCell(table, "Diferencia");

                        foreach (var e in report.Discrepancies.AmountMismatches)
                        {
                            var bg = "#FFEBEE";
                            StatusCell(table, e.Codigo.ToString(), bg);
                            StatusNumberCell(table, e.BoliviaAmount, bg);
                            StatusNumberCell(table, e.ChileAmount, bg);
                            StatusNumberCell(table, e.Difference, bg);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });

            // ── Page 4: Balance ──────────────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeHeader(h,
                    $"Balance de Remesas Bolivia — Chile — {report.Mes:D2}/{report.Anio}", report.Fecha));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // Summary
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1);
                        });
                        HeaderCell(table, "Balance Positivo (Favor Bolivia)");
                        HeaderCell(table, "Balance Negativo (Favor Chile)");
                        HeaderCell(table, "Saldo Neto");
                        NumberCell(table, report.Balance.BalancePositivo, false);
                        NumberCell(table, report.Balance.BalanceNegativo, false);
                        NumberCell(table, report.Balance.SaldoNeto, false);
                    });

                    // Accumulated
                    col.Item().PaddingTop(16).Text("Saldo Acumulado por Fecha").Bold().FontSize(10);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.5f); c.RelativeColumn(1.5f);
                            c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f);
                        });
                        HeaderCell(table, "Fecha"); HeaderCell(table, "CHL -> BOL");
                        HeaderCell(table, "BOL -> CHL"); HeaderCell(table, "Neto Diario"); HeaderCell(table, "Acumulado");

                        int ri = 0;
                        foreach (var d in report.Balance.SaldoAcumulado)
                        {
                            bool z = ri++ % 2 == 1;
                            TextCell(table, d.Fecha.ToString("yyyy-MM-dd"), z);
                            NumberCell(table, d.ChileToBoliviaAmount, z);
                            NumberCell(table, d.BoliviaToChileAmount, z);
                            NumberCell(table, d.NetDaily, z);
                            NumberCell(table, d.Accumulated, z);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    // ── Shared header / footer composers ────────────────────────

    private static void ComposeHeader(IContainer container, string title, DateTime fecha)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                var logoStream = GetLogoStream();
                if (logoStream != null)
                {
                    row.ConstantItem(70).AlignMiddle().Image(logoStream).FitArea();
                    logoStream.Dispose();
                }

                row.RelativeItem().PaddingLeft(8).Column(inner =>
                {
                    inner.Item().Text("GAMBARTE S.R.L.")
                        .Bold().FontSize(14).FontColor(JetBlack);
                    inner.Item().Text("Cambio de Divisas, Giros y Remesas")
                        .FontSize(8).FontColor(MediumGray).Italic();
                });

                row.ConstantItem(160).AlignRight().AlignMiddle().Text(text =>
                {
                    text.Span($"Fecha: {fecha:yyyy-MM-dd}").FontSize(8).FontColor(MediumGray);
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

    private static void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Text("GAMBARTE S.R.L.")
                .FontSize(7).FontColor(MediumGray);
            row.RelativeItem().AlignCenter().Text(text =>
            {
                text.Span("Página ").FontSize(7).FontColor(MediumGray);
                text.CurrentPageNumber().FontSize(7).FontColor(JetBlack).Bold();
                text.Span(" de ").FontSize(7).FontColor(MediumGray);
                text.TotalPages().FontSize(7).FontColor(JetBlack).Bold();
            });
            row.RelativeItem().AlignRight().Text($"{DateTime.Now:yyyy-MM-dd HH:mm}")
                .FontSize(7).FontColor(MediumGray);
        });
    }

    // ── Cell helpers ────────────────────────────────────────────

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

    private static void NullableNumberCell(TableDescriptor table, decimal? value, bool zebra)
    {
        if (value.HasValue)
        {
            NumberCell(table, value.Value, zebra);
        }
        else
        {
            MissingCell(table, zebra);
        }
    }

    private static void MissingCell(TableDescriptor table, bool _)
    {
        // Always use the soft peach background regardless of zebra state
        table.Cell().Background(MissingBg)
            .BorderBottom(0.5f).BorderColor("#E0E0E0")
            .Padding(3).AlignCenter()
            .Text("—").FontColor(MediumGray).Italic();
    }

    private static void TotalLabelCell(TableDescriptor table, string label, uint colspan)
    {
        table.Cell().ColumnSpan(colspan).Background(TotalRowBg)
            .BorderTop(2).BorderColor(Orange)
            .Padding(3).AlignRight()
            .Text(label).Bold().FontColor(JetBlack);
    }

    private static void TotalNumberCell(TableDescriptor table, decimal value)
    {
        table.Cell().Background(TotalRowBg)
            .BorderTop(2).BorderColor(Orange)
            .Padding(3).AlignRight()
            .Text(value.ToString("N2")).Bold().FontColor(JetBlack);
    }

    // ── Status-colored cell helpers (for accounting validation) ──

    private static void StatusCell(TableDescriptor table, string text, string bgColor)
    {
        table.Cell().Background(bgColor)
            .BorderBottom(0.5f).BorderColor("#E0E0E0")
            .Padding(2).Text(text).FontSize(7);
    }

    private static void StatusNumberCell(TableDescriptor table, decimal value, string bgColor)
    {
        table.Cell().Background(bgColor)
            .BorderBottom(0.5f).BorderColor("#E0E0E0")
            .Padding(2).AlignRight().Text(value.ToString("N2")).FontSize(7);
    }

    private static void StatusNullableNumberCell(TableDescriptor table, decimal? value, string bgColor)
    {
        if (value.HasValue)
        {
            StatusNumberCell(table, value.Value, bgColor);
        }
        else
        {
            table.Cell().Background(bgColor)
                .BorderBottom(0.5f).BorderColor("#E0E0E0")
                .Padding(2).AlignCenter().Text("—").FontSize(7).FontColor(MediumGray).Italic();
        }
    }

    // ── Logo ────────────────────────────────────────────────────

    private static Stream? GetLogoStream()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));
        return name != null ? assembly.GetManifestResourceStream(name) : null;
    }
}
