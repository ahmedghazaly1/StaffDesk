# StaffDesk Changelog

All notable changes are documented here, newest first.
Format: **[version/date]** — what a user can now do · what changed for existing behaviour · what an operator must do to deploy.

---

## [Phase 3 acceptance gap-close] — 2026-09-03

SLA overdue tasks are `BREACHED` (signed remaining minutes). Webhook jobs run as `WEBHOOK_DISPATCH` with retry-then-disable. Audit covers SLA/delegation mutations and GET 403/hidden 404. Legal hold also skips notification and session purge. Threat models: `docs/threat-model-part-a.md`, `docs/threat-model-part-e.md`. Tests: `docs/testing.md`.

---

## [Phase 3 NFR / acceptance] — 2026-09-03

### New capabilities
- Rate limits, auth lockout, and operations thresholds are configuration (`docs/configuration.md`).
- Jobs and sessions lists are page/limit bounded (`data`, `page`, `limit`, `totalCount`).
- Swagger documents 401/429/412/428, `If-Match`, `Idempotency-Key`, and rate-limit headers (NFR-21).
- Canonical metric definitions: `docs/metrics-definitions.md`. Recorded analytics/rollup timings: `docs/nfr-measurements.md`.

### Operator
1. Set `STAFFDESK_JWT_SECRET`. Apply migrations including `AddAuthSessionsAndLockout`.
2. Optional: `scripts/measure-nfr.ps1` against a running API; after OB-10 seed, append results to `docs/nfr-measurements.md`.

---

## [Phase 3 Part H] — 2026-09-03 — Security & Privacy

### New capabilities
- Access + rotating refresh tokens; reuse kills the session family (SP-1).
- List/revoke sessions; Admin bulk-revoke; revoke on deactivate/password/role change (SP-2/SP-3).
- Login lockout per account and IP (SP-5). Field redaction filter (SP-7). Hidden records return 404 (SP-8).
- **Security** tab in the UI: password change and session list.

### Operator
Signing secret is environment-only. Existing JWTs without `sid` are rejected — users must log in again.

---

## [Phase 3 Part G] — 2026-09-03 — Observability & Operations

Health live/ready, Prometheus `/v1/metrics`, Operations admin UI, runbook, restore demonstration, scale seed CLI (`seed-scale`).

---

## [Phase 3 Part F] — 2026-09-03 — Platform & API Maturity

### New capabilities
- **Idempotency keys (PL-1–3):** Every creating `POST` accepts an `Idempotency-Key` header. Replaying the same key returns the stored response without re-executing. A key reused with a different body returns `422`.
- **Optimistic concurrency (PL-4–7):** `GET` responses for Tasks, Employees, Reviews, and Goals now carry an `ETag` header. `PUT`/`PATCH` on those resources require `If-Match`; a stale write returns `412` with the current ETag so clients can re-fetch and retry.
- **Rate limiting (PL-9–10):** All endpoints are rate-limited per authenticated principal (falls back to IP for unauthenticated requests). Limits differ by endpoint class — auth (10/min), export (5/min), write (100/min), read (300/min). Rejected requests receive `429` with `Retry-After`.
- **API keys (PL-11–12):** Service accounts can now authenticate with a scoped, revocable API key (`POST /v1/api-keys`). The raw key is shown exactly once at creation; only the SHA-256 hash is stored. Creation, first use, and revocation are audited.
- **Webhooks (PL-13–14):** Outbound webhook subscriptions (`POST /v1/webhooks`) deliver signed event payloads via the job runner with exponential backoff. Each delivery is recorded and individually replayable by Admin (`POST /v1/webhooks/deliveries/{id}/replay`). Subscriptions are automatically disabled after a configurable run of consecutive failures.
- **Deprecation headers (PL-15):** Any endpoint registered in `DeprecationMiddleware.Registry` now returns `Deprecation` and `Sunset` response headers (RFC 8594). No endpoints are currently deprecated; the registry is ready for future use.

### Changed behaviour
- `POST` requests that include an `Idempotency-Key` are now intercepted before the audit middleware, so a replayed response does not produce a second audit event.

### Operator deployment steps
1. Run `dotnet ef database update --project StaffDesk.Infrastructure --startup-project StaffDesk.API` to apply the `AddPartFPlatformMaturity` migration (adds `IdempotencyRecords`, `ApiKeys`, `WebhookSubscriptions`, `WebhookDeliveries` tables and `UpdatedAt` on `Employees`).
2. Rate limits are in `appsettings.json` (`RateLimits`). JWT signing secret remains `STAFFDESK_JWT_SECRET`.

---

## [Phase 3 Parts A–E] — 2026-09-02 — Audit, Work Cycle, Capacity, Analytics, Performance

See inline code comments and the SRS §4–§8 for requirement references. Migration history in `StaffDesk.Infrastructure/Migrations/`.

---

## [Phase 2] — Aug 2026 — Organisational Structure & Task Management

See StaffDesk SRS v2.0.

---

## [Phase 1] — Jul 2026 — Directory API & Minimal Web UI

See StaffDesk SRS v1.0/1.1.
