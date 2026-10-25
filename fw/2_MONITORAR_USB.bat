@echo off
chcp 65001 > nul
cd /d "%~dp0"
echo.
"%~dp0FwDoctor.exe" monitor
echo.
pause
