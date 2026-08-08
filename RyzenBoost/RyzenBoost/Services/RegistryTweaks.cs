using System;
using System.Security;
using Microsoft.Win32;

namespace RyzenBoost.Services
{
    /// <summary>
    /// Ajustes reales de registro de Windows. Todas las claves usadas aquí son
    /// oficiales y documentadas por Microsoft; no son hacks ni claves ocultas.
    /// El proceso ya corre elevado (ver app.manifest) así que HKLM es escribible.
    /// </summary>
    public static class RegistryTweaks
    {
        /// <summary>
        /// Habilita "GPU Hardware-accelerated Scheduling" (Windows 10 2004+/11).
        /// Reduce la latencia de render descargando el scheduling de la GPU
        /// del driver a un motor dedicado en el hardware (soportado por RTX 3060).
        /// Requiere reinicio de Windows para tomar efecto.
        /// </summary>
        public static bool EnableGpuHardwareScheduling(bool enable)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", writable: true);
                if (key == null) return false;
                key.SetValue("HwSchMode", enable ? 2 : 1, RegistryValueKind.DWord);
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (SecurityException) { return false; }
            catch { return false; }
        }

        public static int? GetGpuHardwareSchedulingState()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
                var val = key?.GetValue("HwSchMode");
                return val != null ? Convert.ToInt32(val) : (int?)null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Activa el Modo de Juego de Windows (Game Bar / Game Mode), que
        /// prioriza recursos de CPU/GPU para el juego en primer plano y
        /// suprime notificaciones y actualizaciones automáticas durante el juego.
        /// </summary>
        public static bool SetGameMode(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
                key.SetValue("AllowAutoGameMode", enable ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("AutoGameModeEnabled", enable ? 1 : 0, RegistryValueKind.DWord);
                return true;
            }
            catch { return false; }
        }

        public static bool? GetGameModeState()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
                var val = key?.GetValue("AllowAutoGameMode");
                return val != null ? Convert.ToInt32(val) == 1 : (bool?)null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Deshabilita el algoritmo de Nagle en las interfaces de red activas
        /// (TcpAckFrequency=1, TCPNoDelay=1). Reduce micro-latencia en juegos
        /// online. Efecto modesto y depende del router/ISP; se ofrece como
        /// ajuste opcional avanzado, no como milagro.
        /// </summary>
        public static int ApplyNetworkLatencyTweaks(bool enable)
        {
            int applied = 0;
            try
            {
                using var interfaces = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", writable: true);
                if (interfaces == null) return 0;

                foreach (var subKeyName in interfaces.GetSubKeyNames())
                {
                    using var iface = interfaces.OpenSubKey(subKeyName, writable: true);
                    if (iface == null) continue;
                    // Solo interfaces con IP asignada (evita tocar entradas vacías/plantilla).
                    if (iface.GetValue("DhcpIPAddress") == null && iface.GetValue("IPAddress") == null)
                        continue;

                    if (enable)
                    {
                        iface.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                        iface.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                    }
                    else
                    {
                        iface.DeleteValue("TcpAckFrequency", throwOnMissingValue: false);
                        iface.DeleteValue("TCPNoDelay", throwOnMissingValue: false);
                    }
                    applied++;
                }
            }
            catch { /* si falla, applied queda con lo que se alcanzó a aplicar */ }
            return applied;
        }

        public static int ApplyWiFiLatencyTweaks(bool enable)
        {
            return ApplyLatencyTweaksByType(enable, "Wireless");
        }

        public static int ApplyEthernetLatencyTweaks(bool enable)
        {
            return ApplyLatencyTweaksByType(enable, "Ethernet");
        }

        private static int ApplyLatencyTweaksByType(bool enable, string typePrefix)
        {
            int applied = 0;
            try
            {
                using var interfaces = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", writable: true);
                if (interfaces == null) return 0;

                foreach (var subKeyName in interfaces.GetSubKeyNames())
                {
                    using var iface = interfaces.OpenSubKey(subKeyName, writable: true);
                    if (iface == null) continue;
                    if (iface.GetValue("DhcpIPAddress") == null && iface.GetValue("IPAddress") == null)
                        continue;

                    var description = iface.GetValue("Description")?.ToString() ?? string.Empty;
                    var adapterType = iface.GetValue("ADAPTER_TYPE")?.ToString() ?? string.Empty;
                    var name = iface.GetValue("Name")?.ToString() ?? string.Empty;

                    if (!description.Contains(typePrefix, StringComparison.OrdinalIgnoreCase)
                        && !adapterType.Contains(typePrefix, StringComparison.OrdinalIgnoreCase)
                        && !name.Contains(typePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (enable)
                    {
                        iface.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                        iface.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                    }
                    else
                    {
                        iface.DeleteValue("TcpAckFrequency", throwOnMissingValue: false);
                        iface.DeleteValue("TCPNoDelay", throwOnMissingValue: false);
                    }
                    applied++;
                }
            }
            catch { }
            return applied;
        }
    }
}
