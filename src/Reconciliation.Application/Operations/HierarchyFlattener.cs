namespace Reconciliation.Application.Operations;

/// <summary>
/// Flattens indented hierarchical text (e.g. Excel mat-tree column A)
/// into tabular rows by propagating parent values down to each leaf.
/// </summary>
public static class HierarchyFlattener
{
    /// <summary>
    /// Parses indented lines into flat rows. Each line's indentation level
    /// determines its position in the hierarchy. A row is emitted every time
    /// the deepest level (<paramref name="depth"/>) is reached.
    /// </summary>
    /// <param name="lines">Lines with leading-space indentation.</param>
    /// <param name="depth">Number of hierarchy levels (columns in output).</param>
    /// <param name="indentSize">Spaces per level. 0 = auto-detect from first indented line.</param>
    /// <returns>List of string arrays, each of length <paramref name="depth"/>.</returns>
    public static List<string[]> Flatten(IReadOnlyList<string> lines, int depth, int indentSize = 0)
    {
        if (indentSize <= 0)
            indentSize = DetectIndentUnit(lines);

        var results = new List<string[]>();
        var path = new string[depth];

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var trimmed = line.TrimStart();
            var leadingSpaces = line.Length - trimmed.Length;
            var level = indentSize > 0 ? leadingSpaces / indentSize : 0;

            if (level >= depth)
                continue;

            // Update current level, parent values (0..level-1) stay unchanged
            path[level] = trimmed;

            // Leaf reached → emit row with all parent values propagated
            if (level == depth - 1)
                results.Add((string[])path.Clone());
        }

        return results;
    }

    /// <summary>
    /// Typed overload: flattens 8-level hierarchy into <see cref="FlatOperationRow"/> objects.
    /// Levels: Ciudad, Pais, Codigo, Rubro, Tipo, Agencia, Operacion, Mes.
    /// </summary>
    public static List<FlatOperationRow> FlattenToOperationRows(IReadOnlyList<string> lines, int indentSize = 0)
    {
        const int depth = 8;
        var raw = Flatten(lines, depth, indentSize);

        return raw.Select(r => new FlatOperationRow
        {
            Ciudad    = r[0],
            Pais      = r[1],
            Codigo    = r[2],
            Rubro     = r[3],
            Tipo      = r[4],
            Agencia   = r[5],
            Operacion = r[6],
            Mes       = r[7]
        }).ToList();
    }

    private static int DetectIndentUnit(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var spaces = line.Length - line.TrimStart().Length;
            if (spaces > 0) return spaces;
        }
        return 3; // fallback: matches OperationsExcelExportService indent
    }
}

public class FlatOperationRow
{
    public string Ciudad { get; init; } = "";
    public string Pais { get; init; } = "";
    public string Codigo { get; init; } = "";
    public string Rubro { get; init; } = "";
    public string Tipo { get; init; } = "";
    public string Agencia { get; init; } = "";
    public string Operacion { get; init; } = "";
    public string Mes { get; init; } = "";
}
