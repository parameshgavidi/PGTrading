@echo off
echo ============================================
echo  Enable Windows Developer Mode for PG One
echo ============================================
echo.
echo PG One uses MSIX packaging for reliable UI in Visual Studio.
echo Developer Mode is required once to sideload the debug app.
echo.
echo Option A - Settings (easiest):
echo   1. Press Win+R
echo   2. Type: ms-settings:developers
echo   3. Turn ON "Developer Mode"
echo.
echo Option B - This script (run as Administrator):
echo   Right-click this file ^> Run as administrator
echo.
pause
echo.
echo Enabling Developer Mode...
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" /t REG_DWORD /f /v AllowDevelopmentWithoutDevLicense /d 1
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" /t REG_DWORD /f /v AllowAllTrustedApps /d 1
echo.
echo Done. Restart Visual Studio, then press F5.
pause
