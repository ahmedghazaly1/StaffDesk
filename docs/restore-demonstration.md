# Restore demonstration record (OB-7)

An untested backup is a belief, not a control. This is the written record of an **actual** restore into a scratch database.

| Field | Value |
|-------|--------|
| Date / time | 3 September 2026, 19:33 (UTC+3) |
| Operator | Local StaffDesk development machine (Windows) |
| Source database | `StaffDesk` on `localhost` (PostgreSQL) |
| Scratch database | `StaffDesk_OB7_scratch` (created empty, then restored) |
| Dump file | `%TEMP%\staffdesk-ob7.dump` (custom format `-F c`) |
| Dump size | 249,984 bytes |
| Dump elapsed | **0.20 seconds** |
| Restore elapsed | **0.66 seconds** |
| Method | `scripts/restore-demo.ps1` (`pg_dump` → `createdb` → `pg_restore`) |

## Row-count check (source vs scratch)

| Table | Source `StaffDesk` | Scratch `StaffDesk_OB7_scratch` |
|-------|--------------------|----------------------------------|
| `Employees` | 15 | 15 |
| `Tasks` | 37 | 37 |
| `AuditEvents` | 377 | 377 |

Scratch `\dt` listed 61 relations, including `AuditEvents`, `Jobs`, `WorkerHeartbeats` (after schema restore), and `__EFMigrationsHistory`. Counts matched the source; the restore is treated as successful.

## How to repeat

```powershell
cd d:\PROJECTS\StaffDesk
powershell -ExecutionPolicy Bypass -File .\scripts\restore-demo.ps1
```

After a scale seed (OB-10), re-run the same script and update this table with the new times and counts. Drop the scratch database when finished:

```powershell
dropdb -h localhost -U postgres --if-exists StaffDesk_OB7_scratch
```
