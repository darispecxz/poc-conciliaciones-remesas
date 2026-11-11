namespace Reconciliation.Application.Operations;

public class OperationsReportDto
{
    public DateTime FechaDesde { get; init; }
    public DateTime FechaHasta { get; init; }
    public int TotalOperaciones { get; init; }
    public decimal TotalBob { get; init; }
    public int TotalUsuarios { get; init; }
    public List<OperationItemDto> Operaciones { get; init; } = new();
}

public class OperationItemDto
{
    public string UsuarioFinanciero { get; init; } = "";
    public string DptoTransaccion { get; init; } = "";
    public string PaisTransaccion { get; init; } = "";
    public int Edad { get; init; }
    public string ActEconomica { get; init; } = "";
    public string Pep { get; init; } = "";
    public string Agencia { get; init; } = "";
    public string TipoOperacion { get; init; } = "";
    public string TipoTransaccion { get; init; } = "";
    public string Mes { get; init; } = "";
    public string Dia { get; init; } = "";
    public DateTime Fecha { get; init; }
    public int NroOp { get; init; }
    public decimal TotalOperacionBob { get; init; }
}
