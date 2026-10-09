@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\run.ps1" -Check %*
if errorlevel 1 pause
