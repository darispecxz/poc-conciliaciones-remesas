# Financial Reconciliation System — Full Analysis

---

## 1. Data Model Summary

### Core Reconciliation Tables (from Data Dictionary)

```
agencia (PK: id_agencia int)
  ├── boveda (PK: id_boveda auto_inc)
  │     FK: id_agencia, id_moneda, id_tipo_unidad
  │     Fields: num_boveda, maximo decimal(14,4), minimo decimal(14,4)
  │     └── saldo_boveda (PK: id_boveda + fec_crea_saldo + conciliacion)
  │           Fields: saldo decimal(14,4), conciliacion char(9)
  │
  ├── caja (PK: id_caja auto_inc)
  │     FK: id_agencia, id_moneda, id_tipo_unidad, cod_usuario
  │     Fields: num_caja, maximo decimal(14,4), minimo decimal(14,4)
  │     └── saldo_caja (PK: id_saldo_caja auto_inc)
  │           FK: id_caja
  │           Fields: saldo decimal(14,4), fec_crea_saldo datetime, conciliacion char(2)
  │
  └── falla_caja (PK: id_caja + fecha)
        Fields: diferencia decimal(14,4), glosa text, estado char(1)

moneda (PK: id_moneda int)
  Fields: descripcion text, abreviatura varchar(3), id_pais FK

cotizacion_oficial (PK: id_moneda + id_operacion + fec_crea_cotizacion_oficial)
  Fields: valor decimal(16,6), valor_me decimal(16,6), estado char(1)

usuario (PK: cod_usuario bigint)
  Fields: login_usu varchar(50), cod_trab bigint

arqueo_caja (PK: composite 7-field)
  Fields: id_corte, id_agrupacion, id_tipo_corte, id_agencia, num_caja, fecha, id_tipo_arqueo, cantidad int

arqueo_boveda (PK: composite 7-field)
  Fields: id_corte, id_agrupacion, id_tipo_corte, id_agencia, num_boveda, fecha, id_tipo_arqueo, cantidad int
```

### Transaction Tables (source of saldo_caja entries)

| Table | PK | Key FKs | Monetary Fields | Status Values |
|-------|-----|---------|----------------|---------------|
| cambio | id_cambio auto_inc | id_agencia, mon_rec, mon_ent, cod_usuario | monto, total_cambio, recibido, entregado, cambio, total_operacion — all decimal(16,6) | facturado: 0/1/2 |
| giro | id_giro auto_inc | id_agencia_origen, id_agencia_destino, mon_rec, mon_ent, cod_usuario | monto decimal(14,4), comision, itf, total, total_bob decimal(14,5) | estado: 0=PENDIENTE, 1=PAGADO, 2=DEVUELTO |
| remesa | id_remesa auto_inc | id_agencia, id_destino, mon_rec, mon_ent, cod_usuario | monto, comision, itf, iva, total — all decimal(14,5) | estado: 0=PENDIENTE, 1=PAGADO, 2=DEVUELTO, 5=ANULADO |
| pago | id_pago auto_inc | id_agencia, id_forma_pago, id_tipo_pago, cod_usuario | monto, total_bob, comision_bob — decimal(16,6) | — |

### Transfer Tables (source of traspaso/transferencia movements)

| Table | PK | Key FKs | Amount |
|-------|-----|---------|--------|
| transferencia_boveda | id bigint auto_inc | boveda_origen, boveda_destino | monto_total decimal(14,4) |
| transferencia_caja | id bigint auto_inc | id_caja, id_boveda | monto_total decimal(14,4) |
| transferencia_cajero | id bigint auto_inc | caja_origen, caja_destino | monto_total decimal(14,4) |

---

## 2. Business Rules Extracted

### 2.1 Conciliation Code Mappings — saldo_caja

**CRITICAL: Data dictionary documents only 12 codes. SQL uses 25+. Full map reconstructed from SQL + Excel cross-reference:**

