using Dapper;
using Reconciliation.Domain.Interfaces;
using Reconciliation.Infrastructure.Data;

namespace Reconciliation.Infrastructure.Repositories;

public class ExchangeRateRepository : IExchangeRateRepository
{
    private readonly IDbConnectionFactory _db;

    public ExchangeRateRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<ExchangeRateRow>> GetLatestRatesAsync()
    {
        const string sql = @"
            SELECT co1.id_moneda                     AS IdMoneda,
                   m.abreviatura                     AS MonedaAbreviatura,
                   co1.valor                         AS Valor,
                   co1.fec_crea_cotizacion_oficial   AS FechaCotizacion
            FROM cotizacion_oficial co1
            INNER JOIN (
                SELECT id_moneda, MAX(fec_crea_cotizacion_oficial) AS max_fecha
                FROM cotizacion_oficial
                WHERE id_operacion = 1
                GROUP BY id_moneda
            ) co2 ON co1.id_moneda = co2.id_moneda
                 AND co1.fec_crea_cotizacion_oficial = co2.max_fecha
            INNER JOIN moneda m ON m.id_moneda = co1.id_moneda
            WHERE co1.id_operacion = 1
            ORDER BY m.abreviatura";

        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ExchangeRateRow>(sql);
    }
}
