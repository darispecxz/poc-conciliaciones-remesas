using Reconciliation.Application.DTOs;

namespace Reconciliation.Application.Interfaces;

public interface IGerencialExcelExportService
{
    byte[] GenerateGerencialReport(GerencialReport report, GerencialChartImages charts);
}
