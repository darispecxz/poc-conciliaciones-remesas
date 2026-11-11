namespace Reconciliation.Domain;

// ── Flat row types for Dapper mapping — Gerencial module ────────────

public class GerencialAgencyRow
{
    public int IdAgencia { get; set; }
    public string Descripcion { get; set; } = "";
    public string Departamento { get; set; } = "";
}

/// <summary>
/// Remittance daily aggregation from the remesa table.
/// One row per (date, agency, currency abbreviation).
/// </summary>
public class GerencialRemittanceRow
{
    public DateTime Fecha { get; set; }
    public int IdAgencia { get; set; }
    public string Agencia { get; set; } = "";
    public string Departamento { get; set; } = "";
    public string MonedaAbreviatura { get; set; } = "";
    public int Transacciones { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal TotalBob { get; set; }
    public decimal ComisionBob { get; set; }
}

/// <summary>
/// Remittance payment type breakdown.
/// One row per (date, agency, currency, forma_pago).
/// </summary>
public class GerencialRemittancePaymentRow
{
    public DateTime Fecha { get; set; }
    public int IdAgencia { get; set; }
    public string Departamento { get; set; } = "";
    public string MonedaAbreviatura { get; set; } = "";
    public int IdFormaPago { get; set; }
    public int Transacciones { get; set; }
    public decimal MontoTotal { get; set; }
}

/// <summary>
/// Currency exchange daily aggregation from the cambio table.
/// One row per (date, agency).
/// Buy = house buys USD (mon_rec = USD), Sell = house sells USD (mon_ent = USD).
/// </summary>
public class GerencialExchangeRow
{
    public DateTime Fecha { get; set; }
    public int IdAgencia { get; set; }
    public string Agencia { get; set; } = "";
    public string Departamento { get; set; } = "";
    public int TotalOperaciones { get; set; }
    public decimal BobRecibido { get; set; }
    public decimal BobEntregado { get; set; }
}

/// <summary>
/// Accounting alert row from co_asientos.
/// Captures flagged/large/reversed/AML transactions.
/// </summary>
public class GerencialAlertRow
{
    public long IdAsiento { get; set; }
    public DateTime Fecha { get; set; }
    public string Concepto { get; set; } = "";
    public decimal Importe { get; set; }
    public string TipoAlerta { get; set; } = "";
}
