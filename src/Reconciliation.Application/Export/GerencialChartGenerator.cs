using Microsoft.Extensions.Logging;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;
using ScottPlot;

namespace Reconciliation.Application.Export;

public class GerencialChartGenerator : IGerencialChartGenerator
{
    private readonly ILogger<GerencialChartGenerator> _logger;

    // ── Gambarte corporate palette ──────────────────────────────────
    private static readonly ScottPlot.Color MainGreen = ScottPlot.Color.FromHex("#0F6E56");
    private static readonly ScottPlot.Color LightGreen = ScottPlot.Color.FromHex("#1D9E75");
    private static readonly ScottPlot.Color VeryLightGreen = ScottPlot.Color.FromHex("#5DCAA5");
    private static readonly ScottPlot.Color Blue = ScottPlot.Color.FromHex("#378ADD");
    private static readonly ScottPlot.Color LightBlue = ScottPlot.Color.FromHex("#85B7EB");
    private static readonly ScottPlot.Color DarkBlue = ScottPlot.Color.FromHex("#185FA5");
    private static readonly ScottPlot.Color Amber = ScottPlot.Color.FromHex("#EF9F27");
    private static readonly ScottPlot.Color Gray = ScottPlot.Color.FromHex("#888780");
    private static readonly ScottPlot.Color White = ScottPlot.Color.FromHex("#FFFFFF");

    public GerencialChartGenerator(ILogger<GerencialChartGenerator> logger)
    {
        _logger = logger;
    }

    // ═══ Chart 1: Remittance volume by period ═══════════════════════
    // Grouped bar chart: "Enviadas BOB" (#1D9E75) | "Pagadas BOB" (#378ADD)
    // Size: 700 × 300 px

