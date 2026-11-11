using Dapper;
using Reconciliation.Domain.Operations;
using Reconciliation.Infrastructure.Data;

namespace Reconciliation.Infrastructure.Repositories;

public class OperationsRepository : IOperationsRepository
{
    private readonly IDbConnectionFactory _db;

    static OperationsRepository()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public OperationsRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<OperationRow>> GetOperationsAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        const string sql = @"
            -- REMESAS
            SELECT
                CONCAT(uf.nombres, ' ', uf.ape_pat, ' ', uf.ape_mat) AS usuario_financiero,
                d.descripcion AS dpto_transaccion,
                'BOLIVIA' AS pais_transaccion,
                TIMESTAMPDIFF(YEAR, uf.fecha_nacimiento, CURDATE()) AS edad,
                ae.descripcion AS act_economica,
                uf.pep,
                a.descripcion AS agencia,
                o.descripcion AS tipo_operacion,
                'REMESA' AS tipo_transaccion,
                DATE_FORMAT(vrr.fec_crea_remesa, '%b') AS mes,
                DATE_FORMAT(vrr.fec_crea_remesa, '%e-%b') AS dia,
                vrr.fec_crea_remesa AS fecha,
                1 AS nro_op,
                vrr.monto_bob AS total_operacion_bob
            FROM v_reporte_remesa vrr
            INNER JOIN usuario u ON vrr.cod_usuario = u.cod_usuario
            INNER JOIN remesa r ON vrr.id_remesa = r.id_remesa
            INNER JOIN usuario_financiero uf ON uf.id = r.usuario_financiero_id
            INNER JOIN tipo_doc_identidad tdi ON tdi.id_tipo_doc_identidad = uf.id_tipo_doc_identidad
            INNER JOIN actividad_economica ae ON ae.id_actividad_economica = uf.id_actividad_economica
            INNER JOIN agencia a ON a.id_agencia = vrr.id_agencia
            INNER JOIN departamento d ON d.id_departamento = a.id_departamento
            INNER JOIN operacion o ON o.id_operacion = r.id_operacion
            INNER JOIN pais p ON p.id_pais = uf.id_pais
            INNER JOIN expedicion e ON e.id_expedicion = uf.id_expedicion
            WHERE vrr.fec_crea_remesa BETWEEN @FechaDesde AND @FechaHasta
              AND r.facturado <> 0

            UNION ALL

            -- CAMBIOS
            SELECT
                CONCAT(uf.nombres, ' ', uf.ape_pat, ' ', uf.ape_mat) AS usuario_financiero,
                d.descripcion AS dpto_transaccion,
                'BOLIVIA' AS pais_transaccion,
                TIMESTAMPDIFF(YEAR, uf.fecha_nacimiento, CURDATE()) AS edad,
                ae.descripcion AS act_economica,
                uf.pep,
                a.descripcion AS agencia,
                o.descripcion AS tipo_operacion,
                'CAMBIO' AS tipo_transaccion,
                DATE_FORMAT(c.fec_crea_cambio, '%b') AS mes,
                DATE_FORMAT(c.fec_crea_cambio, '%e-%b') AS dia,
                c.fec_crea_cambio AS fecha,
                1 AS nro_op,
                CASE
                    WHEN c.id_operacion = 1 THEN ROUND((c.recibido - c.cambio) * c.tipo_cambio, 2)
                    ELSE ROUND(c.entregado * c.tipo_cambio_bob, 2)
                END AS total_operacion_bob
            FROM cambio c
            INNER JOIN usuario u ON c.cod_usuario = u.cod_usuario
            INNER JOIN usuario_financiero uf ON uf.id = c.usuario_financiero_id
            INNER JOIN tipo_doc_identidad tdi ON tdi.id_tipo_doc_identidad = uf.id_tipo_doc_identidad
            INNER JOIN actividad_economica ae ON ae.id_actividad_economica = uf.id_actividad_economica
            INNER JOIN agencia a ON a.id_agencia = c.id_agencia
            INNER JOIN departamento d ON d.id_departamento = a.id_departamento
            INNER JOIN operacion o ON o.id_operacion = c.id_operacion
            INNER JOIN pais p ON p.id_pais = uf.id_pais
            INNER JOIN expedicion e ON e.id_expedicion = uf.id_expedicion
            INNER JOIN moneda mr ON mr.id_moneda = c.mon_rec
            INNER JOIN moneda me ON me.id_moneda = c.mon_ent
            WHERE c.fec_crea_cambio BETWEEN @FechaDesde AND @FechaHasta
              AND c.facturado = 1

            UNION ALL

            -- GIROS
            SELECT
                CONCAT(uf.nombres, ' ', uf.ape_pat, ' ', uf.ape_mat) AS usuario_financiero,
                d.descripcion AS dpto_transaccion,
                'BOLIVIA' AS pais_transaccion,
                TIMESTAMPDIFF(YEAR, uf.fecha_nacimiento, CURDATE()) AS edad,
                ae.descripcion AS act_economica,
                uf.pep,
                ao.descripcion AS agencia,
                o.descripcion AS tipo_operacion,
                'GIRO' AS tipo_transaccion,
                DATE_FORMAT(g.fec_crea_giro, '%b') AS mes,
                DATE_FORMAT(g.fec_crea_giro, '%e-%b') AS dia,
                g.fec_crea_giro AS fecha,
                1 AS nro_op,
                g.total_cambio AS total_operacion_bob
            FROM giro g
            INNER JOIN usuario u ON g.cod_usuario = u.cod_usuario
            INNER JOIN usuario_financiero uf ON uf.id = g.usuario_financiero_id
            INNER JOIN tipo_doc_identidad tdi ON tdi.id_tipo_doc_identidad = uf.id_tipo_doc_identidad
            INNER JOIN actividad_economica ae ON ae.id_actividad_economica = uf.id_actividad_economica
            INNER JOIN agencia ao ON ao.id_agencia = g.id_agencia_origen
            INNER JOIN agencia ad ON ad.id_agencia = g.id_agencia_destino
            INNER JOIN departamento d ON d.id_departamento = ao.id_departamento
            INNER JOIN operacion o ON o.id_operacion = g.id_operacion
            INNER JOIN pais p ON p.id_pais = uf.id_pais
            INNER JOIN expedicion e ON e.id_expedicion = uf.id_expedicion
            INNER JOIN moneda mr ON mr.id_moneda = g.mon_rec
            INNER JOIN moneda me ON me.id_moneda = g.mon_ent
            WHERE g.fec_crea_giro BETWEEN @FechaDesde AND @FechaHasta
              AND g.estado NOT IN (0, 5)

            ORDER BY usuario_financiero ASC, fecha ASC;";

        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<OperationRow>(sql, new { FechaDesde = fechaDesde, FechaHasta = fechaHasta });
    }
}
