namespace Reconciliation.Application.Operations;

public interface IOperationsService
{
    Task<OperationsReportDto> GetReportAsync(DateTime fechaDesde, DateTime fechaHasta);
}
