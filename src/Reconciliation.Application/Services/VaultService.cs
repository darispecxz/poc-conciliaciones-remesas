using Microsoft.Extensions.Logging;
using Reconciliation.Application.DTOs;
using Reconciliation.Application.Interfaces;
using Reconciliation.Domain.Interfaces;

namespace Reconciliation.Application.Services;

public class VaultService : IVaultService
{
    private readonly IVaultRepository _vaultRepo;
    private readonly ILogger<VaultService> _logger;

    public VaultService(IVaultRepository vaultRepo, ILogger<VaultService> logger)
    {
        _vaultRepo = vaultRepo;
        _logger = logger;
    }

    public async Task<VaultReconciliationReport> GetReconciliationAsync(DateTime fecha, int? idAgencia = null)
    {
        var rows = await _vaultRepo.GetReconciliationAsync(fecha, idAgencia);

        var agencies = rows
            .GroupBy(r => (r.IdAgencia, r.Agencia))
            .Select(g => new AgencyVaultBreakdown
            {
                IdAgencia = g.Key.IdAgencia,
                Agencia = g.Key.Agencia,
                Movimientos = g.Select(r => new VaultMovement
                {
                    IdBoveda = r.IdBoveda,
                    NumBoveda = r.NumBoveda,
                    IdMoneda = r.IdMoneda,
                    Moneda = r.MonedaAbreviatura,
                    SaldoApertura = r.SaldoApertura,
                    SalidaAperturaCaja = r.SalidaAperturaCaja,
                    SalidaTraspasoBoveda = r.SalidaTraspasoBoveda,
                    SalidaTraspasoCaja = r.SalidaTraspasoCaja,
                    TotalEgresos = r.TotalEgresos,
                    EntradaCierreCaja = r.EntradaCierreCaja,
                    EntradaTraspasoBoveda = r.EntradaTraspasoBoveda,
                    EntradaTraspasoCasa = r.EntradaTraspasoCasa,
                    TotalIngresos = r.TotalIngresos,
                    SaldoFinal = r.SaldoFinal
                }).OrderBy(m => m.Moneda).ToList()
            })
            .OrderBy(a => a.Agencia)
            .ToList();

        _logger.LogInformation(
            "[Vault] {Fecha}: {AgencyCount} agencies, {BovedaCount} bovedas processed",
            fecha.ToString("yyyy-MM-dd"), agencies.Count,
            agencies.Sum(a => a.Movimientos.Count));

        return new VaultReconciliationReport { Fecha = fecha, Agencies = agencies };
    }

    public async Task<VaultAvailabilityReport> GetAvailabilityAsync(DateTime fecha)
    {
        var rows = await _vaultRepo.GetAvailabilityAsync(fecha);

        var entries = rows.Select(r => new VaultAvailabilityEntry
        {
            IdAgencia = r.IdAgencia,
            Agencia = r.Agencia,
            Moneda = r.MonedaAbreviatura,
            Saldo = r.Saldo,
            TipoCambioOficial = r.TipoCambioOficial,
            SaldoBob = r.SaldoBob,
            FechaCotizacion = r.FechaCotizacion
        }).OrderBy(e => e.Agencia).ThenBy(e => e.Moneda).ToList();

        return new VaultAvailabilityReport
        {
            Fecha = fecha,
            Entries = entries,
            TotalSaldoBob = entries.Sum(e => e.SaldoBob)
        };
    }
}