    public byte[] GenerateRemittanceVolume(IEnumerable<RemesasSerieTemporal> series)
    {
        var data = series.ToList();
        if (data.Count == 0) return GenerateNoDataImage(700, 300);
        if (data.All(s => s.MontoBob == 0 && s.PagadasBob == 0))
            return GenerateNoActivityImage(700, 300, "Remesas: Enviadas vs Pagadas");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var plot = new Plot();
            ConfigurePlot(plot);

            var positions = Enumerable.Range(0, data.Count).Select(i => (double)i).ToArray();
            double barWidth = 0.35;

            var sentBars = new List<Bar>();
            var paidBars = new List<Bar>();

            for (int i = 0; i < data.Count; i++)
            {
                sentBars.Add(new Bar { Position = i - barWidth / 2, Value = (double)data[i].MontoBob, FillColor = LightGreen });
                paidBars.Add(new Bar { Position = i + barWidth / 2, Value = (double)data[i].PagadasBob, FillColor = Blue });
            }

            var bp1 = plot.Add.Bars(sentBars.ToArray());
            bp1.LegendText = "Enviadas BOB";
            var bp2 = plot.Add.Bars(paidBars.ToArray());
            bp2.LegendText = "Pagadas BOB";

            plot.Axes.Bottom.SetTicks(
                positions,
                data.Select(d => TruncateLabel(d.Periodo)).ToArray());
            plot.Title("Remesas: Enviadas vs Pagadas (BOB)");
            plot.YLabel("Monto BOB");
            plot.ShowLegend(Alignment.UpperRight);

            var bytes = plot.GetImageBytes(700, 300, ImageFormat.Png);
            sw.Stop();
            _logger.LogInformation("[Chart] RemittanceVolume generated in {Ms}ms", sw.ElapsedMilliseconds);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Chart] Failed to generate RemittanceVolume");
            return GenerateNoDataImage(700, 300);
        }
    }

    // ═══ Chart 2: Payment type distribution ═════════════════════════
    // Donut chart — Size: 400 × 350 px

    public byte[] GeneratePaymentTypeDistribution(RemesasPorTipoPago paymentTypes)
    {
        var values = new[]
        {
            (double)paymentTypes.VentanillaBob.Monto,
            (double)paymentTypes.VentanillaUsd.Monto,
            (double)paymentTypes.TransferenciaBob.Monto,
            (double)paymentTypes.TransferenciaUsd.Monto
        };
        var labels = new[] { "Ventanilla BOB", "Ventanilla USD", "Transferencia BOB", "Transferencia USD" };
        var colors = new[] { LightGreen, VeryLightGreen, DarkBlue, LightBlue };

        if (values.All(v => v == 0))
            return GenerateNoActivityImage(400, 350, "Distribucion por Tipo de Pago");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var plot = new Plot();
            ConfigurePlot(plot);

            var slices = new List<PieSlice>();
            double total = values.Sum();
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] > 0)
                {
                    var pct = total > 0 ? values[i] / total * 100 : 0;
                    slices.Add(new PieSlice
                    {
                        Value = values[i],
                        FillColor = colors[i],
                        Label = $"{labels[i]} ({pct:F1}%)"
                    });
                }
            }

            var pie = plot.Add.Pie(slices);
            pie.DonutFraction = 0.5;
            plot.Title("Distribucion por Tipo de Pago");
            plot.ShowLegend(Alignment.LowerCenter);
            plot.Axes.Frameless();
            plot.HideGrid();

            var bytes = plot.GetImageBytes(400, 350, ImageFormat.Png);
            sw.Stop();
            _logger.LogInformation("[Chart] PaymentTypeDistribution generated in {Ms}ms", sw.ElapsedMilliseconds);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Chart] Failed to generate PaymentTypeDistribution");
            return GenerateNoDataImage(400, 350);
        }
    }

    // ═══ Chart 3: Exchange operations by period ═════════════════════
    // Grouped bar chart: "BOB Recibido" (#378ADD) | "BOB Entregado" (#1D9E75)
    // Size: 700 × 300 px

    public byte[] GenerateExchangeOperations(IEnumerable<CambioSerieTemporal> series)
    {
        var data = series.ToList();
        if (data.Count == 0) return GenerateNoDataImage(700, 300);
        if (data.All(s => s.BobRecibido == 0 && s.BobEntregado == 0))
            return GenerateNoActivityImage(700, 300, "Cambio de Divisas: Flujo BOB");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var plot = new Plot();
            ConfigurePlot(plot);

            double barWidth = 0.35;
            var recBars = new List<Bar>();
            var entBars = new List<Bar>();

            for (int i = 0; i < data.Count; i++)
            {
                recBars.Add(new Bar { Position = i - barWidth / 2, Value = (double)data[i].BobRecibido, FillColor = Blue });
                entBars.Add(new Bar { Position = i + barWidth / 2, Value = (double)data[i].BobEntregado, FillColor = LightGreen });
            }

            var bp1 = plot.Add.Bars(recBars.ToArray());
            bp1.LegendText = "BOB Recibido";
            var bp2 = plot.Add.Bars(entBars.ToArray());
            bp2.LegendText = "BOB Entregado";

            var tickPositions = data.Select((_, i) => (double)i).ToArray();
            var tickLabels = data.Select(d => TruncateLabel(d.Periodo)).ToArray();
            plot.Axes.Bottom.SetTicks(tickPositions, tickLabels);
            plot.Title("Cambio de Divisas: Flujo BOB por Periodo");
            plot.YLabel("Monto BOB");
            plot.ShowLegend(Alignment.UpperRight);

            var bytes = plot.GetImageBytes(700, 300, ImageFormat.Png);
            sw.Stop();
            _logger.LogInformation("[Chart] ExchangeOperations generated in {Ms}ms", sw.ElapsedMilliseconds);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Chart] Failed to generate ExchangeOperations");
            return GenerateNoDataImage(700, 300);
        }
    }

    // ═══ Chart 4: Exchange profit/loss ═══════════════════════════════
    // Simple bar chart: positive = green (#1D9E75), negative = red (#EF9F27)
    // Size: 700 × 250 px

    public byte[] GenerateExchangeDifferential(IEnumerable<CambioSerieTemporal> series)
    {
        var data = series.ToList();
        if (data.Count == 0) return GenerateNoDataImage(700, 250);
        if (data.All(s => s.GananciaBob == 0))
            return GenerateNoActivityImage(700, 250, "Ganancia/Perdida Cambio por Periodo");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var plot = new Plot();
            ConfigurePlot(plot);

            var bars = data.Select((d, i) => new Bar
            {
                Position = i,
                Value = (double)d.GananciaBob,
                FillColor = d.GananciaBob >= 0 ? LightGreen : Amber
            }).ToArray();

            var bp = plot.Add.Bars(bars);
            bp.LegendText = "Ganancia BOB";

            var tickPositions = data.Select((_, i) => (double)i).ToArray();
            var tickLabels = data.Select(d => TruncateLabel(d.Periodo)).ToArray();
            plot.Axes.Bottom.SetTicks(tickPositions, tickLabels);
            plot.Title("Ganancia/Perdida Cambio por Periodo");
            plot.YLabel("Monto BOB");

            var bytes = plot.GetImageBytes(700, 250, ImageFormat.Png);
            sw.Stop();
            _logger.LogInformation("[Chart] ExchangeDifferential generated in {Ms}ms", sw.ElapsedMilliseconds);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Chart] Failed to generate ExchangeDifferential");
            return GenerateNoDataImage(700, 250);
        }
    }

    // ═══ Chart 5: Consolidated income ═══════════════════════════════
    // Stacked bar chart: "Remesas" (#1D9E75) + "Cambio" (#EF9F27)
    // Size: 700 × 300 px

    public byte[] GenerateConsolidatedIncome(IEnumerable<SerieIngresos> series)
    {
        var data = series.ToList();
        if (data.Count == 0) return GenerateNoDataImage(700, 300);
        if (data.All(s => s.TotalBob == 0))
            return GenerateNoActivityImage(700, 300, "Ingresos Consolidados por Periodo");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var plot = new Plot();
            ConfigurePlot(plot);

            // Stacked: remesas at base, cambio on top
            var remBars = data.Select((d, i) => new Bar
            {
                Position = i,
                Value = (double)d.RemesasBob,
                FillColor = LightGreen
            }).ToArray();

            var camBars = data.Select((d, i) => new Bar
            {
                Position = i,
                ValueBase = (double)d.RemesasBob,
                Value = (double)(d.RemesasBob + d.CambioBob),
                FillColor = Amber
            }).ToArray();

            var bp1 = plot.Add.Bars(remBars);
            bp1.LegendText = "Remesas";
            var bp2 = plot.Add.Bars(camBars);
            bp2.LegendText = "Cambio";

            var tickPositions = data.Select((_, i) => (double)i).ToArray();
            var tickLabels = data.Select(d => TruncateLabel(d.Periodo)).ToArray();
            plot.Axes.Bottom.SetTicks(tickPositions, tickLabels);
            plot.Title("Ingresos Consolidados por Periodo");
            plot.YLabel("Monto BOB");
            plot.ShowLegend(Alignment.UpperRight);

            var bytes = plot.GetImageBytes(700, 300, ImageFormat.Png);
            sw.Stop();
            _logger.LogInformation("[Chart] ConsolidatedIncome generated in {Ms}ms", sw.ElapsedMilliseconds);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Chart] Failed to generate ConsolidatedIncome");
            return GenerateNoDataImage(700, 300);
        }
    }

    // ═══ Chart 6: Operations by department ══════════════════════════
    // Horizontal bar chart: "Remesas" (#1D9E75) | "Cambio" (#378ADD)
    // Size: 500 × 350 px

    public byte[] GenerateOperationsByDepartment(IEnumerable<ConsolidadoPorDepartamento> departments)
    {
        var data = departments
            .OrderBy(d => d.TotalOperaciones)
            .ToList();

        if (data.Count == 0) return GenerateNoDataImage(500, 350);
        if (data.All(d => d.TotalOperaciones == 0))
            return GenerateNoActivityImage(500, 350, "Operaciones por Departamento");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var plot = new Plot();
            ConfigurePlot(plot);

            double barWidth = 0.35;
            var remBars = new List<Bar>();
            var exBars = new List<Bar>();

            for (int i = 0; i < data.Count; i++)
            {
                remBars.Add(new Bar
                {
                    Position = i - barWidth / 2,
                    Value = data[i].TransaccionesRemesas,
                    FillColor = LightGreen
                });
                exBars.Add(new Bar
                {
                    Position = i + barWidth / 2,
                    Value = data[i].OperacionesCambio,
                    FillColor = Blue
                });
            }

            var bp1 = plot.Add.Bars(remBars.ToArray());
            bp1.LegendText = "Remesas";
            bp1.Horizontal = true;
            var bp2 = plot.Add.Bars(exBars.ToArray());
            bp2.LegendText = "Cambio";
            bp2.Horizontal = true;

            var tickPositions = data.Select((_, i) => (double)i).ToArray();
            var tickLabels = data.Select(d => TruncateLabel(d.Departamento, 15)).ToArray();
            plot.Axes.Left.SetTicks(tickPositions, tickLabels);
            plot.Title("Operaciones por Departamento");
            plot.XLabel("Numero de operaciones");
            plot.ShowLegend(Alignment.LowerRight);

            var bytes = plot.GetImageBytes(500, 350, ImageFormat.Png);
            sw.Stop();
            _logger.LogInformation("[Chart] OperationsByDepartment generated in {Ms}ms", sw.ElapsedMilliseconds);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Chart] Failed to generate OperationsByDepartment");
            return GenerateNoDataImage(500, 350);
        }
    }

    // ═══ Helpers ════════════════════════════��═══════════════════════

    private static void ConfigurePlot(Plot plot)
    {
        plot.FigureBackground.Color = White;
        plot.DataBackground.Color = White;
    }

    private static string TruncateLabel(string label, int max = 10)
    {
        if (string.IsNullOrEmpty(label)) return "";
        return label.Length > max ? label[..max] : label;
    }

    private static byte[] GenerateNoDataImage(int width, int height)
    {
        try
        {
            var plot = new Plot();
            ConfigurePlot(plot);
            plot.Title("Sin datos para el periodo");
            plot.Axes.Frameless();
            plot.HideGrid();
            return plot.GetImageBytes(width, height, ImageFormat.Png);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static byte[] GenerateNoActivityImage(int width, int height, string title)
    {
        try
        {
            var plot = new Plot();
            ConfigurePlot(plot);
            plot.Title($"{title}\n(Sin actividad)");
            return plot.GetImageBytes(width, height, ImageFormat.Png);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}
