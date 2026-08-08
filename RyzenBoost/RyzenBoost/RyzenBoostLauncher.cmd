@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%RyzenBoostLauncher.ps1"

if not exist "%PS_SCRIPT%" (
    echo No se encontro el launcher PowerShell.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"
if %errorlevel% neq 0 pause
