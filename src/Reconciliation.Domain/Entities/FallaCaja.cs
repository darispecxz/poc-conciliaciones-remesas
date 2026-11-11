namespace Reconciliation.Domain.Entities;

/// <summary>
/// Records known discrepancies per caja per day.
/// PK: (id_caja, fecha). Estado: P=PENDIENTE, R=RECTIFICADO, C=CANCELADO.
/// </summary>
public class FallaCaja
{
    public int IdCaja { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Diferencia { get; set; }
    public string Glosa { get; set; } = string.Empty;
    public string Estado { get; set; } = "P";
}
