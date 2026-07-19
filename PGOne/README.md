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
- Visual Studio 2022 17.12+ (**stable**, not Preview) with **.NET MAUI** workload
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) — **stable release only** (not preview)
- Zerodha Kite Connect API credentials ([developers.kite.trade](https://developers.kite.trade))

> **"You are using a preview version of .NET"** — This means a preview SDK or Visual Studio Preview is active. Install the stable .NET 9 SDK and use stable Visual Studio 2022. The repo includes a `global.json` that blocks preview SDKs.

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
dotnet build -f net9.0-windows10.0.19041.0
dotnet run -f net9.0-windows10.0.19041.0
```

Or open `PGOne.csproj` in Visual Studio 2022 and press F5.

### Troubleshooting: Preview .NET warning

If Visual Studio shows **"You are using a preview version of .NET"**:

1. Check your SDK: `dotnet --version` — should be `9.0.x` without `-preview`
2. Install stable [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (x64)
3. Use **Visual Studio 2022 stable** (not Preview edition)
4. The repo `global.json` sets `"allowPrerelease": false` to force stable SDK
5. Run `dotnet workload update` after installing the stable SDK

### Troubleshooting: `GenerateAppManifestFromAppx` / `AppxManifest.xml` error

This project is configured as an **unpackaged** Windows app (`WindowsPackageType=None`). If you see:

```
The "GenerateAppManifestFromAppx" task failed unexpectedly.
Could not find ... MsixContent\AppxManifest.xml
```

1. Delete the `bin` and `obj` folders inside `PGOne`
2. Rebuild from the **PGOne project only** (not the whole solution)
3. Do not pass `-p:WindowsAppSDKSelfContained=true` globally on the CLI — it is already set in the `.csproj`

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
