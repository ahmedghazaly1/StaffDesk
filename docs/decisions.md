# Decisions — Phase 3 Part A

## Stack

StaffDesk remains on .NET 8 / EF Core / PostgreSQL rather than the Node/Prisma stack named in Phase 1–2 SRS. Mentor accepted the stack change for earlier phases; Part A follows the same stack.

## Audit export storage

Export files are written under `{AppContext.BaseDirectory}/exports/` as JSON and served via `GET /v1/audit/exports/{id}/download`. This keeps AU-17 asynchronous (job runner produces the file) without introducing object storage.

## Audit purge vs hash chain

Purging expired audit events (DG-2) removes old rows. Chain verification (AU-11) is defined over a date range of *retained* events. Deleting history older than retention is intentional; `PURGE_EXECUTED` events are never purged.

## Chain concurrency (AU-13)

PostgreSQL `pg_advisory_xact_lock(87201401)` serialises hash-chain appends. Non-Npgsql providers (tests) skip the advisory lock and rely on a transaction alone.

## Subject-access downloads

Subject-access packs reuse the `DataExport` table but Auditors cannot read non-`AUDIT` export rows (AU-15).

## Part E — Performance Management

### Exact §8.10 endpoints only

Part E exposes only the nineteen routes listed in SRS §8.10. There is no list-cycles, decide-appeal, or Admin elevated-grant API. Peer manager approval is handled on `POST /reviews/:id/peer-invitations` via `approveInvitationIds`. Appeal outcomes remain nullable until a future endpoint is specified.

### ADMIN denied by default (PM-38)

The technical `Admin` role cannot read or write performance reviews/goals/feedback. HR_ADMIN owns cycle administration. The competency library (`GET /competencies`) is reference data and remains readable to authenticated callers.

### Employee.JoinedAt

Added for PM-10 cycle eligibility. Existing rows default to `2020-01-01` so they remain eligible unless a later cut-off is set.

## Part G — Observability

Worker liveness is a singleton `WorkerHeartbeats` row, not a separate health process. Metrics are in-process Prometheus text, not a sidecar.

## Part H — Sessions

Refresh tokens rotate **in place** on the same session id so a still-valid access token is not invalidated by a routine refresh. Presenting the previous refresh hash is treated as reuse and revokes the family.

## NFR-23

Rate limits moved from `Program.cs` constants to `RateLimits` in `appsettings.json`. Remaining named constant: analytics suppression threshold (`InsufficientData.DefaultThreshold`).

## Webhook dispatch

`WEBHOOK_DISPATCH` is handled in `JobService` via `IWebhookDispatchService`. Consecutive HTTP failures disable the subscription.

## SLA remaining time

`GetWorkingMinutes(from, to)` is zero when `from >= to`. Breach uses signed remaining: minutes until target minus minutes overdue.

