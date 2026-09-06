# NFR-16 / NFR-17 recorded measurements

Recorded: 3 September 2026, Windows, local PostgreSQL `StaffDesk`, API `http://localhost:5139` with Worker running (readiness includes heartbeat).

## Dataset

Not a full OB-10 `seed-scale` volume. Rebuild wrote **1476** daily snapshots for `2026-01-01` … `2026-09-03`. Re-run after `dotnet run --project StaffDesk.Worker -- seed-scale` for volume numbers.

## NFR-17 — rollup duration (budget 10 minutes)

| Run | Range | Snapshots | Wall clock | Budget |
|-----|-------|-----------|------------|--------|
| `dotnet run --project StaffDesk.Worker -- rebuild-metrics 2026-01-01 2026-09-03` | 2026-01-01 … 2026-09-03 | 1476 | **19.2 s** | 10 min — pass |

Includes host startup. Rebuild is idempotent (AN-5).

## NFR-16 — analytics / health p95 (budget 500 ms)

Method: 40 sequential GETs after `admin` / `admin123` login. Client-side wall clock. Analytics queries include `departmentId` (required).

| Endpoint | n | p50 (ms) | p95 (ms) | Budget |
|----------|---|----------|----------|--------|
| `GET /v1/analytics/flow` | 40 | 31.4 | **40.4** | 500 — pass |
| `GET /v1/analytics/throughput` | 40 | 22.5 | **27.9** | 500 — pass |
| `GET /v1/analytics/wip` | 40 | 23.9 | **25.3** | 500 — pass |
| `GET /v1/health/live` | 40 | 20.5 | **22.5** | 500 — pass |
| `GET /v1/health/ready` | 40 | 78 | **191.6** | 500 — pass |

Re-run:

```powershell
$env:STAFFDESK_JWT_SECRET = "LocalDev-StaffDesk-Signing-Secret-32ch"
powershell -File .\scripts\measure-nfr.ps1
```
