namespace Reconciliation.Application.DTOs;

// ═══ Top-level report ═══════════════════════════════════════════════

public class GerencialReport
{
    public string Tipo { get; init; } = "";
    public DateTime FechaDesde { get; init; }
    public DateTime FechaHasta { get; init; }
    public DateTime FechaProcesamiento { get; init; }
    public RemesasResumen Remesas { get; init; } = new();
    public CambioResumen CambioMoneda { get; init; } = new();
    public ConsolidadoResumen Consolidado { get; init; } = new();
}

// ═══ Remittances ════════════════════════════════════════════════════

public class RemesasResumen
{
    public decimal TotalBob { get; init; }
    public decimal TotalUsd { get; init; }
    public int TotalTransacciones { get; init; }
    public decimal TotalComisiones { get; init; }
    public decimal TicketPromedioBob { get; init; }
    public decimal TicketPromedioUsd { get; init; }
    public decimal? VariacionPorcentual { get; init; }
    // Paid remittances (estado=1 in period)
    public decimal PagadasBob { get; init; }
    public decimal PagadasUsd { get; init; }
    public int PagadasTransacciones { get; init; }
    public RemesasPorTipoPago PorTipoPago { get; init; } = new();
    public List<RemesasPorDepartamento> PorDepartamento { get; init; } = new();
    public List<RemesasSerieTemporal> SerieTemporal { get; init; } = new();
}

public class RemesasPorTipoPago
{
    public TipoPagoDetalle VentanillaBob { get; init; } = new();
    public TipoPagoDetalle VentanillaUsd { get; init; } = new();
    public TipoPagoDetalle TransferenciaBob { get; init; } = new();
    public TipoPagoDetalle TransferenciaUsd { get; init; } = new();
}

public class TipoPagoDetalle
{
    public decimal Monto { get; init; }
    public int Transacciones { get; init; }
}

public class RemesasPorDepartamento
{
    public string Departamento { get; init; } = "";
    public int AgenciasTotales { get; init; }
    public int AgenciasActivas { get; init; }
    public int Transacciones { get; init; }
    public decimal MontoBob { get; init; }
    public decimal MontoUsd { get; init; }
    public int TransaccionesVentanilla { get; init; }
    public int TransaccionesTransferencia { get; init; }
    public decimal Comisiones { get; init; }
    public string Estado { get; init; } = "";
}

public class RemesasSerieTemporal
{
    public string Periodo { get; init; } = "";
    public decimal MontoBob { get; init; }
    public decimal MontoUsd { get; init; }
    public int Transacciones { get; init; }
    // Paid in this period
    public decimal PagadasBob { get; init; }
    public decimal PagadasUsd { get; init; }
    public int PagadasTransacciones { get; init; }
}

// ═══ Currency exchange ══════════════════════════════════════════════

public class CambioResumen
{
    public int TotalOperaciones { get; init; }
    public decimal BobRecibido { get; init; }
    public decimal BobEntregado { get; init; }
    public decimal GananciaBob { get; init; }
    public decimal? VariacionPorcentual { get; init; }
    public List<CambioPorDepartamento> PorDepartamento { get; init; } = new();
    public List<CambioSerieTemporal> SerieTemporal { get; init; } = new();
}

public class CambioPorDepartamento
{
    public string Departamento { get; init; } = "";
    public int TotalOperaciones { get; init; }
    public decimal BobRecibido { get; init; }
    public decimal BobEntregado { get; init; }
    public decimal GananciaBob { get; init; }
}

public class CambioSerieTemporal
{
    public string Periodo { get; init; } = "";
    public int TotalOperaciones { get; init; }
    public decimal BobRecibido { get; init; }
    public decimal BobEntregado { get; init; }
    public decimal GananciaBob { get; init; }
}

// ═══ Consolidated ═══════════════════════════════════════════════════

public class ConsolidadoResumen
{
    public decimal IngresoRemesasBob { get; init; }
    public decimal IngresoCambioBob { get; init; }
    public decimal IngresoTotalBob { get; init; }
    public int TotalOperaciones { get; init; }
    public int AgenciasActivas { get; init; }
    public int AgenciasTotales { get; init; }
    public decimal? VariacionIngresoTotal { get; init; }
    public List<AlertaOperacional> Alertas { get; init; } = new();
    public List<ConsolidadoPorDepartamento> PorDepartamento { get; init; } = new();
    public List<SerieIngresos> SerieIngresos { get; init; } = new();
}

public class AlertaOperacional
{
    public string TipoAlerta { get; init; } = "";
    public int Cantidad { get; init; }
    public decimal MontoTotal { get; init; }
}

public class ConsolidadoPorDepartamento
{
    public string Departamento { get; init; } = "";
    public int TransaccionesRemesas { get; init; }
    public int OperacionesCambio { get; init; }
    public int TotalOperaciones { get; init; }
    public decimal IngresoRemesasBob { get; init; }
    public decimal IngresoCambioBob { get; init; }
    public decimal IngresoTotalBob { get; init; }
}

public class SerieIngresos
{
    public string Periodo { get; init; } = "";
    public decimal RemesasBob { get; init; }
    public decimal CambioBob { get; init; }
    public decimal TotalBob { get; init; }
}

// ═══ Chart images container ═════════════════════════════════════════

public class GerencialChartImages
{
    public byte[] RemittanceVolume { get; init; } = Array.Empty<byte>();
    public byte[] PaymentTypeDistribution { get; init; } = Array.Empty<byte>();
    public byte[] ExchangeOperations { get; init; } = Array.Empty<byte>();
    public byte[] ExchangeDifferential { get; init; } = Array.Empty<byte>();
    public byte[] ConsolidatedIncome { get; init; } = Array.Empty<byte>();
    public byte[] OperationsByDepartment { get; init; } = Array.Empty<byte>();
}
