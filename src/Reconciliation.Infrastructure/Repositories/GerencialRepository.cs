using Dapper;
using Reconciliation.Domain;
using Reconciliation.Domain.Interfaces;
using Reconciliation.Infrastructure.Data;

namespace Reconciliation.Infrastructure.Repositories;

public class GerencialRepository : IGerencialRepository
{
    private readonly IDbConnectionFactory _db;

    public GerencialRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<GerencialAgencyRow>> GetAgenciesAsync()
    {
        const string sql = @"
            SELECT
                a.id_agencia    AS IdAgencia,
                a.descripcion   AS Descripcion,
                a.ciudad        AS Departamento
            FROM agencia a
            ORDER BY a.descripcion";

        using var conn = _db.CreateConnection("Bolivia");
        return await conn.QueryAsync<GerencialAgencyRow>(sql);
    }

    public async Task<IEnumerable<GerencialRemittanceRow>> GetRemittanceSummaryAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? idAgencia)
    {
        const string sql = @"
            SELECT
                DATE(r.fec_crea_remesa)     AS Fecha,
                r.id_agencia                AS IdAgencia,
                a.descripcion               AS Agencia,
                a.ciudad                    AS Departamento,
                m.abreviatura               AS MonedaAbreviatura,
                COUNT(*)                    AS Transacciones,
                SUM(r.monto)                AS MontoTotal,
                SUM(r.total_bob)            AS TotalBob,
                SUM(r.comision_bob)         AS ComisionBob
            FROM remesa r
            INNER JOIN agencia a ON a.id_agencia = r.id_agencia
            INNER JOIN moneda m  ON m.id_moneda  = r.mon_rec
            WHERE r.fec_crea_remesa >= @FechaDesde
              AND r.fec_crea_remesa < DATE_ADD(@FechaHasta, INTERVAL 1 DAY)
              AND r.id_operacion IN (4, 16)
              AND r.estado != 5
              AND (@idAgencia = 0 OR a.id_agencia = @idAgencia)
            GROUP BY DATE(r.fec_crea_remesa), r.id_agencia, a.descripcion,
                     a.id_agencia, m.abreviatura
            ORDER BY Fecha, Agencia, MonedaAbreviatura";

        using var conn = _db.CreateConnection("Bolivia");
        return await conn.QueryAsync<GerencialRemittanceRow>(sql, new
        {
            FechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
            FechaHasta = fechaHasta.ToString("yyyy-MM-dd"),
            idAgencia
        });
    }

    public async Task<IEnumerable<GerencialRemittanceRow>> GetRemittancesPaidAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? idAgencia)
    {
        const string sql = @"
            SELECT
                DATE(r.fec_mod_remesa)      AS Fecha,
                r.id_agencia                AS IdAgencia,
                a.descripcion               AS Agencia,
                a.ciudad                    AS Departamento,
                m.abreviatura               AS MonedaAbreviatura,
                COUNT(*)                    AS Transacciones,
                SUM(r.monto)                AS MontoTotal,
                SUM(r.total_bob)            AS TotalBob,
                SUM(r.comision_bob)         AS ComisionBob
            FROM remesa r
            INNER JOIN agencia a ON a.id_agencia = r.id_agencia
            INNER JOIN moneda m  ON m.id_moneda  = r.mon_rec
            WHERE r.fec_mod_remesa >= @FechaDesde
              AND r.fec_mod_remesa < DATE_ADD(@FechaHasta, INTERVAL 1 DAY)
              AND r.id_operacion IN (4, 16)
              AND r.estado = 1
              AND (@idAgencia = 0 OR a.id_agencia = @idAgencia)
            GROUP BY DATE(r.fec_mod_remesa), r.id_agencia, a.descripcion,
                     a.ciudad, m.abreviatura
            ORDER BY Fecha, Agencia, MonedaAbreviatura";

        using var conn = _db.CreateConnection("Bolivia");
        return await conn.QueryAsync<GerencialRemittanceRow>(sql, new
        {
            FechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
            FechaHasta = fechaHasta.ToString("yyyy-MM-dd"),
            idAgencia
        });
    }

    public async Task<IEnumerable<GerencialExchangeRow>> GetExchangeSummaryAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? idAgencia)
    {
        // All currency exchange operations — track BOB flow for profit calculation.
        // BobRecibido = BOB received by house (when client buys foreign currency, pays BOB).
        // BobEntregado = BOB delivered by house (when client sells foreign currency, receives BOB).
        // Profit = BobRecibido - BobEntregado (spread income).
        const string sql = @"
            SELECT
                DATE(c.fec_crea_cambio)     AS Fecha,
                c.id_agencia                AS IdAgencia,
                a.descripcion               AS Agencia,
                a.ciudad                    AS Departamento,
                COUNT(*)                    AS TotalOperaciones,
                SUM(CASE WHEN mr.abreviatura = 'BOB' THEN c.recibido ELSE 0 END) AS BobRecibido,
                SUM(CASE WHEN me.abreviatura = 'BOB' THEN c.entregado ELSE 0 END) AS BobEntregado
            FROM cambio c
            INNER JOIN agencia a  ON a.id_agencia = c.id_agencia
            INNER JOIN moneda mr  ON mr.id_moneda = c.mon_rec
            INNER JOIN moneda me  ON me.id_moneda = c.mon_ent
            WHERE c.fec_crea_cambio >= @FechaDesde
              AND c.fec_crea_cambio < DATE_ADD(@FechaHasta, INTERVAL 1 DAY)
              AND (@idAgencia = 0 OR a.id_agencia = @idAgencia)
            GROUP BY DATE(c.fec_crea_cambio), c.id_agencia, a.descripcion, a.ciudad
            ORDER BY Fecha, Agencia";

        using var conn = _db.CreateConnection("Bolivia");
        return await conn.QueryAsync<GerencialExchangeRow>(sql, new
        {
            FechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
            FechaHasta = fechaHasta.ToString("yyyy-MM-dd"),
            idAgencia
        });
    }

    public async Task<IEnumerable<GerencialAlertRow>> GetAccountingAlertsAsync(
        DateTime fechaDesde, DateTime fechaHasta)
    {
        const string sql = @"
            SELECT
                ca.idasiento    AS IdAsiento,
                ca.fecha        AS Fecha,
                ca.concepto     AS Concepto,
                ca.importe      AS Importe,
                CASE
                    WHEN ca.concepto LIKE '%ANULADO%' OR ca.concepto LIKE '%ANULACION%'
                        THEN 'ANULACION'
                    WHEN ca.concepto LIKE '%DEVOLUCION%' OR ca.concepto LIKE '%DEVUELTO%'
                        THEN 'DEVOLUCION'
                    WHEN ca.importe > 50000
                        THEN 'MONTO_ELEVADO'
                    ELSE 'OTRO'
                END AS TipoAlerta
            FROM co_asientos ca
            WHERE ca.fecha BETWEEN @FechaDesde AND @FechaHasta
              AND (
                  ca.concepto LIKE '%ANULADO%'
                  OR ca.concepto LIKE '%ANULACION%'
                  OR ca.concepto LIKE '%DEVOLUCION%'
                  OR ca.concepto LIKE '%DEVUELTO%'
                  OR ca.importe > 50000
              )
            ORDER BY ca.fecha, ca.idasiento";

        using var conn = _db.CreateConnection("Accounting");
        return await conn.QueryAsync<GerencialAlertRow>(sql, new
        {
            FechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
            FechaHasta = fechaHasta.ToString("yyyy-MM-dd")
        });
    }
}
