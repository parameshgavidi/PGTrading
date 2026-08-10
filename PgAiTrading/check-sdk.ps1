# Check .NET SDK setup for PG AI Trading (requires .NET 10 SDK for MAUI 10)
Write-Host "PG AI Trading - .NET SDK Check" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

$dotnetX64 = "C:\Program Files\dotnet\dotnet.exe"
$dotnet = if (Test-Path $dotnetX64) { $dotnetX64 } else { (Get-Command dotnet -ErrorAction SilentlyContinue).Source }

if (-not $dotnet) {
    Write-Host "ERROR: dotnet not found. Install .NET 10 SDK." -ForegroundColor Red
    Write-Host "https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
}

Write-Host "Using: $dotnet" -ForegroundColor Yellow
Write-Host "Version: $(& $dotnet --version)"
Write-Host ""
Write-Host "Installed SDKs:" -ForegroundColor Yellow
& $dotnet --list-sdks

$sdks = & $dotnet --list-sdks
$hasNet10 = $sdks | Where-Object { $_ -match "^10\.0\." }

Write-Host ""
Write-Host "Installed workloads:" -ForegroundColor Yellow
& $dotnet workload list

Write-Host ""
Write-Host "MAUI package alignment:" -ForegroundColor Yellow
$localProps = Join-Path $PSScriptRoot "MauiVersion.local.props"
if (Test-Path $localProps) {
    Write-Host "  OK  MauiVersion.local.props exists" -ForegroundColor Green
} else {
    Write-Host "  TIP Run .\sync-maui-version.ps1 to align NuGet MAUI packages with your workload." -ForegroundColor Yellow
    Write-Host "      A version mismatch often causes build errors or a blank/black BlazorWebView screen."
}

Write-Host ""
if ($hasNet10) {
    Write-Host "OK: .NET 10 SDK found." -ForegroundColor Green
    Write-Host "Build with:" -ForegroundColor Yellow
    Write-Host "  .\sync-maui-version.ps1" -ForegroundColor White
    Write-Host "  dotnet workload install maui" -ForegroundColor White
    Write-Host "  dotnet build -f net10.0-windows10.0.19041.0" -ForegroundColor White
} else {
    Write-Host "ERROR: .NET 10 SDK is NOT installed." -ForegroundColor Red
    Write-Host ""
    Write-Host "PG AI Trading targets net10.0-windows (MAUI 10). Install:" -ForegroundColor Yellow
    Write-Host "  1. Download: https://dotnet.microsoft.com/download/dotnet/10.0"
    Write-Host "  2. Install the x64 SDK (10.0.x)"
    Write-Host "  3. In Visual Studio Installer, check:"
    Write-Host "     - .NET Multi-platform App UI development"
    Write-Host "     - .NET 10.0 SDK"
    Write-Host "  4. Run: dotnet workload install maui"
    Write-Host "  5. Run: .\sync-maui-version.ps1"
    Write-Host ""
    Write-Host "Then run this script again." -ForegroundColor Cyan
}
