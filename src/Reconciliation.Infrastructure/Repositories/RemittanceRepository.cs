using Dapper;
using Reconciliation.Domain;
using Reconciliation.Domain.Interfaces;
using Reconciliation.Infrastructure.Data;

namespace Reconciliation.Infrastructure.Repositories;

public class RemittanceRepository : IRemittanceRepository
{
    private readonly IDbConnectionFactory _db;

    public RemittanceRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<BoliviaRemittanceRow>> GetRemittancesAsync(
        DateTime fechaDesde, DateTime fechaHasta)
    {
        const string sql = @"
            SELECT
                r.id_remesa         AS IdRemesa,
                r.codigo            AS Codigo,
                r.id_operacion      AS IdOperacion,
                r.id_agencia        AS IdAgencia,
                a.descripcion       AS Agencia,
                r.id_destino        AS IdDestino,
                COALESCE(d.descripcion, r.destino) AS Destino,
                r.cod_usuario       AS CodUsuario,
                u.login_usu         AS LoginUsuario,
                r.correlativo       AS Correlativo,
                r.mon_rec           AS MonRec,
                r.mon_ent           AS MonEnt,
                mr.abreviatura      AS MonedaRecAbreviatura,
                me.abreviatura      AS MonedaEntAbreviatura,
                r.monto             AS Monto,
                r.tipo_cambio       AS TipoCambio,
                r.total_cambio      AS TotalCambio,
                r.comision          AS Comision,
                r.gastos_corresponsal     AS GastosCorresponsal,
                r.comision_gambarte       AS ComisionGambarte,
                r.itf               AS Itf,
                r.itf_bob           AS ItfBob,
                r.iva               AS Iva,
                r.iva_bob           AS IvaBob,
                r.total             AS Total,
                r.comision_bob      AS ComisionBob,
                r.gastos_corresponsal_bob AS GastosCorresponsalBob,
                r.comision_gambarte_bob   AS ComisionGambarteBob,
                r.total_bob         AS TotalBob,
                r.tipo_cambio_clp   AS TipoCambioClp,
                r.total_clp         AS TotalClp,
                r.recibido          AS Recibido,
                r.cambio            AS Cambio,
                r.estado            AS Estado,
                r.fec_crea_remesa   AS FecCreRemesa,
                r.fec_mod_remesa    AS FecModRemesa,
                COALESCE(r.origen, '') AS Origen
            FROM remesa r
            INNER JOIN agencia a  ON a.id_agencia = r.id_agencia
            LEFT  JOIN agencia d  ON d.id_agencia = r.id_destino
            INNER JOIN moneda mr  ON mr.id_moneda = r.mon_rec
            INNER JOIN moneda me  ON me.id_moneda = r.mon_ent
            INNER JOIN usuario u  ON u.cod_usuario = r.cod_usuario
            WHERE 
            (
                (
                    fec_crea_remesa >= @FechaDesde 
                    AND fec_crea_remesa < DATE_ADD(@FechaHasta, INTERVAL 1 DAY)
                )
                OR (
                    fec_crea_remesa < @FechaDesde 
                    AND fec_mod_remesa >= @FechaDesde
                )
            )
            AND r.id_operacion IN @IdOperacion";

        using var conn = _db.CreateConnection("Bolivia");
        return await conn.QueryAsync<BoliviaRemittanceRow>(sql, new
        {
            FechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
            FechaHasta = fechaHasta.ToString("yyyy-MM-dd"),
            IdOperacion = RemittanceOperationType.Bolivia
        });
    }

    public async Task<IEnumerable<BoliviaPaymentRow>> GetPaymentsByCodigosAsync(
        DateTime fechaDesde, DateTime fechaHasta)
    {
        const string sql = @"
            SELECT
                p.id_pago           AS IdPago,
                p.codigo            AS Codigo,
                p.id_operacion      AS IdOperacion,
                p.id_agencia        AS IdAgencia,
                a.descripcion       AS Agencia,
                p.cod_usuario       AS CodUsuario,
                u.login_usu         AS LoginUsuario,
                p.mon_rec           AS MonRec,
                m.abreviatura       AS MonedaAbreviatura,
                p.monto             AS Monto,
                p.tipo_cambio       AS TipoCambio,
                p.total_bob         AS TotalBob,
                p.comision_bob      AS ComisionBob,
                p.fec_crea_pago     AS FecCreaPago
            FROM pago p
            INNER JOIN agencia a ON a.id_agencia = p.id_agencia
            INNER JOIN moneda m  ON m.id_moneda = p.mon_rec
            INNER JOIN usuario u ON u.cod_usuario = p.cod_usuario
            WHERE DATE(p.fec_crea_pago) BETWEEN @FechaDesde AND @FechaHasta
              AND p.id_operacion IN @IdOperacion";

        using var conn = _db.CreateConnection("Bolivia");
        return await conn.QueryAsync<BoliviaPaymentRow>(sql, new
        {
            FechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
            FechaHasta = fechaHasta.ToString("yyyy-MM-dd"),
            IdOperacion = RemittancePaymentType.Bolivia
        });
    }

    public async Task<decimal> GetHistoricalPaidTotalAsync(DateTime beforeDate)
    {
        var startOfYear = new DateTime(beforeDate.Year, 1, 1);

        const string sql = @"
            SELECT COALESCE(SUM(r.total_cambio), 0)
            FROM remesa r
            WHERE r.estado = @Pagado
              AND r.fec_crea_remesa >= @StartOfYear
              AND r.fec_crea_remesa < @BeforeDate
              AND r.id_operacion IN @IdOperacion";

        using var conn = _db.CreateConnection("Bolivia");
        return await conn.ExecuteScalarAsync<decimal>(sql, new
        {
            Pagado = RemittanceStatusBolivia.Pagado,
            StartOfYear = startOfYear,
            BeforeDate = beforeDate,
            IdOperacion = RemittanceOperationType.Bolivia
        });
    }

    public async Task<CountAmountResult> GetPendingAsOfDateAsync(DateTime asOfDate)
    {
        const string sql = @"
            SELECT COUNT(*)              AS Count,
                   COALESCE(SUM(r.total_cambio), 0) AS Amount
            FROM remesa r
            WHERE r.estado = @Pendiente
              AND DATE(r.fec_crea_remesa) <= @AsOfDate
              AND r.id_operacion IN @IdOperacion";

        using var conn = _db.CreateConnection("Bolivia");
        return await conn.QuerySingleAsync<CountAmountResult>(sql, new
        {
            Pendiente = RemittanceStatusBolivia.Pendiente,
            AsOfDate = asOfDate.ToString("yyyy-MM-dd"),
            IdOperacion = RemittanceOperationType.Bolivia
        });
    }
}
