using System;
using System.Collections.Generic;
using RyzenBoost.Models;

namespace RyzenBoost.Services
{
    public class OptimizationLogEntry
    {
        public bool Ok;
        public string Text = "";
    }

    /// <summary>
    /// Orquesta todos los servicios de bajo nivel según la configuración del
    /// usuario. Es el único punto de entrada que llama la UI, así el backend
    /// queda desacoplado y testeable independientemente de WPF.
    /// </summary>
    public class Optimizer
    {
        private readonly AppSettings _settings;

        public Optimizer(AppSettings settings)
        {
            _settings = settings;
        }

        public List<OptimizationLogEntry> ApplyProfile()
        {
            var log = new List<OptimizationLogEntry>();

            if (_settings.OptPowerPlan)
            {
                _settings.PreviousPowerSchemeGuid = PowerManager.GetActiveSchemeGuid();
                var ok = PowerManager.ActivateMaxPerformance(out var planName);
                log.Add(new OptimizationLogEntry { Ok = ok, Text = $"Plan de energía: {planName}" });
            }

            if (_settings.OptGameMode)
            {
                var ok = RegistryTweaks.SetGameMode(true);
                log.Add(new OptimizationLogEntry { Ok = ok, Text = ok ? "Modo de Juego de Windows activado" : "No se pudo activar el Modo de Juego" });
            }

            if (_settings.OptGpuScheduling)
            {
                var ok = RegistryTweaks.EnableGpuHardwareScheduling(true);
                log.Add(new OptimizationLogEntry
                {
                    Ok = ok,
                    Text = ok
                        ? "GPU Hardware Scheduling activado (requiere reiniciar Windows para tomar efecto)"
                        : "No se pudo activar GPU Hardware Scheduling"
                });
            }

            if (_settings.OptProcessPriority)
            {
                var result = ProcessManager.ApplyGamePriority(_settings.TargetProcessName, _settings.UseHighPriorityInsteadOfAboveNormal);
                log.Add(new OptimizationLogEntry { Ok = result.Success, Text = result.Message });
            }

            if (_settings.OptStopBackgroundServices)
            {
                var report = ProcessManager.StopOptionalServices();
                _settings.ServicesWereStopped = report.Success.Count > 0;
                string text;
                if (report.Failed.Count > 0)
                {
                    text = $"Servicios pausados: {string.Join(", ", report.Success)}" +
                           $" | Fallaron: {string.Join(", ", report.Failed)}";
                }
                else if (report.Success.Count > 0)
                {
                    text = $"Servicios pausados: {string.Join(", ", report.Success)}";
                }
                else
                {
                    text = "No se encontraron servicios activos para pausar.";
                }
                log.Add(new OptimizationLogEntry
                {
                    Ok = report.Failed.Count == 0,
                    Text = text
                });
            }

            if (_settings.OptDisableStartupPrograms)
            {
                var disabled = StartupManager.DisableStartupEntries(out var startupMessage);
                if (disabled.Count > 0)
                {
                    _settings.DisabledStartupEntries.AddRange(disabled);
                }
                _settings.Save();
                log.Add(new OptimizationLogEntry
                {
                    Ok = !startupMessage.StartsWith("No se pudo", StringComparison.OrdinalIgnoreCase),
                    Text = startupMessage
                });
            }

            if (_settings.OptFortniteSettings)
            {
                var fortniteService = new FortniteOptimizerService();
                var result = fortniteService.ApplyOptimizationsAsync().GetAwaiter().GetResult();
                log.Add(new OptimizationLogEntry { Ok = result.Success, Text = result.Message });
            }

            if (_settings.OptNetworkTweaks)
            {
                var count = RegistryTweaks.ApplyNetworkLatencyTweaks(true);
                log.Add(new OptimizationLogEntry { Ok = count > 0, Text = $"Ajustes de latencia de red aplicados a {count} interfaz(es)" });
            }

            if (_settings.OptNetworkWireless)
            {
                var wirelessCount = RegistryTweaks.ApplyWiFiLatencyTweaks(true);
                log.Add(new OptimizationLogEntry { Ok = wirelessCount > 0, Text = $"Ajustes de Wi-Fi aplicados a {wirelessCount} interfaz(es) inalámbricas" });
            }

            if (_settings.OptNetworkEthernet)
            {
                var ethernetCount = RegistryTweaks.ApplyEthernetLatencyTweaks(true);
                log.Add(new OptimizationLogEntry { Ok = ethernetCount > 0, Text = $"Ajustes de Ethernet aplicados a {ethernetCount} interfaz(es) cableadas" });
            }

            _settings.Save();
            return log;
        }

        public List<OptimizationLogEntry> RevertProfile()
        {
            var log = new List<OptimizationLogEntry>();

            if (!string.IsNullOrEmpty(_settings.PreviousPowerSchemeGuid))
            {
                var ok = PowerManager.RestoreScheme(_settings.PreviousPowerSchemeGuid);
                log.Add(new OptimizationLogEntry { Ok = ok, Text = "Plan de energía restaurado al original" });
            }

            RegistryTweaks.SetGameMode(false);
            log.Add(new OptimizationLogEntry { Ok = true, Text = "Modo de Juego revertido" });

            RegistryTweaks.EnableGpuHardwareScheduling(false);
            log.Add(new OptimizationLogEntry { Ok = true, Text = "GPU Hardware Scheduling revertido" });

            if (_settings.ServicesWereStopped)
            {
                var report = ProcessManager.StartOptionalServices();
                _settings.ServicesWereStopped = false;
                log.Add(new OptimizationLogEntry { Ok = report.Failed.Count == 0, Text = $"Servicios reiniciados: {string.Join(", ", report.Success)}" +
                                                                 (report.Failed.Count > 0 ? $" | Fallaron: {string.Join(", ", report.Failed)}" : "") });
            }

            if (_settings.DisabledStartupEntries.Count > 0)
            {
                var restored = StartupManager.RestoreStartupEntries(_settings.DisabledStartupEntries, out var restoreMessage);
                _settings.DisabledStartupEntries.Clear();
                log.Add(new OptimizationLogEntry { Ok = restored.Count > 0, Text = restoreMessage });
            }

            if (_settings.OptFortniteSettings)
            {
                var fortniteService = new FortniteOptimizerService();
                var result = fortniteService.RestoreDefaultsAsync().GetAwaiter().GetResult();
                log.Add(new OptimizationLogEntry { Ok = result.Success, Text = result.Message });
            }

            RegistryTweaks.ApplyNetworkLatencyTweaks(false);
            log.Add(new OptimizationLogEntry { Ok = true, Text = "Ajustes de red revertidos" });

            if (_settings.OptNetworkWireless)
            {
                var wirelessCount = RegistryTweaks.ApplyWiFiLatencyTweaks(false);
                log.Add(new OptimizationLogEntry { Ok = true, Text = $"Ajustes de Wi-Fi revertidos en {wirelessCount} interfaz(es)" });
            }

            if (_settings.OptNetworkEthernet)
            {
                var ethernetCount = RegistryTweaks.ApplyEthernetLatencyTweaks(false);
                log.Add(new OptimizationLogEntry { Ok = true, Text = $"Ajustes de Ethernet revertidos en {ethernetCount} interfaz(es)" });
            }

            _settings.Save();
            return log;
        }
    }
}
