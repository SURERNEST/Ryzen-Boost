# RyzenBoost

A modern Windows desktop optimizer built with C# and WPF on .NET 8. RyzenBoost helps improve system responsiveness by applying safe, reversible tweaks for performance, memory, startup behavior, and gaming-focused workflows.

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows-blue" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8-512BD4" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Language-C%23-239120" alt="C#" />
  <img src="https://img.shields.io/badge/License-MIT-green" alt="MIT License" />
</p>

> RyzenBoost is designed for local use on systems you own or manage. It does not collect personal data or require any external account.

## Overview

RyzenBoost combines a lightweight desktop interface with a set of system-level optimizations that can help reduce background overhead, improve responsiveness, and make gaming or productivity sessions feel smoother. The project is built around a modular architecture so each optimization can be applied independently and reverted cleanly.

## Why RyzenBoost?

- Improve responsiveness during gaming and heavy multitasking
- Reduce unnecessary background load from startup applications and services
- Apply performance-oriented tweaks in a controlled and reversible way
- Keep the experience local-first, transparent, and privacy-conscious

## Features

- Real-time monitoring of CPU, RAM, and GPU usage
- Optional optimizations for power plans, Game Mode, GPU scheduling, process priority, network latency, startup program control, and memory trimming
- A conservative Fortnite-specific module that updates the local GameUserSettings.ini file while preserving the rest of the user configuration
- Safe and reversible behavior using local backups before changing system settings

## Privacy and Safety

RyzenBoost is built with user control and privacy in mind:

- No telemetry or cloud-based data collection by default
- Settings are stored locally under the user profile
- The Fortnite module only touches the local configuration file and keeps a backup before applying changes
- The tool is intended for user-controlled optimization and should be used responsibly

## Requirements

- Windows operating system
- .NET 8 Desktop Runtime or SDK
- Administrator privileges may be required for some adjustments

## Getting Started

From the project directory, run:

```powershell
dotnet build -c Release
```

Then launch the generated application from the build output folder.

## Project Structure

```text
RyzenBoost/
├── Models/
├── Services/
├── Scripts/
├── Assets/
├── Resources/
├── MainWindow.xaml
├── MainWindow.xaml.cs
└── RyzenBoost.csproj
```

## License

This project is licensed under the [MIT License](LICENSE).
