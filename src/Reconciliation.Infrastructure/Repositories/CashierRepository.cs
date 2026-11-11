using Dapper;
using Reconciliation.Domain.Interfaces;
using Reconciliation.Infrastructure.Data;

namespace Reconciliation.Infrastructure.Repositories;

public class CashierRepository : ICashierRepository
{
    private readonly IDbConnectionFactory _db;

    public CashierRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<CashierReconciliationRow>> GetReconciliationAsync(
        DateTime fecha, int? idAgencia = null)
    {
        // CTE gets the latest BCB exchange rate per currency (fixes original bug #4:
        // nondeterministic valor). Uses INNER JOIN on max date to guarantee correct valor.
        const string sql = @"
            WITH LatestRate AS (
                SELECT co1.id_moneda, co1.valor
                FROM cotizacion_oficial co1
                INNER JOIN (
                    SELECT id_moneda, MAX(fec_crea_cotizacion_oficial) AS max_fecha
                    FROM cotizacion_oficial
                    WHERE id_operacion = 1
                    GROUP BY id_moneda
                ) co2 ON co1.id_moneda = co2.id_moneda
                     AND co1.fec_crea_cotizacion_oficial = co2.max_fecha
                WHERE co1.id_operacion = 1
            )
            SELECT
                c.id_caja                       AS IdCaja,
                c.num_caja                      AS NumCaja,
                u.login_usu                     AS LoginUsuario,
                a.id_agencia                    AS IdAgencia,
                a.descripcion                   AS Agencia,
                m.abreviatura                   AS MonedaAbreviatura,
                COALESCE(lr.valor, 1)           AS TipoCambioOficial,

                /* ── Ingresos (amounts) ─────────────────────────── */
                SUM(CASE WHEN sc.conciliacion = 'A'           THEN sc.saldo ELSE 0 END) AS AperturaCaja,
                SUM(CASE WHEN sc.conciliacion IN ('EC','AC')  THEN sc.saldo ELSE 0 END) AS CambioMonedaIngreso,
                SUM(CASE WHEN sc.conciliacion IN ('EG','IG','OG','AG') THEN sc.saldo ELSE 0 END) AS EnvioGiro,
                SUM(CASE WHEN sc.conciliacion IN ('ER','IR','OR','AR') THEN sc.saldo ELSE 0 END) AS EnvioRemesa,
                SUM(CASE WHEN sc.conciliacion = 'ET'          THEN sc.saldo ELSE 0 END) AS EntradaTransferenciaBoveda,
                SUM(CASE WHEN sc.conciliacion = 'EO'          THEN sc.saldo ELSE 0 END) AS TraspasoDeCaja,
                SUM(CASE WHEN sc.conciliacion IN ('EE','RB')  THEN sc.saldo ELSE 0 END) AS CobroCheque,
                SUM(CASE WHEN sc.conciliacion = 'IA'          THEN sc.saldo ELSE 0 END) AS IngresoControlado,

                /* ── Egresos (amounts) ──────────────────────────── */
                SUM(CASE WHEN sc.conciliacion IN ('SC','VC')  THEN sc.saldo ELSE 0 END) AS CambioMonedaEgreso,
                SUM(CASE WHEN sc.conciliacion = 'SG'          THEN sc.saldo ELSE 0 END) AS PagoGiro,
                SUM(CASE WHEN sc.conciliacion = 'DG'          THEN sc.saldo ELSE 0 END) AS DevolucionGiro,
                SUM(CASE WHEN sc.conciliacion IN ('SR','LR')  THEN sc.saldo ELSE 0 END) AS PagoRemesa,
                SUM(CASE WHEN sc.conciliacion = 'VR'          THEN sc.saldo ELSE 0 END) AS DevolucionRemesa,
                SUM(CASE WHEN sc.conciliacion = 'ST'          THEN sc.saldo ELSE 0 END) AS SalidaTransferenciaBoveda,
                SUM(CASE WHEN sc.conciliacion = 'SO'          THEN sc.saldo ELSE 0 END) AS TraspasoACaja,
                SUM(CASE WHEN sc.conciliacion IN ('SE','DR','DB') THEN sc.saldo ELSE 0 END) AS DepositoBancario,
                SUM(CASE WHEN sc.conciliacion IN ('EA','PG')  THEN sc.saldo ELSE 0 END) AS EgresoControlado,

                /* ── Cierre ─────────────────────────────────────── */
                SUM(CASE WHEN sc.conciliacion = 'C'           THEN sc.saldo ELSE 0 END) AS CierreCaja,

                /* ── Counts (for # columns) ─────────────────────── */
                COUNT(CASE WHEN sc.conciliacion IN ('EC','AC') THEN 1 END) AS CambioMonedaIngresoCount,
                COUNT(CASE WHEN sc.conciliacion IN ('SC','VC') THEN 1 END) AS CambioMonedaEgresoCount,
                COUNT(CASE WHEN sc.conciliacion IN ('EG','IG','OG','AG') THEN 1 END) AS EnvioGiroCount,
                COUNT(CASE WHEN sc.conciliacion = 'SG'         THEN 1 END) AS PagoGiroCount,
                COUNT(CASE WHEN sc.conciliacion IN ('ER','IR','OR','AR') THEN 1 END) AS EnvioRemesaCount,
                COUNT(CASE WHEN sc.conciliacion IN ('SR','LR') THEN 1 END) AS PagoRemesaCount,
                COUNT(CASE WHEN sc.conciliacion = 'DG'         THEN 1 END) AS DevolucionGiroCount,
                COUNT(CASE WHEN sc.conciliacion = 'VR'         THEN 1 END) AS DevolucionRemesaCount,
                COUNT(CASE WHEN sc.conciliacion = 'SO'         THEN 1 END) AS TraspasoACajaCount,
                COUNT(CASE WHEN sc.conciliacion = 'EO'         THEN 1 END) AS TraspasoDeCajaCount,
                COUNT(CASE WHEN sc.conciliacion IN ('SE','DR','DB') THEN 1 END) AS DepositoBancarioCount,
                COUNT(CASE WHEN sc.conciliacion IN ('EE','RB') THEN 1 END) AS CobroChequeCount,
                COUNT(CASE WHEN sc.conciliacion IN ('EA','PG') THEN 1 END) AS EgresoControladoCount,
                COUNT(CASE WHEN sc.conciliacion = 'IA'         THEN 1 END) AS IngresoControladoCount,
                COUNT(CASE WHEN sc.conciliacion = 'ET'         THEN 1 END) AS TransferenciaBovedaIngresoCount,
                COUNT(CASE WHEN sc.conciliacion = 'ST'         THEN 1 END) AS TransferenciaBovedaEgresoCount

            FROM saldo_caja sc
            INNER JOIN caja c    ON c.id_caja = sc.id_caja
            INNER JOIN moneda m  ON m.id_moneda = c.id_moneda
            INNER JOIN agencia a ON a.id_agencia = c.id_agencia
            INNER JOIN usuario u ON u.cod_usuario = c.cod_usuario
            LEFT  JOIN LatestRate lr ON lr.id_moneda = c.id_moneda
            WHERE DATE(sc.fec_crea_saldo) = @Fecha
              AND (@IdAgencia IS NULL OR a.id_agencia = @IdAgencia)
            GROUP BY c.id_caja, c.num_caja, u.login_usu,
                     a.id_agencia, a.descripcion, m.abreviatura, lr.valor";

        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<CashierReconciliationRow>(sql, new
        {
            Fecha = fecha.ToString("yyyy-MM-dd"),
            IdAgencia = idAgencia
        });
    }

