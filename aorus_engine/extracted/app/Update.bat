@echo off
set curdir=%~dp0
cd /d %curdir%

cd GvTemp

flash.exe /p

cd..

start AORUS.exe
