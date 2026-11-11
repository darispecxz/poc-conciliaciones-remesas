using Reconciliation.Application.DTOs;

namespace Reconciliation.Application.Interfaces;

public interface IGerencialService
{
    Task<GerencialReport> GetReportAsync(
        DateTime fechaDesde, DateTime fechaHasta, string tipo, int? idAgencia = null);
}
