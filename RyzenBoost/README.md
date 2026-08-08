# RyzenBoost

RyzenBoost is a Windows desktop optimizer built with C# / WPF on .NET 8. It applies safe, reversible system tweaks to improve responsiveness, reduce background load, and help gaming sessions run more smoothly.

> This project is intended for local use on computers you own or manage. It does not collect personal data or require any external account.

## What it does
- Monitors CPU, RAM, and GPU usage in real time.
- Applies optional performance tweaks for Windows power plans, Game Mode, GPU scheduling, process priority, network latency, startup-program disabling, and memory trimming.
- Includes an optional Fortnite-specific module that edits GameUserSettings.ini in a conservative way and keeps a backup.

## Privacy and safety
- The app does not collect personal data or upload telemetry by default.
- It stores only local settings under the user profile.
- The Fortnite optimizer only writes to the local GameUserSettings.ini file and keeps a backup before modifying it.
- The project is intended for local, user-controlled optimization only.

## Build
From the project folder:

```powershell
dotnet build -c Release
```

## Notes
- Some optimizations require administrator privileges.
- Some changes, such as GPU scheduling, may require a system restart to take effect.
- The app should be used responsibly and only on systems you control.