| Code | Direction | Category | Business Meaning |
|------|-----------|----------|-----------------|
| A | INGRESO | Apertura | Apertura de caja (opening balance) |
| C | EGRESO | Cierre | Cierre de caja (closing balance) |
| EC | INGRESO | Cambio | Entrada por Cambio de moneda (buy foreign) |
| AC | INGRESO | Cambio | Anulación/Ajuste Cambio (reversal counted as income) |
| SC | EGRESO | Cambio | Salida por Cambio de moneda (sell foreign) |
| VC | EGRESO | Cambio | Venta Cambio (sale of currency) |
| EG | INGRESO | Giro | Envío de Giro (wire sent = cash received from client) |
| IG | INGRESO | Giro | Ingreso por Giro |
| OG | INGRESO | Giro | Operación/Comisión Giro |
| AG | INGRESO | Giro | Anulación Giro (reversal) |
| SG | EGRESO | Giro | Salida por Giro (pago de giro) |
| DG | EGRESO | Giro | Devolución de Giro |
| ER | INGRESO | Remesa | Envío de Remesa (cash received from client) |
| IR | INGRESO | Remesa | Ingreso por Remesa |
| OR | INGRESO | Remesa | Operación/Comisión Remesa |
| AR | INGRESO | Remesa | Anulación Remesa (reversal) |
| SR | EGRESO | Remesa | Salida por Remesa (pago de remesa) |
| LR | EGRESO | Remesa | Liquidación Remesa |
| VR | EGRESO | Remesa | Devolución de Remesa |
| ET | INGRESO | Transferencia | Entrada por Transferencia de Bóveda |
| ST | EGRESO | Transferencia | Salida por Transferencia a Bóveda |
| EO | INGRESO | Traspaso | Entrada por traspasO de caja (received from another cashier) |
| SO | EGRESO | Traspaso | Salida por traspasO a caja (sent to another cashier) |
| EE | INGRESO | Cheque | Entrada por cobro de cheque |
| RB | INGRESO | Cheque | Reembolso Bancario (cheque clearing) |
| SE | EGRESO | Depósito | Salida por depósito bancario |
| DR | EGRESO | Depósito | Depósito por Remesa |
| DB | EGRESO | Depósito | Depósito Bancario |
| IA | INGRESO | Controlado | Ingreso Autorizado |
| EA | EGRESO | Controlado | Egreso Autorizado |
| PG | EGRESO | Controlado | Pago autorizado (grouped with EA) |

### 2.2 Conciliation Code Mappings — saldo_boveda

Compound format: `PREFIX-ID` where ID references the source caja/boveda.

| Prefix | Direction | Business Meaning |
|--------|-----------|-----------------|
| A | — | Apertura (opening balance, no suffix) |
| C | — | Cierre (closing balance, no suffix) |
| SAC | EGRESO | Salida por Apertura de Caja (cash sent to open a register) |
| STB | EGRESO | Salida por Transferencia a Bóveda |
| STC | EGRESO | Salida por Traspaso a Caja |
| ECC | INGRESO | Entrada por Cierre de Caja (cash returned at close) |
| ETB | INGRESO | Entrada por Transferencia de Bóveda |
| ETC | INGRESO | Entrada por Traspaso de Casa central |

### 2.3 Reconciliation Balance Formula

From Excel Section D, per cashier per currency:

```
TOTAL_SALDO =
  + APERTURA_CAJA
  + CAMBIO_INGRESO    - CAMBIO_EGRESO
  + ENVIO_GIRO        - PAGO_GIRO
  + ENVIO_REMESA      - PAGO_REMESA
  - DEVOLUCION_GIRO   - DEVOLUCION_REMESA
  + TRASPASO_DE_CAJA  - TRASPASO_A_CAJA
  + COBRO_CHEQUE      - DEPOSITOS_BANCARIOS
  + INGRESO_AUTORIZADO - EGRESO_AUTORIZADO
  + TRANSFERENCIA_BOVEDA_IN - TRANSFERENCIA_BOVEDA_OUT

DIFERENCIA = CIERRE_CAJA - TOTAL_SALDO  (must be 0 when balanced)
```

### 2.4 Daily Reconciliation Formula (Section C)

Per currency:
```
TOTAL_CAJAS_BOVEDA = TOTAL_CAJAS + TOTAL_BOVEDA
MONEDAS_EN_BOB     = TOTAL_CAJAS_BOVEDA * TC_OFICIAL
DIF                = MONEDAS_EN_BOB - CONTAB
```

Where CONTAB is an external accounting value (source: **unknown/external system**).

### 2.5 Section E Closing Formula

Per currency:
```
CIERRE_CAJA = APERTURA + CAMBIO_ME + ENVIO_GIRO + PAGO_GIRO
            + ENVIO_REMESA + PAGO_REMESA + EGRESOS
            + TRANSFERENCIAS + FALLAS_DE_CAJA
```

Note: Here the signs are embedded in the values (egresos are negative).

### 2.6 Currencies

13 currencies in the system: BOB, USD, EUR, PEN, BRL, CLP, ARS, GBP, CHF, CAD, AUD, MXN, COP.

