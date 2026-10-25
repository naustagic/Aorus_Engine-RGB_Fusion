@echo off
chcp 65001 > nul
echo.
"%~dp0FwDoctor.exe" monitor
echo.
pause
