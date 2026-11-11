using Dapper;
using Reconciliation.Domain.Interfaces;
using Reconciliation.Infrastructure.Data;

namespace Reconciliation.Infrastructure.Repositories;

public class VaultRepository : IVaultRepository
{
    private readonly IDbConnectionFactory _db;

    public VaultRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<VaultReconciliationRow>> GetReconciliationAsync(
        DateTime fecha, int? idAgencia = null)
    {
        // Fixed bugs from original PHP:
        // - Bug #3: sb.id_boveda now included in GROUP BY
        // - Bug #2: parameterized queries (no string interpolation)
        const string sql = @"
            SELECT
                a.id_agencia    AS IdAgencia,
                a.descripcion   AS Agencia,
                b.id_moneda     AS IdMoneda,
                m.abreviatura   AS MonedaAbreviatura,
                b.id_boveda     AS IdBoveda,
                b.num_boveda    AS NumBoveda,

                SUM(CASE WHEN sb.conciliacion = 'A' THEN sb.saldo ELSE 0 END) AS SaldoApertura,

                /* Egresos */
                SUM(CASE WHEN SUBSTRING_INDEX(sb.conciliacion, '-', 1) = 'SAC' THEN sb.saldo ELSE 0 END) AS SalidaAperturaCaja,
                SUM(CASE WHEN SUBSTRING_INDEX(sb.conciliacion, '-', 1) = 'STB' THEN sb.saldo ELSE 0 END) AS SalidaTraspasoBoveda,
                SUM(CASE WHEN SUBSTRING_INDEX(sb.conciliacion, '-', 1) = 'STC' THEN sb.saldo ELSE 0 END) AS SalidaTraspasoCaja,
                SUM(CASE WHEN SUBSTRING_INDEX(sb.conciliacion, '-', 1) IN ('SAC','STB','STC') THEN sb.saldo ELSE 0 END) AS TotalEgresos,

                /* Ingresos */
                SUM(CASE WHEN SUBSTRING_INDEX(sb.conciliacion, '-', 1) = 'ECC' THEN sb.saldo ELSE 0 END) AS EntradaCierreCaja,
                SUM(CASE WHEN SUBSTRING_INDEX(sb.conciliacion, '-', 1) = 'ETB' THEN sb.saldo ELSE 0 END) AS EntradaTraspasoBoveda,
                SUM(CASE WHEN SUBSTRING_INDEX(sb.conciliacion, '-', 1) = 'ETC' THEN sb.saldo ELSE 0 END) AS EntradaTraspasoCasa,
                SUM(CASE WHEN SUBSTRING_INDEX(sb.conciliacion, '-', 1) IN ('ECC','ETB','ETC') THEN sb.saldo ELSE 0 END) AS TotalIngresos,

                SUM(CASE WHEN sb.conciliacion != 'C' THEN sb.saldo ELSE 0 END) AS SaldoFinal

            FROM saldo_boveda sb
            INNER JOIN boveda b  ON b.id_boveda = sb.id_boveda
            INNER JOIN moneda m  ON m.id_moneda = b.id_moneda
            INNER JOIN agencia a ON a.id_agencia = b.id_agencia
            WHERE DATE(sb.fec_crea_saldo) = @Fecha
              AND (@IdAgencia IS NULL OR a.id_agencia = @IdAgencia)
            GROUP BY a.id_agencia, a.descripcion, b.id_moneda,
                     m.abreviatura, b.id_boveda, b.num_boveda";

        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<VaultReconciliationRow>(sql, new
        {
            Fecha = fecha.ToString("yyyy-MM-dd"),
            IdAgencia = idAgencia
        });
    }

