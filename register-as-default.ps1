$exe = "D:\Fylo\bin\Release\net8.0-windows\Fylo.exe"

Write-Host "Registratsiya Fylo kak defoltnogo provodnika..." -ForegroundColor Cyan

# Folder (papki)
New-Item -Path "HKCU:\Software\Classes\Folder\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\Folder\shell\open\command" -Name "(default)" -Value "`"$exe`" `"%1`""
Set-ItemProperty -Path "HKCU:\Software\Classes\Folder\shell\open\command" -Name "DelegateExecute" -Value ""
Write-Host "  [+] Papki (Folder)"

# Directory
New-Item -Path "HKCU:\Software\Classes\Directory\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\Directory\shell\open\command" -Name "(default)" -Value "`"$exe`" `"%1`""
Set-ItemProperty -Path "HKCU:\Software\Classes\Directory\shell\open\command" -Name "DelegateExecute" -Value ""
Write-Host "  [+] Directory"

# Directory\Background (fon)
New-Item -Path "HKCU:\Software\Classes\Directory\Background\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\Directory\Background\shell\open\command" -Name "(default)" -Value "`"$exe`" `"%V`""
Write-Host "  [+] Fon papki (Directory\Background)"

# Drive (diski)
New-Item -Path "HKCU:\Software\Classes\Drive\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\Drive\shell\open\command" -Name "(default)" -Value "`"$exe`" `"%1`""
Set-ItemProperty -Path "HKCU:\Software\Classes\Drive\shell\open\command" -Name "DelegateExecute" -Value ""
Write-Host "  [+] Diski (Drive)"

Write-Host ""
Write-Host "Gotovo! Fylo teper budet otkryvatsya vmesto Provodnika." -ForegroundColor Green
Write-Host "Chtoby otmenit izmeneniya, zapustite restore-default-explorer.ps1" -ForegroundColor Yellow