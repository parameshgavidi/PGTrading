# PG One — SuperTrend Nifty Trading Assistant

A **.NET MAUI Blazor Hybrid (Windows)** desktop application for SuperTrend-based Nifty trading with Zerodha Kite Connect integration.

![PG One](Resources/Images/pgone_logo.png)

## Features

- **Dashboard** — NIFTY price, watchlist, candlestick chart, multi-timeframe SuperTrend analysis
- **Live Chart** — TradingView-style candlestick chart with SuperTrend overlay
- **Market Scanner** — Scans watchlist for 1H + 15m + 5m trend alignment
- **Trade Signals** — BUY/SELL signals with confidence score and option strategy recommendations
- **Positions & Orders** — Live data from Zerodha when connected
- **Watchlist** — Track NIFTY, BANKNIFTY, and key stocks
- **Strategy Configuration** — Customize SuperTrend, RSI, ADX parameters
- **Zerodha Integration** — Kite Connect API for quotes, orders, and positions

## Prerequisites

- Windows 10/11 (version 1809 or later)
- **.NET 8 SDK** (x64) — [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2026 Community** (or VS 2022 17.8+) with **.NET Multi-platform App UI development** workload
- Zerodha Kite Connect API credentials ([developers.kite.trade](https://developers.kite.trade))

> **You have .NET 6.0.400?** That is too old. You must install **.NET 8 SDK**. Run `.\check-sdk.ps1` to verify.

## Setup on D:\PGOne

### Option 1: Clone this repository

```powershell
git clone <repo-url> D:\PGOne
cd D:\PGOne\PGOne
```

### Option 2: Copy the PGOne folder

Copy the entire `PGOne` project folder to `D:\PGOne`.

### Build & Run

```powershell
cd D:\PGOne\PGOne

# Clean stale MSIX artifacts (required if you hit AppxManifest errors)
Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue

dotnet restore
dotnet workload restore
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

Or open `PGOne.csproj` in **Visual Studio 2026 Community** and press **F5**.

### VS 2026 Community setup

1. Open **Visual Studio Installer** → Modify VS 2026 Community
2. Ensure these workloads are checked:
   - **.NET Multi-platform App UI development**
   - **.NET desktop development** (recommended for Windows)
3. Click **Modify** and wait for install to complete
4. Open the project and let VS restore NuGet packages

### Troubleshooting: `dotnet --version` shows 6.x or 8.x (need .NET 10)

This project requires **.NET 10 SDK**. If you see `6.0.400` or similar, your terminal is using an old SDK — not the one from VS 2026.

**Step 1 — Check all installed SDKs:**
```powershell
dotnet --list-sdks
```

You need a line like `10.0.xxx` in the list.

**Step 2 — Install .NET 10 via Visual Studio Installer:**
1. Open **Visual Studio Installer**
2. Click **Modify** on **VS 2026 Community**
3. Under **Workloads**, check:
   - **.NET Multi-platform App UI development**
   - **.NET desktop development**
4. Under **Individual components**, search and check:
   - **.NET 10.0 Runtime**
   - **.NET 10.0 SDK**
5. Click **Modify** and wait for install to finish

**Step 3 — Use the correct dotnet (PATH fix):**

VS 2026 installs the SDK here:
```
C:\Program Files\dotnet\dotnet.exe
```

Run this in PowerShell to verify:
```powershell
& "C:\Program Files\dotnet\dotnet.exe" --version
```

If that shows `10.0.x` but `dotnet --version` shows `6.0.400`, your PATH points to an old SDK. Fix:

1. Open **System Properties → Environment Variables**
2. In **Path**, move `C:\Program Files\dotnet` **above** any older .NET paths (e.g. `C:\Program Files (x86)\dotnet` or old SDK folders)
3. Remove obsolete .NET 6 SDK entries if you no longer need them
4. **Close and reopen** PowerShell / Visual Studio

**Step 4 — Build from VS 2026 (easiest):**

You do not need the command line if PATH is wrong — just open `PGTrading.sln` in **VS 2026 Community** and press **F5**. Visual Studio uses its own bundled SDK automatically.

**Optional — Download .NET 10 SDK directly:**
https://dotnet.microsoft.com/download/dotnet/10.0

### Troubleshooting: Preview .NET warning

If Visual Studio shows **"You are using a preview version of .NET"**:

1. Check SDK: `dotnet --version` — should be `10.0.x` (not `11.0.0-preview.x`)
2. VS 2026 ships with **.NET 10 stable** — do not install .NET 11 preview unless testing it
3. The repo `global.json` blocks preview SDKs (`allowPrerelease: false`) but does not pin a specific SDK version — any stable .NET 10 SDK from VS 2026 will work
4. Run from the project folder:
   ```powershell
   dotnet workload restore
   dotnet workload update
   ```
5. Clean and rebuild: delete `bin` and `obj`, then rebuild in VS

### Troubleshooting: CS0263 App base class conflict

If you see **"Partial declarations of 'App' must not specify different base classes"**:

- Root `App.xaml` must be `x:Class="PGOne.App"` inheriting from MAUI `Application`
- Windows `Platforms/Windows/App.xaml` must be `x:Class="PGOne.WinUI.App"` inheriting from `MauiWinUIApplication`
- Do **not** set Windows App.xaml to `x:Class="PGOne.App"` — that causes this error
- Pull latest, delete `bin`/`obj`, rebuild

### Troubleshooting: `GenerateAppManifestFromAppx` / `AppxManifest.xml` error

If the error path shows **`net8.0-windows...`** you have **stale build cache** from an old version. Run:

```powershell
cd D:\PGOne\parameshgavidi\PGTrading\PGOne
git pull origin main
.\clean.ps1
dotnet workload restore
dotnet build -f net10.0-windows10.0.19041.0
```

This project is an **unpackaged** Windows app (`WindowsPackageType=None`). The build uses `Directory.Build.targets` to skip MSIX tasks. If you still see the error:

1. Close Visual Studio completely
2. Run `.\clean.ps1` (deletes `bin`, `obj`, `.vs`)
3. Reopen `PGTrading.sln` and rebuild
4. Do not pass `-p:WindowsAppSDKSelfContained=true` on the CLI — it is already set in the project

## Zerodha Connection

1. Register at [Kite Connect Developer Portal](https://developers.kite.trade)
2. Create an app and note your **API Key** and **API Secret**
3. In PG One, go to **Settings**
4. Enter API Key and Secret, then click **Save Settings**
5. Click **Open Zerodha Login** and authorize
6. Copy the `request_token` from the redirect URL
7. Paste it and click **Connect**

## Color Theme

| Item       | Color   |
|------------|---------|
| Background | #121212 |
| Card       | #1E1E1E |
| Buy        | Green   |
| Sell       | Red     |
| Text       | White   |
| Secondary  | #B0B0B0 |
| Buttons    | #2196F3 |

## Project Structure

```
PGOne/
├── Components/
│   ├── Layout/          # MainLayout, NavMenu
│   └── Pages/           # Dashboard, Chart, Scanner, Signals, etc.
├── Models/              # Candle, Signal, Position, WatchItem
├── Services/            # Zerodha, SuperTrend, MarketData, Indicators
├── ViewModels/          # Dashboard, Strategy, Signal, Settings
├── Resources/
│   ├── Images/          # Logo
│   ├── Styles/          # Colors, Styles
│   └── Fonts/
├── Platforms/Windows/   # Windows-specific entry point
└── wwwroot/             # Blazor CSS, JS, images
```

## Navigation

| Page            | Route       |
|-----------------|-------------|
| Dashboard       | `/`         |
| Live Chart      | `/chart`    |
| Market Scanner  | `/scanner`  |
| Trade Signals   | `/signals`  |
| Positions       | `/positions`|
| Orders          | `/orders`   |
| Watchlist       | `/watchlist`|
| Strategy        | `/strategy` |
| Settings        | `/settings` |

## Disclaimer

This application is for educational and personal use. Trading involves substantial risk. Always validate signals before placing trades. The authors are not responsible for any financial losses.