### 2.7 Known Agencies (from Excel)

SUCRE CENTRAL, ORURO CENTRAL, COLON, YAPACANI, UYUNI, COCHABAMBA CENTRAL, 6 DE MARZO, SANTA CRUZ CENTRAL.

---

## 3. Report Structure (normalized from Excel)

The Excel workbook has **1 sheet per business day**, each containing **5 side-by-side report sections** (columns 0–73):

### Section A — Saldos de Caja (cols 0–6)
Raw transaction log from `saldo_caja` joined with `caja` + `usuario`.
- Granularity: one row per transaction
- Key fields: timestamp, cashier username, INGRESO/EGRESO, currency, amount, glosa

### Section B — Disponibilidad en Bóveda (cols 7–12)
Vault balances by agency and currency with exchange rate conversion.
- Granularity: one row per agency × currency
- Source: `saldo_boveda` + `boveda` + `moneda` + `cotizacion_oficial`
- Formula: Saldo BOB = Saldo × T/C Oficial

### Section C — Cuadre Diario Sistema-Contabilidad (cols 14–21)
Cross-check between system totals and accounting (CONTAB).
- Granularity: one row per currency + TOTAL row
- Columns: currency, TOTAL CAJAS, TOTAL BOVEDA, CAJAS+BOVEDA, T/C OFICIAL, EN BOB, CONTAB, DIF
- Also includes a "next day exchange rate update" sub-table (rows 33-43)

