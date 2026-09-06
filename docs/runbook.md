# Operations runbook (OB-6)

Each configured alert has an entry below with: what it means, how to confirm, immediate mitigation, and escalation. Do not add an alert in `docs/alerts.md` without a matching section here.

---

## Readiness failure {#readiness-failure}

**Alert IDs:** `readiness.database`, `readiness.migrations`, `readiness.worker`

**What it means:** `GET /v1/health/ready` is 503. The process is up (liveness may still pass) but it must not receive traffic: PostgreSQL unreachable, pending EF migrations, or the job worker heartbeat is missing/stale.

**How to confirm:**
1. `GET /v1/health/live` vs `GET /v1/health/ready`.
2. Admin Operations tab → Readiness table, or `GET /v1/operations/summary`.
3. Database: `psql` or `CanConnect`. Worker: `StaffDesk.Worker` process running; `WorkerHeartbeats` row `LastSeenAt`.

**Immediate mitigation:**
- `database`: restore PostgreSQL; check `ConnectionStrings:DefaultConnection`.
- `migrations`: `dotnet ef database update --project StaffDesk.Infrastructure --startup-project StaffDesk.API` (API also auto-migrates on start).
- `worker`: start `dotnet run --project StaffDesk.Worker`; wait one poll (~10s) for heartbeat.

**Escalation:** If not restored within 15 minutes, page the on-call backend owner. Do not route production traffic to an unready instance.

---

## Dead jobs {#dead-jobs}

**Alert ID:** `jobs.dead`

**What it means:** One or more jobs exhausted retries and are in `DEAD`. Work (SLA, rollup, bulk, purge) will not complete until they are fixed and requeued.

**How to confirm:** Operations tab → Jobs (Dead), or `GET /v1/jobs?state=DEAD`. Read `lastError`.

**Immediate mitigation:** Fix the root cause in `lastError`. Admin: Requeue (`POST /v1/jobs/{id}/requeue`) or the Requeue button. Confirm `SUCCEEDED` or a new error.

**Escalation:** If the same job type dies twice after requeue, treat as a code defect; stop requeue loops and escalate to the module owner for that job type.

---

## Queue backlog {#queue-backlog}

**Alert ID:** `jobs.queue_age`

**What it means:** A queued job is older than 300 seconds. The worker is down, slow, or overloaded.

**How to confirm:** Operations summary `jobs.oldestQueuedAgeSeconds`; Prometheus `staffdesk_job_queue_oldest_age_seconds`; worker heartbeat freshness.

**Immediate mitigation:** Confirm worker is running. Inspect RUNNING jobs. Run additional worker processes if the queue keeps growing. Pause non-critical enqueues if the backlog is extreme.

**Escalation:** If oldest age is still over the bound after 30 minutes with the worker healthy, escalate to platform/ops for capacity.

---

## Rollup stale {#rollup-stale}

**Alert ID:** `rollups.stale`

**What it means:** Analytics snapshots are older than 36 hours. Dashboards may be wrong or empty.

**How to confirm:** Operations → Metric rollups `latestComputedAt` / `isStale`. Check DEAD `METRIC_ROLLUP` jobs.

**Immediate mitigation:** Confirm worker heartbeat. Rebuild: `dotnet run --project StaffDesk.Worker -- rebuild-metrics yyyy-MM-dd yyyy-MM-dd`. Requeue dead rollup jobs per [dead-jobs](#dead-jobs).

**Escalation:** If rebuild fails or analytics stay stale after a successful rebuild, escalate to the analytics owner (Part D).

---

## HTTP errors {#http-errors}

**Alert ID:** `http.error_rate`

**What it means:** More than 1% of requests on this API instance returned HTTP 5xx (after at least 100 requests).

**How to confirm:** Operations HTTP panel; JSON logs with `"LogLevel": "Error"` and `RequestId`; `/v1/health/ready`.

**Immediate mitigation:** Correlate `RequestId` in logs. Check readiness and recent deploys. Roll back the last release if the error spike started with it.

**Escalation:** If 5xx stay above 1% for 15 minutes after mitigation, escalate to the on-call backend owner with sample RequestIds.

---

## Audit chain failure {#audit-chain-failure}

**Alert ID:** `audit.chain_broken`

**What it means:** The audit hash chain does not verify (or verification threw). Events may have been altered, truncated, or written out of order. This is an immediate, unconditional integrity alert — there is no “acceptable break count.”

**How to confirm:**
1. Operations summary `auditChain.isValid` / `firstBreakIndex`.
2. CLI: `dotnet run --project StaffDesk.Worker -- verify-audit`
3. Optional range: `verify-audit <fromIso> <toIso>`

**Immediate mitigation:**
1. Stop purge/erasure jobs that touch `AuditEvents`.
2. Capture a database backup immediately ([backup-restore.md](./backup-restore.md)).
3. Do not rewrite hashes to “make verify pass.”
4. Compare the break id to the previous backup.

**Escalation:** Immediate. Treat as a security incident. Notify the engineering mentor / security owner before any repair write. Legal hold may already apply (Part A).
