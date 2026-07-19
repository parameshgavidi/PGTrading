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

# 1. Check SDK (must show .NET 8.x)
.\check-sdk.ps1

# 2. Install MAUI workload (one-time)
dotnet workload install maui

# 3. Clean and build
.\clean.ps1
dotnet restore
dotnet build -f net8.0-windows10.0.19041.0
dotnet run -f net8.0-windows10.0.19041.0
```

Or open `PGTrading.sln` in **Visual Studio 2026 Community** and press **F5**.

### Install .NET 8 SDK (required if you have .NET 6)

Your machine shows `dotnet version: 6.0.400` — you need **.NET 8 SDK**.

#### Easiest: Run the install script
```powershell
cd D:\PGOne\parameshgavidi\PGTrading\PGOne
.\install-dotnet8.ps1
```
Choose option **1** (winget) or **3** (direct download).

#### Option A — winget (one command)
Open **PowerShell** and run:
```powershell
winget install Microsoft.DotNet.SDK.8
```
Restart PowerShell, then verify: `dotnet --list-sdks` (should show `8.0.xxx`).

#### Option B — Direct download link
Click this link to download the installer directly:

**https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.423-windows-x64-installer**

Run the downloaded `.exe` file, then restart PowerShell.

#### Option C — Manual from Microsoft page
1. Go to: **https://dotnet.microsoft.com/download/dotnet/8.0**
2. Find the section **"Build apps - SDK"** (not Runtime)
3. Click the latest version (e.g. **8.0.423**)
4. Under **Windows**, click **x64** (not x86)
5. Run the installer

#### Option D — Via Visual Studio Installer
1. Open **Visual Studio Installer**
2. Click **Modify** on VS 2026 Community
3. Go to **Individual components** tab
4. Search for **".NET 8"**
5. Check:
   - **.NET 8.0 Runtime**
   - **.NET 8.0 SDK**
6. Also check workload: **.NET Multi-platform App UI development**
7. Click **Modify**

#### After installing .NET 8
```powershell
dotnet --list-sdks          # must show 8.0.xxx
dotnet workload install maui
.\check-sdk.ps1
.\clean.ps1
dotnet build -f net8.0-windows10.0.19041.0
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

- Root `App.xaml` must be `x:Class="PGOne.App"` inheriting from MAUI `Application`
- Windows `Platforms/Windows/App.xaml` must be `x:Class="PGOne.WinUI.App"` inheriting from `MauiWinUIApplication`
- Do **not** set Windows App.xaml to `x:Class="PGOne.App"` — that causes this error
- Pull latest, delete `bin`/`obj`, rebuild

### Troubleshooting: WinRT ActivationFactory crash at startup

If the app crashes with `TypeInitializationException` for `WinRT.ActivationFactory` in `App.g.i.cs`:

1. Pull latest — project uses **MSIX packaging** (default MAUI mode for Visual Studio)
2. Close Visual Studio completely
3. Run `.\clean.ps1`
4. Reopen `PGTrading.sln`
5. If VS shows a yellow **"reload project"** banner, right-click the project → **Reload Project**
6. Ensure startup profile is **Windows Machine** (uses `MsixPackage`) and press **F5**

If it still fails, install **Windows App Runtime** from the Microsoft Store, then rebuild.

### Troubleshooting: `GenerateAppManifestFromAppx` / `AppxManifest.xml` error

If you see an AppxManifest build error:

```powershell
cd D:\PGOne\parameshgavidi\PGTrading\PGOne
git pull origin main
.\clean.ps1
dotnet workload restore
dotnet build -f net8.0-windows10.0.19041.0
```

Then close and reopen Visual Studio, reload the project if prompted, and rebuild.

### Troubleshooting: Blank / black screen after launch

If the window opens but shows only a black screen:

1. Pull latest — `wwwroot/index.html` must include `_framework/blazor.webview.js`
2. Run `.\clean.ps1`, delete the `.vs` folder, then rebuild
3. Press **F5** again — you should see the sidebar, dashboard, and PG One logo

If you still see a blank screen, install **WebView2 Runtime** from Microsoft:

```powershell
.\install-webview2.ps1
```

Or manually: https://go.microsoft.com/fwlink/p/?LinkId=2124703

> **Yes, WebView2 is required.** PG One is a Blazor Hybrid app — the entire UI runs inside WebView2 on Windows. Windows 11 usually has it pre-installed; Windows 10 often needs a separate install.

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
