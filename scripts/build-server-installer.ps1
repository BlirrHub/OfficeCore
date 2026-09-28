$ErrorActionPreference = 'Stop'
$rootDir = (Get-Location).Path
$distDir = Join-Path $rootDir 'dist'
$serverDist = Join-Path $distDir 'OfficeCore-ServerStation-Portable'
$tempServerDir = Join-Path $distDir 'temp_server_installer'

Write-Host "[1/2] Publishing OfficeCore Server Station Portable to $serverDist..." -ForegroundColor Cyan
dotnet publish "$rootDir/src/OfficeCore.Desktop.Server/OfficeCore.Desktop.Server.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $serverDist

Write-Host "[2/2] Publishing Setup-OfficeCore-Server.exe..." -ForegroundColor Cyan
dotnet publish "$rootDir/src/OfficeCore.Server.Installer/OfficeCore.Server.Installer.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $tempServerDir

$outputExe = Join-Path $tempServerDir 'Setup-OfficeCore-Server.exe'
$targetExe = Join-Path $distDir 'Setup-OfficeCore-Server.exe'

if (Test-Path $outputExe) {
    Copy-Item $outputExe $targetExe -Force
    Remove-Item $tempServerDir -Recurse -Force
    Write-Host "Success: Setup-OfficeCore-Server.exe updated in $distDir!" -ForegroundColor Green
} else {
    Write-Error "Output binary not found at $outputExe"
}
