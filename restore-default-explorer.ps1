Write-Host "Vosstanovlenie standartnogo Provodnika Windows..." -ForegroundColor Cyan

Remove-Item -Path "HKCU:\Software\Classes\Folder\shell\open\command" -Force -ErrorAction SilentlyContinue
Write-Host "  [-] Papki (Folder)"
Remove-Item -Path "HKCU:\Software\Classes\Directory\shell\open\command" -Force -ErrorAction SilentlyContinue
Write-Host "  [-] Directory"
Remove-Item -Path "HKCU:\Software\Classes\Directory\Background\shell\open\command" -Force -ErrorAction SilentlyContinue
Write-Host "  [-] Fon papki (Directory\Background)"
Remove-Item -Path "HKCU:\Software\Classes\Drive\shell\open\command" -Force -ErrorAction SilentlyContinue
Write-Host "  [-] Diski (Drive)"

Write-Host ""
Write-Host "Gotovo! Standartnyj Provodnik vosstanovlen." -ForegroundColor Green