    public async Task<IEnumerable<FallaCajaRow>> GetFallasCajaAsync(DateTime fecha, int? idAgencia = null)
    {
        const string sql = @"
            SELECT fc.id_caja AS IdCaja,
                   fc.fecha   AS Fecha,
                   fc.diferencia AS Diferencia,
                   fc.glosa   AS Glosa,
                   fc.estado  AS Estado
            FROM falla_caja fc
            INNER JOIN caja c ON c.id_caja = fc.id_caja
            WHERE fc.fecha = @Fecha
              AND (@IdAgencia IS NULL OR c.id_agencia = @IdAgencia)";

        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<FallaCajaRow>(sql, new
        {
            Fecha = fecha.ToString("yyyy-MM-dd"),
            IdAgencia = idAgencia
        });
    }

    public async Task<IEnumerable<CurrencyTotalRow>> GetCurrencyTotalsAsync(DateTime fecha)
    {
        const string sql = @"
            WITH LatestRate AS (
                SELECT co1.id_moneda, co1.valor
                FROM cotizacion_oficial co1
                INNER JOIN (
                    SELECT id_moneda, MAX(fec_crea_cotizacion_oficial) AS max_fecha
                    FROM cotizacion_oficial 
                    WHERE id_operacion = 1
                      AND fec_crea_cotizacion_oficial < DATE_ADD(@Fecha, INTERVAL 1 DAY)
                    GROUP BY id_moneda
                ) co2 
                    ON co1.id_moneda = co2.id_moneda
                   AND co1.fec_crea_cotizacion_oficial = co2.max_fecha
                WHERE co1.id_operacion = 1
            )
            SELECT m.abreviatura          AS MonedaAbreviatura,
                   SUM(CASE WHEN sc.conciliacion = 'C' THEN sc.saldo ELSE 0 END) AS TotalCajas,
                   COALESCE(lr.valor, 1)  AS TipoCambioOficial
            FROM saldo_caja sc
            INNER JOIN caja c   ON c.id_caja = sc.id_caja
            INNER JOIN moneda m ON m.id_moneda = c.id_moneda
            LEFT  JOIN LatestRate lr ON lr.id_moneda = c.id_moneda
            WHERE DATE(sc.fec_crea_saldo) = @Fecha
            GROUP BY m.abreviatura, lr.valor";

        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<CurrencyTotalRow>(sql, new { Fecha = fecha.ToString("yyyy-MM-dd") });
    }

