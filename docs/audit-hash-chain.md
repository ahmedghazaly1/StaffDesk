# Phase 3 Part A — Audit hash chain (AU-10..AU-14)

## Canonical serialisation (AU-12)

Each event’s hash is SHA-256 over a camelCase JSON object with fixed field order:

`occurredAt`, `eventType`, `actorId`, `actorLabel`, `onBehalfOfId`, `targetType`, `targetId`, `outcome`, `changesJson`, `requestId`, `sourceIp`, `userAgent`, `prevHash`

`Id` is excluded because it is assigned by the database after hash computation.

## Chain writes (AU-13)

`AuditRepository.CreateAsync` takes a PostgreSQL transaction-scoped advisory lock (`pg_advisory_xact_lock(87201401)`) before reading the latest hash and inserting the next event, so concurrent writers cannot share a predecessor.

## Verification (AU-11)

- Admin API: `POST /v1/audit/verify` with `{ "fromDate", "toDate" }`
- CLI: `dotnet run --project StaffDesk.Worker -- verify-audit [fromIso] [toIso]`

## Limitation

A hash chain in the same database detects silent edit of history; it does not stop an administrator who rewrites the entire chain. Periodic external anchors would be required for stronger guarantees.
