# NPUtil Windows 11 Installer
# Installs NPUtil to %LOCALAPPDATA%\Programs\NPUtil and registers Start Menu shortcuts
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " NPUtil — Windows 11 Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$sourceDir = Join-Path $PSScriptRoot "publish\win-arm64"
if (-not (Test-Path "$sourceDir\NPUtil.exe")) {
    Write-Host "Publish binaries not found. Running build.ps1 first..." -ForegroundColor Yellow
    & "$PSScriptRoot\build.ps1"
}

$installDir = Join-Path $env:LOCALAPPDATA "Programs\NPUtil"
Write-Host "[1/4] Preparing target directory: $installDir" -ForegroundColor Yellow
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# Stop running instance if any
Get-Process -Name "NPUtil" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

Write-Host "[2/4] Copying files..." -ForegroundColor Yellow
Copy-Item "$sourceDir\*" -Destination $installDir -Recurse -Force

# Copy uninstall script into install dir
Copy-Item "$PSScriptRoot\uninstall.ps1" -Destination "$installDir\uninstall.ps1" -Force

Write-Host "[3/4] Creating Start Menu shortcuts..." -ForegroundColor Yellow
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\NPUtil"
if (-not (Test-Path $startMenuDir)) {
    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
}

$wshShell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path $startMenuDir "NPUtil.lnk"
$shortcut = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDir "NPUtil.exe"
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "NPUtil — Modular Windows Utility Suite"
$shortcut.IconLocation = Join-Path $installDir "Assets\nputil.ico"
$shortcut.Save()

Write-Host "[4/4] Registering in Windows Apps & Features..." -ForegroundColor Yellow
$regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NPUtil"
if (-not (Test-Path $regKey)) {
    New-Item -Path $regKey -Force | Out-Null
}

Set-ItemProperty -Path $regKey -Name "DisplayName" -Value "NPUtil — Windows Utility Suite"
Set-ItemProperty -Path $regKey -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $regKey -Name "Publisher" -Value "adamikoo"
Set-ItemProperty -Path $regKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $regKey -Name "DisplayIcon" -Value "$installDir\Assets\nputil.ico"
Set-ItemProperty -Path $regKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`""
Set-ItemProperty -Path $regKey -Name "URLInfoAbout" -Value "https://github.com/adamikoo/NPUtil"

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " INSTALLATION COMPLETE!" -ForegroundColor Green
Write-Host " You can now launch NPUtil from the Start Menu" -ForegroundColor Green
Write-Host " or press Win and search for 'NPUtil'" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
