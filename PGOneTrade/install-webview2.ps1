# Install Microsoft Edge WebView2 Runtime (required for PG One Trade UI on Windows)
Write-Host "PG One Trade requires WebView2 to display the trading UI." -ForegroundColor Cyan
Write-Host ""

$webviewKey = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
$installed = Test-Path $webviewKey

if ($installed) {
    $version = (Get-ItemProperty $webviewKey -ErrorAction SilentlyContinue).pv
    Write-Host "WebView2 appears to be installed. Version: $version" -ForegroundColor Green
    Write-Host "If the app is still blank, run .\clean.ps1, rebuild, and press F5 again."
    exit 0
}

Write-Host "WebView2 Runtime not detected." -ForegroundColor Yellow
Write-Host ""
Write-Host "Option 1 - winget (recommended):"
Write-Host "  winget install Microsoft.EdgeWebView2Runtime"
Write-Host ""
Write-Host "Option 2 - direct download:"
Write-Host "  https://go.microsoft.com/fwlink/p/?LinkId=2124703"
Write-Host ""

$choice = Read-Host "Install with winget now? (Y/N)"
if ($choice -eq 'Y' -or $choice -eq 'y') {
    winget install Microsoft.EdgeWebView2Runtime --accept-package-agreements --accept-source-agreements
    Write-Host ""
    Write-Host "Done. Restart Visual Studio and press F5." -ForegroundColor Green
}
