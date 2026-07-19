# Clean all build artifacts including stale net8.0 obj folders
Write-Host "Cleaning PGOne build artifacts..." -ForegroundColor Cyan

$paths = @(
    "bin",
    "obj",
    ".vs"
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
Write-Host "  dotnet build -f net8.0-windows10.0.19041.0" -ForegroundColor White
