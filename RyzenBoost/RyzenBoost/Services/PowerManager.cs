using System;
using System.Diagnostics;

namespace RyzenBoost.Services
{
    /// <summary>
    /// Controla el plan de energía de Windows usando powercfg.exe (herramienta
    /// nativa de Windows). No hay nada simulado: esto ejecuta el mismo binario
    /// que usarías desde la consola, con el proceso ya elevado por el manifest.
    /// </summary>
    public static class PowerManager
    {
        // GUID estándar de Windows para "Alto rendimiento"
        private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        // GUID estándar de Windows para "Rendimiento máximo" (Ultimate Performance,
        // disponible en Windows 10/11 Pro y superior; en Home puede no existir).
        private const string UltimatePerformanceGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        public static string? RunPowercfg(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return output;
        }

        public static string GetActiveSchemeGuid()
        {
            var output = RunPowercfg("/getactivescheme") ?? string.Empty;
            // Formato típico: "Esquema de energía actual: GUID  (Nombre)"
            var parts = output.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (Guid.TryParse(part, out var g)) return g.ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// Intenta activar "Rendimiento máximo"; si el plan no existe en el
        /// sistema (Windows Home), lo duplica primero. Si tampoco es posible,
        /// cae a "Alto rendimiento", que siempre está disponible.
        /// </summary>
        public static bool ActivateMaxPerformance(out string appliedPlanName)
        {
            try
            {
                var dup = RunPowercfg($"-duplicatescheme {UltimatePerformanceGuid}");
                var setResult = RunPowercfg($"/setactive {UltimatePerformanceGuid}");
                if (GetActiveSchemeGuid().Equals(UltimatePerformanceGuid, StringComparison.OrdinalIgnoreCase))
                {
                    appliedPlanName = "Rendimiento máximo (Ultimate Performance)";
                    return true;
                }
            }
            catch { /* seguimos al plan alterno */ }

            try
            {
                RunPowercfg($"/setactive {HighPerformanceGuid}");
                appliedPlanName = "Alto rendimiento";
                return GetActiveSchemeGuid().Equals(HighPerformanceGuid, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                appliedPlanName = $"Error: {ex.Message}";
                return false;
            }
        }

        public static bool RestoreScheme(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return false;
            try
            {
                RunPowercfg($"/setactive {guid}");
                return true;
            }
            catch { return false; }
        }
    }
}
