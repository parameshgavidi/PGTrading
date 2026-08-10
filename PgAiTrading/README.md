# PG AI Trading — SuperTrend Nifty Trading Assistant

A **.NET MAUI Blazor Hybrid (Windows)** desktop application for SuperTrend-based Nifty trading with Zerodha Kite Connect integration.

![PG AI Trading](Resources/Images/pgaitrading_logo.png)

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
- **.NET 10 SDK** (x64) — [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Visual Studio 2026 Community** (or VS 2022 17.8+) with **.NET Multi-platform App UI development** workload
- Zerodha Kite Connect API credentials ([developers.kite.trade](https://developers.kite.trade))

> **Requires .NET 10 SDK** for MAUI 10. Run `.\check-sdk.ps1` to verify.

## Setup on D:\PgAiTrading

### Option 1: Clone this repository

```powershell
git clone <repo-url> D:\PgAiTrading
cd D:\PgAiTrading\PgAiTrading
```

### Option 2: Copy the PgAiTrading folder

Copy the entire `PgAiTrading` project folder to `D:\PgAiTrading`.

### Build & Run

```powershell
cd D:\PgAiTrading\PgAiTrading

# 1. Check SDK (must show .NET 10.x)
.\check-sdk.ps1

# 2. Align MAUI packages with workload
.\sync-maui-version.ps1

# 3. Install MAUI workload (one-time)
dotnet workload install maui

# 3. Clean and build
.\clean.ps1
dotnet restore
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

Or open `PG AI Trading.sln` in **Visual Studio 2026 Community** and press **F5**.

### Install .NET 10 SDK

PG AI Trading targets **.NET 10 / MAUI 10**. If `.\check-sdk.ps1` reports a missing SDK:

#### Option A — winget (one command)
```powershell
winget install Microsoft.DotNet.SDK.10
```
Restart PowerShell, then verify: `dotnet --list-sdks` (should show `10.0.xxx`).

#### Option B — Manual from Microsoft page
1. Go to: **https://dotnet.microsoft.com/download/dotnet/10.0**
2. Download the **x64 SDK**
3. Run the installer

#### Option C — Via Visual Studio Installer
1. Open **Visual Studio Installer**
2. Click **Modify** on VS 2026 Community
3. Check workload: **.NET Multi-platform App UI development**
4. Under **Individual components**, check **.NET 10.0 SDK**
5. Click **Modify**

#### After installing .NET 10
```powershell
dotnet --list-sdks          # must show 10.0.xxx
dotnet workload install maui
.\sync-maui-version.ps1
.\check-sdk.ps1
.\clean.ps1
dotnet build -f net10.0-windows10.0.19041.0
```

### VS 2026 Community setup

1. Open **Visual Studio Installer** → Modify VS 2026 Community
2. Ensure these workloads are checked:
   - **.NET Multi-platform App UI development**
   - **.NET desktop development** (recommended for Windows)
3. Click **Modify** and wait for install to complete
4. Open the project and let VS restore NuGet packages

### Troubleshooting: CS0263 App base class conflict

If you see **"Partial declarations of 'App' must not specify different base classes"**:

- Root `App.xaml` must be `x:Class="PgAiTrading.App"` inheriting from MAUI `Application`
- Windows `Platforms/Windows/App.xaml` must be `x:Class="PgAiTrading.WinUI.App"` inheriting from `MauiWinUIApplication`
- Do **not** set Windows App.xaml to `x:Class="PgAiTrading.App"` — that causes this error
- Pull latest, delete `bin`/`obj`, rebuild

### Troubleshooting: WinRT ActivationFactory crash at startup

If the app crashes with `TypeInitializationException` for `WinRT.ActivationFactory` in `App.g.i.cs`:

1. Pull latest — project runs **unpackaged** (plain `.exe`, no MSIX)
2. Close Visual Studio completely
3. Run `.\clean.ps1`, delete `.vs`
4. Reopen `PG AI Trading.sln` and press **F5**

If it still fails, install **Windows App Runtime** from the Microsoft Store, then rebuild.

### Troubleshooting: `GenerateAppManifestFromAppx` / `AppxManifest.xml` error

If you see an AppxManifest build error:

```powershell
cd D:\PgAiTrading\parameshgavidi\PGTrading\PgAiTrading
git pull origin main
.\clean.ps1
dotnet workload restore
dotnet build -f net10.0-windows10.0.19041.0
```

Then close and reopen Visual Studio, reload the project if prompted, and rebuild.

### Troubleshooting: `ExpandPriContent` / `MSB4062` PriGen error

If `dotnet build` fails with:

```
error MSB4062: The "Microsoft.Build.Packaging.Pri.Tasks.ExpandPriContent" task could not be loaded
```

This happens when `EnableMsixTooling` is `false` — `dotnet build` cannot find legacy Visual Studio packaging tasks. The project keeps `EnableMsixTooling=true` with `WindowsPackageType=None` so CLI builds work while the app still runs unpackaged as a plain `.exe`.

```powershell
git pull origin main
cd PgAiTrading
.\clean.ps1
dotnet build -f net10.0-windows10.0.19041.0
```

### Troubleshooting: Blank / black screen after launch

The app runs **unpackaged** — no Developer Mode, no Deploy checkbox needed.

**Step 1 — align MAUI package versions with your workload** (most common cause of a permanent black screen):

```powershell
cd PgAiTrading
.\sync-maui-version.ps1
.\clean.ps1
```

Then reopen Visual Studio and press **F5**.

**Step 2 — full clean rebuild** if still blank:

```powershell
git pull origin main
cd PgAiTrading
.\clean.ps1
```

Delete `bin`, `obj`, and `.vs`, reopen Visual Studio, press **F5**.

**Step 3 — verify build output:**

```powershell
.\verify-build.ps1
.\install-webview2.ps1   # if WebView2 Runtime is missing
```

Known fixes already in the codebase:

- **BlazorWebView loads but shows an empty rectangle** — `AppContext.SetSwitch("BlazorWebView.AppHostAddressAlways0000", true)` in `MauiProgram.cs`.
- **WebView2 loads content but the window stays solid black** — GPU compositing disabled via `WebView2Bootstrap.cs` (runs before WebView2 loads).
- **MAUI package / workload version mismatch** — run `sync-maui-version.ps1` to generate `MauiVersion.local.props`.

In **DEBUG** builds, WebView2 DevTools opens automatically — check the Console tab for script errors.

#### What you should see

| Screen | Meaning |
|--------|---------|
| Blue "Starting PG AI Trading..." → "Loading PG AI Trading UI…" → full UI | Working |
| Red error panel / red diagnostic bar at bottom | Read the message — it names the actual failure |
| Blue banner stuck, black below | WebView2 or Blazor failed to start — run steps above |

## Zerodha Connection

1. Register at [Kite Connect Developer Portal](https://developers.kite.trade)
2. Create an app and note your **API Key** and **API Secret**
3. In PG AI Trading, go to **Settings**
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
PgAiTrading/
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
