<#
.SYNOPSIS
    Static sanity check for the StaffDesk wwwroot frontend.

.DESCRIPTION
    Cross-references the inline handlers and element references in the HTML
    against what the JavaScript files actually define, so a renamed or removed
    function shows up here instead of as a silent console error in the browser.

    Checks performed:
      1. Every onclick/onsubmit/onchange handler resolves to a defined function.
      2. Every getElementById('x') used by the JS has a matching id in the HTML.
      3. Every nav button id listed in ALL_NAV_TABS exists in index.html.
      4. Every view id listed in ALL_VIEWS exists in index.html.

.EXAMPLE
    pwsh ./scripts/check-frontend.ps1
#>

[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'

if (-not $Root) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $Root = Join-Path $scriptDir '..\StaffDesk.API\wwwroot'
}
$Root = (Resolve-Path $Root).Path

$htmlFiles = Get-ChildItem -Path $Root -Filter *.html -File
$jsFiles   = Get-ChildItem -Path $Root -Filter *.js   -File

$html = ($htmlFiles | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$js   = ($jsFiles   | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

# Inline <script> blocks count as source too (login.html defines login() there).
$inlineJs = ([regex]::Matches($html, '(?s)<script\b[^>]*>(.*?)</script>') |
    ForEach-Object { $_.Groups[1].Value }) -join "`n"
$allJs = $js + "`n" + $inlineJs

# ---- Collect every function name the JS defines -------------------------------
$defined = [System.Collections.Generic.HashSet[string]]::new()

# function foo(...)  /  window.foo = function foo(...)
[regex]::Matches($allJs, '(?m)^\s*(?:async\s+)?function\s+([A-Za-z_$][\w$]*)') |
    ForEach-Object { [void]$defined.Add($_.Groups[1].Value) }

# window.foo = ... / const foo = (...) => / var foo = function
[regex]::Matches($allJs, 'window\.([A-Za-z_$][\w$]*)\s*=') |
    ForEach-Object { [void]$defined.Add($_.Groups[1].Value) }
[regex]::Matches($allJs, '(?m)^\s*(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*(?:async\s*)?(?:function|\()') |
    ForEach-Object { [void]$defined.Add($_.Groups[1].Value) }

# Browser built-ins that legitimately appear in handlers.
@('alert','confirm','print','history','location','window') |
    ForEach-Object { [void]$defined.Add($_) }

# Language keywords are not calls even though they look like one to a regex.
$keywords = @('if','for','while','switch','return','typeof','catch','function','new','do','else')

# ---- 1. Inline handlers -------------------------------------------------------
$handlerCalls = [regex]::Matches(
    $html,
    'on(?:click|submit|change|input|keyup|keydown)\s*=\s*"([^"]*)"'
) | ForEach-Object {
    [regex]::Matches($_.Groups[1].Value, '([A-Za-z_$][\w$]*)\s*\(')
} | ForEach-Object { $_.Groups[1].Value } |
    Where-Object { $keywords -notcontains $_ } |
    Sort-Object -Unique

$missingFns = $handlerCalls | Where-Object { -not $defined.Contains($_) }

# ---- 2. Element ids referenced from JS ---------------------------------------
$htmlIds = [System.Collections.Generic.HashSet[string]]::new()
[regex]::Matches($html, 'id\s*=\s*"([^"]+)"') |
    ForEach-Object { [void]$htmlIds.Add($_.Groups[1].Value) }

# Much of the UI is rendered by innerHTML, so ids minted inside JS template
# strings are just as real as ids written in the static markup.
[regex]::Matches($allJs, 'id\s*=\s*[\\]?["'']([A-Za-z0-9_-]+)[\\]?["'']') |
    ForEach-Object { [void]$htmlIds.Add($_.Groups[1].Value) }

# Only literal, non-templated ids can be checked statically.
$jsIds = [regex]::Matches($allJs, "getElementById\(\s*'([A-Za-z0-9_-]+)'\s*\)") |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

$missingIds = $jsIds | Where-Object { -not $htmlIds.Contains($_) }

# ---- 3 & 4. Nav tab + view registries ----------------------------------------
function Get-ArrayLiteral([string]$name) {
    $m = [regex]::Match($js, "const\s+$name\s*=\s*\[(.*?)\]", 'Singleline')
    if (-not $m.Success) { return @() }
    [regex]::Matches($m.Groups[1].Value, "'([^']+)'") | ForEach-Object { $_.Groups[1].Value }
}

$missingNav  = Get-ArrayLiteral 'ALL_NAV_TABS' | Where-Object { -not $htmlIds.Contains($_) }
$missingView = Get-ArrayLiteral 'ALL_VIEWS'    | Where-Object { -not $htmlIds.Contains($_) }

# ---- Report -------------------------------------------------------------------
Write-Host ''
Write-Host 'StaffDesk frontend check' -ForegroundColor Cyan
Write-Host ('-' * 46)
Write-Host ("HTML files          : {0}" -f $htmlFiles.Count)
Write-Host ("JS files            : {0}" -f $jsFiles.Count)
Write-Host ("Functions defined   : {0}" -f $defined.Count)
Write-Host ("Handlers referenced : {0}" -f $handlerCalls.Count)
Write-Host ("Element ids in HTML : {0}" -f $htmlIds.Count)
Write-Host ("Element ids from JS : {0}" -f $jsIds.Count)
Write-Host ''

$failed = $false

function Write-Result([string]$label, $items) {
    if ($items -and $items.Count -gt 0) {
        Write-Host ("FAIL  {0} ({1})" -f $label, $items.Count) -ForegroundColor Red
        $items | ForEach-Object { Write-Host ("        - {0}" -f $_) -ForegroundColor Red }
        return $true
    }
    Write-Host ("PASS  {0}" -f $label) -ForegroundColor Green
    return $false
}

$failed = (Write-Result 'All inline handlers resolve to a defined function' $missingFns) -or $failed
$failed = (Write-Result 'All JS getElementById targets exist in the HTML'   $missingIds) -or $failed
$failed = (Write-Result 'All ALL_NAV_TABS ids exist in the HTML'            $missingNav) -or $failed
$failed = (Write-Result 'All ALL_VIEWS ids exist in the HTML'               $missingView) -or $failed

Write-Host ''
if ($failed) {
    Write-Host 'Frontend check FAILED' -ForegroundColor Red
    exit 1
}

Write-Host 'Frontend check passed' -ForegroundColor Green
exit 0