    public async Task<IEnumerable<VaultAvailabilityRow>> GetAvailabilityAsync(DateTime fecha)
    {
        // Fixed bug #4 from original: CTE now deterministic (INNER JOIN on max date)
        // Fixed bug #5: b.id_boveda removed from SELECT, grouped by agency + currency
        const string sql = @"
            WITH LatestRate AS (
                SELECT co1.id_moneda, co1.valor, co1.fec_crea_cotizacion_oficial
                FROM cotizacion_oficial co1
                INNER JOIN (
                    SELECT id_moneda, MAX(fec_crea_cotizacion_oficial) AS max_fecha
                    FROM cotizacion_oficial WHERE id_operacion = 1
                    GROUP BY id_moneda
                ) co2 ON co1.id_moneda = co2.id_moneda
                     AND co1.fec_crea_cotizacion_oficial = co2.max_fecha
                WHERE co1.id_operacion = 1
            )
            SELECT
                a.id_agencia                    AS IdAgencia,
                a.descripcion                   AS Agencia,
                m.id_moneda                     AS IdMoneda,
                m.abreviatura                   AS MonedaAbreviatura,
                SUM(CASE WHEN sb.conciliacion != 'C' THEN sb.saldo ELSE 0 END) AS Saldo,
                COALESCE(lr.valor, 1)           AS TipoCambioOficial,
                SUM(CASE WHEN sb.conciliacion != 'C' THEN sb.saldo ELSE 0 END)
                    * COALESCE(lr.valor, 1)     AS SaldoBob,
                lr.fec_crea_cotizacion_oficial  AS FechaCotizacion
            FROM saldo_boveda sb
            INNER JOIN boveda b  ON b.id_boveda = sb.id_boveda
            INNER JOIN moneda m  ON m.id_moneda = b.id_moneda
            INNER JOIN agencia a ON a.id_agencia = b.id_agencia
            LEFT  JOIN LatestRate lr ON lr.id_moneda = m.id_moneda
            WHERE DATE(sb.fec_crea_saldo) = @Fecha
            GROUP BY a.id_agencia, a.descripcion, m.id_moneda,
                     m.abreviatura, lr.valor, lr.fec_crea_cotizacion_oficial";

        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<VaultAvailabilityRow>(sql, new
        {
            Fecha = fecha.ToString("yyyy-MM-dd")
        });
    }

    public async Task<IEnumerable<VaultCurrencyTotalRow>> GetCurrencyTotalsAsync(DateTime fecha)
    {
        const string sql = @"
SELECT 
    m.abreviatura AS MonedaAbreviatura,
    SUM(sb.saldo) AS TotalBoveda,
    COALESCE((
        SELECT co.valor
        FROM cotizacion_oficial co
        WHERE co.id_moneda = m.id_moneda
          AND co.id_operacion = 1
          AND co.fec_crea_cotizacion_oficial < DATE_ADD(@Fecha, INTERVAL 1 DAY)
        ORDER BY co.fec_crea_cotizacion_oficial DESC
        LIMIT 1
    ), 1) AS TipoCambioOficial
FROM (
    -- 1. Movimientos del día (SIN 'C')
    SELECT sb.*
    FROM saldo_boveda sb
    WHERE sb.fec_crea_saldo >= @Fecha
      AND sb.fec_crea_saldo < DATE_ADD(@Fecha, INTERVAL 1 DAY)
      AND sb.conciliacion != 'C'

    UNION ALL

    -- 2. Fallback: último 'C' SOLO si no hay movimientos
SELECT sb2.*
FROM saldo_boveda sb2
INNER JOIN boveda b ON b.id_boveda = sb2.id_boveda
INNER JOIN agencia a ON a.id_agencia = b.id_agencia
WHERE a.id_agencia >= 2
  AND sb2.conciliacion = 'C'
  AND sb2.fec_crea_saldo < @Fecha
  AND sb2.fec_crea_saldo = (
      SELECT MAX(sb4.fec_crea_saldo)
      FROM saldo_boveda sb4
      WHERE sb4.id_boveda = sb2.id_boveda
        AND sb4.conciliacion = 'C'
        AND sb4.fec_crea_saldo < @Fecha
  )
  AND NOT EXISTS (
      SELECT 1
      FROM saldo_boveda sb3
      WHERE sb3.id_boveda = sb2.id_boveda
        AND sb3.fec_crea_saldo >= @Fecha
        AND sb3.fec_crea_saldo < DATE_ADD(@Fecha, INTERVAL 1 DAY)
        AND sb3.conciliacion != 'C'
  )
) sb
INNER JOIN boveda b ON b.id_boveda = sb.id_boveda
INNER JOIN moneda m ON m.id_moneda = b.id_moneda
GROUP BY m.abreviatura, m.id_moneda;";
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<VaultCurrencyTotalRow>(sql, new
        {
            Fecha = fecha.ToString("yyyy-MM-dd")
        });
    }
}
