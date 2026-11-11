using Dapper;
using Reconciliation.Domain;
using Reconciliation.Domain.Interfaces;
using Reconciliation.Infrastructure.Data;

namespace Reconciliation.Infrastructure.Repositories;

public class ChileRemittanceRepository : IChileRemittanceRepository
{
    private readonly IDbConnectionFactory _db;

    public ChileRemittanceRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<ChileRemittanceRow>> GetRemittancesAsync(
        DateTime fechaDesde, DateTime fechaHasta)
    {
        const string sql = @"
            SELECT
                id_remesa           AS IdRemesa,
                id_operacion        AS IdOperacion,
                correlativo_pais    AS CorrelativoPais,
                correlativo_global  AS CorrelativoGlobal,
                correlativo_agencia AS CorrelativoAgencia,
                correlativo_destino AS CorrelativoDestino,
                login_usu           AS LoginUsu,
                cod_ope             AS CodOpe,
                codigo              AS Codigo,
                fec_crea_remesa     AS FecCreRemesa,
                fec_mod_remesa      AS FecModRemesa,
                COALESCE(doc_usu_fin, '')       AS DocUsuFin,
                COALESCE(usuario_financiero, '') AS UsuarioFinanciero,
                COALESCE(tel_usu, '')           AS TelUsu,
                COALESCE(doc_des, '')           AS DocDes,
                COALESCE(destinatario, '')      AS Destinatario,
                COALESCE(nom_des, '')           AS NomDes,
                COALESCE(pat_des, '')           AS PatDes,
                COALESCE(mat_des, '')           AS MatDes,
                COALESCE(tel_des, '')           AS TelDes,
                id_agencia          AS IdAgencia,
                COALESCE(origen, '')            AS Origen,
                COALESCE(abr_ori, '')           AS AbrOri,
                id_destino          AS IdDestino,
                COALESCE(destino, '')           AS DestinoNombre,
                COALESCE(abr_des, '')           AS AbrDes,
                COALESCE(moneda, '')            AS Moneda,
                envio               AS Envio,
                porcentaje          AS Porcentaje,
                comision            AS Comision,
                tipo_cambio         AS TipoCambio,
                monto_clp           AS MontoClp,
                recibido_usd        AS RecibidoUsd,
                recibido_clp        AS RecibidoClp,
                total_clp           AS TotalClp,
                estado              AS Estado,
                cod_usuario         AS CodUsuario,
                comision_bob        AS ComisionBob
            FROM tmp_reporte_remesa
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
            AND id_operacion IN @IdOperacion";

        using var conn = _db.CreateConnection("Chile");
        return await conn.QueryAsync<ChileRemittanceRow>(sql, new
        {
            FechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
            FechaHasta = fechaHasta.ToString("yyyy-MM-dd"),
            IdOperacion = RemittanceOperationType.Chile
        });
    }

    public async Task<IEnumerable<ChilePaymentRow>> GetPaymentsByCodigosAsync(
        DateTime fechaDesde, DateTime fechaHasta)
    {
        const string sql = @"
            SELECT
                id_pago             AS IdPago,
                id_tipo_pago        AS IdTipoPago,
                codigo              AS Codigo,
                id_operacion        AS IdOperacion,
                id_agencia          AS IdAgencia,
                cod_usuario         AS CodUsuario,
                mon_rec             AS MonRec,
                monto               AS Monto,
                tipo_cambio         AS TipoCambio,
                total               AS Total,
                fec_crea_pago       AS FecCreaPago
            FROM pago
            WHERE DATE(fec_crea_pago) BETWEEN @FechaDesde AND @FechaHasta
              AND id_operacion IN @IdOperacion";

        using var conn = _db.CreateConnection("Chile");
        return await conn.QueryAsync<ChilePaymentRow>(sql, new
        {
            FechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
            FechaHasta = fechaHasta.ToString("yyyy-MM-dd"),
            IdOperacion = RemittanceOperationType.Chile
        });
    }

    public async Task<decimal> GetHistoricalPaidTotalAsync(DateTime beforeDate)
    {
        var startOfYear = new DateTime(beforeDate.Year, 1, 1);

        const string sql = @"
            SELECT COALESCE(SUM(envio), 0)
            FROM tmp_reporte_remesa
            WHERE estado = @Pagado
              AND fec_crea_remesa >= @StartOfYear
              AND fec_crea_remesa < @BeforeDate
              AND id_operacion IN @IdOperacion";

        using var conn = _db.CreateConnection("Chile");
        return await conn.ExecuteScalarAsync<decimal>(sql, new
        {
            Pagado = RemittanceStatusChile.Pagado,
            StartOfYear = startOfYear,
            BeforeDate = beforeDate,
            IdOperacion = RemittanceOperationType.Chile
        });
    }

    public async Task<CountAmountResult> GetPendingAsOfDateAsync(DateTime asOfDate)
    {
        const string sql = @"
            SELECT COUNT(*)               AS Count,
                   COALESCE(SUM(envio), 0) AS Amount
            FROM tmp_reporte_remesa
            WHERE estado IN @PendingStatuses
              AND DATE(fec_crea_remesa) <= @AsOfDate
              AND id_operacion IN @IdOperacion";

        using var conn = _db.CreateConnection("Chile");
        return await conn.QuerySingleAsync<CountAmountResult>(sql, new
        {
            PendingStatuses = new[] { RemittanceStatusChile.Pendiente, RemittanceStatusChile.EnProceso },
            AsOfDate = asOfDate.ToString("yyyy-MM-dd"),
            IdOperacion = RemittanceOperationType.Chile
        });
    }
}
