@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

echo ==========================================
echo DNSYar - Release Builder (Windows x64)
echo ==========================================

echo [1/4] Checking .NET SDK...
where dotnet >nul 2>nul
if errorlevel 1 (
  echo.
  echo ERROR: .NET 8 SDK is not installed or dotnet is not in PATH.
  echo Install .NET 8 SDK and Visual Studio 2022 with Windows App SDK / WinUI workload, then run this file again.
  pause
  exit /b 1
)

dotnet --version

echo.
echo [2/4] Restoring NuGet packages...
dotnet restore DNSYar.csproj
if errorlevel 1 goto :fail

echo.
echo [3/4] Publishing self-contained Windows x64 build...
if exist "Release\win-x64" rmdir /s /q "Release\win-x64"
dotnet publish DNSYar.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:Platform=x64 ^
  -p:WindowsAppSDKSelfContained=true ^
  -p:PublishSingleFile=false ^
  -o "Release\win-x64"
if errorlevel 1 goto :fail

echo.
echo [4/4] Done.
echo Output folder:
echo %CD%\Release\win-x64
if exist "Release\win-x64\DNSYar.exe" (
  echo.
  echo DNSYar.exe created successfully.
  explorer "%CD%\Release\win-x64"
) else (
  echo.
  echo Build completed but DNSYar.exe was not found at the expected path.
  echo Open the Release\win-x64 folder and inspect the build output.
)
pause
exit /b 0

:fail
echo.
echo BUILD FAILED.
echo Make sure Visual Studio 2022, Windows 10/11 SDK 10.0.26100 (or compatible), .NET 8 SDK and WinUI/Windows App SDK components are installed.
pause
exit /b 1
