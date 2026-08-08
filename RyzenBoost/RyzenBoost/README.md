# RyzenBoost

**RyzenBoost** es un optimizador de rendimiento para Windows enfocado en laptops y PCs AMD Ryzen. Automatiza ajustes seguros y reversibles en energía, GPU, memoria y procesos para mejorar la capacidad de respuesta durante sesiones de juego y cargas intensivas.

## Descripción del repositorio

`Windows performance optimizer for Ryzen laptops, automating safe power, GPU, memory and startup tuning with .NET 8 WPF.`

## Qué hace RyzenBoost

- Establece el plan de energía en **Rendimiento máximo**
- Habilita **GPU Hardware Accelerated Scheduling** de forma segura
- Controla el **Modo de Juego de Windows** y los procesos de segundo plano
- Libera memoria periódicamente sin congelar el sistema
- Deshabilita programas de inicio para reducir la carga de arranque
- Monitorea **CPU, RAM y GPU** en tiempo real con una interfaz nativa
- Guarda configuración persistente y permite revertir todos los cambios

## Qué no hace

- No modifica ni inyecta código en juegos
- No rompe o evade anticheats como Easy Anti-Cheat
- No aumenta el rendimiento físico más allá de lo que permite tu hardware
- No es una herramienta de trampa; es un afinador de sistema operativo

## Arquitectura

```text
RyzenBoost/
├── App.xaml / App.xaml.cs
│   └── Inicializa la aplicación y verifica permisos de administrador
├── MainWindow.xaml / MainWindow.xaml.cs
│   └── Lógica de interfaz y control de estado
├── app.manifest
│   └── Forza UAC (`requireAdministrator`) para aplicar ajustes de sistema
├── Models/AppSettings.cs
│   └── Configuración persistente en JSON
├── Services/
│   ├── Optimizer.cs
│   │   └── Orquesta aplicación y reversión de optimizaciones
│   ├── PowerManager.cs
│   │   └── Gestiona `powercfg` y planes de energía
│   ├── RegistryTweaks.cs
│   │   └── Ajusta GPU, Game Mode y red via registro
│   ├── ProcessManager.cs
│   │   └── Administra prioridad, afinidad y servicios en segundo plano
│   ├── SystemMonitor.cs
│   │   └── Recupera métricas de sistema en vivo
│   ├── MemoryManager.cs
│   │   └── Libera working set para reducir huella de memoria
│   └── StartupManager.cs
│       └── Deshabilita / restaura entradas de inicio
├── Scripts/Optimize-Ryzen5600H.ps1
│   └── Script independiente para aplicar o revertir ajustes
└── Assets/
    └── Recursos de UI y sonidos
```

## Requisitos

- Windows 10 o Windows 11
- .NET 8 SDK
- Visual Studio 2022 con workload de escritorio .NET o `dotnet` CLI

## Compilar y ejecutar

```powershell
cd RyzenBoost\RyzenBoost
dotnet build -c Release
dotnet run --project RyzenBoost.csproj
```

O abre el proyecto en Visual Studio y ejecuta desde allí. El UAC es esperado porque la app necesita permisos elevados para aplicar ajustes de sistema.

## Uso recomendado

1. Ejecuta la aplicación como administrador.
2. Selecciona el perfil de optimización que quieres aplicar.
3. Activa las opciones de memoria y programas de inicio si deseas mayor liberación de recursos.
4. Reinicia Windows tras habilitar GPU Hardware Scheduling.

## Notas clave

- GPU Hardware Scheduling puede requerir reiniciar el equipo para activarse.
- La prioridad de procesos solo se aplica correctamente cuando el juego o la aplicación ya está en ejecución.
- Los ajustes de red son avanzados y tienen efecto variable según tu hardware y configuración.
- No hay garantía de FPS: RyzenBoost alinea el sistema, pero el límite real depende de CPU/GPU y temperaturas.

## Revertir cambios

Desde la aplicación:

- Ajustes → Revertir todos los cambios

O desde PowerShell:

```powershell
cd RyzenBoost\RyzenBoost
.\Scripts\Optimize-Ryzen5600H.ps1 -Revert
```

## Contribuir

- Abre un issue antes de enviar un pull request para discutir cambios relevantes.
- Mantén la mentalidad de seguridad, reversibilidad y estabilidad.
- Usa `dotnet format` y `dotnet build -c Release` antes de proponer cambios.

## Licencia

Añade una licencia como `MIT`, `Apache-2.0` o la que prefieras antes de publicar.
