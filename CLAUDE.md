# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build src/Reconciliation.sln
cd src/Reconciliation.Functions && func start
```

Configure `src/Reconciliation.Functions/local.settings.json` with the following connection strings before running:

| Setting | Required? | Database | Purpose |
|---------|-----------|----------|---------|
| `SqlConnectionString` | **Yes** | `u752765750_cgr` (MariaDB) | Bolivia — reconciliation + remittance source |
| `ChileConnectionString` | Optional | Chile DB (`tmp_reporte_remesa`) | Remittance module — Chile side |
| `AccountingConnectionString` | Optional | `u752765750_conta` (`co_asientos`) | Remittance module — accounting validation |

The remittance endpoints are only functional when both `ChileConnectionString` and `AccountingConnectionString` are configured; otherwise `Program.cs` falls back to the Bolivia-only registration and the remittance repositories are not wired up.

## Architecture

.NET 8 Azure Functions (Isolated Worker) — clean architecture, 4 layers:

```
src/
  Reconciliation.Domain          — Entities, repository interfaces, ConciliacionCodes,
                                   RemittanceStatus (Bolivia/Chile), AccountingConceptPatterns
  Reconciliation.Application     — DTOs, service interfaces/implementations, export services (Excel/PDF)
  Reconciliation.Infrastructure  — Dapper repositories + DbConnectionFactory (multi-connection MariaDB/MySQL)
  Reconciliation.Functions       — Timer + HTTP functions (JSON + download), DI wiring
