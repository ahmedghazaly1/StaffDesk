# NFR-16 measurement helper. Requires a running API (default http://localhost:5139).
param(
    [string]$BaseUrl = "http://localhost:5139",
    [int]$Iterations = 40
)

$ErrorActionPreference = "Stop"
$outFile = Join-Path $PSScriptRoot "..\docs\nfr-measurements.md"

function Percentile($values, $p) {
    $sorted = @($values | Sort-Object)
    if ($sorted.Count -eq 0) { return 0 }
    $idx = [Math]::Min($sorted.Count - 1, [int][Math]::Ceiling($p * $sorted.Count) - 1)
    if ($idx -lt 0) { $idx = 0 }
    return [math]::Round($sorted[$idx], 1)
}

function Measure-Path([string]$url, $headers) {
    $samples = @()
    for ($i = 0; $i -lt $Iterations; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing -TimeoutSec 30 | Out-Null
        } catch {
            $sw.Stop()
            return @{ error = $_.Exception.Message; p50 = 0; p95 = 0; n = 0 }
        }
        $sw.Stop()
        $samples += $sw.Elapsed.TotalMilliseconds
    }
    return @{
        p50 = Percentile $samples 0.50
        p95 = Percentile $samples 0.95
        n = $samples.Count
        error = $null
    }
}

$loginBody = '{"username":"admin","password":"admin123"}'
try {
    $login = Invoke-RestMethod -Method Post -Uri ($BaseUrl + "/v1/auth/login") -ContentType "application/json" -Body $loginBody
} catch {
    Write-Host ("API not reachable at {0}: {1}" -f $BaseUrl, $_.Exception.Message)
    exit 0
}

$token = $login.accessToken
if (-not $token) { $token = $login.token }
$headers = @{ Authorization = ("Bearer " + $token) }
$from = (Get-Date).AddDays(-30).ToString("yyyy-MM-dd")
$to = (Get-Date).ToString("yyyy-MM-dd")
$deptId = 1
try {
    $depts = Invoke-RestMethod -Uri ($BaseUrl + "/v1/departments") -Headers $headers
    if ($depts -is [System.Array] -and $depts.Count -gt 0) { $deptId = $depts[0].id }
    elseif ($depts.data -and $depts.data.Count -gt 0) { $deptId = $depts.data[0].id }
    elseif ($depts.id) { $deptId = $depts.id }
} catch { }

$paths = @(
    ('{0}/v1/analytics/flow?departmentId={1}&from={2}&to={3}' -f $BaseUrl, $deptId, $from, $to),
    ('{0}/v1/analytics/throughput?departmentId={1}&from={2}&to={3}' -f $BaseUrl, $deptId, $from, $to),
    ('{0}/v1/analytics/wip?departmentId={1}&from={2}&to={3}' -f $BaseUrl, $deptId, $from, $to),
    ($BaseUrl + "/v1/health/live"),
    ($BaseUrl + "/v1/health/ready")
)

$stamp = Get-Date -Format "yyyy-MM-dd HH:mm"
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("")
$lines.Add(("## Auto-run {0} against {1} - {2} iterations" -f $stamp, $BaseUrl, $Iterations))
$lines.Add("")
$lines.Add("| Endpoint | n | p50 (ms) | p95 (ms) |")
$lines.Add("|----------|---|----------|----------|")

foreach ($p in $paths) {
    $r = Measure-Path $p $headers
    if ($r.error) {
        $lines.Add(("| {0} | - | error | {1} |" -f $p, $r.error))
        Write-Host ("FAIL {0} {1}" -f $p, $r.error)
    } else {
        $ok = "OVER"
        if ($r.p95 -le 500) { $ok = "OK" }
        $lines.Add(("| {0} | {1} | {2} | {3} ({4}) |" -f $p, $r.n, $r.p50, $r.p95, $ok))
        Write-Host ("{0} p50={1} p95={2} {3}" -f $p, $r.p50, $r.p95, $ok)
    }
}

Add-Content -Path $outFile -Value ($lines -join "`n")
Write-Host ("Appended results to {0}" -f $outFile)
