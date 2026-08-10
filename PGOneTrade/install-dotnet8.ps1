# PG One Trade - Install .NET 8 SDK (run as normal user, not admin required for winget in some cases)
Write-Host "PG One Trade - .NET 8 SDK Installer" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Check if already installed
$dotnetX64 = "C:\Program Files\dotnet\dotnet.exe"
if (Test-Path $dotnetX64) {
    $sdks = & $dotnetX64 --list-sdks 2>$null
    if ($sdks -match "^8\.0\.") {
        Write-Host "OK: .NET 8 SDK is already installed!" -ForegroundColor Green
        & $dotnetX64 --list-sdks
        Write-Host ""
        Write-Host "Next: dotnet workload install maui" -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "Choose an install method:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  [1] winget (recommended - automatic)" -ForegroundColor White
Write-Host "  [2] Open download page in browser" -ForegroundColor White
Write-Host "  [3] Direct download link" -ForegroundColor White
Write-Host ""
$choice = Read-Host "Enter 1, 2, or 3"

switch ($choice) {
    "1" {
        Write-Host "Installing via winget..." -ForegroundColor Cyan
        winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Installed! Restart PowerShell, then run: .\check-sdk.ps1" -ForegroundColor Green
        } else {
            Write-Host "winget failed. Try option 2 or 3." -ForegroundColor Red
        }
    }
    "2" {
        Start-Process "https://dotnet.microsoft.com/en-us/download/dotnet/8.0"
        Write-Host ""
        Write-Host "On the page:" -ForegroundColor Yellow
        Write-Host "  1. Find section 'Build apps - SDK'" -ForegroundColor White
        Write-Host "  2. Click latest 'SDK 8.0.xxx'" -ForegroundColor White
        Write-Host "  3. Under Windows, click 'x64' to download" -ForegroundColor White
        Write-Host "  4. Run the downloaded .exe installer" -ForegroundColor White
        Write-Host "  5. Restart PowerShell and run: .\check-sdk.ps1" -ForegroundColor White
    }
    "3" {
        $url = "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.423-windows-x64-installer"
        Write-Host "Opening direct download..." -ForegroundColor Cyan
        Start-Process $url
        Write-Host ""
        Write-Host "Run the downloaded installer, restart PowerShell, then: .\check-sdk.ps1" -ForegroundColor Yellow
    }
    default {
        Write-Host "Invalid choice. Run script again." -ForegroundColor Red
    }
}
