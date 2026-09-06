# Phase 3 process evidence (PE-1 … PE-4)

Questions lists and design notes were produced in implementation conversations before each module (Parts A–H). This file is the in-repo pointer so §15.2 Process is not empty.

| Module | Design decisions (acknowledged in code/docs) |
|--------|-----------------------------------------------|
| A | Hash chain, purge dry-run, legal hold — `docs/audit-hash-chain.md`, `docs/threat-model-part-a.md` |
| B | Status intervals unique index; SLA derived state |
| C | Shared `WorkingCalendarService` for SLA and analytics |
| D | Snapshots only on GET; rebuild CLI |
| E | Stage machine + PM-38 — `docs/threat-model-part-e.md` |
| F–H | Idempotency middleware, ETag helper, webhook jobs, refresh families |

Release notes: `CHANGELOG.md`. Migrations are independently reversible (`docs/migrations-policy.md`).
