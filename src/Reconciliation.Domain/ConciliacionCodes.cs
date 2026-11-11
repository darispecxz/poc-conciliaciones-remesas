namespace Reconciliation.Domain;

/// <summary>
/// All conciliation codes used in saldo_caja and saldo_boveda.
/// Reconstructed from SQL + Excel cross-reference (data dictionary is incomplete).
/// </summary>
public static class ConciliacionCaja
{
    public const string Apertura = "A";
    public const string Cierre = "C";

    // Cambio de Moneda
    public static readonly string[] CambioIngreso = { "EC", "AC" };
    public static readonly string[] CambioEgreso = { "SC", "VC" };

    // Giro Nacional
    public static readonly string[] GiroIngreso = { "EG", "IG", "OG", "AG" };
    public static readonly string[] GiroEgresoPago = { "SG" };
    public static readonly string[] GiroEgresoDevolucion = { "DG" };

    // Remesa Internacional
    public static readonly string[] RemesaIngreso = { "ER", "IR", "OR", "AR" };
    public static readonly string[] RemesaEgresoPago = { "SR", "LR" };
    public static readonly string[] RemesaEgresoDevolucion = { "VR" };

    // Transferencia Bóveda ↔ Caja
    public static readonly string[] TransferenciaBovedaIngreso = { "ET" };
    public static readonly string[] TransferenciaBovedaEgreso = { "ST" };

    // Traspaso entre Cajas
    public static readonly string[] TraspasoCajaIngreso = { "EO" };
    public static readonly string[] TraspasoCajaEgreso = { "SO" };

    // Cobro de Cheque
    public static readonly string[] CobroCheque = { "EE", "RB" };

    // Depósito Bancario
    public static readonly string[] DepositoBancario = { "SE", "DR", "DB" };

    // Controlados (autorizados)
    public static readonly string[] IngresoControlado = { "IA" };
    public static readonly string[] EgresoControlado = { "EA", "PG" };
}

/// <summary>
/// Boveda conciliation codes use PREFIX-ID format (e.g., SAC-12).
/// Use SUBSTRING_INDEX(conciliacion, '-', 1) in SQL to match.
/// </summary>
public static class ConciliacionBoveda
{
    public const string Apertura = "A";
    public const string Cierre = "C";

    // Egresos (prefix)
    public const string SalidaAperturaCaja = "SAC";
    public const string SalidaTraspasoBoveda = "STB";
    public const string SalidaTraspasoCaja = "STC";

    // Ingresos (prefix)
    public const string EntradaCierreCaja = "ECC";
    public const string EntradaTraspasoBoveda = "ETB";
    public const string EntradaTraspasoCasa = "ETC";

    public static readonly string[] EgresoPrefixes = { SalidaAperturaCaja, SalidaTraspasoBoveda, SalidaTraspasoCaja };
    public static readonly string[] IngresoPrefixes = { EntradaCierreCaja, EntradaTraspasoBoveda, EntradaTraspasoCasa };
}
