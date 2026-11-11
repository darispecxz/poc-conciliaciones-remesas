namespace Reconciliation.Domain;

/// <summary>
/// Operation type identifiers used to classify remittances in each database.
/// Only records matching these id_operacion values are valid remittance transactions.
/// </summary>
public static class RemittanceOperationType
{
    /// <summary>Bolivia remittance operation types (remesa + pago tables).</summary>
    public static readonly int[] Bolivia = [4, 16];

    /// <summary>Chile remittance operation type (tmp_reporte_remesa + pago tables).</summary>
    public static readonly int[] Chile = [3, 5, 6, 8];
}

public static class RemittancePaymentType
{
    /// <summary>Bolivia remittance operation types (remesa + pago tables).</summary>
    public static readonly int[] Bolivia = [4, 7];

    /// <summary>Chile remittance operation type (tmp_reporte_remesa + pago tables).</summary>
    public static readonly int[] Chile = [3, 5, 6, 8];
}

/// <summary>
/// Status codes for Bolivia remesa table.
/// </summary>
public static class RemittanceStatusBolivia
{
    public const int Pendiente = 0;
    public const int Pagado = 1;
    public const int Devuelto = 2;
    public const int Anulado = 5;

    public static string ToLabel(int estado) => estado switch
    {
        Pendiente => "PENDIENTE",
        Pagado => "PAGADO",
        Devuelto => "DEVUELTO",
        Anulado => "ANULADO",
        _ => $"DESCONOCIDO({estado})"
    };
}

/// <summary>
/// Status codes for Chile tmp_reporte_remesa table.
/// </summary>
public static class RemittanceStatusChile
{
    public const int Pendiente = 0;
    public const int Pagado = 1;
    public const int Devuelto = 2;
    public const int DevueltoSC = 3;
    public const int Cambiado = 4;
    public const int Anulado = 5;
    public const int ADevolver = 6;
    public const int EnProceso = 7;
    public const int Ahorro = 8;

    public static string ToLabel(int estado) => estado switch
    {
        Pendiente => "PENDIENTE",
        Pagado => "PAGADO",
        Devuelto => "DEVUELTO",
        DevueltoSC => "DEVUELTO SC",
        Cambiado => "CAMBIADO",
        Anulado => "ANULADO",
        ADevolver => "A DEVOLVER",
        EnProceso => "EN PROCESO",
        Ahorro => "AHORRO",
        _ => $"DESCONOCIDO({estado})"
    };
}

/// <summary>
/// Concepto patterns used to match remittance-related accounting entries in co_asientos.
/// </summary>
public static class AccountingConceptPatterns
{
    /// <summary>BOL→CHL pending transfer entry.</summary>
    public const string EnvioDe = "%ENVIO DE%";

    /// <summary>BOL→CHL paid: payment at destination.</summary>
    public const string PagoEnDestino = "PAGO EN DESTINO%";

    /// <summary>CHL→BOL paid: pending collection entry.</summary>
    public const string PendientesCobro = "%PENDIENTES DE COBRO DEL PAGO DE REMESA%";

    /// <summary>CHL→BOL paid: payment of remittance entry (includes CGR code).</summary>
    public const string PagoRemesaCgr = "%PAGO DE REMESA CGR%";
}
