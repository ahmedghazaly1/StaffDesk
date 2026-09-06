# Module deployability (NFR-22)

| Process | Role | Failure isolation |
|---------|------|-------------------|
| `StaffDesk.API` | HTTP, UI, health, metrics | Can serve tasks/employees if analytics snapshots are stale (AN-8 reads snapshots; missing data returns empty/suppressed, not 500). |
| `StaffDesk.Worker` | Jobs: SLA, rollup, webhooks, purge | If stopped, API stays up; readiness `worker` check fails (OB-1). Tasks still CRUD. |

Each EF migration has `Up` and `Down` and is applied independently (`dotnet ef database update`). Expand-and-contract policy: `docs/migrations-policy.md`.

Revert a module by redeploying the previous API or Worker binary; schema rollback uses the previous migration only after a backup (OB-7).
