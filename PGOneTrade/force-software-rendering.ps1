# Force WebView2 to use software rendering (fixes black-screen GPU compositing issues).
# Use this to test whether a black WebView2 screen is caused by a GPU/driver problem,
# independent of app code (the app itself also sets this automatically).

$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--disable-gpu --disable-gpu-compositing --disable-gpu-driver-bug-workarounds"

Write-Host "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS set for this session:" -ForegroundColor Cyan
Write-Host "  $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"
Write-Host ""
Write-Host "Now launch PGOneTrade.exe from THIS PowerShell window (not double-click), e.g.:" -ForegroundColor Yellow
Write-Host "  .\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\PGOneTrade.exe"
Write-Host ""
Write-Host "If the UI now renders, the black screen was caused by GPU/graphics driver" -ForegroundColor Green
Write-Host "compositing on this machine. The app's own code already applies this fix"
Write-Host "automatically, so a normal F5 / double-click run should also work once you"
Write-Host "pull latest and rebuild."
