using Reconciliation.Application.DTOs;

namespace Reconciliation.Application.Interfaces;

public interface IGerencialChartGenerator
{
    byte[] GenerateRemittanceVolume(IEnumerable<RemesasSerieTemporal> series);
    byte[] GeneratePaymentTypeDistribution(RemesasPorTipoPago paymentTypes);
    byte[] GenerateExchangeOperations(IEnumerable<CambioSerieTemporal> series);
    byte[] GenerateExchangeDifferential(IEnumerable<CambioSerieTemporal> series);
    byte[] GenerateConsolidatedIncome(IEnumerable<SerieIngresos> series);
    byte[] GenerateOperationsByDepartment(IEnumerable<ConsolidadoPorDepartamento> departments);
}
