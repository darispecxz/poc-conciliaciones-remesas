namespace Reconciliation.Domain.Entities;

public class Moneda
{
    public int IdMoneda { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Abreviatura { get; set; } = string.Empty;
}
