# Verify PG One build output contains Blazor UI files
Write-Host "Checking PG One build output..." -ForegroundColor Cyan

$paths = @(
    "bin\Debug\net8.0-windows10.0.19041.0\win-x64\wwwroot\index.html",
    "bin\Debug\net8.0-windows10.0.19041.0\win-x64\wwwroot\_framework\blazor.webview.js",
    "bin\Debug\net8.0-windows10.0.19041.0\win-x64\wwwroot\css\app.css"
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
Write-Host "  1. Enable Developer Mode: Win+R -> ms-settings:developers"
Write-Host "  2. In VS: right-click PGOne project -> Deploy"
Write-Host "  3. Press F5 with profile 'Windows Machine'"
