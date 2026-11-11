using Dapper;
using Reconciliation.Domain.Interfaces;
using Reconciliation.Infrastructure.Data;

namespace Reconciliation.Infrastructure.Repositories;

public class AccountingRepository : IAccountingRepository
{
    private readonly IDbConnectionFactory _db;

    public AccountingRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<AccountingEntryRow>> GetRemittanceEntriesAsync(
        DateTime fechaDesde, DateTime fechaHasta)
    {
        const string sql = @"
            SELECT
                ca.idasiento      AS IdAsiento,
                ca.fecha          AS Fecha,
                ca.concepto       AS Concepto,
                ca.codejercicio   AS CodEjercicio,
                ca.importe        AS Importe,
                ca.numero         AS Numero,
                COALESCE(ca.documento, '')      AS Documento,
                COALESCE(ca.tipodocumento, '')  AS TipoDocumento
            FROM co_asientos ca
            WHERE ca.fecha BETWEEN @FechaDesde AND @FechaHasta
              AND (
                  ca.concepto LIKE '%ENVIO DE%'
                  OR ca.concepto LIKE '%PAGO EN DESTINO%'
                  OR ca.concepto LIKE '%PENDIENTES DE COBRO DEL PAGO DE REMESA%'
                  OR ca.concepto LIKE '%PAGO DE REMESA CGR%'
              )
            ORDER BY ca.fecha, ca.idasiento";

        using var conn = _db.CreateConnection("Accounting");
        return await conn.QueryAsync<AccountingEntryRow>(sql, new
        {
            FechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
            FechaHasta = fechaHasta.ToString("yyyy-MM-dd")
        });
    }
}
