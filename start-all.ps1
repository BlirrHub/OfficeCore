Write-Host "Starting OfficeCore Tailwind CSS Watcher..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit -Command `"cd src/OfficeCore.Web; npm run css:watch`""

Write-Host "Starting OfficeCore API Server..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit -Command `"cd src/OfficeCore.Api; dotnet run`""

Write-Host "Starting OfficeCore Web Frontend (Blazor Watch)..." -ForegroundColor Magenta
Start-Process powershell -ArgumentList "-NoExit -Command `"cd src/OfficeCore.Web; dotnet watch`""

Write-Host "All services started in separate windows!" -ForegroundColor Yellow
