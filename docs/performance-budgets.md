# Performance budgets and load test (OB-9)

## Budgets (p50 latency, local dev baseline)

| Endpoint class | Budget (p50) | Notes |
|----------------|--------------|-------|
| Read (list tasks, employees) | ≤ 200 ms | Paginated, indexed queries |
| Read (single resource) | ≤ 100 ms | By id |
| Write (create/update task) | ≤ 300 ms | Excludes bulk jobs |
| Analytics summary | ≤ 500 ms | Uses precomputed rollups |
| Health ready | ≤ 500 ms | Includes DB + migration check |

Budgets are **SHOULD** targets for regression detection, not hard SLA guarantees.

## Load test script

Run against a running API (default `http://localhost:5139`):

```powershell
cd scripts
./load-test.ps1 -BaseUrl http://localhost:5139 -DurationSeconds 30 -Concurrency 10
```

The script hammers anonymous health/metrics probes and authenticated read endpoints (requires a valid JWT via login). It prints approximate requests/sec and error counts.

After OB-10 scale seed, re-run to validate list performance at volume:

```powershell
dotnet run --project StaffDesk.Worker -- seed-scale
./load-test.ps1 -BaseUrl http://localhost:5139 -DurationSeconds 60 -Concurrency 20
```

Investigate regressions with structured logs (`RequestId=`) and Prometheus latency summaries.

Recorded analytics/rollup timings: `docs/nfr-measurements.md`. Re-run `scripts/measure-nfr.ps1` after OB-10 seed.