### Section D — Arqueo de Cajas Consolidado (cols 22–57)
Detailed per-cashier reconciliation — the core report.
- Granularity: one row per cashier × currency (13 currencies per cashier)
- 16 paired transaction-type columns, each with count (#) and amount
- Ends with TOTAL SALDO, CIERRE, and DIFERENCIAS

### Section E — Resumen de Movimientos (cols 59–73)
Two sub-sections:
- E1: Consolidated movements per currency (10 category columns + CIERRE)
- E2: Agency-level closing balances (matrix: agency rows × currency columns)
- Also includes CANTIDADES (transaction count totals)

---

## 4. Reusable SQL Patterns

### 4.1 Cashier Reconciliation (from findCuadreCajas, corrected)

```sql
-- Pattern: SUM(CASE WHEN conciliacion IN (...) THEN saldo ELSE 0 END) AS category
-- Group by: agencia + moneda (via caja join)
-- Filter: DATE(fec_crea_saldo) = @Fecha
-- Tables: saldo_caja → caja → moneda, caja → agencia
```

### 4.2 Vault Reconciliation (from findCuadreBovedas, corrected)

```sql
-- Pattern: SUBSTRING_INDEX(conciliacion, '-', 1) to extract prefix
-- Group by: agencia + moneda + boveda
-- Filter: DATE(fec_crea_saldo) = @Fecha
-- Tables: saldo_boveda → boveda → moneda, boveda → agencia
-- saldo_final excludes 'C' (cierre) records
```

### 4.3 Exchange Rate Lookup (from getSaldoEfectivo)

```sql
-- CTE to get latest cotizacion_oficial per currency where id_operacion = 1
WITH UltimaCotizacion AS (
    SELECT id_moneda, valor,
           MAX(fec_crea_cotizacion_oficial) AS fec_crea_cotizacion_oficial
    FROM cotizacion_oficial
    WHERE id_operacion = 1
    GROUP BY id_moneda
)
-- Then JOIN and multiply: saldo * valor = saldo_en_bob
```

### 4.4 Per-Cashier Detail (NEW — needed for Section D)

```sql
-- Not in original SQL. Must be built:
-- Group by: caja.cod_usuario + moneda
-- Include: COUNT per category (for # columns) + SUM per category (for amount columns)
-- Compute: total_saldo, diferencia = cierre - total_saldo
```

---

## 5. Issues / Gaps Detected

### 5.1 CRITICAL — Data Dictionary vs SQL Conflicts

| # | Issue | Severity |
|---|-------|----------|
| 1 | **saldo_caja.conciliacion declared as char(2)** but data dictionary description mentions 'SAC-<nro>' pattern (which belongs to saldo_boveda). Some SQL codes are 1 char ('A', 'C'). Type is adequate for all known saldo_caja codes. | LOW |
| 2 | **saldo_caja conciliation codes massively undocumented.** Dictionary documents ~12 codes, SQL uses 25+. 13+ codes have no official definition. | HIGH |
| 3 | **saldo_boveda.conciliacion declared as char(9)** with compound 'PREFIX-ID' format. If boveda IDs exceed 5 digits, char(9) overflows (e.g., 'SAC-100000' = 10 chars). | MEDIUM |

### 5.2 SQL Bugs in Original PHP Code

| # | Bug | Location |
|---|-----|----------|
| 1 | **SQL injection**: `$fecha` and `$filtrar` interpolated directly | findCuadreCajas, findCuadreBovedas |
| 2 | **Invalid SQL syntax**: `'$fecha' '$filtrar'` — two adjacent string literals, `$filtrar` starts with `AND` which would produce `'2026-01-15' 'AND a.id_agencia = 5'` — implicit string concat in MySQL, but the AND becomes part of the string | findCuadreCajas:73, findCuadreBovedas:134 |
| 3 | **GROUP BY missing sb.id_boveda**: Inner query selects `sb.id_boveda` but groups only by `a.id_agencia, b.id_moneda` — MySQL allows this with ONLY_FULL_GROUP_BY off, but the result for id_boveda is nondeterministic | findCuadreBovedas:136 |
| 4 | **getSaldoEfectivo CTE non-deterministic**: Selects `CO.valor` while grouping by `co.id_moneda` only. The `valor` returned may not correspond to the MAX date row. | getSaldoEfectivo:147-158 |
| 5 | **getSaldoEfectivo missing GROUP BY b.id_boveda**: b.id_boveda is in SELECT but not in GROUP BY | getSaldoEfectivo:179 |

### 5.3 Missing SQL — Excel Sections Not Covered

| Excel Section | SQL Function | Status |
|---------------|-------------|--------|
| A — Transaction Log | (none) | **MISSING** — need raw saldo_caja query with usuario join |
| B — Vault Availability | getSaldoEfectivo (partial) | PARTIAL — SQL groups differently, missing per-agency breakdown |
| C — Daily System vs Accounting | (none) | **MISSING** — need sum(cajas) + sum(bovedas) per currency; CONTAB source unknown |
| D — Cashier Reconciliation | findCuadreCajas (partial) | PARTIAL — SQL groups by agencia+moneda, not by cashier; missing transaction counts; missing DIFERENCIAS computation |
| E — Movement Summary | (none) | **MISSING** — need consolidated summary + falla_caja integration + agency-level matrix |

### 5.4 Missing Data Source

- **Section C "CONTAB" column**: The accounting comparison value has no identified source table in the data dictionary. This likely comes from an **external accounting system** or manual entry.

### 5.5 Current .NET Implementation Gaps

The existing Reconciliation.* solution only implements the 3 original PHP queries. Based on this analysis, it needs:

1. **Per-cashier reconciliation** (Section D granularity) — not per-agency
2. **Transaction counts** alongside amounts
3. **DIFERENCIAS computation** per cashier
4. **Raw transaction log** query (Section A)
5. **Vault availability by agency** (Section B)
6. **Daily reconciliation summary** with exchange rates (Section C)
7. **Movement summary + agency matrix** (Section E)
8. **Falla de caja** integration
9. Fix getSaldoEfectivo CTE bug
10. Fix GROUP BY in findCuadreBovedas

---

## 6. Final Unified Interpretation (Single Source of Truth)

### 6.1 System Overview

Gambarte Bolivia is a **licensed financial services company** (regulated by ASFI) operating 8+ branch agencies. Core operations:
- **Cambio de moneda** — Foreign currency exchange (buy/sell across 13 currencies)
- **Giros nacionales** — Domestic wire transfers between agencies
- **Remesas internacionales** — International remittances (primarily Bolivia ↔ Chile)
- **Pagos** — Payment of incoming remittances from Chile

### 6.2 Cash Flow Architecture

```
                    ┌──────────────┐
                    │  COTIZACION  │ (BCB exchange rates)
                    │   OFICIAL    │
                    └──────┬───────┘
                           │ T/C
  ┌────────┐      ┌───────┴────────┐      ┌──────────┐
  │CLIENTES│─────►│     CAJA       │◄────►│  BOVEDA  │
  │        │◄─────│  (per cashier  │      │  (vault) │
  └────────┘      │   per currency)│      └────┬─────┘
                  └───────┬────────┘           │
                          │                    │
            ┌─────────────┼─────────────┐      │
            ▼             ▼             ▼      ▼
       ┌─────────┐  ┌─────────┐  ┌──────────────────┐
       │ CAMBIO  │  │  GIRO   │  │     REMESA       │
       │(exchange│  │(domestic│  │(international    │
       │ buy/sell)│  │ wire)   │  │ remittance)      │
       └─────────┘  └─────────┘  └──────────────────┘
```

Every financial operation creates entries in `saldo_caja` with a 2-char conciliation code. Transfers between caja↔bóveda create entries in both `saldo_caja` and `saldo_boveda`.

### 6.3 Daily Reconciliation Process

```
1. APERTURA  → Each cashier opens with an opening balance (code 'A')
2. OPERATIONS → Cambios, giros, remesas create ingreso/egreso entries
3. TRANSFERS  → Cash moves between cajas and bóvedas as needed
4. CIERRE     → Cashier closes register (code 'C')
5. ARQUEO     → Physical count (arqueo_caja/arqueo_boveda tables)
6. CUADRE     → System computes: DIFERENCIA = CIERRE - CALCULATED_TOTAL
                 If != 0, a falla_caja record is created
7. REPORT     → 5-section Excel report generated for the day
```

### 6.4 Definitive Conciliation Code Reference

#### saldo_caja — 25 codes in 11 categories

| Category | Ingreso Codes | Egreso Codes | SQL Aggregation Field |
|----------|--------------|-------------|----------------------|
| Apertura/Cierre | A | C | apertura_caja / cierre_caja |
| Cambio de ME | EC, AC | SC, VC | cambio_moneda / salida_cambio_moneda |
| Giro | EG, IG, OG, AG | SG, DG | envio_giro / pago_giro + devolucion_giro |
| Remesa | ER, IR, OR, AR | SR, LR, VR | envio_remesa / pago_remesa + devolucion_remesa |
| Transferencia Bóveda | ET | ST | entrada_transferencia_boveda / salida_transferencia_boveda |
| Traspaso Cajas | EO | SO | entrada_traspaso_cajas / salida_traspaso_cajas |
| Cobro Cheque | EE, RB | — | cobro_cheque |
| Depósito Bancario | — | SE, DR, DB | salida_deposito_para |
| Controlado | IA | EA, PG | ingreso_controlado / egreso_controlado |

#### saldo_boveda — 8 codes (prefix-based)

| Category | Ingreso Prefix | Egreso Prefix |
|----------|---------------|---------------|
| Apertura/Cierre | A | C |
| Apertura Caja | ECC | SAC |
| Transferencia Bóveda | ETB | STB |
| Traspaso Casa/Caja | ETC | STC |

### 6.5 Required Query Inventory for Complete System

| # | Query | Granularity | Source Tables | Maps to Excel |
|---|-------|-------------|---------------|---------------|
| Q1 | Transaction Log | per-transaction | saldo_caja + caja + usuario + moneda | Section A |
| Q2 | Vault Availability | per-agency × currency | saldo_boveda + boveda + moneda + agencia + cotizacion_oficial | Section B |
| Q3 | Daily Cuadre Summary | per-currency + total | saldo_caja + saldo_boveda aggregated + cotizacion_oficial | Section C |
| Q4 | Cashier Reconciliation | per-cashier × currency | saldo_caja + caja + moneda + usuario + agencia | Section D |
| Q5 | Movement Summary | per-currency consolidated | saldo_caja aggregated + falla_caja | Section E1 |
| Q6 | Agency Closing Matrix | per-agency × currency | saldo_caja WHERE conciliacion='C' grouped by agency | Section E2 |
| Q7 | Vault Reconciliation | per-agency × currency × boveda | saldo_boveda + boveda + moneda + agencia | (API/internal) |
| Q8 | Exchange Rate Lookup | per-currency | cotizacion_oficial WHERE id_operacion=1, latest date | Shared |

### 6.6 Decimal Precision Map

| Context | Precision | Used In |
|---------|-----------|---------|
| Balances (saldo_caja, saldo_boveda) | decimal(14,4) | All reconciliation |
| Exchange rates | decimal(16,6) | cotizacion_oficial, cotizacion |
| Transaction amounts (cambio) | decimal(16,6) | cambio table |
| Transaction amounts (giro) | decimal(14,4) to decimal(14,5) | giro table |
| Transaction amounts (remesa) | decimal(14,5) | remesa table |
| Commissions | decimal(14,4) to decimal(14,5) | comision, giro, remesa |
| Caja limits | decimal(14,4) | caja.maximo/minimo |

**FLAG**: Mixed precision across related fields (14,4 vs 14,5 vs 16,6). The .NET implementation should use `decimal` throughout (C# decimal supports 28-29 significant digits) to avoid precision loss.
