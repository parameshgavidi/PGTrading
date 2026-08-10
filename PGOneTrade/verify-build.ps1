# Verify PG One Trade build output contains Blazor UI files
Write-Host "Checking PG One Trade build output..." -ForegroundColor Cyan

$tfm = "net10.0-windows10.0.19041.0"
$candidateRoots = @(
    "bin\Debug\$tfm\win-x64",
    "bin\x64\Debug\$tfm\win-x64"
)

$root = $candidateRoots | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $root) {
    Write-Host "  MISSING  build output folder under bin\Debug or bin\x64\Debug" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run: .\clean.ps1"
    Write-Host "Then rebuild in Visual Studio (F5)."
    exit 1
}

$paths = @(
    "$root\wwwroot\index.html",
    "$root\wwwroot\_framework\blazor.webview.js",
    "$root\wwwroot\css\app.css",
    "$root\wwwroot\js\chart.js"
)

$allOk = $true
foreach ($path in $paths) {
    if (Test-Path $path) {
        Write-Host "  OK  $path" -ForegroundColor Green
    } else {
        Write-Host "  MISSING  $path" -ForegroundColor Red
        $allOk = $false
    }
}

if (-not $allOk) {
    Write-Host ""
    Write-Host "Blazor UI files are missing from build output." -ForegroundColor Yellow
    Write-Host "Run: .\clean.ps1"
    Write-Host "Then rebuild in Visual Studio (F5)."
    exit 1
}

Write-Host ""
Write-Host "Build output looks correct. If UI is still blank:" -ForegroundColor Green
Write-Host "  1. Run .\sync-maui-version.ps1"
Write-Host "  2. Run .\install-webview2.ps1 if WebView2 Runtime is missing"
Write-Host "  3. Press F5 with profile 'Windows Machine'"
