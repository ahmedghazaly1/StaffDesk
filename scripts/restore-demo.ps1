# OB-7: dump StaffDesk and restore into StaffDesk_OB7_scratch; print elapsed times.
param(
    [string]$HostName = "localhost",
    [string]$User = "postgres",
    [string]$SourceDb = "StaffDesk",
    [string]$ScratchDb = "StaffDesk_OB7_scratch",
    [string]$Password = "password123",
    [string]$DumpPath = ""
)

$ErrorActionPreference = "Stop"
$env:PGPASSWORD = $Password
if (-not $DumpPath) {
$DumpPath = Join-Path $env:TEMP "staffdesk-ob7.dump"
}

function Find-PgBin {
    $cmds = @("pg_dump", "createdb", "pg_restore", "dropdb", "psql")
    $found = Get-Command pg_dump -ErrorAction SilentlyContinue
    if ($found) { return (Split-Path $found.Source) }
    $roots = @(
        "C:\Program Files\PostgreSQL",
        "C:\Program Files (x86)\PostgreSQL"
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $bin = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "bin" } |
            Where-Object { Test-Path (Join-Path $_ "pg_dump.exe") } |
            Select-Object -First 1
        if ($bin) { return $bin }
    }
    throw "PostgreSQL client tools (pg_dump) not found on PATH or under Program Files\PostgreSQL."
}

$bin = Find-PgBin
$pgDump = Join-Path $bin "pg_dump.exe"
$createdb = Join-Path $bin "createdb.exe"
$pgRestore = Join-Path $bin "pg_restore.exe"
$dropdb = Join-Path $bin "dropdb.exe"
$psql = Join-Path $bin "psql.exe"

New-Item -ItemType Directory -Force -Path (Split-Path $DumpPath) | Out-Null

Write-Host "Dumping $SourceDb -> $DumpPath"
$dumpSw = [System.Diagnostics.Stopwatch]::StartNew()
& $pgDump -h $HostName -U $User -d $SourceDb -F c -f $DumpPath
if ($LASTEXITCODE -ne 0) { throw "pg_dump failed with exit $LASTEXITCODE" }
$dumpSw.Stop()

Write-Host "Dropping scratch $ScratchDb if it exists"
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
& $dropdb -h $HostName -U $User --if-exists $ScratchDb 2>&1 | Out-Null
$ErrorActionPreference = $prevEap

Write-Host "Creating $ScratchDb"
& $createdb -h $HostName -U $User $ScratchDb
if ($LASTEXITCODE -ne 0) { throw "createdb failed with exit $LASTEXITCODE" }

Write-Host "Restoring into $ScratchDb"
$restoreSw = [System.Diagnostics.Stopwatch]::StartNew()
& $pgRestore -h $HostName -U $User -d $ScratchDb --no-owner --no-acl $DumpPath
$restoreExit = $LASTEXITCODE
$restoreSw.Stop()
if ($restoreExit -ne 0) {
    Write-Warning "pg_restore exited $restoreExit (warnings on some objects are common; checking counts)"
}

$sqlFile = Join-Path $env:TEMP "staffdesk-ob7-counts.sql"
@'
SELECT (SELECT count(*) FROM "Employees") AS employees, (SELECT count(*) FROM "Tasks") AS tasks, (SELECT count(*) FROM "AuditEvents") AS audit_events;
'@ | Set-Content -Path $sqlFile -Encoding utf8
$counts = (& $psql -h $HostName -U $User -d $ScratchDb -t -A -F "," -f $sqlFile).Trim()

Write-Host "DUMP_SECONDS=$([math]::Round($dumpSw.Elapsed.TotalSeconds, 2))"
Write-Host "RESTORE_SECONDS=$([math]::Round($restoreSw.Elapsed.TotalSeconds, 2))"
Write-Host "SCRATCH_COUNTS=$counts"
Write-Host "DUMP_PATH=$DumpPath"
Write-Host "SCRATCH_DB=$ScratchDb"
