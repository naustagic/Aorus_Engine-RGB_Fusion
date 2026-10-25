@echo off
chcp 65001 > nul
cd /d "%~dp0"
echo.
echo ====================================================================
echo  ATENCAO: Este procedimento ira gravar o firmware original F1.4
echo  e restaurar a inicializacao do Water Cooler.
echo ====================================================================
echo.
"%~dp0FwDoctor.exe" recover
echo.
pause
