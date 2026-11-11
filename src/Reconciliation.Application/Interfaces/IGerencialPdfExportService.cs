using Reconciliation.Application.DTOs;

namespace Reconciliation.Application.Interfaces;

public interface IGerencialPdfExportService
{
    byte[] GenerateGerencialReport(GerencialReport report, GerencialChartImages charts);
}
