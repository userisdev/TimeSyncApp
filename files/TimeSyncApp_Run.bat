@echo off
setlocal

title TimeSyncApp

chcp 65001 > NUL

sudo %~dp0TimeSyncApp.exe

endlocal
exit /b 0
