# =============================================================
# سكريبت تهيئة مشروع مجلس المخامرة الشرقي
# شغّل بـ: PowerShell -ExecutionPolicy Bypass -File setup.ps1
# =============================================================

Write-Host "=== تهيئة مشروع مجلس المخامرة الشرقي ===" -ForegroundColor Cyan

# 1. استعادة الحزم
Write-Host "`n[1] استعادة الحزم..." -ForegroundColor Yellow
dotnet restore MajlisManagement.csproj
if ($LASTEXITCODE -ne 0) { Write-Host "فشل: dotnet restore" -ForegroundColor Red; exit 1 }

# 2. تثبيت dotnet-ef إذا لم يكن موجوداً
Write-Host "`n[2] التحقق من dotnet-ef..." -ForegroundColor Yellow
$efInstalled = dotnet tool list -g | Select-String "dotnet-ef"
if (-not $efInstalled) {
    Write-Host "تثبيت dotnet-ef..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

# 3. إنشاء Migrations وتطبيقها (بديل: استخدم CreateDatabase.sql مباشرة)
Write-Host "`n[3] تطبيق Migrations على SQL Server..." -ForegroundColor Yellow
Write-Host "ملاحظة: يمكنك بدلاً من ذلك تشغيل: Database\CreateDatabase.sql على SSMS" -ForegroundColor Gray
dotnet ef database update --connection "Server=.\SQLEXPRESS;Database=MajlisDB;Trusted_Connection=True;TrustServerCertificate=True;"

# 4. بناء المشروع
Write-Host "`n[4] بناء المشروع..." -ForegroundColor Yellow
dotnet build MajlisManagement.csproj --configuration Release
if ($LASTEXITCODE -ne 0) { Write-Host "فشل: dotnet build" -ForegroundColor Red; exit 1 }

Write-Host "`n=== جاهز! ===" -ForegroundColor Green
Write-Host "تشغيل المشروع:  dotnet run" -ForegroundColor Cyan
Write-Host "Swagger UI:      https://localhost:5001" -ForegroundColor Cyan
Write-Host "`nبيانات الإدارة الافتراضية:" -ForegroundColor Yellow
Write-Host "  Email:    admin@majlis.com" -ForegroundColor White
Write-Host "  Password: Admin@123" -ForegroundColor White
