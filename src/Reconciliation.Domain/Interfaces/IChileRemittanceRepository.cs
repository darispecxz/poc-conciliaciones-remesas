namespace Reconciliation.Domain.Interfaces;

public interface IChileRemittanceRepository
{
    /// <summary>
    /// Remittances from Chile's tmp_reporte_remesa table for a given date range.
    /// Direction determined by abr_ori / abr_des columns.
    /// </summary>
    Task<IEnumerable<ChileRemittanceRow>> GetRemittancesAsync(DateTime fechaDesde, DateTime fechaHasta);

    /// <summary>
    /// Payments from Chile's pago table matching the given remittance codes.
    /// No date restriction — fetches all payments for these codes so that
    /// remittances paid after the report cutoff can be correctly classified as pending.
    /// </summary>
    Task<IEnumerable<ChilePaymentRow>> GetPaymentsByCodigosAsync(DateTime fechaDesde, DateTime fechaHasta);

    /// <summary>
    /// Sum of envio for paid CHL→BOL remittances created strictly before the given date.
    /// Used to compute the carry-forward accumulated balance for the General Reconciliation matrix.
    /// </summary>
    Task<decimal> GetHistoricalPaidTotalAsync(DateTime beforeDate);

    /// <summary>
    /// Count and sum of envio for CHL→BOL remittances still in pending/en-proceso status,
    /// created on or before the given date. Used for the carry-forward pending row.
    /// </summary>
    Task<CountAmountResult> GetPendingAsOfDateAsync(DateTime asOfDate);
}

// ── Flat row mapped by Dapper ──────────────────────────────────────

public class ChileRemittanceRow
{
    // Identifiers
    public int IdRemesa { get; set; }
    public int IdOperacion { get; set; }
    public long CorrelativoPais { get; set; }
    public long CorrelativoGlobal { get; set; }
    public long CorrelativoAgencia { get; set; }
    public long CorrelativoDestino { get; set; }
    public string LoginUsu { get; set; } = "";
    public string CodOpe { get; set; } = "";
    public int Codigo { get; set; }

    // Dates
    public DateTime FecCreRemesa { get; set; }
    public DateTime? FecModRemesa { get; set; }

    // Sender
    public string DocUsuFin { get; set; } = "";
    public string UsuarioFinanciero { get; set; } = "";
    public string TelUsu { get; set; } = "";

    // Recipient
    public string DocDes { get; set; } = "";
    public string Destinatario { get; set; } = "";
    public string NomDes { get; set; } = "";
    public string PatDes { get; set; } = "";
    public string MatDes { get; set; } = "";
    public string TelDes { get; set; } = "";

    // Origin / Destination
    public int IdAgencia { get; set; }
    public string Origen { get; set; } = "";
    public string AbrOri { get; set; } = "";
    public int IdDestino { get; set; }
    public string DestinoNombre { get; set; } = "";
    public string AbrDes { get; set; } = "";

    // Amounts
    public string Moneda { get; set; } = "";
    public decimal Envio { get; set; }
    public decimal Porcentaje { get; set; }
    public decimal Comision { get; set; }
    public decimal TipoCambio { get; set; }
    public decimal MontoClp { get; set; }
    public decimal RecibidoUsd { get; set; }
    public decimal RecibidoClp { get; set; }
    public decimal TotalClp { get; set; }
    public decimal ComisionBob { get; set; }

    // Status
    public int Estado { get; set; }
    public long CodUsuario { get; set; }
}

public class ChilePaymentRow
{
    public long IdPago { get; set; }
    public int IdTipoPago { get; set; }
    public int Codigo { get; set; }
    public int IdOperacion { get; set; }
    public int IdAgencia { get; set; }
    public long CodUsuario { get; set; }
    public int MonRec { get; set; }
    public decimal Monto { get; set; }
    public decimal TipoCambio { get; set; }
    public decimal Total { get; set; }
    public DateTime FecCreaPago { get; set; }
}
