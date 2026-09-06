# Expand-and-contract migration policy (OB-8)

StaffDesk uses **EF Core** migrations against PostgreSQL. **Any change to a populated table** must be expand-and-contract: add, backfill, dual-write or dual-read, switch, then remove. Each step is a **separate, deployable, reversible** migration (Up and Down).

## When this applies

| Change | Policy |
|--------|--------|
| New empty table | Expand only in one migration. Must have a working `Down` (drop table). Example: `AddWorkerHeartbeat` / `WorkerHeartbeats`. |
| New column on a populated table | Nullable or defaulted first (expand). Backfill. Dual-write. Switch reads. Later migration drops the old column (contract). |
| Rename / type change on populated data | Add new column → backfill → dual-read → switch → drop old. Never rename in one step. |
| Drop column/table with data | Contract only after application code no longer reads it. |

## Phases (populated tables)

### 1. Expand (deploy A)

- Add the new column/table/index. Do not drop or rename anything.
- Application writes **both** old and new shapes (dual-write), or writes new only when the column exists.

### 2. Backfill (deploy B, or a one-off job)

- Copy existing rows into the new shape.
- Verify counts. This step is reversible by leaving the old column intact.

### 3. Switch (deploy C)

- Application **reads** the new shape only. Dual-write may continue until the next release.

### 4. Contract (deploy D)

- Remove dual-write.
- Drop the obsolete column/table in a later migration with a working `Down` that can recreate it if needed (restore from backup if data was discarded).

## Rules

| Do | Don't |
|----|--------|
| Add nullable columns first | Rename a populated column in one migration |
| Ship Up **and** Down | Drop a populated column in the same release as the add |
| Backup before contract (OB-7) | Change stored enum/string values without backfill |
| Fail readiness on pending migrations (OB-2) | Run destructive SQL against production without a restore drill |

## Commands

```powershell
dotnet ef migrations add <Name> --project StaffDesk.Infrastructure --startup-project StaffDesk.API
dotnet ef database update --project StaffDesk.Infrastructure --startup-project StaffDesk.API
dotnet ef database update <PreviousMigration> --project StaffDesk.Infrastructure --startup-project StaffDesk.API
```

The last command is rollback to the previous migration (`Down`).
