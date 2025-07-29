# PowerShell script for Package Manager Console approach
# Copy and paste this into Visual Studio Package Manager Console

Write-Host "=== MyPhotoHelper EF Model Generation ===" -ForegroundColor Green
Write-Host "Copy the command below and paste into Visual Studio Package Manager Console:" -ForegroundColor Yellow
Write-Host

$command = 'Scaffold-DbContext "Data Source=Database\dev_myphotohelper.db" Microsoft.EntityFrameworkCore.Sqlite -OutputDir Models -ContextDir Data -Context MyPhotoHelperDbContext -Force -NoPluralize -UseDatabaseNames -NoOnConfiguring'

Write-Host $command -ForegroundColor Cyan

Write-Host
Write-Host "Steps:" -ForegroundColor Yellow
Write-Host "1. Open Visual Studio" -ForegroundColor White
Write-Host "2. Open the MyPhotoHelper project" -ForegroundColor White
Write-Host "3. Tools → NuGet Package Manager → Package Manager Console" -ForegroundColor White
Write-Host "4. Make sure 'MyPhotoHelper' is selected in Default project dropdown" -ForegroundColor White
Write-Host "5. Paste the command above and press Enter" -ForegroundColor White

Write-Host
Write-Host "This will generate:" -ForegroundColor Yellow
Write-Host "- Models\TblImages.cs" -ForegroundColor White
Write-Host "- Models\TblImageMetadata.cs" -ForegroundColor White  
Write-Host "- Models\TblImageAnalysis.cs" -ForegroundColor White
Write-Host "- Models\TblAppSettings.cs" -ForegroundColor White
Write-Host "- Models\TblVersion.cs" -ForegroundColor White
Write-Host "- Data\MyPhotoHelperDbContext.cs" -ForegroundColor White

Write-Host
Write-Host "Alternative - Manual CLI approach:" -ForegroundColor Yellow
Write-Host "If you prefer command line, first install the global tool:" -ForegroundColor White
Write-Host "dotnet tool install --global dotnet-ef" -ForegroundColor Gray
Write-Host "Then run:" -ForegroundColor White
Write-Host "dotnet ef dbcontext scaffold `"Data Source=Database\dev_myphotohelper.db`" Microsoft.EntityFrameworkCore.Sqlite --output-dir Models --context-dir Data --context MyPhotoHelperDbContext --force --no-pluralize --use-database-names --no-onconfiguring" -ForegroundColor Gray