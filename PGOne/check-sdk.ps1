# Check .NET SDK setup for PG One (requires .NET 8 SDK minimum)
Write-Host "PG One - .NET SDK Check" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

$dotnetX64 = "C:\Program Files\dotnet\dotnet.exe"
$dotnet = if (Test-Path $dotnetX64) { $dotnetX64 } else { (Get-Command dotnet -ErrorAction SilentlyContinue).Source }

if (-not $dotnet) {
    Write-Host "ERROR: dotnet not found. Install .NET 8 SDK." -ForegroundColor Red
    Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
}

Write-Host "Using: $dotnet" -ForegroundColor Yellow
Write-Host "Version: $(& $dotnet --version)"
Write-Host ""
Write-Host "Installed SDKs:" -ForegroundColor Yellow
& $dotnet --list-sdks

$sdks = & $dotnet --list-sdks
$hasNet8 = $sdks | Where-Object { $_ -match "^8\.0\." }

Write-Host ""
if ($hasNet8) {
    Write-Host "OK: .NET 8 SDK found." -ForegroundColor Green
    Write-Host "Build with:" -ForegroundColor Yellow
    Write-Host "  dotnet workload install maui" -ForegroundColor White
    Write-Host "  dotnet build -f net8.0-windows10.0.19041.0" -ForegroundColor White
} else {
    Write-Host "ERROR: .NET 8 SDK is NOT installed (you have .NET 6 or older)." -ForegroundColor Red
    Write-Host ""
    Write-Host "Install .NET 8 SDK:" -ForegroundColor Yellow
    Write-Host "  1. Download: https://dotnet.microsoft.com/download/dotnet/8.0"
    Write-Host "  2. Install the x64 SDK (8.0.x)"
    Write-Host "  3. In Visual Studio Installer, check:"
    Write-Host "     - .NET Multi-platform App UI development"
    Write-Host "     - .NET 8.0 SDK"
    Write-Host "  4. Run: dotnet workload install maui"
    Write-Host ""
    Write-Host "Then run this script again." -ForegroundColor Cyan
}