    public async Task<IEnumerable<AgencyClosingRow>> GetAgencyClosingsAsync(DateTime fecha)
    {
        const string sql = @"
            SELECT a.id_agencia    AS IdAgencia,
                   a.descripcion   AS Agencia,
                   m.abreviatura   AS MonedaAbreviatura,
                   SUM(CASE WHEN sb.conciliacion <> 'C' THEN sb.saldo ELSE 0 END) AS CierreCaja
            FROM saldo_boveda sb
            INNER JOIN boveda b ON b.id_boveda = sb.id_boveda
            INNER JOIN moneda m ON m.id_moneda = b.id_moneda
            INNER JOIN agencia a ON a.id_agencia = b.id_agencia
            WHERE DATE(sb.fec_crea_saldo) = @Fecha
            GROUP BY a.id_agencia, a.descripcion, m.abreviatura
            HAVING SUM(CASE WHEN sb.conciliacion = 'C' THEN sb.saldo ELSE 0 END) != 0";

        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AgencyClosingRow>(sql, new { Fecha = fecha.ToString("yyyy-MM-dd") });
    }

    public async Task<bool> HasClosingRecordAsync(DateTime fecha, int idCaja)
    {
        const string sql = @"
            SELECT COUNT(1) > 0
            FROM saldo_caja
            WHERE id_caja = @IdCaja
              AND conciliacion = 'C'
              AND DATE(fec_crea_saldo) = @Fecha";

        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<bool>(sql, new
        {
            IdCaja = idCaja,
            Fecha = fecha.ToString("yyyy-MM-dd")
        });
    }
}
