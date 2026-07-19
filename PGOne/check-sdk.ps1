# Check .NET SDK setup for PG One (requires .NET 10)
Write-Host "PG One - .NET SDK Check" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

$dotnetDefault = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetX64 = "C:\Program Files\dotnet\dotnet.exe"

Write-Host "Default dotnet:" -ForegroundColor Yellow
if ($dotnetDefault) {
    Write-Host "  Path: $($dotnetDefault.Source)"
    $ver = & $dotnetDefault.Source --version 2>$null
    Write-Host "  Version: $ver"
} else {
    Write-Host "  NOT FOUND in PATH" -ForegroundColor Red
}

Write-Host ""
Write-Host "x64 dotnet (VS 2026):" -ForegroundColor Yellow
if (Test-Path $dotnetX64) {
    $verX64 = & $dotnetX64 --version 2>$null
    Write-Host "  Path: $dotnetX64"
    Write-Host "  Version: $verX64"
} else {
    Write-Host "  NOT FOUND at $dotnetX64" -ForegroundColor Red
}

Write-Host ""
Write-Host "All installed SDKs:" -ForegroundColor Yellow
if ($dotnetDefault) {
    & $dotnetDefault.Source --list-sdks
} elseif (Test-Path $dotnetX64) {
    & $dotnetX64 --list-sdks
}

Write-Host ""
$targetVer = "10.0"
$sdks = if (Test-Path $dotnetX64) { & $dotnetX64 --list-sdks } else { @() }
$hasNet10 = $sdks | Where-Object { $_ -match "^10\.0\." }

if ($hasNet10) {
    Write-Host "OK: .NET 10 SDK is installed." -ForegroundColor Green
    Write-Host "Build with:" -ForegroundColor Yellow
    Write-Host '  & "C:\Program Files\dotnet\dotnet.exe" build -f net10.0-windows10.0.19041.0' -ForegroundColor White
} else {
    Write-Host "ERROR: .NET 10 SDK is NOT installed." -ForegroundColor Red
    Write-Host ""
    Write-Host "Install via Visual Studio Installer:" -ForegroundColor Yellow
    Write-Host "  1. Modify VS 2026 Community"
    Write-Host "  2. Check: .NET Multi-platform App UI development"
    Write-Host "  3. Check: .NET 10.0 SDK (Individual components)"
    Write-Host ""
    Write-Host "Or download: https://dotnet.microsoft.com/download/dotnet/10.0"
    Write-Host ""
    Write-Host "Easiest: Open PGTrading.sln in VS 2026 and press F5." -ForegroundColor Cyan
}
