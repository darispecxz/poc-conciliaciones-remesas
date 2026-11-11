namespace Reconciliation.Application.Operations;

public interface IOperationsExcelExportService
{
    byte[] GenerateOperationsReport(OperationsReportDto report);
}
