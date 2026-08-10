# Clean all build artifacts including stale obj folders
Write-Host "Cleaning PgAiTrading build artifacts..." -ForegroundColor Cyan

$paths = @(
    "bin",
    "obj",
    ".vs",
    "Properties\PublishProfiles\*.user"
)

foreach ($p in $paths) {
    if (Test-Path $p) {
        Remove-Item -Recurse -Force $p
        Write-Host "  Removed $p" -ForegroundColor Green
    }
}

# Also clean solution-level .vs if present
if (Test-Path "..\.vs") {
    Remove-Item -Recurse -Force "..\.vs"
    Write-Host "  Removed ..\.vs" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Now run:" -ForegroundColor Yellow
Write-Host "  dotnet workload restore" -ForegroundColor White
Write-Host "  dotnet build -f net10.0-windows10.0.19041.0" -ForegroundColor White
