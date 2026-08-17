$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'DNSYar - Release Builder (Windows x64)' -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK / dotnet was not found. Install .NET 8 SDK and Visual Studio with WinUI/Windows App SDK components.'
}

Write-Host '[1/3] Restore...' -ForegroundColor Yellow
dotnet restore .\DNSYar.csproj

$out = Join-Path $PSScriptRoot 'Release\win-x64'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

Write-Host '[2/3] Publish...' -ForegroundColor Yellow
dotnet publish .\DNSYar.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Platform=x64 `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishSingleFile=false `
    -o $out

$exe = Join-Path $out 'DNSYar.exe'
Write-Host '[3/3] Finished.' -ForegroundColor Green
if (Test-Path $exe) {
    Write-Host "EXE: $exe" -ForegroundColor Green
    Start-Process explorer.exe $out
} else {
    Write-Warning "Publish finished but DNSYar.exe was not found in $out"
}
