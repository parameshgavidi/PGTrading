# PG Trading — PG AI Trading SuperTrend Nifty Trading Assistant

.NET MAUI Blazor Hybrid desktop app for SuperTrend-based Nifty trading with Zerodha integration.

**Requires:** .NET 10 SDK + MAUI 10 workload

See [PgAiTrading/README.md](PgAiTrading/README.md) for full setup instructions.

## Quick Start (Windows)

```powershell
cd D:\PgAiTrading\PgAiTrading
.\check-sdk.ps1
dotnet workload install maui
dotnet restore
dotnet build -f net10.0-windows10.0.19041.0
```

Or open `PgAiTrading.sln` in Visual Studio 2026 and press F5.

**Don't have .NET 10?** Download: https://dotnet.microsoft.com/download/dotnet/10.0

## Android APK (arm64 sideload)

Requires .NET 10 SDK, `maui-android` workload, and an Android SDK (`ANDROID_HOME`).

```bash
cd PgAiTrading
chmod +x ./build-android-apk.sh
./build-android-apk.sh
```

Output: `PgAiTrading/bin/Release/net10.0-android/android-arm64/publish/com.pgaitrading.trading-Signed.apk`

Install on a phone with “Install unknown apps” enabled. UI is still desktop-oriented; treat this as a first Android packaging pass.
