namespace Reconciliation.Domain.Operations;

public class OperationRow
{
    public string UsuarioFinanciero { get; set; } = "";
    public string DptoTransaccion { get; set; } = "";
    public string PaisTransaccion { get; set; } = "";
    public int Edad { get; set; }
    public string ActEconomica { get; set; } = "";
    public string Pep { get; set; } = "";
    public string Agencia { get; set; } = "";
    public string TipoOperacion { get; set; } = "";
    public string TipoTransaccion { get; set; } = "";
    public string Mes { get; set; } = "";
    public string Dia { get; set; } = "";
    public DateTime Fecha { get; set; }
    public int NroOp { get; set; }
    public decimal TotalOperacionBob { get; set; }
}
