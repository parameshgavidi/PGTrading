# AGENTS.md

## Cursor Cloud specific instructions

### What this is
PG One is a single **.NET 10 MAUI Blazor Hybrid desktop app** (`PGOne/PGOne.csproj`,
target `net10.0-windows10.0.19041.0`). It is a self-contained Windows `.exe` — there is
no web server, database, container, or Node build. Standard build/run commands live in
`README.md` and `PGOne/README.md` (Windows + Visual Studio 2026).

### Platform constraint (important)
This app targets Windows (WinUI / WindowsAppSDK / WebView2) and **can only be fully built
and run on Windows**. It **cannot be launched on the Linux cloud VM**. Do not expect
`dotnet run` to produce a running GUI here.

- The .NET 10 SDK and MAUI workloads are preinstalled in the VM image (`dotnet` is on
  `PATH`; `dotnet --version` → `10.0.3xx`, satisfying `global.json`).
- `dotnet restore` on Linux **requires** `-p:EnableWindowsTargeting=true`, otherwise it
  fails with `NETSDK1100` ("set the EnableWindowsTargeting property to true").
- A full `dotnet build` on Linux fails with `NETSDK1022` (duplicate `wwwroot` content) —
  this is a Windows-target-on-Linux quirk, not a repo bug. Build the app on Windows.
- `dotnet workload restore` needs `sudo` here because the SDK lives in the root-owned
  `/usr/local/dotnet`; the workloads are already installed in the image, so this is
  normally unnecessary.

### Verifying core logic on Linux
`Services/*` and `Models/*` are plain, cross-platform C# and can be compiled and run for
verification. Only `SettingsService` depends on MAUI `Preferences` (Windows-only). When
not connected to Zerodha, `ZerodhaService`/`MarketDataService` return built-in demo data,
so trading logic (SuperTrend, RSI/ADX, multi-timeframe signal generation) is fully
exercisable offline without credentials. The intended workflow to verify logic changes on
Linux is a small throwaway console project that `Compile Include`s the repo's `Models` +
core `Services` (excluding `SettingsService.cs`), supplies a stub `ISettingsService`, and
calls `SignalService.GenerateSignalAsync(...)`.

### Lint / test
There is **no test project and no linter/formatter config** in this repo. Code analysis is
the default Roslyn analyzer set that runs during `dotnet build` on Windows. `dotnet format`
cannot load the Windows-targeted project on Linux.

### External services
The only external dependency is the **Zerodha Kite Connect API** (`api.kite.trade`), used
for live quotes/positions/orders. It is optional — the app auto-falls back to demo data.
Connecting requires an API key/secret + interactive login (see `PGOne/README.md`).
