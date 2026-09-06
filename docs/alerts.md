# Alert thresholds (OB-4)

StaffDesk operational alerts are derived from `Operations` settings in `appsettings.json` and from live checks. Active breaches: `GET /v1/operations/alerts` (Admin) and the Operations tab.

Every configured alert has a runbook entry in [runbook.md](./runbook.md). Alerts without a runbook must not be added.

| Alert ID | Condition | Threshold | Severity | Runbook |
|----------|-----------|-----------|----------|---------|
| `readiness.*` | Any readiness check not PASS | none (fail = alert) | critical | [readiness-failure](./runbook.md#readiness-failure) |
| `jobs.dead` | Jobs in `DEAD` state | ≥ 1 | warning | [dead-jobs](./runbook.md#dead-jobs) |
| `jobs.queue_age` | Oldest queued job age | > 300 s | warning | [queue-backlog](./runbook.md#queue-backlog) |
| `rollups.stale` | Hours since latest metric rollup | > 36 h | warning | [rollup-stale](./runbook.md#rollup-stale) |
| `http.error_rate` | HTTP 5xx rate (min 100 requests) | > 1.0 % | warning | [http-errors](./runbook.md#http-errors) |
| `audit.chain_broken` | Hash-chain verification fails | **none — immediate, unconditional** | critical | [audit-chain-failure](./runbook.md#audit-chain-failure) |

`audit.chain_broken` has no numeric bound. Any invalid chain, or any exception while verifying, raises the alert.

## Configuration

```json
"Operations": {
  "WorkerHeartbeatMaxAgeSeconds": 90,
  "JobQueueOldestAgeWarnSeconds": 300,
  "RollupStalenessWarnHours": 36,
  "HttpErrorRateWarnPercent": 1.0,
  "DeadJobsWarnCount": 1
}
```
