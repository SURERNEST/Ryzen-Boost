param()

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $scriptDir 'bin\Release\net8.0-windows\RyzenBoost.exe'

function Write-ErrorLine($text) {
    Write-Host $text -ForegroundColor Red
}

function IsAdministrator {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($current)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function DotNetInstalled {
    try {
        & dotnet --version > $null 2>&1
        return $true
    } catch {
        return $false
    }
}

function IsWingetInstalled {
    try {
        & where.exe winget > $null 2>&1
        return $true
    } catch {
        return $false
    }
}

function InstallDotNetRuntime {
    if (-not (IsWingetInstalled)) {
        return $false
    }
    Write-Host 'dotnet no está instalado. Intentando instalar .NET 8 Desktop Runtime con winget...'
    & winget install --id Microsoft.DotNet.DesktopRuntime.8 --silent --accept-package-agreements --accept-source-agreements > $null 2>&1
    return IsDotNetInstalled
}

if (-not (IsDotNetInstalled)) {
    if (-not (InstallDotNetRuntime)) {
        Write-ErrorLine 'No se encontró dotnet. Instala .NET 8 Runtime o SDK desde https://dot.net/.'
        exit 1
    }
}

$runtimeInstalled = $false
try {
    $runtimes = & dotnet --list-runtimes 2>$null
    $runtimeInstalled = $runtimes -match 'Microsoft\.WindowsDesktop\.App\s+8\.'
} catch {
    $runtimeInstalled = $false
}

if (-not $runtimeInstalled) {
    Write-Host '.NET 8 Desktop Runtime no está presente. Intentando instalarlo con winget...'
    if (-not (InstallDotNetRuntime)) {
        Write-ErrorLine 'Falta .NET 8 Desktop Runtime. Instala manualmente desde https://dot.net/.'
        exit 1
    }
    try {
        $runtimes = & dotnet --list-runtimes 2>$null
        $runtimeInstalled = $runtimes -match 'Microsoft\.WindowsDesktop\.App\s+8\.'
    } catch {
        $runtimeInstalled = $false
    }
    if (-not $runtimeInstalled) {
        Write-ErrorLine 'Falta .NET 8 Desktop Runtime. Instala manualmente desde https://dot.net/.'
        exit 1
    }
}

if (-not (Test-Path $exe)) {
    Write-Host 'No se encontró el ejecutable compilado. Compilando el proyecto...'
    Push-Location $scriptDir
    dotnet build -c Release | Write-Host
    $buildStatus = $LASTEXITCODE
    Pop-Location
    if ($buildStatus -ne 0) {
        Write-ErrorLine 'La compilación falló. Abre el proyecto en Visual Studio o ejecuta dotnet build -c Release manualmente.'
        exit 1
    }
    if (-not (Test-Path $exe)) {
        Write-ErrorLine 'No se generó el ejecutable después de compilar.'
        exit 1
    }
}

$admin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Write-Host 'Elevando permisos a administrador...'
    Start-Process -FilePath $exe -Verb RunAs -WorkingDirectory $scriptDir
    exit 0
}

Start-Process -FilePath $exe -WorkingDirectory $scriptDir
