namespace Reconciliation.Domain.Operations;

public interface IOperationsRepository
{
    Task<IEnumerable<OperationRow>> GetOperationsAsync(DateTime fechaDesde, DateTime fechaHasta);
}
