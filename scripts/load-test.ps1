# OB-9: lightweight load test for StaffDesk API
param(
    [string]$BaseUrl = "http://localhost:5139",
    [int]$DurationSeconds = 30,
    [int]$Concurrency = 10
)

$ErrorActionPreference = "Stop"

function Invoke-Timed {
    param([scriptblock]$Action)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try { & $Action } catch { $_ }
    finally { $sw.Stop() }
    return $sw.ElapsedMilliseconds
}

# Login as admin for authenticated reads
$loginBody = @{ username = "admin"; password = "admin123" } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/auth/login" -ContentType "application/json" -Body $loginBody
$token = $login.token
$headers = @{ Authorization = "Bearer $token" }

$end = (Get-Date).AddSeconds($DurationSeconds)
$ok = 0
$fail = 0
$latencies = [System.Collections.Generic.List[int]]::new()

Write-Host "Load test $BaseUrl for ${DurationSeconds}s @ concurrency $Concurrency"

while ((Get-Date) -lt $end) {
    $jobs = 1..$Concurrency | ForEach-Object {
        Start-Job -ScriptBlock {
            param($BaseUrl, $Headers)
            $ms = Measure-Command {
                try {
                    Invoke-RestMethod -Uri "$BaseUrl/v1/health/live" -TimeoutSec 5 | Out-Null
                    Invoke-RestMethod -Uri "$BaseUrl/v1/departments" -Headers $Headers -TimeoutSec 10 | Out-Null
                    return $true
                } catch { return $false }
            }
            return [int]$ms.TotalMilliseconds
        } -ArgumentList $BaseUrl, $headers
    }
    $results = $jobs | Receive-Job -Wait
    $jobs | Remove-Job -Force
    foreach ($r in $results) {
        if ($r -is [bool]) {
            if ($r) { $ok++ } else { $fail++ }
        } elseif ($r -is [int]) {
            $latencies.Add($r)
            $ok++
        } else { $fail++ }
    }
}

$p50 = 0
if ($latencies.Count -gt 0) {
    $sorted = $latencies | Sort-Object
    $p50 = $sorted[[int]($sorted.Count / 2)]
}

$total = $ok + $fail
$rps = [math]::Round($total / $DurationSeconds, 1)
Write-Host "Done: $total requests (~$rps req/s), failures=$fail, p50~${p50}ms"
