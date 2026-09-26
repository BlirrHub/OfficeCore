# ==============================================================================
# OfficeCore — Build & Package Installers Script
# Builds portable binaries and 1-click standalone setup installers into dist/
# ==============================================================================

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$distDir = Join-Path $rootDir "dist"

Write-Host "Building OfficeCore Desktop and Installers Package..." -ForegroundColor Magenta

if (!(Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

# 1. Publish Portable Client Terminal
Write-Host "[1/4] Publishing OfficeCore Client Terminal Portable..." -ForegroundColor Cyan
$clientDist = Join-Path $distDir "OfficeCore-Client-Portable"
dotnet publish "$rootDir/src/OfficeCore.Desktop.Client/OfficeCore.Desktop.Client.csproj" -c Release -r win-x64 --self-contained false -o $clientDist

# 2. Build Client Setup Wizard
Write-Host "[2/4] Publishing Setup-OfficeCore-Client.exe..." -ForegroundColor Cyan
$tempClientDir = Join-Path $distDir "temp_client_installer"
dotnet publish "$rootDir/src/OfficeCore.Client.Installer/OfficeCore.Client.Installer.csproj" -c Release -r win-x64 --self-contained false -o $tempClientDir
Copy-Item "$tempClientDir/Setup-OfficeCore-Client.exe" "$distDir/Setup-OfficeCore-Client.exe" -Force
Remove-Item $tempClientDir -Recurse -Force

# 3. Publish Portable Server Station
Write-Host "[3/4] Publishing OfficeCore Server Station Portable..." -ForegroundColor Cyan
$serverDist = Join-Path $distDir "OfficeCore-ServerStation-Portable"
dotnet publish "$rootDir/src/OfficeCore.Desktop.Server/OfficeCore.Desktop.Server.csproj" -c Release -r win-x64 --self-contained false -o $serverDist

# 4. Build Server Setup Wizard
Write-Host "[4/4] Publishing Setup-OfficeCore-Server.exe..." -ForegroundColor Cyan
$tempServerDir = Join-Path $distDir "temp_server_installer"
dotnet publish "$rootDir/src/OfficeCore.Server.Installer/OfficeCore.Server.Installer.csproj" -c Release -r win-x64 --self-contained false -o $tempServerDir
Copy-Item "$tempServerDir/Setup-OfficeCore-Server.exe" "$distDir/Setup-OfficeCore-Server.exe" -Force
Remove-Item $tempServerDir -Recurse -Force

Write-Host "Build completed successfully! Packages generated in: $distDir" -ForegroundColor Green
Write-Host "  - dist/Setup-OfficeCore-Client.exe (Workstation Setup)" -ForegroundColor Yellow
Write-Host "  - dist/Setup-OfficeCore-Server.exe (Server Host Setup)" -ForegroundColor Yellow
Write-Host "  - dist/OfficeCore-Client-Portable/" -ForegroundColor Gray
Write-Host "  - dist/OfficeCore-ServerStation-Portable/" -ForegroundColor Gray
