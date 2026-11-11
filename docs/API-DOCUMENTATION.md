# Sistema de Conciliación Financiera — Documentación Técnica

## Tabla de Contenido

1. [Descripción General](#1-descripción-general)
2. [Endpoints Disponibles](#2-endpoints-disponibles)
3. [Endpoints de Descarga (Excel/PDF)](#3-endpoints-de-descarga-excelpdf)
4. [Módulo de Remesas (Bolivia ↔ Chile)](#4-módulo-de-remesas-bolivia--chile)
5. [Casos de Uso](#5-casos-de-uso)
6. [Guía de Uso](#6-guía-de-uso)
7. [Ejemplos Prácticos](#7-ejemplos-prácticos)
8. [Consideraciones Técnicas](#8-consideraciones-técnicas)

---

## 1. Descripción General

### Qué hace el sistema

Sistema de conciliación financiera (cuadre de cajas y bóvedas) para Gambarte Bolivia, una empresa de servicios financieros regulada por la ASFI que opera cambio de moneda, giros nacionales y remesas internacionales a través de 8 agencias.

El sistema reemplaza la lógica legacy en PHP con una API moderna en .NET 8 (Azure Functions Isolated Worker) que:

- Calcula el **saldo teórico** de cada caja por cajero y moneda
- Compara contra el **cierre real** registrado por el cajero
- Detecta y reporta **diferencias** (descuadres)
- Genera reportes consolidados de bóvedas con conversión a moneda local (BOB)
- Permite descargar cada reporte como archivo **Excel (.xlsx)** o **PDF (.pdf)** con branding corporativo
- Ejecuta automáticamente el cuadre diario a las 23:30 UTC
- Concilia el **corredor de remesas Bolivia ↔ Chile**, cruzando la base CGR de Bolivia, `tmp_reporte_remesa` de Chile y los asientos contables en `co_asientos`

### Problema que resuelve

Antes, la conciliación se realizaba con consultas PHP que construían JSON directamente en SQL (`CONCAT`/`GROUP_CONCAT`), con inyección SQL, agrupación incorrecta y lógica de tipo de cambio no determinista. Este sistema:

- Corrige 5 bugs críticos del sistema legacy
- Cambia la granularidad de **por agencia** a **por cajero** (como requiere el reporte real)
- Agrega conteos de transacciones por categoría
- Implementa detección automática de descuadres con logging estructurado

### Arquitectura

```
┌──────────────────────────────────────────────────────────────────────┐
│                       Azure Functions (.NET 8)                       │
│  ┌──────────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │  TimerTrigger     │  │  JSON (6)    │  │  Download (6)          │ │
│  │  23:30 UTC diario │  │  GET /api/*  │  │  GET /api/*/download   │ │
│  └────────┬─────────┘  └──────┬───────┘  └──────────┬─────────────┘ │
│           │                   │                     │               │
│  ┌────────▼───────────────────▼─────────────────────▼─────────────┐ │
│  │                    Application Layer                            │ │
│  │  CashierService (4)  │  VaultService (2)                       │ │
│  │  Regla: Teórico = Ing - Egr  │  Conversión T/C                │ │
│  │─────────────────────────────────────────────────────────────── │ │
│  │  ExcelExportService (ClosedXML)  │  PdfExportService (QuestPDF)│ │
│  │  Logo + colores corporativos  │  Datos faltantes resaltados    │ │
│  └────────────────────┬──────────────────────────────────────────┘ │
│  ┌────────────────────▼──────────────────────────────────────────┐ │
│  │                    Infrastructure Layer                        │ │
│  │  CashierRepository │ VaultRepository │ ExchangeRate            │ │
│  │  Dapper + SQL parametrizado (MariaDB/MySQL)                   │ │
│  └───────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 2. Endpoints Disponibles

**Base URL:** `http://localhost:7071/api/reconciliation`

**Autenticación:** Nivel `Function` (requiere `x-functions-key` o `code` en query string en producción).

**Formato de respuesta:** JSON con propiedades en `snake_case` (endpoints de datos). Los endpoints `/download` retornan archivos Excel o PDF.

**Convención de nulos:** Los campos con valor `null` se omiten de la respuesta JSON.

---

### 2.1 Cuadre de Cajas por Cajero

Reporte completo de conciliación por cajero y moneda con las 16 categorías de transacción, conteos, saldo teórico, cierre real y diferencias.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **URL** | `/api/reconciliation/cashiers` |
| **Función** | `GetCashierReconciliation` |

**Parámetros de consulta:**

| Parámetro | Tipo | Requerido | Ejemplo | Descripción |
|-----------|------|-----------|---------|-------------|
| `date` | string | No | `2026-01-15` | Fecha en formato `yyyy-MM-dd`. Por defecto: fecha actual UTC. |
| `agencia` | int | No | `1` | Filtrar por ID de agencia. Si se omite, retorna todas. |

**Request:**

```http
GET /api/reconciliation/cashiers?date=2026-01-15&agencia=1 HTTP/1.1
Host: localhost:7071
```

**Response (200 OK):**

```json
{
  "fecha": "2026-01-15T00:00:00",
  "cashiers": [
    {
      "cajero": "avallejos",
      "id_agencia": 1,
      "agencia": "AGENCIA SUCRE CENTRAL",
      "currencies": [
        {
          "id_caja": 12,
          "num_caja": 1,
          "moneda": "BOB",
          "tipo_cambio_oficial": 1.0,
          "apertura_caja": 15000.0000,
          "cambio_moneda_ingreso": { "count": 8, "amount": 42560.5000 },
          "envio_giro": { "count": 12, "amount": 67800.0000 },
          "envio_remesa": { "count": 5, "amount": 34200.0000 },
          "traspaso_de_caja": { "count": 1, "amount": 5000.0000 },
          "cobro_cheque": { "count": 0, "amount": 0.0000 },
          "ingreso_controlado": { "count": 0, "amount": 0.0000 },
          "transferencia_boveda_ingreso": { "count": 2, "amount": 80000.0000 },
          "cambio_moneda_egreso": { "count": 6, "amount": 38750.0000 },
          "pago_giro": { "count": 4, "amount": 23400.0000 },
          "devolucion_giro": 0.0000,
          "pago_remesa": { "count": 3, "amount": 18900.0000 },
          "devolucion_remesa": 0.0000,
          "traspaso_a_caja": { "count": 1, "amount": 3000.0000 },
          "deposito_bancario": { "count": 2, "amount": 120000.0000 },
          "egreso_controlado": { "count": 1, "amount": 1500.0000 },
          "transferencia_boveda_egreso": { "count": 1, "amount": 25000.0000 },
          "total_ingreso": 244560.5000,
          "total_egreso": 230550.0000,
          "total_saldo": 14010.5000,
          "cierre_caja": 14010.5000,
          "diferencia": 0.0000
        },
        {
          "id_caja": 13,
          "num_caja": 2,
          "moneda": "USD",
          "tipo_cambio_oficial": 6.860000,
          "apertura_caja": 2000.0000,
          "cambio_moneda_ingreso": { "count": 5, "amount": 3200.0000 },
          "envio_giro": { "count": 0, "amount": 0.0000 },
          "envio_remesa": { "count": 2, "amount": 1500.0000 },
          "traspaso_de_caja": { "count": 0, "amount": 0.0000 },
          "cobro_cheque": { "count": 0, "amount": 0.0000 },
          "ingreso_controlado": { "count": 0, "amount": 0.0000 },
          "transferencia_boveda_ingreso": { "count": 1, "amount": 5000.0000 },
          "cambio_moneda_egreso": { "count": 4, "amount": 2800.0000 },
          "pago_giro": { "count": 0, "amount": 0.0000 },
          "devolucion_giro": 0.0000,
          "pago_remesa": { "count": 1, "amount": 800.0000 },
          "devolucion_remesa": 0.0000,
          "traspaso_a_caja": { "count": 0, "amount": 0.0000 },
          "deposito_bancario": { "count": 1, "amount": 5000.0000 },
          "egreso_controlado": { "count": 0, "amount": 0.0000 },
          "transferencia_boveda_egreso": { "count": 0, "amount": 0.0000 },
          "total_ingreso": 11700.0000,
          "total_egreso": 8600.0000,
          "total_saldo": 3100.0000,
          "cierre_caja": 3100.0000,
          "diferencia": 0.0000
        }
      ],
      "fallas_registradas": {}
    }
  ],
  "total_discrepancies": 0
}
```

**Explicación de la respuesta:**

| Campo | Descripción |
|-------|-------------|
| `cashiers[].cajero` | Login del usuario/cajero (tabla `usuario.login_usu`) |
| `cashiers[].currencies[]` | Un registro por cada caja (una moneda) del cajero |
| `total_ingreso` | Suma de todas las categorías de ingreso |
| `total_egreso` | Suma de todas las categorías de egreso |
| `total_saldo` | **Saldo teórico** = `total_ingreso - total_egreso` |
| `cierre_caja` | Saldo registrado al cerrar la caja (código `C` en `saldo_caja`) |
| `diferencia` | `cierre_caja - total_saldo`. **Debe ser 0** si está cuadrado |
| `fallas_registradas` | Discrepancias ya registradas en tabla `falla_caja`. Key=IdCaja |
| `total_discrepancies` | Cantidad total de monedas con `diferencia ≠ 0` |
| Campos `TransactionEntry` | `count` = número de transacciones, `amount` = monto total |

---

### 2.2 Cuadre de Bóvedas

Conciliación de movimientos por bóveda, agrupado por agencia y moneda.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **URL** | `/api/reconciliation/vaults` |
| **Función** | `GetVaultReconciliation` |

**Parámetros:** `date` (yyyy-MM-dd), `agencia` (int, opcional).

**Request:**

```http
GET /api/reconciliation/vaults?date=2026-01-15 HTTP/1.1
```

**Response (200 OK):**

```json
{
  "fecha": "2026-01-15T00:00:00",
  "agencies": [
    {
      "id_agencia": 1,
      "agencia": "AGENCIA SUCRE CENTRAL",
      "movimientos": [
        {
          "id_boveda": 3,
          "num_boveda": 1,
          "id_moneda": 1,
          "moneda": "BOB",
          "saldo_apertura": 500000.0000,
          "salida_apertura_caja": 95000.0000,
          "salida_traspaso_boveda": 0.0000,
          "salida_traspaso_caja": 0.0000,
          "total_egresos": 95000.0000,
          "entrada_cierre_caja": 320000.0000,
          "entrada_traspaso_boveda": 0.0000,
          "entrada_traspaso_casa": 0.0000,
          "total_ingresos": 320000.0000,
          "saldo_final": 725000.0000
        },
        {
          "id_boveda": 4,
          "num_boveda": 2,
          "id_moneda": 2,
          "moneda": "USD",
          "saldo_apertura": 25000.0000,
          "salida_apertura_caja": 8000.0000,
          "salida_traspaso_boveda": 0.0000,
          "salida_traspaso_caja": 0.0000,
          "total_egresos": 8000.0000,
          "entrada_cierre_caja": 12500.0000,
          "entrada_traspaso_boveda": 0.0000,
          "entrada_traspaso_casa": 0.0000,
          "total_ingresos": 12500.0000,
          "saldo_final": 29500.0000
        }
      ]
    }
  ]
}
```

**Explicación:**

| Campo | Descripción |
|-------|-------------|
| `saldo_apertura` | Monto de apertura de la bóveda (código `A`) |
| `salida_apertura_caja` | Dinero enviado para abrir cajas de cajeros (prefijo `SAC`) |
| `entrada_cierre_caja` | Dinero devuelto al cerrar cajas (prefijo `ECC`) |
| `total_egresos` | Suma de salidas: SAC + STB + STC |
| `total_ingresos` | Suma de entradas: ECC + ETB + ETC |
| `saldo_final` | Suma de todos los movimientos excepto cierre (código `C`) |

---

### 2.3 Disponibilidad en Bóveda

Saldos de bóveda por agencia y moneda con conversión a BOB usando tipo de cambio oficial del BCB.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **URL** | `/api/reconciliation/availability` |
| **Función** | `GetVaultAvailability` |

**Parámetros:** `date` (yyyy-MM-dd).

**Request:**

```http
GET /api/reconciliation/availability?date=2026-01-15 HTTP/1.1
```

**Response (200 OK):**

```json
{
  "fecha": "2026-01-15T00:00:00",
  "entries": [
    {
      "id_agencia": 1,
      "agencia": "AGENCIA SUCRE CENTRAL",
      "moneda": "BOB",
      "saldo": 725000.0000,
      "tipo_cambio_oficial": 1.000000,
      "saldo_bob": 725000.0000,
      "fecha_cotizacion": "2026-01-15T00:00:00"
    },
    {
      "id_agencia": 1,
      "agencia": "AGENCIA SUCRE CENTRAL",
      "moneda": "USD",
      "saldo": 29500.0000,
      "tipo_cambio_oficial": 6.860000,
      "saldo_bob": 202370.0000,
      "fecha_cotizacion": "2026-01-15T00:00:00"
    },
    {
      "id_agencia": 5,
      "agencia": "AGENCIA COCHABAMBA CENTRAL",
      "moneda": "BOB",
      "saldo": 310000.0000,
      "tipo_cambio_oficial": 1.000000,
      "saldo_bob": 310000.0000,
      "fecha_cotizacion": "2026-01-15T00:00:00"
    }
  ],
  "total_saldo_bob": 1237370.0000
}
```

**Fórmula:** `saldo_bob = saldo × tipo_cambio_oficial`

---

### 2.4 Resumen Diario (Cajas + Bóvedas)

Totales consolidados por moneda: cajas, bóvedas, suma total, conversión a BOB.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **URL** | `/api/reconciliation/summary` |
| **Función** | `GetDailySummary` |

**Parámetros:** `date` (yyyy-MM-dd).

**Request:**

```http
GET /api/reconciliation/summary?date=2026-01-15 HTTP/1.1
```

**Response (200 OK):**

```json
{
  "fecha": "2026-01-15T00:00:00",
  "currencies": [
    {
      "moneda": "BOB",
      "total_cajas": 482350.5000,
      "total_boveda": 1035000.0000,
      "total_cajas_boveda": 1517350.5000,
      "tipo_cambio_oficial": 1.000000,
      "monedas_en_bob": 1517350.5000
    },
    {
      "moneda": "USD",
      "total_cajas": 18200.0000,
      "total_boveda": 54500.0000,
      "total_cajas_boveda": 72700.0000,
      "tipo_cambio_oficial": 6.860000,
      "monedas_en_bob": 498722.0000
    },
    {
      "moneda": "EUR",
      "total_cajas": 3200.0000,
      "total_boveda": 8500.0000,
      "total_cajas_boveda": 11700.0000,
      "tipo_cambio_oficial": 7.450000,
      "monedas_en_bob": 87165.0000
    }
  ],
  "grand_total_bob": 2103237.5000
}
```

**Explicación:**

| Campo | Fórmula |
|-------|---------|
| `total_cajas` | Suma de cierres de caja (conciliación `C`) por moneda |
| `total_boveda` | Suma de saldos de bóveda (excluye cierre) por moneda |
| `total_cajas_boveda` | `total_cajas + total_boveda` |
| `monedas_en_bob` | `total_cajas_boveda × tipo_cambio_oficial` |
| `grand_total_bob` | Suma de todas las `monedas_en_bob` |
| `contabilidad` | Valor del sistema contable externo. **Actualmente `null`** (fuente no disponible en BD CGR) |
| `diferencia` | `monedas_en_bob - contabilidad`. **`null`** cuando `contabilidad` no está disponible |

> **Nota:** Los campos `contabilidad` y `diferencia` se omiten del JSON cuando son `null` (configuración `WhenWritingNull`).

---

### 2.5 Cierres por Agencia

Matriz de saldos de cierre: filas = agencias, columnas = monedas.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **URL** | `/api/reconciliation/closings` |
| **Función** | `GetAgencyClosings` |

**Parámetros:** `date` (yyyy-MM-dd).

**Request:**

```http
GET /api/reconciliation/closings?date=2026-01-15 HTTP/1.1
```

**Response (200 OK):**

```json
{
  "fecha": "2026-01-15T00:00:00",
  "agencies": [
    {
      "id_agencia": 1,
      "agencia": "AGENCIA SUCRE CENTRAL",
      "currency_closings": {
        "BOB": 142350.5000,
        "USD": 8200.0000,
        "EUR": 1200.0000
      }
    },
    {
      "id_agencia": 3,
      "agencia": "AGENCIA COLON",
      "currency_closings": {
        "BOB": 98400.0000,
        "USD": 3500.0000
      }
    },
    {
      "id_agencia": 5,
      "agencia": "AGENCIA COCHABAMBA CENTRAL",
      "currency_closings": {
        "BOB": 115200.0000,
        "USD": 4100.0000,
        "EUR": 800.0000,
        "BRL": 2500.0000
      }
    }
  ],
  "totals": {
    "BOB": 355950.5000,
    "USD": 15800.0000,
    "EUR": 2000.0000,
    "BRL": 2500.0000
  }
}
```

**Explicación:**

- `currency_closings`: Diccionario donde la clave es la abreviatura de moneda y el valor es la suma de cierres de caja en esa agencia para esa moneda.
- `totals`: Suma de todas las agencias por moneda. Permite validar que la suma vertical cuadra.

---

### 2.6 Resumen de Movimientos

Movimientos consolidados por moneda con signos embebidos (ingresos positivos, egresos negativos).

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **URL** | `/api/reconciliation/movements` |
| **Función** | `GetMovementSummary` |

**Parámetros:** `date` (yyyy-MM-dd).

**Request:**

```http
GET /api/reconciliation/movements?date=2026-01-15 HTTP/1.1
```

**Response (200 OK):**

```json
{
  "fecha": "2026-01-15T00:00:00",
  "currencies": [
    {
      "moneda": "BOB",
      "apertura_caja": 150000.0000,
      "cambio_moneda": 3810.5000,
      "envio_giro": 185400.0000,
      "pago_giro": -67200.0000,
      "envio_remesa": 98500.0000,
      "pago_remesa": -42300.0000,
      "egresos": -127500.0000,
      "transferencias": 155000.0000,
      "fallas_caja": -360.5000,
      "cierre_caja": 355350.0000
    },
    {
      "moneda": "USD",
      "apertura_caja": 12000.0000,
      "cambio_moneda": 400.0000,
      "envio_giro": 0.0000,
      "pago_giro": 0.0000,
      "envio_remesa": 4500.0000,
      "pago_remesa": -2100.0000,
      "egresos": -5000.0000,
      "transferencias": 8400.0000,
      "fallas_caja": 0.0000,
      "cierre_caja": 18200.0000
    }
  ]
}
```

**Convención de signos:**

| Campo | Signo | Descripción |
|-------|-------|-------------|
| `apertura_caja` | + | Siempre positivo |
| `cambio_moneda` | ± | Ingreso cambio − Egreso cambio |
| `envio_giro` | + | Dinero recibido por envío de giros |
| `pago_giro` | − | `-(pago_giro + devolucion_giro)` |
| `envio_remesa` | + | Dinero recibido por envío de remesas |
| `pago_remesa` | − | `-(pago_remesa + devolucion_remesa)` |
| `egresos` | − | `-(depositos + egresos_controlados) + ingresos_controlados` |
| `transferencias` | ± | Entrada bóveda − Salida bóveda + Traspaso recibido − Traspaso enviado + Cobro cheque |
| `fallas_caja` | − | Diferencias registradas en `falla_caja` |
| `cierre_caja` | + | Saldo real al cierre |

---

## 3. Endpoints de Descarga (Excel/PDF)

Cada endpoint JSON tiene un equivalente `/download` que retorna el mismo reporte como archivo descargable en formato Excel (.xlsx) o PDF (.pdf), con presentación corporativa.

**Características visuales de los reportes:**

- **Encabezado corporativo:** Logo de Gambarte S.R.L., nombre de empresa, slogan, título del reporte y fecha/hora de generación
- **Paleta corporativa:** Naranja (#E87722), Negro (#000000), Gris (#808184) y Blanco (#FFFFFF)
- **Datos faltantes:** Resaltados con fondo durazno suave (#FDE8D0) para fácil identificación
- **Filas alternadas:** Sombreado cebra en tono naranja suave para mejorar legibilidad
- **Fila de totales:** Fondo gris claro con borde superior doble en naranja
- **Bordes y tipografía:** Bordes finos internos, borde exterior medio, encabezados en blanco sobre fondo negro

---

### 3.1 Parámetros comunes

Todos los endpoints de descarga aceptan los mismos parámetros que su equivalente JSON, más un parámetro adicional:

| Parámetro | Tipo | Requerido | Default | Descripción |
|-----------|------|-----------|---------|-------------|
| `date` | string | No | Fecha actual UTC | Fecha en formato `yyyy-MM-dd` |
| `agencia` | int | No | Todas | Filtrar por ID de agencia (solo en cashiers y vaults) |
| `format` | string | No | `xlsx` | Formato del archivo: `xlsx` (Excel) o `pdf` (PDF) |

---

### 3.2 Tabla de Endpoints

| Reporte | URL de descarga | Función | Nombre del archivo generado |
|---------|----------------|---------|-----------------------------|
| Cuadre de Cajas | `GET /api/reconciliation/cashiers/download` | `DownloadCashierReconciliation` | `Report_CashierReconciliation_YYYY-MM-DD.xlsx\|pdf` |
| Cuadre de Bóvedas | `GET /api/reconciliation/vaults/download` | `DownloadVaultReconciliation` | `Report_VaultReconciliation_YYYY-MM-DD.xlsx\|pdf` |
| Disponibilidad Bóveda | `GET /api/reconciliation/availability/download` | `DownloadVaultAvailability` | `Report_VaultAvailability_YYYY-MM-DD.xlsx\|pdf` |
| Resumen Diario | `GET /api/reconciliation/summary/download` | `DownloadDailySummary` | `Report_DailySummary_YYYY-MM-DD.xlsx\|pdf` |
| Cierres por Agencia | `GET /api/reconciliation/closings/download` | `DownloadAgencyClosings` | `Report_AgencyClosings_YYYY-MM-DD.xlsx\|pdf` |
| Resumen Movimientos | `GET /api/reconciliation/movements/download` | `DownloadMovementSummary` | `Report_MovementSummary_YYYY-MM-DD.xlsx\|pdf` |

---

### 3.3 Response

**Headers de la respuesta:**

| Header | Valor (Excel) | Valor (PDF) |
|--------|---------------|-------------|
| `Content-Type` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | `application/pdf` |
| `Content-Disposition` | `attachment; filename="Report_<nombre>_<fecha>.xlsx"` | `attachment; filename="Report_<nombre>_<fecha>.pdf"` |

**Body:** Contenido binario del archivo (bytes).

---

### 3.4 Ejemplos de uso

**Descargar cuadre de cajas en Excel (formato por defecto):**

```bash
curl -s "http://localhost:7071/api/reconciliation/cashiers/download?date=2026-01-15" \
  -H "x-functions-key: tu-key-aqui" \
  -o CashierReconciliation.xlsx
```

**Descargar cuadre de cajas en PDF:**

```bash
curl -s "http://localhost:7071/api/reconciliation/cashiers/download?date=2026-01-15&format=pdf" \
  -H "x-functions-key: tu-key-aqui" \
  -o CashierReconciliation.pdf
```

**Descargar disponibilidad de bóveda en Excel:**

```bash
curl -s "http://localhost:7071/api/reconciliation/availability/download?date=2026-01-15" \
  -H "x-functions-key: tu-key-aqui" \
  -o VaultAvailability.xlsx
```

**Descargar cuadre de bóvedas filtrado por agencia en PDF:**

```bash
curl -s "http://localhost:7071/api/reconciliation/vaults/download?date=2026-01-15&agencia=1&format=pdf" \
  -H "x-functions-key: tu-key-aqui" \
  -o VaultReconciliation_Ag1.pdf
```

---

### 3.5 Detalle de cada hoja/reporte

#### Cuadre de Cajas (Section D)

| Hoja Excel | `Arqueo de Caja` |
|------------|------------------|
| **Columnas** | Cajero, Agencia, Moneda, Num Caja, Apertura Caja, [16 categorías de transacción con # y monto], Total Ingreso, Total Egreso, Total Saldo, Cierre Caja, Diferencia |
| **Filas** | Una fila por caja (combinación cajero × moneda) |
| **Orientación PDF** | Horizontal (A4 Landscape) |

#### Cuadre de Bóvedas

| Hoja Excel | `Boveda` |
|------------|----------|
| **Columnas** | Agencia, Boveda, Moneda, Saldo Apertura, [3 tipos de salida], Total Egresos, [3 tipos de entrada], Total Ingresos, Saldo Final |
| **Filas** | Una fila por bóveda (combinación agencia × moneda) |
| **Orientación PDF** | Horizontal (A4 Landscape) |

#### Disponibilidad en Bóveda (Section B)

| Hoja Excel | `Disponibilidad Boveda` |
|------------|------------------------|
| **Columnas** | Agencia, Moneda, Saldo, Tipo Cambio, Saldo BOB |
| **Filas** | Una fila por combinación agencia × moneda + fila TOTAL |
| **Orientación PDF** | Vertical (A4 Portrait) |

#### Cuadre Diario (Section C)

| Hoja Excel | `Cuadre Diario` |
|------------|-----------------|
| **Columnas** | Moneda, Total Cajas, Total Boveda, Total Cajas+Boveda, Tipo Cambio, Monedas en BOB, Contabilidad, Diferencia |
| **Filas** | Una fila por moneda + fila TOTAL |
| **Orientación PDF** | Horizontal (A4 Landscape) |

#### Cierres por Agencia (Section E2)

| Hoja Excel | `Cierres por Agencia` |
|------------|----------------------|
| **Columnas** | Agencia, [una columna por cada moneda presente dinámicamente] |
| **Filas** | Una fila por agencia + fila TOTAL |
| **Orientación PDF** | Horizontal (A4 Landscape) |

#### Resumen de Movimientos (Section E1)

| Hoja Excel | `Resumen Movimientos` |
|------------|----------------------|
| **Columnas** | Moneda, Apertura Caja, Cambio Moneda, Envio Giro, Pago Giro, Envio Remesa, Pago Remesa, Egresos, Transferencias, Fallas Caja, Cierre Caja |
| **Filas** | Una fila por moneda |
| **Orientación PDF** | Horizontal (A4 Landscape) |

---

### 3.6 Implementación técnica

| Componente | Tecnología | Ubicación |
|-----------|------------|-----------|
| Generación Excel | **ClosedXML** 0.104.2 | `Application/Export/ExcelExportService.cs` |
| Generación PDF | **QuestPDF** 2024.12.3 (Community License) | `Application/Export/PdfExportService.cs` |
| Logo corporativo | Recurso embebido PNG | `Application/Export/Img/logo.png` |
| Interfaces | `IExcelExportService`, `IPdfExportService` | `Application/Interfaces/` |
| Funciones HTTP | `ReconciliationDownloadFunction` | `Functions/Functions/` |

---

## 4. Módulo de Remesas (Bolivia ↔ Chile)

Conjunto de endpoints que cruzan la base de datos de Bolivia (`u752765750_cgr`), la base de Chile (`tmp_reporte_remesa`) y la base contable (`u752765750_conta.co_asientos`) para conciliar el corredor de remesas Bolivia ↔ Chile.

**Base URL:** `http://localhost:7071/api/reconciliation/remittances` *(ver ruta exacta por endpoint)*

**Requisitos de configuración:** `local.settings.json` debe incluir **las tres** connection strings:

```json
{
  "Values": {
    "SqlConnectionString":         "Server=...;Database=u752765750_cgr;...",
    "ChileConnectionString":       "Server=...;Database=<chile_db>;...",
    "AccountingConnectionString":  "Server=...;Database=u752765750_conta;..."
  }
}
```

Si falta `ChileConnectionString` o `AccountingConnectionString`, `Program.cs` se degrada a la configuración Bolivia-only y los endpoints de remesas no estarán registrados (retornarán 404).

### 4.1 Conceptos clave

| Concepto | Descripción |
|----------|-------------|
| **Código CGR (`codigo`)** | Identificador compartido entre Bolivia y Chile que permite cruzar registros entre ambas bases. En Chile, un `codigo < 0` implica una remesa **anulada**. |
| **Dirección** | `CHL->BOL` o `BOL->CHL`, derivada de `abr_ori` y `abr_des` en `tmp_reporte_remesa`. |
| **`id_operacion` válidos** | Bolivia `remesa`: `[4, 16]`; Bolivia `pago`: `[4, 7]`; Chile `tmp_reporte_remesa` / `pago`: `[3, 5, 6, 8]`. Registros con otros `id_operacion` se ignoran. |
| **Carry-forward** | La matriz general arrastra desde meses anteriores: (a) remesas aún pendientes en cualquier dirección y (b) el saldo acumulado histórico (pagos BOL→CHL − pagos CHL→BOL anteriores al mes). |
| **Ventana del reporte** | Los 3 endpoints reciben un `date` puntual pero operan sobre **el mes completo** que contiene esa fecha (`FechaDesde = 1er día del mes`, `FechaHasta = último día del mes`). |

**Estados de remesa**

| Base | Código | Label |
|------|--------|-------|
| Bolivia | 0 | PENDIENTE |
| Bolivia | 1 | PAGADO |
| Bolivia | 2 | DEVUELTO |
| Bolivia | 5 | ANULADO |
| Chile | 0 | PENDIENTE |
| Chile | 1 | PAGADO |
| Chile | 2 | DEVUELTO |
| Chile | 3 | DEVUELTO SC |
| Chile | 4 | CAMBIADO |
| Chile | 5 | ANULADO |
| Chile | 6 | A DEVOLVER |
| Chile | 7 | EN PROCESO |
| Chile | 8 | AHORRO |

**Patrones de validación contable (`co_asientos.concepto`)**

| Tipo (`AccountingValidationType`) | Concepto esperado | Importe esperado |
|-----------------------------------|-------------------|------------------|
| `BoliviaToChilePending` | `LIKE '%ENVIO DE%'` | `remesa.total_bob + remesa.itf_bob` |
| `BoliviaToChilePaid` | `LIKE 'PAGO EN DESTINO%'` | `pago.total_bob` |
| `ChileToBoliviaPaid` | Dos asientos: `%PENDIENTES DE COBRO DEL PAGO DE REMESA%` + `%PAGO DE REMESA CGR%` | Mismo importe en ambos asientos |

---

### 4.2 Operaciones Diarias

Resumen del día: enviadas, anuladas, pagadas y pendientes acumuladas por dirección y moneda.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **URL** | `/api/reconciliation/remittances/daily-operations` |
| **Función** | `GetRemittanceDailyOperations` |

**Parámetros:** `date` (yyyy-MM-dd, opcional).

**Request:**

```http
GET /api/reconciliation/remittances/daily-operations?date=2026-04-10 HTTP/1.1
```

**Response (200 OK):**

```json
{
  "fecha": "2026-04-10T00:00:00",
  "mes": 4,
  "anio": 2026,
  "fecha_procesamiento": "2026-04-10T23:35:12",
  "summary": {
    "total_sent_today": 12,
    "total_sent_amount_today": 18400.00,
    "total_cancelled_today": 1,
    "total_cancelled_amount_today": 450.00,
    "total_paid_today": 9,
    "total_paid_amount_today": 14200.00,
    "total_pending_bolivia": 5,
    "total_pending_amount_bolivia": 6500.00,
    "total_pending_chile": 3,
    "total_pending_amount_chile": 4100.00
  },
  "sent_today": [
    { "direction": "CHL->BOL", "moneda": "CLP", "count": 7, "total_amount": 980000.00, "status": "PENDIENTE" },
    { "direction": "BOL->CHL", "moneda": "BOB", "count": 5, "total_amount": 4200.00,   "status": "PENDIENTE" }
  ],
  "cancelled_today": [
    { "direction": "CHL->BOL", "moneda": "CLP", "count": 1, "total_amount": 450.00, "status": "ANULADO" }
  ],
  "paid_today": [
    { "direction": "CHL->BOL", "moneda": "BOB", "count": 6, "total_amount": 9800.00, "status": "PAGADO" },
    { "direction": "BOL->CHL", "moneda": "CLP", "count": 3, "total_amount": 4400.00, "status": "PAGADO" }
  ],
  "pending_in_bolivia": [
    { "moneda": "BOB", "count": 5, "total_amount": 6500.00 }
  ],
  "pending_in_chile": [
    { "moneda": "CLP", "count": 3, "total_amount": 4100.00 }
  ]
}
```

**Notas:**
- `sent_today` / `cancelled_today` se filtran por **fecha de creación** de la remesa (`fec_crea_remesa`).
- `paid_today` se filtra por **fecha del pago** (`pago.fec_crea_pago`), no por la fecha de creación de la remesa.
- `pending_in_bolivia` son remesas `CHL→BOL` con `estado = PENDIENTE` creadas hasta el día del reporte (las que esperan pago en Bolivia).
- `pending_in_chile` son remesas `BOL→CHL` pendientes de pago en Chile.

---

### 4.3 Validación Contable

Valida cada remesa del mes contra los asientos en `co_asientos`, verificando que el concepto y el importe coincidan con lo esperado.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **URL** | `/api/reconciliation/remittances/accounting-validation` |
| **Función** | `GetRemittanceAccountingValidation` |

**Parámetros:** `date` (yyyy-MM-dd). El endpoint valida **todo el mes** que contiene esa fecha.

**Request:**

```http
GET /api/reconciliation/remittances/accounting-validation?date=2026-04-10 HTTP/1.1
```

**Response (200 OK):**

```json
{
  "fecha": "2026-04-10T00:00:00",
  "mes": 4,
  "anio": 2026,
  "fecha_procesamiento": "2026-04-10T23:35:12",
  "accounting_validation": {
    "total_validated": 128,
    "total_valid": 124,
    "total_errors": 4,
    "entries": [
      {
        "codigo": 54210,
        "direction": "BOL->CHL",
        "remittance_status": "PENDIENTE",
        "expected_amount": 3486.40,
        "accounting_amount": 3486.40,
        "expected_concepto": "%ENVIO DE%",
        "actual_concepto": "ENVIO DE REMESA CGR 54210",
        "is_valid": true,
        "validation_type": "BoliviaToChilePending"
      },
      {
        "codigo": 54198,
        "direction": "BOL->CHL",
        "remittance_status": "PAGADO",
        "expected_amount": 1720.00,
        "accounting_amount": null,
        "expected_concepto": "PAGO EN DESTINO%",
        "actual_concepto": null,
        "is_valid": false,
        "error_message": "No se encontró asiento contable con concepto 'PAGO EN DESTINO%' para codigo 54198",
        "validation_type": "BoliviaToChilePaid"
      },
      {
        "codigo": 54199,
        "direction": "CHL->BOL",
        "remittance_status": "PAGADO",
        "expected_amount": 980.00,
        "accounting_amount": 985.00,
        "expected_concepto": "%PAGO DE REMESA CGR%",
        "actual_concepto": "PAGO DE REMESA CGR 54199",
        "is_valid": false,
        "error_message": "Importe del asiento (985.00) no coincide con el esperado (980.00)",
        "validation_type": "ChileToBoliviaPaid"
      }
    ]
  }
}
```

**`validation_type`:**
- `BoliviaToChilePending` — Remesa BOL→CHL pendiente. Espera asiento `ENVIO DE` con `importe = total_bob + itf_bob`.
- `BoliviaToChilePaid` — Remesa BOL→CHL pagada. Espera asiento `PAGO EN DESTINO` con `importe = pago.total_bob`.
- `ChileToBoliviaPaid` — Remesa CHL→BOL pagada. Espera **dos** asientos (`PENDIENTES DE COBRO` + `PAGO DE REMESA CGR`), ambos con el mismo importe.

---

### 4.4 Conciliación General

Entrega la matriz diaria del mes con carry-forward, la validación de pagos (field-level), las discrepancias entre bases y el balance final.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **URL** | `/api/reconciliation/remittances/general-reconciliation` |
| **Función** | `GetRemittanceGeneralReconciliation` |

**Parámetros:** `date` (yyyy-MM-dd). Opera sobre todo el mes hasta la fecha indicada.

**Request:**

```http
GET /api/reconciliation/remittances/general-reconciliation?date=2026-04-10 HTTP/1.1
```

**Response (200 OK):**

```json
{
  "fecha": "2026-04-10T00:00:00",
  "mes": 4,
  "anio": 2026,
  "fecha_procesamiento": "2026-04-10T23:35:12",
  "daily_matrix": {
    "carry_forward": {
      "chl_to_bol_pending_count": 4,
      "chl_to_bol_pending_amount": 5200.00,
      "bol_to_chl_pending_count": 6,
      "bol_to_chl_pending_amount": 8900.00,
      "saldo_acumulado": 12450.00
    },
    "daily_rows": [
      {
        "fecha": "2026-04-01T00:00:00",
        "chl_to_bol_recibidas":  { "count": 3, "total_amount": 420.00 },
        "chl_to_bol_anuladas":   { "count": 0, "total_amount": 0.00   },
        "chl_to_bol_pendientes": { "count": 7, "total_amount": 5620.00 },
        "chl_to_bol_pagadas":    { "count": 2, "total_amount": 300.00 },
        "bol_to_chl_enviadas":   { "count": 4, "total_amount": 3200.00 },
        "bol_to_chl_anuladas":   { "count": 0, "total_amount": 0.00   },
        "bol_to_chl_pendientes": { "count": 10, "total_amount": 12100.00 },
        "bol_to_chl_pagadas":    { "count": 1, "total_amount": 850.00 },
        "saldo_favor_contra": -550.00,
        "saldo_acumulado": 11900.00
      }
    ],
    "totals": {
      "chl_to_bol_recibidas": { "count": 38,  "total_amount": 5400.00 },
      "chl_to_bol_anuladas":  { "count": 2,   "total_amount": 210.00  },
      "chl_to_bol_pagadas":   { "count": 31,  "total_amount": 4800.00 },
      "bol_to_chl_enviadas":  { "count": 45,  "total_amount": 34200.00 },
      "bol_to_chl_anuladas":  { "count": 1,   "total_amount": 120.00   },
      "bol_to_chl_pagadas":   { "count": 36,  "total_amount": 28700.00 },
      "saldo_favor_contra_total": -23900.00
    }
  },
  "payment_validation": {
    "total_remittances": 128,
    "total_with_payment": 120,
    "total_missing": 4,
    "total_duplicates": 1,
    "total_field_errors": 3,
    "total_valid": 120,
    "entries": [
      {
        "direction": "BOL->CHL",
        "codigo": 54198,
        "moneda": "BOB",
        "remittance_envio": 1720.00,
        "remittance_estado_label": "PAGADO",
        "remittance_fecha": "2026-04-07T10:22:00",
        "pago_monto": 1720.00,
        "pago_tipo_cambio": 142.00,
        "pago_total": 244240.00,
        "pago_comision_bob": 5.00,
        "pago_fec_crea": "2026-04-08T11:00:00",
        "monto_match": true,
        "tipo_cambio_match": true,
        "total_match": true,
        "comision_match": true,
        "status": "Valid",
        "is_valid": true
      }
    ]
  },
  "discrepancies": {
    "in_bolivia_not_in_chile": [
      { "codigo": 54301, "source": "Bolivia", "direction": "BOL->CHL", "amount": 1500.00, "status": "PENDIENTE", "fecha": "2026-04-09T00:00:00" }
    ],
    "in_chile_not_in_bolivia": [],
    "missing_accounting_entries": [
      { "codigo": 54198, "source": "Bolivia", "direction": "BOL->CHL", "amount": 1720.00, "status": "PAGADO", "fecha": "2026-04-07T00:00:00" }
    ],
    "orphan_accounting_entries": [
      { "id_asiento": 98123, "fecha": "2026-04-05T00:00:00", "concepto": "ENVIO DE REMESA CGR 99999", "importe": 850.00 }
    ],
    "amount_mismatches": [
      { "codigo": 54199, "bolivia_amount": 980.00, "chile_amount": 985.00, "difference": 5.00 }
    ]
  },
  "balance": {
    "balance_positivo": 32100.00,
    "balance_negativo": 28700.00,
    "saldo_neto": 3400.00,
    "saldo_acumulado": [
      { "fecha": "2026-04-01T00:00:00", "chile_to_bolivia_amount": 300.00, "bolivia_to_chile_amount": 850.00, "net_daily": -550.00, "accumulated": 11900.00 }
    ]
  }
}
```

**Secciones:**

| Campo | Descripción |
|-------|-------------|
| `daily_matrix.carry_forward` | Pendientes y saldo acumulado arrastrado desde meses anteriores |
| `daily_matrix.daily_rows[]` | Una fila por día del mes hasta la fecha indicada. `pendientes` es un *snapshot* corrido (no un delta) |
| `daily_matrix.totals` | Suma del mes por dirección y estado (excluye carry-forward) |
| `payment_validation` | Cruce de cada remesa contra su registro en `pago`. `status`: `Valid` / `Missing` / `Duplicate` / `FieldMismatch` |
| `discrepancies` | 5 tipos de discrepancias cross-DB (ver DTO `DiscrepancyReport`) |
| `balance` | `BalancePositivo` (BOL a favor) − `BalanceNegativo` (CHL a favor) = `SaldoNeto` |

---

### 4.5 Endpoints de Descarga de Remesas

Cada endpoint JSON del módulo de remesas tiene un equivalente `/download` que acepta `format=xlsx|pdf`:

| Reporte | URL de descarga | Nombre del archivo |
|---------|----------------|--------------------|
| Operaciones Diarias | `GET /api/reconciliation/remittances/daily-operations/download` | `Report_RemittanceDailyOperations_YYYY-MM-DD.xlsx\|pdf` |
| Validación Contable | `GET /api/reconciliation/remittances/accounting-validation/download` | `Report_RemittanceAccountingValidation_YYYY-MM-DD.xlsx\|pdf` |
| Conciliación General | `GET /api/reconciliation/remittances/general-reconciliation/download` | `Report_RemittanceGeneralReconciliation_YYYY-MM-DD.xlsx\|pdf` |

**Ejemplo:**

```bash
curl -s "http://localhost:7071/api/reconciliation/remittances/general-reconciliation/download?date=2026-04-10&format=pdf" \
  -H "x-functions-key: tu-key-aqui" \
  -o RemittanceGeneralReconciliation.pdf
```

---

## 5. Casos de Uso

### Caso 1: Cuadre diario de cajas al cierre del día

**Descripción:** El supervisor revisa si todos los cajeros cuadran al final del día.

**Flujo:**

1. Cajeros realizan operaciones durante el día (cambios, giros, remesas, etc.)
2. Cada operación crea un registro en `saldo_caja` con su código de conciliación
3. Al cierre, cada cajero registra su saldo final (código `C`)
4. El supervisor consulta el cuadre:

```http
GET /api/reconciliation/cashiers?date=2026-01-15 HTTP/1.1
```

5. Verifica el campo `total_discrepancies` en la respuesta
6. Si `total_discrepancies > 0`, revisa cada cajero buscando `diferencia ≠ 0`
7. Cruza contra `fallas_registradas` para ver si la diferencia ya fue documentada

**Criterio de éxito:** `total_discrepancies = 0`

---

### Caso 2: Consulta de cuadre por fecha y agencia específica

**Descripción:** Auditoría necesita revisar el cuadre de la agencia Cochabamba del 10 de enero.

**Flujo:**

1. Obtener el cuadre de cajas filtrado:

```http
GET /api/reconciliation/cashiers?date=2026-01-10&agencia=5 HTTP/1.1
```

2. Obtener el cuadre de bóvedas de la misma agencia:

```http
GET /api/reconciliation/vaults?date=2026-01-10&agencia=5 HTTP/1.1
```

3. Cruzar los totales de cierre de cajas vs. entradas a bóveda (`entrada_cierre_caja`)
4. Verificar que el dinero que salió de cajas ingresó a la bóveda

---

### Caso 3: Revisión de diferencias (descuadres)

**Descripción:** El proceso automático detectó 2 descuadres. Se necesita identificar qué cajeros y en qué monedas.

**Flujo:**

1. Consultar cuadre completo:

```http
GET /api/reconciliation/cashiers?date=2026-01-15 HTTP/1.1
```

2. En la respuesta, buscar registros donde `diferencia ≠ 0`:

```json
{
  "cajero": "mllenes",
  "agencia": "AGENCIA ORURO CENTRAL",
  "currencies": [
    {
      "moneda": "BOB",
      "total_saldo": 45230.0000,
      "cierre_caja": 44870.0000,
      "diferencia": -360.0000
    }
  ],
  "fallas_registradas": { "27": -360.0000 }
}
```

3. Verificar si la diferencia está en `fallas_registradas`:
   - **Sí está** → La diferencia ya fue reconocida y documentada
   - **No está** → Es una diferencia nueva que requiere investigación

4. Consultar movimientos consolidados para contexto:

```http
GET /api/reconciliation/movements?date=2026-01-15 HTTP/1.1
```

---

### Caso 4: Consulta de disponibilidad de efectivo en bóvedas

**Descripción:** Tesorería necesita saber cuánto efectivo hay en total, expresado en BOB.

**Flujo:**

1. Consultar disponibilidad:

```http
GET /api/reconciliation/availability?date=2026-01-15 HTTP/1.1
```

2. El campo `total_saldo_bob` muestra el total consolidado en bolivianos
3. Cada entrada muestra el saldo por agencia y moneda con el tipo de cambio aplicado

---

### Caso 5: Consolidado diario en moneda local

**Descripción:** Contabilidad necesita el total del día en BOB para su cierre contable.

**Flujo:**

1. Consultar resumen diario:

```http
GET /api/reconciliation/summary?date=2026-01-15 HTTP/1.1
```

2. El campo `grand_total_bob` muestra el total consolidado
3. Cada moneda muestra `total_cajas`, `total_boveda` y la conversión a BOB
4. Los tipos de cambio se obtienen automáticamente de `cotizacion_oficial` (BCB)

---

### Caso 6: Descargar reportes para distribución

**Descripción:** El supervisor necesita enviar por correo el cuadre del día a gerencia en formato PDF, y archivar la versión Excel para análisis posterior.

**Flujo:**

1. Descargar el cuadre de cajas en PDF para envío:

```http
GET /api/reconciliation/cashiers/download?date=2026-01-15&format=pdf HTTP/1.1
```

2. Descargar la misma información en Excel para archivo:

```http
GET /api/reconciliation/cashiers/download?date=2026-01-15 HTTP/1.1
```

3. Descargar el resumen diario consolidado para contabilidad:

```http
GET /api/reconciliation/summary/download?date=2026-01-15&format=pdf HTTP/1.1
```

4. Los archivos descargados incluyen:
   - Logo y nombre de la empresa en el encabezado
   - Título del reporte y fecha/hora de generación
   - Datos faltantes visualmente identificados con sombreado
   - Filas alternadas para facilitar la lectura

---

### Caso 7: Descargar reporte de una agencia específica

**Descripción:** Auditoría solicita el cuadre de bóvedas de la agencia Cochabamba en formato Excel.

**Flujo:**

1. Descargar con filtro de agencia:

```http
GET /api/reconciliation/vaults/download?date=2026-01-10&agencia=5 HTTP/1.1
```

2. El archivo Excel contiene solo los datos de la agencia solicitada
3. El nombre del archivo incluye la fecha: `Report_VaultReconciliation_2026-01-10.xlsx`

---

### Caso 8: Cierre mensual del corredor de remesas Bolivia ↔ Chile

**Descripción:** El área de finanzas necesita cerrar el mes de abril del corredor Bolivia–Chile: validar que cada remesa tiene su asiento contable, identificar faltantes en una u otra base, y calcular el saldo neto.

**Flujo:**

1. Revisar las operaciones del último día del mes para confirmar el estado actual:

```http
GET /api/reconciliation/remittances/daily-operations?date=2026-04-30 HTTP/1.1
```

2. Obtener la conciliación general mensual con matriz diaria, discrepancias y balance:

```http
GET /api/reconciliation/remittances/general-reconciliation?date=2026-04-30 HTTP/1.1
```

3. Revisar los campos:
   - `daily_matrix.totals.saldo_favor_contra_total` → saldo neto del mes
   - `discrepancies.in_bolivia_not_in_chile` → remesas en Bolivia sin contraparte en Chile
   - `discrepancies.in_chile_not_in_bolivia` → remesas en Chile sin contraparte en Bolivia
   - `discrepancies.amount_mismatches` → diferencias de monto entre ambas bases
   - `payment_validation.total_missing` → pagos faltantes

4. Ejecutar la validación contable para cruzar contra `co_asientos`:

```http
GET /api/reconciliation/remittances/accounting-validation?date=2026-04-30 HTTP/1.1
```

5. Filtrar `accounting_validation.entries[]` por `is_valid == false` y escalar cada una a contabilidad.

6. Descargar el paquete de reportes para archivo:

```bash
curl -s "http://localhost:7071/api/reconciliation/remittances/general-reconciliation/download?date=2026-04-30&format=xlsx" -o General.xlsx
curl -s "http://localhost:7071/api/reconciliation/remittances/accounting-validation/download?date=2026-04-30&format=pdf"  -o AcctValidation.pdf
```

---

## 6. Guía de Uso

### Cómo consumir la API

**Headers requeridos:**

```http
Content-Type: application/json
x-functions-key: <tu-function-key>
```

**Formato de fecha:** Siempre `yyyy-MM-dd`. Si se omite, usa la fecha actual UTC.

**Ejemplo con curl:**

```bash
curl -s "http://localhost:7071/api/reconciliation/cashiers?date=2026-01-15" \
  -H "x-functions-key: tu-key-aqui" | jq .
```

**Ejemplo con C# HttpClient:**

```csharp
var client = new HttpClient { BaseAddress = new Uri("http://localhost:7071") };
var response = await client.GetAsync("/api/reconciliation/cashiers?date=2026-01-15");
var report = await response.Content.ReadFromJsonAsync<CashierReconciliationReport>();
```

### Orden recomendado de consulta

Para un cuadre completo del día, consumir en este orden:

```
1. /reconciliation/cashiers    → Detalle por cajero (¿hay descuadres?)
2. /reconciliation/vaults      → Movimientos de bóvedas
3. /reconciliation/availability → Efectivo disponible en BOB
4. /reconciliation/summary     → Totales consolidados cajas + bóvedas
5. /reconciliation/closings    → Matriz agencia × moneda
6. /reconciliation/movements   → Resumen de categorías de movimiento
```

Los endpoints 1 y 2 son los más importantes para el cuadre operativo diario. Los endpoints 3-6 son para reportería y consolidación.

Para el cierre mensual del corredor de remesas Bolivia ↔ Chile:

```
1. /reconciliation/remittances/daily-operations         → Snapshot diario
2. /reconciliation/remittances/general-reconciliation   → Matriz, discrepancias, balance
3. /reconciliation/remittances/accounting-validation    → Cruce con co_asientos
```

> **Tip:** Cada endpoint tiene un equivalente `/download` para obtener el reporte como archivo. Agregar `/download` a cualquier URL y opcionalmente `&format=pdf` para PDF.

### Buenas prácticas

1. **Consultar siempre con fecha explícita.** No depender del default (fecha actual), especialmente para cuadres de días anteriores.

2. **Verificar `total_discrepancies` primero.** Si es 0, todas las cajas cuadraron. No es necesario iterar cada cajero.

3. **Usar filtro de agencia** cuando solo se necesita una agencia. Reduce el volumen de datos y mejora el tiempo de respuesta.

4. **Los montos usan 4 decimales** (`decimal(14,4)` en la base de datos). No redondear en el cliente.

5. **Los tipos de cambio usan 6 decimales** (`decimal(16,6)`). Respetar la precisión al hacer cálculos adicionales.

### Manejo de errores

| Código HTTP | Significado | Acción |
|-------------|-------------|--------|
| `200 OK` | Consulta exitosa | Procesar respuesta |
| `401 Unauthorized` | Function key inválida o faltante | Verificar `x-functions-key` |
| `500 Internal Server Error` | Error de base de datos o lógica interna | Revisar logs de Application Insights |

Si la respuesta contiene un array vacío (`cashiers: []` o `agencies: []`), significa que **no hay datos para esa fecha**, no es un error. Posibles causas:
- La fecha es un día no laborable
- No se registraron transacciones ese día
- El ID de agencia no existe

---

## 7. Ejemplos Prácticos

### Escenario 1: Día sin diferencias (cuadre perfecto)

Tres cajeros en AGENCIA SUCRE CENTRAL, todos cuadran:

```http
GET /api/reconciliation/cashiers?date=2026-01-20&agencia=1 HTTP/1.1
```

```json
{
  "fecha": "2026-01-20T00:00:00",
  "cashiers": [
    {
      "cajero": "avallejos",
      "id_agencia": 1,
      "agencia": "AGENCIA SUCRE CENTRAL",
      "currencies": [
        {
          "id_caja": 12,
          "num_caja": 1,
          "moneda": "BOB",
          "tipo_cambio_oficial": 1.0,
          "apertura_caja": 15000.0000,
          "cambio_moneda_ingreso": { "count": 3, "amount": 12400.0000 },
          "envio_giro": { "count": 7, "amount": 45000.0000 },
          "envio_remesa": { "count": 0, "amount": 0.0000 },
          "traspaso_de_caja": { "count": 0, "amount": 0.0000 },
          "cobro_cheque": { "count": 0, "amount": 0.0000 },
          "ingreso_controlado": { "count": 0, "amount": 0.0000 },
          "transferencia_boveda_ingreso": { "count": 1, "amount": 50000.0000 },
          "cambio_moneda_egreso": { "count": 2, "amount": 8500.0000 },
          "pago_giro": { "count": 3, "amount": 28000.0000 },
          "devolucion_giro": 0.0000,
          "pago_remesa": { "count": 0, "amount": 0.0000 },
          "devolucion_remesa": 0.0000,
          "traspaso_a_caja": { "count": 0, "amount": 0.0000 },
          "deposito_bancario": { "count": 1, "amount": 70000.0000 },
          "egreso_controlado": { "count": 0, "amount": 0.0000 },
          "transferencia_boveda_egreso": { "count": 0, "amount": 0.0000 },
          "total_ingreso": 122400.0000,
          "total_egreso": 106500.0000,
          "total_saldo": 15900.0000,
          "cierre_caja": 15900.0000,
          "diferencia": 0.0000
        }
      ],
      "fallas_registradas": {}
    },
    {
      "cajero": "jllanos",
      "id_agencia": 1,
      "agencia": "AGENCIA SUCRE CENTRAL",
      "currencies": [
        {
          "id_caja": 14,
          "num_caja": 3,
          "moneda": "BOB",
          "tipo_cambio_oficial": 1.0,
          "apertura_caja": 10000.0000,
          "cambio_moneda_ingreso": { "count": 5, "amount": 22800.0000 },
          "envio_giro": { "count": 4, "amount": 31000.0000 },
          "envio_remesa": { "count": 2, "amount": 18000.0000 },
          "traspaso_de_caja": { "count": 0, "amount": 0.0000 },
          "cobro_cheque": { "count": 1, "amount": 5000.0000 },
          "ingreso_controlado": { "count": 0, "amount": 0.0000 },
          "transferencia_boveda_ingreso": { "count": 1, "amount": 40000.0000 },
          "cambio_moneda_egreso": { "count": 4, "amount": 19200.0000 },
          "pago_giro": { "count": 2, "amount": 15000.0000 },
          "devolucion_giro": 2000.0000,
          "pago_remesa": { "count": 1, "amount": 8500.0000 },
          "devolucion_remesa": 0.0000,
          "traspaso_a_caja": { "count": 0, "amount": 0.0000 },
          "deposito_bancario": { "count": 1, "amount": 65000.0000 },
          "egreso_controlado": { "count": 0, "amount": 0.0000 },
          "transferencia_boveda_egreso": { "count": 0, "amount": 0.0000 },
          "total_ingreso": 126800.0000,
          "total_egreso": 109700.0000,
          "total_saldo": 17100.0000,
          "cierre_caja": 17100.0000,
          "diferencia": 0.0000
        }
      ],
      "fallas_registradas": {}
    }
  ],
  "total_discrepancies": 0
}
```

**Verificación rápida:** `total_discrepancies = 0` → Todas las cajas cuadran.

---

### Escenario 2: Día con descuadre

El cajero `mllenes` tiene una diferencia de -360 BOB (le falta dinero):

```json
{
  "fecha": "2026-01-15T00:00:00",
  "cashiers": [
    {
      "cajero": "mllenes",
      "id_agencia": 2,
      "agencia": "AGENCIA ORURO CENTRAL",
      "currencies": [
        {
          "id_caja": 27,
          "num_caja": 1,
          "moneda": "BOB",
          "tipo_cambio_oficial": 1.0,
          "apertura_caja": 12000.0000,
          "cambio_moneda_ingreso": { "count": 6, "amount": 18500.0000 },
          "envio_giro": { "count": 9, "amount": 52000.0000 },
          "envio_remesa": { "count": 3, "amount": 21000.0000 },
          "traspaso_de_caja": { "count": 0, "amount": 0.0000 },
          "cobro_cheque": { "count": 0, "amount": 0.0000 },
          "ingreso_controlado": { "count": 0, "amount": 0.0000 },
          "transferencia_boveda_ingreso": { "count": 1, "amount": 30000.0000 },
          "cambio_moneda_egreso": { "count": 5, "amount": 15800.0000 },
          "pago_giro": { "count": 4, "amount": 22000.0000 },
          "devolucion_giro": 3500.0000,
          "pago_remesa": { "count": 2, "amount": 14000.0000 },
          "devolucion_remesa": 0.0000,
          "traspaso_a_caja": { "count": 0, "amount": 0.0000 },
          "deposito_bancario": { "count": 1, "amount": 30000.0000 },
          "egreso_controlado": { "count": 1, "amount": 2970.0000 },
          "transferencia_boveda_egreso": { "count": 0, "amount": 0.0000 },
          "total_ingreso": 133500.0000,
          "total_egreso": 88270.0000,
          "total_saldo": 45230.0000,
          "cierre_caja": 44870.0000,
          "diferencia": -360.0000
        }
      ],
      "fallas_registradas": {
        "27": -360.0000
      }
    }
  ],
  "total_discrepancies": 1
}
```

**Análisis:**

1. `diferencia = -360.0000` → El cajero cerró con 360 BOB menos de lo calculado
2. `fallas_registradas: { "27": -360.0000 }` → La diferencia ya fue registrada en `falla_caja`
3. Acción: Verificar si la falla está en estado `P` (pendiente), `R` (rectificado) o `C` (cancelado)

---

### Escenario 3: Cuadre con múltiples monedas

Un cajero maneja BOB y USD simultáneamente (tiene dos cajas, una por moneda):

```json
{
  "cajero": "gquisberth",
  "id_agencia": 7,
  "agencia": "AGENCIA SANTA CRUZ CENTRAL",
  "currencies": [
    {
      "id_caja": 45,
      "num_caja": 1,
      "moneda": "BOB",
      "tipo_cambio_oficial": 1.0,
      "apertura_caja": 20000.0000,
      "cambio_moneda_ingreso": { "count": 15, "amount": 85600.0000 },
      "envio_giro": { "count": 8, "amount": 42000.0000 },
      "envio_remesa": { "count": 0, "amount": 0.0000 },
      "traspaso_de_caja": { "count": 0, "amount": 0.0000 },
      "cobro_cheque": { "count": 0, "amount": 0.0000 },
      "ingreso_controlado": { "count": 0, "amount": 0.0000 },
      "transferencia_boveda_ingreso": { "count": 2, "amount": 100000.0000 },
      "cambio_moneda_egreso": { "count": 12, "amount": 72000.0000 },
      "pago_giro": { "count": 5, "amount": 35000.0000 },
      "devolucion_giro": 0.0000,
      "pago_remesa": { "count": 0, "amount": 0.0000 },
      "devolucion_remesa": 0.0000,
      "traspaso_a_caja": { "count": 0, "amount": 0.0000 },
      "deposito_bancario": { "count": 1, "amount": 100000.0000 },
      "egreso_controlado": { "count": 0, "amount": 0.0000 },
      "transferencia_boveda_egreso": { "count": 1, "amount": 20000.0000 },
      "total_ingreso": 247600.0000,
      "total_egreso": 227000.0000,
      "total_saldo": 20600.0000,
      "cierre_caja": 20600.0000,
      "diferencia": 0.0000
    },
    {
      "id_caja": 46,
      "num_caja": 2,
      "moneda": "USD",
      "tipo_cambio_oficial": 6.860000,
      "apertura_caja": 3000.0000,
      "cambio_moneda_ingreso": { "count": 12, "amount": 10500.0000 },
      "envio_giro": { "count": 0, "amount": 0.0000 },
      "envio_remesa": { "count": 4, "amount": 6200.0000 },
      "traspaso_de_caja": { "count": 0, "amount": 0.0000 },
      "cobro_cheque": { "count": 0, "amount": 0.0000 },
      "ingreso_controlado": { "count": 0, "amount": 0.0000 },
      "transferencia_boveda_ingreso": { "count": 1, "amount": 10000.0000 },
      "cambio_moneda_egreso": { "count": 10, "amount": 8900.0000 },
      "pago_giro": { "count": 0, "amount": 0.0000 },
      "devolucion_giro": 0.0000,
      "pago_remesa": { "count": 2, "amount": 3800.0000 },
      "devolucion_remesa": 0.0000,
      "traspaso_a_caja": { "count": 0, "amount": 0.0000 },
      "deposito_bancario": { "count": 1, "amount": 12000.0000 },
      "egreso_controlado": { "count": 0, "amount": 0.0000 },
      "transferencia_boveda_egreso": { "count": 0, "amount": 0.0000 },
      "total_ingreso": 29700.0000,
      "total_egreso": 24700.0000,
      "total_saldo": 5000.0000,
      "cierre_caja": 5000.0000,
      "diferencia": 0.0000
    }
  ],
  "fallas_registradas": {}
}
```

**Observaciones:**
- Cada moneda se cuadra independientemente
- El `tipo_cambio_oficial` para BOB es siempre `1.0` (moneda base)
- La caja 45 (BOB) y caja 46 (USD) pertenecen al mismo cajero `gquisberth`
- Ambas monedas cuadran: `diferencia = 0` en ambos casos

---

## 8. Consideraciones Técnicas

### Idempotencia

- **El TimerTrigger es idempotente.** Ejecuta solo consultas de lectura; no inserta ni modifica datos. Puede re-ejecutarse múltiples veces para el mismo día sin efectos secundarios.
- **Los endpoints HTTP son idempotentes.** Son todos `GET` (lectura pura).
- **Existe `HasClosingRecordAsync`** en el repositorio para verificar si un cierre ya fue registrado antes de intentar crear uno (preparado para futuras funcionalidades de escritura).

### Manejo de fechas

- **Parámetro de entrada:** `yyyy-MM-dd` (string).
- **Filtro SQL:** `DATE(sc.fec_crea_saldo) = @Fecha`. Esto filtra por día calendario, ignorando la hora.
- **Zona horaria:** El TimerTrigger ejecuta a las 23:30 UTC. Para Bolivia (UTC-4), esto equivale a las 19:30 hora local. Si la operación cierra más tarde, ajustar el cron en `host.json`.
- **Consulta histórica:** Se puede consultar cualquier fecha pasada. No hay restricción de rango.

### Performance y consultas SQL

**Optimizaciones implementadas:**

1. **CTE para tipos de cambio** — Se calcula una sola vez (`LatestRate`) y se reutiliza via `LEFT JOIN`. Evita subconsultas correlacionadas por fila.

2. **Consultas paralelas** — El TimerTrigger y el `CashierService` ejecutan múltiples queries en paralelo con `Task.WhenAll`:
   - Reconciliación + fallas de caja se cargan simultáneamente
   - Totales de caja + totales de bóveda se cargan simultáneamente
   - El timer lanza las 6 secciones del reporte en paralelo

3. **Filtro por agencia** — El parámetro `@IdAgencia` usa `AND (@IdAgencia IS NULL OR a.id_agencia = @IdAgencia)`, permitiendo que MySQL optimice cuando se filtra por una agencia.

4. **Módulo de remesas — fetch único del mes.** `RemittanceService.FetchMonthDataAsync` carga, en dos fases paralelas:
   - Fase 1: remesas Bolivia + remesas Chile + asientos contables (3 tareas en paralelo)
   - Fase 2: pagos Bolivia + pagos Chile (2 tareas en paralelo)

   Los tres endpoints de remesas comparten el mismo `RemittanceDataContext` dentro de la misma invocación. Los pagos se consultan sin restricción de fecha para permitir clasificar correctamente como pendientes las remesas que se pagaron después del corte.

**Índices recomendados para producción:**

```sql
-- Críticos para rendimiento del cuadre diario
CREATE INDEX idx_saldo_caja_fecha ON saldo_caja (fec_crea_saldo, id_caja);
CREATE INDEX idx_saldo_boveda_fecha ON saldo_boveda (fec_crea_saldo, id_boveda);
CREATE INDEX idx_falla_caja_fecha ON falla_caja (fecha, id_caja);

-- Para el CTE de tipo de cambio
CREATE INDEX idx_cotizacion_oficial_lookup
    ON cotizacion_oficial (id_operacion, id_moneda, fec_crea_cotizacion_oficial);
```

> **Advertencia:** `DATE(sc.fec_crea_saldo)` en el `WHERE` impide el uso de índices sobre `fec_crea_saldo`. Para mejorar el rendimiento en tablas grandes, considerar cambiar a un rango: `sc.fec_crea_saldo >= @FechaInicio AND sc.fec_crea_saldo < @FechaFin`.

### Limitaciones actuales

| Limitación | Detalle |
|-----------|---------|
| **Campo `contabilidad`** | El valor de contabilidad (CONTAB en el Excel) proviene de un sistema externo no conectado. El campo siempre retorna `null`. |
| **Sección A (log de transacciones)** | El log detallado transacción por transacción no está implementado como endpoint. Solo se implementaron las secciones B, C, D, E1 y E2. |
| **Escritura** | El sistema es de solo lectura. No crea registros de cierre ni de fallas. Esos registros los crea el sistema operativo (legacy). |
| **Códigos de conciliación** | El diccionario de datos solo documenta 12 de los 25+ códigos de `saldo_caja`. Los restantes fueron reconstruidos por ingeniería inversa. Ver `ConciliacionCodes.cs`. |
| **`DATE()` en WHERE** | La función `DATE()` sobre `fec_crea_saldo` impide el uso de índices. Mitigar con índices funcionales (MySQL 8+) o filtro por rango. |
| **13 monedas** | El sistema soporta las 13 monedas definidas (BOB, USD, EUR, PEN, BRL, CLP, ARS, GBP, CHF, CAD, AUD, MXN, COP), pero muchas tienen saldo 0 en la mayoría de agencias. |
| **QuestPDF Community** | La generación de PDF usa la licencia Community de QuestPDF, que es gratuita para ingresos anuales < $1M USD. Si la empresa supera ese umbral, se requiere licencia Professional. |
| **Remesas: configuración opcional** | El módulo de remesas requiere `ChileConnectionString` **y** `AccountingConnectionString` en `local.settings.json`. Si alguna falta, `Program.cs` cae al modo Bolivia-only y los endpoints de remesas **no** quedan registrados (404). |
| **Remesas: sólo lectura** | Al igual que el resto del sistema, los endpoints de remesas son de sólo lectura. La corrección de discrepancias detectadas se hace en los sistemas operativos (legacy). |
| **Remesas: ventana mensual fija** | El parámetro `date` de los 3 endpoints es puntual, pero todos operan sobre el mes completo que contiene esa fecha. No hay filtro por rango arbitrario. |
| **Remesas: `codigo` compartido** | El cruce Bolivia↔Chile depende de que el `codigo` CGR esté presente en ambas bases. Registros huérfanos (sin contraparte) aparecerán en `discrepancies.in_bolivia_not_in_chile` / `in_chile_not_in_bolivia`. |
| **Remesas: anulaciones en Chile** | Las filas de `tmp_reporte_remesa` con `codigo < 0` son tratadas como **ANULADO** (estado 5) en memoria por `RemittanceService`, independientemente del valor real de `estado` en la tabla. |
