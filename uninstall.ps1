# NPUtil Windows 11 Uninstaller
# Removes Start Menu shortcuts, Registry entries, and installed files
$ErrorActionPreference = "SilentlyContinue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " NPUtil — Windows 11 Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Stop running NPUtil process
Write-Host "[1/3] Closing any running NPUtil instances..." -ForegroundColor Yellow
Get-Process -Name "NPUtil" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

# 2. Remove Start Menu Shortcuts
Write-Host "[2/3] Removing Start Menu shortcuts..." -ForegroundColor Yellow
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\NPUtil"
if (Test-Path $startMenuDir) {
    Remove-Item -Path $startMenuDir -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. Remove Windows Registry Uninstall Entry
Write-Host "[3/3] Removing Windows Settings & Apps entry..." -ForegroundColor Yellow
$regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NPUtil"
if (Test-Path $regKey) {
    Remove-Item -Path $regKey -Recurse -Force -ErrorAction SilentlyContinue
}

# 4. Remove program files if called from outside or schedule cleanup
$installDir = Join-Path $env:LOCALAPPDATA "Programs\NPUtil"
if ($PSScriptRoot -ne $installDir) {
    if (Test-Path $installDir) {
        Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "Scheduled post-exit directory cleanup for: $installDir" -ForegroundColor DarkGray
    Start-Process powershell.exe -ArgumentList "-NoProfile -Command `"Start-Sleep -Seconds 1; Remove-Item -Path '$installDir' -Recurse -Force`"" -WindowStyle Hidden
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " NPUtil has been successfully uninstalled." -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
