# Backup and restore (OB-7)

StaffDesk stores application state in **PostgreSQL**. An untested backup is not a control. Procedure is below; the **actual scratch restore** with elapsed time is in [restore-demonstration.md](./restore-demonstration.md).

## Backup

```powershell
$env:PGPASSWORD = "password123"
pg_dump -h localhost -U postgres -d StaffDesk -F c -f staffdesk-backup.dump
```

Plain SQL alternative:

```powershell
pg_dump -h localhost -U postgres -d StaffDesk -f staffdesk-backup.sql
```

Include all tables (employees, tasks, audit chain, jobs, snapshots, performance).

**Frequency:** daily full backup; retain to match audit retention (`Retention:AuditEventsDays`).

## Restore into a scratch database

Never restore over production.

```powershell
$env:PGPASSWORD = "password123"
createdb -h localhost -U postgres StaffDesk_OB7_scratch
pg_restore -h localhost -U postgres -d StaffDesk_OB7_scratch -c staffdesk-backup.dump
```

Repeatable script: `scripts/restore-demo.ps1`.

Point a staging API at the scratch DB, then:

1. `GET /v1/health/ready`
2. `dotnet run --project StaffDesk.Worker -- verify-audit`
3. Compare employee/task counts to the source

Drop the scratch database when finished:

```powershell
dropdb -h localhost -U postgres StaffDesk_OB7_scratch
```
