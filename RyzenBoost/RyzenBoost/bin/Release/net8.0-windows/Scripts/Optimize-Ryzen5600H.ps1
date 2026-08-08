#Requires -RunAsAdministrator
<#
.SINOPSIS
    Ajustes avanzados de sistema para mejorar rendimiento en equipos Windows.
    Este script es independiente de la app WPF: puedes auditarlo y ejecutarlo
    tú mismo con PowerShell si prefieres no usar la GUI.

.DESCRIPCION
    - Activa el plan de energía de "Rendimiento máximo".
    - Ajusta el "Processor performance boost mode" a Agresivo (2), que en
      CPUs Ryzen con AMD PPM (Precision Boost) suele mejorar la respuesta
      en cargas ráfaga como el arranque de shaders/streaming de Nanite/Lumen.
    - Activa el planificador CPPC preferido de núcleos (si el driver AMD
      lo soporta), dejando que Windows priorice los núcleos más rápidos
      del chip para el hilo del juego.
    - Deshabilita temporalmente Xbox Game DVR (graba en 2do plano, consume
      CPU/GPU/disco de forma silenciosa).

.NOTA
    Ningún ajuste aquí toca el ejecutable del juego ni su memoria. Todo es
    configuración estándar del sistema operativo, revertible con -Revert.
#>

param(
    [switch]$Revert
)

function Write-Step($msg) {
    Write-Host "==> $msg" -ForegroundColor Cyan
}

if (-not $Revert) {
    Write-Step "Activando plan de energía de rendimiento máximo..."
    powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 | Out-Null
    powercfg /setactive e9a42b02-d5df-448d-aa00-03f14749eb61

    Write-Step "Configurando 'Processor performance boost mode' en Agresivo..."
    # AC (enchufado)
    powercfg -setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2
    # DC (batería) -- se deja en modo 'Eficiente agresivo' (4) para no vaciar
    # la batería del portátil demasiado rápido en sesiones largas.
    powercfg -setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 4
    powercfg -setactive SCHEME_CURRENT

    Write-Step "Desactivando Xbox Game DVR (grabación en 2do plano)..."
    if (-not (Test-Path "HKCU:\System\GameConfigStore")) {
        New-Item -Path "HKCU:\System\GameConfigStore" -Force | Out-Null
    }
    Set-ItemProperty -Path "HKCU:\System\GameConfigStore" -Name "GameDVR_Enabled" -Value 0 -Type DWord
    New-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR" -Name "AllowGameDVR" -PropertyType DWord -Value 0 -Force | Out-Null

    Write-Step "Activando GPU Hardware-Accelerated Scheduling..."
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name "HwSchMode" -Value 2 -Type DWord

    Write-Host ""
    Write-Host "Listo. Algunos cambios (GPU scheduling) requieren reiniciar Windows." -ForegroundColor Green
}
else {
    Write-Step "Revirtiendo cambios..."
    powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e  # Equilibrado (por defecto en Windows)
    Set-ItemProperty -Path "HKCU:\System\GameConfigStore" -Name "GameDVR_Enabled" -Value 1 -Type DWord -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR" -Name "AllowGameDVR" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name "HwSchMode" -Value 1 -Type DWord -ErrorAction SilentlyContinue
    Write-Host "Cambios revertidos." -ForegroundColor Green
}
