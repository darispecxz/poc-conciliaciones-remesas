using System.Reflection;
using ClosedXML.Excel;

namespace Reconciliation.Application.Operations;

public class OperationsExcelExportService : IOperationsExcelExportService
{
    // ── Corporate palette ───────────────────────────────────────
    private static readonly XLColor Orange = XLColor.FromHtml("#E87722");
    private static readonly XLColor JetBlack = XLColor.FromHtml("#000000");
    private static readonly XLColor MediumGray = XLColor.FromHtml("#808184");
    private static readonly XLColor White = XLColor.FromHtml("#FFFFFF");
    private static readonly XLColor Level0Bg = XLColor.FromHtml("#E8F5EE");
    private static readonly XLColor TotalBg = XLColor.FromHtml("#0F4A31");

    private const int HeaderRowOffset = 5; // rows 1-4 = marquee, row 5 = headers

    public byte[] GenerateOperationsReport(OperationsReportDto report)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Reporte Operaciones");

        const int colCount = 3;
        WriteMarquee(ws, "Reporte de Operaciones", report.FechaDesde, report.FechaHasta, colCount);

        // Headers
        ws.Cell(HeaderRowOffset, 1).Value = "Concepto";
        ws.Cell(HeaderRowOffset, 2).Value = "Nro. Operaciones";
        ws.Cell(HeaderRowOffset, 3).Value = "Total BOB";
        StyleHeaderRow(ws, colCount);

        // Build tree, write rows, and collect outline ranges
        var tree = BuildTree(report.Operaciones);
        var row = HeaderRowOffset + 1;
        var outlineRanges = new List<(int StartRow, int EndRow, int Level)>();
        WriteTreeNode(ws, tree, 0, ref row, outlineRanges);

        var lastDataRow = row - 1;

        // Apply Excel outline grouping (collapse children under each parent)
        ws.Outline.SummaryVLocation = XLOutlineSummaryVLocation.Top;
        foreach (var (startRow, endRow, level) in outlineRanges)
        {
            if (startRow <= endRow)
                ws.Rows(startRow, endRow).Group(Math.Min(level, 8));
        }

        // Total general row
        var totalRow = row;
        ws.Cell(totalRow, 1).Value = "TOTAL GENERAL";
        ws.Cell(totalRow, 2).Value = report.TotalOperaciones;
        ws.Cell(totalRow, 3).Value = report.TotalBob;
        var totalRange = ws.Range(totalRow, 1, totalRow, colCount);
        totalRange.Style.Fill.BackgroundColor = TotalBg;
        totalRange.Style.Font.FontColor = White;
        totalRange.Style.Font.Bold = true;

        // Formatting
        ws.Range(HeaderRowOffset + 1, 2, totalRow, 2)
            .Style.NumberFormat.Format = "#,##0";
        ws.Range(HeaderRowOffset + 1, 3, totalRow, 3)
            .Style.NumberFormat.Format = "#,##0.00";

        ApplyDataBorders(ws, totalRow, colCount);
        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 60;