```

**Dependency flow**: Functions → Application + Infrastructure → Domain

### Key patterns
- **Dapper + MySqlConnector** — all DB access via parameterized queries (anonymous `@Param` objects)
- Repositories return flat row types (e.g., `CashierReconciliationRow`, `BoliviaRemittanceRow`) defined alongside interfaces in Domain
- Services group flat rows into nested DTOs and compute business rules (theoretical balance, discrepancies, accounting validation)
- **DTOs use `init` properties** (not records), so `with` expressions do NOT work on them
- **Export services** — `IExcelExportService` (ClosedXML) and `IPdfExportService` (QuestPDF) generate styled reports with corporate branding
- Shared export helpers: `WriteMarquee`, `WriteHeaders`, `ApplyDecimalFormat`, `ApplyZebraStripes`, `ApplyDataBorders`
- DI via `AddApplication()` + `AddInfrastructure(connectionString)` **or** `AddInfrastructure(Dictionary<string,string>)` extensions. The dictionary overload registers the remittance repositories (`IRemittanceRepository`, `IChileRemittanceRepository`, `IAccountingRepository`); the single-string overload does not.
- **Named connections** in `DbConnectionFactory`: `"Bolivia"` (default), `"Chile"`, `"Accounting"`
- Exchange rates use a deterministic CTE pattern (`INNER JOIN` on `MAX(fec_crea_cotizacion_oficial)`)
- JSON uses `JsonNamingPolicy.SnakeCaseLower` + `DefaultIgnoreCondition.WhenWritingNull`
- Company logo embedded as resource at `Export/Img/logo.png`; QuestPDF Community license set in `Program.cs`

### Core business rule
```
TheoreticalBalance = TotalIngreso - TotalEgreso
Diferencia = CierreCaja - TheoreticalBalance   (zero when balanced)
```

### Functions

#### Reconciliation (Bolivia only — `SqlConnectionString`)

**JSON endpoints (data):**
- `DailyReconciliation` — TimerTrigger 23:30 UTC, runs all 6 report sections in parallel
- `GetCashierReconciliation` — GET /api/reconciliation/cashiers?date=YYYY-MM-DD&agencia=N
- `GetVaultReconciliation` — GET /api/reconciliation/vaults
- `GetVaultAvailability` — GET /api/reconciliation/availability
- `GetDailySummary` — GET /api/reconciliation/summary
- `GetAgencyClosings` — GET /api/reconciliation/closings
- `GetMovementSummary` — GET /api/reconciliation/movements

**Download endpoints (Excel/PDF):** same query params + `&format=xlsx|pdf` (default xlsx)
- `DownloadCashierReconciliation` — GET /api/reconciliation/cashiers/download
- `DownloadVaultReconciliation` — GET /api/reconciliation/vaults/download
- `DownloadVaultAvailability` — GET /api/reconciliation/availability/download
- `DownloadDailySummary` — GET /api/reconciliation/summary/download
- `DownloadAgencyClosings` — GET /api/reconciliation/closings/download
- `DownloadMovementSummary` — GET /api/reconciliation/movements/download

#### Remittances (Bolivia ↔ Chile corridor — all three connections required)

Cross-border reconciliation of the Bolivia–Chile remittance corridor. Joins Bolivia and Chile databases via the `codigo` field (CGR code) and cross-validates against `co_asientos` accounting entries. All three endpoints operate on the **full month** of the provided `date`.

**JSON endpoints:**
- `GetRemittanceDailyOperations` — GET /api/reconciliation/remittances/daily-operations?date=YYYY-MM-DD
  - Sent / paid / cancelled for the specific date, plus accumulated pending (both directions)
- `GetRemittanceAccountingValidation` — GET /api/reconciliation/remittances/accounting-validation?date=YYYY-MM-DD
  - Full-month validation of each remittance against its expected `co_asientos` entry (3 `AccountingValidationType` variants)
- `GetRemittanceGeneralReconciliation` — GET /api/reconciliation/remittances/general-reconciliation?date=YYYY-MM-DD
  - Daily matrix with carry-forward from previous month, payment validation (field-level match), cross-DB discrepancies, and running balance

**Download endpoints:** same query params + `&format=xlsx|pdf`
- `DownloadRemittanceDailyOperations` — GET /api/reconciliation/remittances/daily-operations/download
- `DownloadRemittanceAccountingValidation` — GET /api/reconciliation/remittances/accounting-validation/download
- `DownloadRemittanceGeneralReconciliation` — GET /api/reconciliation/remittances/general-reconciliation/download

### Report sections (mapped from Excel)
| Section | Service Method | Description |
|---------|---------------|-------------|
| D | CashierService.GetReconciliationAsync | Per-cashier × currency with 16 transaction categories + counts |
| B | VaultService.GetAvailabilityAsync | Vault balances with exchange rate conversion |
| C | CashierService.GetDailySummaryAsync | Caja + boveda totals per currency vs accounting |
| E1 | CashierService.GetMovementSummaryAsync | Consolidated movements per currency |
| E2 | CashierService.GetAgencyClosingsAsync | Agency × currency closing matrix |

## Domain Context

Gambarte Bolivia — financial services (currency exchange, wire transfers, remittances). 8 agencies, 13 currencies, 25+ conciliation codes.

### Core tables
- **agencia** — branch offices
- **caja** / **saldo_caja** — cash registers, one per user per currency, balance movements
- **boveda** / **saldo_boveda** — vaults, compound PREFIX-ID conciliation codes
- **moneda** — currencies; **cotizacion_oficial** — BCB exchange rates
- **falla_caja** — registered discrepancies (PK: id_caja + fecha)

### Conciliation codes — see `ConciliacionCodes.cs` for definitive reference

**saldo_caja** (25 codes, char(2)): Ingresos: A, EC/AC, EG/IG/OG/AG, ER/IR/OR/AR, ET, EO, EE/RB, IA. Egresos: C, SC/VC, SG, DG, SR/LR, VR, ST, SO, SE/DR/DB, EA/PG.

**saldo_boveda** (8 prefixes, char(9), split by `-`): SAC, STB, STC (egresos); ECC, ETB, ETC (ingresos); A (apertura), C (cierre).

### Remittance domain — see `RemittanceStatus.cs`

**Operation type ids (filter `id_operacion`):**
- Bolivia `remesa`: `[4, 16]` · Bolivia `pago`: `[4, 7]`
- Chile `tmp_reporte_remesa` / `pago`: `[3, 5, 6, 8]`

**Bolivia `estado` (`remesa`):** `0=PENDIENTE`, `1=PAGADO`, `2=DEVUELTO`, `5=ANULADO`

**Chile `estado` (`tmp_reporte_remesa`):** `0=PENDIENTE`, `1=PAGADO`, `2=DEVUELTO`, `3=DEVUELTO SC`, `4=CAMBIADO`, `5=ANULADO`, `6=A DEVOLVER`, `7=EN PROCESO`, `8=AHORRO`. Chile rows with `codigo < 0` are overridden to `Anulado` (status 5) by `RemittanceService`.

**Direction** is derived from Chile's `abr_ori` / `abr_des` columns, producing `"CHL->BOL"` or `"BOL->CHL"`. Bolivia and Chile records are matched across databases via the shared `codigo` (CGR code) field.

**Accounting validation** (`AccountingConceptPatterns` on `co_asientos.concepto`):
- **BOL→CHL pending** — concepto `LIKE '%ENVIO DE%'`, expected `importe = remesa.total_bob + remesa.itf_bob`
- **BOL→CHL paid** — concepto `LIKE 'PAGO EN DESTINO%'`, expected `importe = pago.total_bob`
- **CHL→BOL paid** — two entries: `%PENDIENTES DE COBRO DEL PAGO DE REMESA%` + `%PAGO DE REMESA CGR%`, same amount

## Analysis reference
See `ANALYSIS.md` for full cross-validation: data dictionary vs SQL vs Excel, all bugs found, and the unified interpretation.
See `docs/API-DOCUMENTATION.md` for per-endpoint request/response schemas and examples.
