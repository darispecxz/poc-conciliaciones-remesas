using Microsoft.Extensions.Logging;
using Reconciliation.Domain.Operations;

namespace Reconciliation.Application.Operations;

public class OperationsService : IOperationsService
{
    private readonly IOperationsRepository _repo;
    private readonly ILogger<OperationsService> _logger;

    public OperationsService(IOperationsRepository repo, ILogger<OperationsService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<OperationsReportDto> GetReportAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        _logger.LogInformation("[OperationsReport] Fetching operations from {From} to {To}",
            fechaDesde.ToString("yyyy-MM-dd"), fechaHasta.ToString("yyyy-MM-dd"));

        const decimal UmbralBob = 34_300m;

        var rows = await _repo.GetOperationsAsync(fechaDesde, fechaHasta);

        // Filter: only users whose total across all operations >= 34,300 BOB
        var qualifiedUsers = rows
            .GroupBy(r => r.UsuarioFinanciero)
            .Where(g => g.Sum(r => r.TotalOperacionBob) >= UmbralBob)
            .Select(g => g.Key)
            .ToHashSet();

        var items = rows
            .Where(r => qualifiedUsers.Contains(r.UsuarioFinanciero))
            .Select(r => new OperationItemDto
            {
                UsuarioFinanciero = r.UsuarioFinanciero,
                DptoTransaccion = r.DptoTransaccion,
                PaisTransaccion = r.PaisTransaccion,
                Edad = r.Edad,
                ActEconomica = r.ActEconomica,
                Pep = r.Pep,
                Agencia = r.Agencia,
                TipoOperacion = r.TipoOperacion,
                TipoTransaccion = r.TipoTransaccion,
                Mes = r.Mes,
                Dia = r.Dia,
                Fecha = r.Fecha,
                NroOp = r.NroOp,
                TotalOperacionBob = r.TotalOperacionBob
            }).ToList();

        var totalOps = items.Sum(i => i.NroOp);
        var totalBob = items.Sum(i => i.TotalOperacionBob);
        var totalUsuarios = qualifiedUsers.Count;

        _logger.LogInformation("[OperationsReport] Found {Count} rows, {Users} users, total BOB {Total:N2}",
            items.Count, totalUsuarios, totalBob);

        return new OperationsReportDto
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            TotalOperaciones = totalOps,
            TotalBob = totalBob,
            TotalUsuarios = totalUsuarios,
            Operaciones = items
        };
    }
}