        return ToBytes(workbook);
    }

    // ── Tree building ───────────────────────────────────────────

    private static readonly Func<OperationItemDto, string>[] GroupSelectors =
    {
        o => o.UsuarioFinanciero,
        o => o.DptoTransaccion,
        o => o.PaisTransaccion,
        o => o.Edad.ToString(),
        o => o.ActEconomica,
        o => o.Pep,
        o => o.Agencia,
        o => o.TipoOperacion,
        o => o.Mes,
        o => o.Dia,
        o => o.Fecha.ToString("yyyy-MM-dd")
    };

    private static TreeNode BuildTree(List<OperationItemDto> items)
    {
        var root = new TreeNode { Label = "ROOT" };
        BuildLevel(root, items, 0);
        return root;
    }

    private static void BuildLevel(TreeNode parent, List<OperationItemDto> items, int level)
    {
        if (level >= GroupSelectors.Length)
        {
            // Leaf: accumulate totals directly on parent
            parent.NroOp = items.Sum(i => i.NroOp);
            parent.TotalBob = items.Sum(i => i.TotalOperacionBob);
            return;
        }

        var selector = GroupSelectors[level];
        var groups = items.GroupBy(selector).OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var child = new TreeNode { Label = group.Key };
            BuildLevel(child, group.ToList(), level + 1);
            parent.Children.Add(child);
        }

        // Roll up totals from children
        parent.NroOp = parent.Children.Sum(c => c.NroOp);
        parent.TotalBob = parent.Children.Sum(c => c.TotalBob);
    }

    private static void WriteTreeNode(IXLWorksheet ws, TreeNode node, int level, ref int row,
        List<(int StartRow, int EndRow, int Level)> outlineRanges)
    {
        if (level > 0) // Skip root
        {
            var indent = new string(' ', (level - 1) * 3);
            ws.Cell(row, 1).Value = indent + node.Label;
            ws.Cell(row, 2).Value = node.NroOp;
            ws.Cell(row, 3).Value = node.TotalBob;

            if (level == 1)
            {
                var range = ws.Range(row, 1, row, 3);
                range.Style.Font.Bold = true;
                range.Style.Fill.BackgroundColor = Level0Bg;
            }

            row++;
        }

        if (node.Children.Count > 0)
        {
            var childrenStart = row;
            foreach (var child in node.Children)
                WriteTreeNode(ws, child, level + 1, ref row, outlineRanges);

            // Group children rows under their parent (level 0 = root, skip)
            if (level > 0)
                outlineRanges.Add((childrenStart, row - 1, level));
        }
    }

    private class TreeNode
    {
        public string Label { get; set; } = "";
        public int NroOp { get; set; }
        public decimal TotalBob { get; set; }
        public List<TreeNode> Children { get; } = new();
    }

    // ── Shared helpers (same as ExcelExportService) ─────────────

    private static void WriteMarquee(IXLWorksheet ws, string title,
        DateTime fechaDesde, DateTime fechaHasta, int colCount)
    {
        // Row 1: Logo + company name
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));
        if (resourceName != null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            var img = ws.AddPicture(ms).MoveTo(ws.Cell(1, 1));
            img.Width = 120;
            img.Height = 48;
        }
        ws.Cell(1, 2).Value = "GAMBARTE S.R.L.";
        ws.Cell(1, 2).Style.Font.Bold = true;
        ws.Cell(1, 2).Style.Font.FontSize = 16;
        ws.Cell(1, 2).Style.Font.FontColor = XLColor.FromHtml("#000000");

        // Row 2: Slogan
        ws.Cell(2, 1).Value = "Cambio de Divisas, Giros y Remesas";
        ws.Cell(2, 1).Style.Font.FontSize = 9;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#808184");
        ws.Cell(2, 1).Style.Font.Italic = true;

        // Row 3: Title bar
        var titleRange = ws.Range(3, 1, 3, colCount);
        titleRange.Merge();
        titleRange.Value = title;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 13;
        titleRange.Style.Font.FontColor = XLColor.FromHtml("#FFFFFF");
        titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E87722");
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(3).Height = 28;

        // Row 4: Date
        ws.Cell(4, colCount).Value =
            $"Periodo: {fechaDesde:yyyy-MM-dd} al {fechaHasta:yyyy-MM-dd}  |  Generado: {DateTime.Now:yyyy-MM-dd HH:mm}";
        ws.Cell(4, colCount).Style.Font.FontSize = 9;
        ws.Cell(4, colCount).Style.Font.FontColor = XLColor.FromHtml("#808184");
        ws.Cell(4, colCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void StyleHeaderRow(IXLWorksheet ws, int colCount)
    {
        var range = ws.Range(HeaderRowOffset, 1, HeaderRowOffset, colCount);
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 10;
        range.Style.Font.FontColor = XLColor.FromHtml("#FFFFFF");
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#000000");
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.WrapText = true;
        ws.Row(HeaderRowOffset).Height = 24;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        range.Style.Border.BottomBorderColor = XLColor.FromHtml("#E87722");
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

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
