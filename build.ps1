$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " NPUtil - Windows ARM64 Build and Publish" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$env:DOTNET_ROOT = "C:\Users\Asus\.dotnet"
$env:PATH = "C:\Users\Asus\.dotnet;" + $env:PATH

Write-Host ""
Write-Host "[1/3] Building Solution (Release)..." -ForegroundColor Yellow
dotnet build NPUtil.sln -c Release

Write-Host ""
Write-Host "[2/3] Running Automated Tests..." -ForegroundColor Yellow
dotnet test tests/NPUtil.Tests/NPUtil.Tests.csproj -c Release --no-build

Write-Host ""
Write-Host "[3/3] Publishing Native win-arm64 Desktop App..." -ForegroundColor Yellow
$publishDir = Join-Path $PSScriptRoot "publish\win-arm64"
dotnet publish src/NPUtil.App/NPUtil.App.csproj -c Release -r win-arm64 -o $publishDir --no-self-contained

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host " Executable: $publishDir\NPUtil.exe" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
