@echo off
echo Diagnostic WinSpaces en cours, merci de patienter (environ 30 secondes)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0diagnostic-windows.ps1"
echo.
pause
