@echo off
chcp 65001 > nul
echo.
"%~dp0FwDoctor.exe" diagnose
echo.
pause
