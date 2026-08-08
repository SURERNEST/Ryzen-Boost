using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;

namespace RyzenBoost.Services
{
    /// <summary>
    /// Ajusta la prioridad de planificación del proceso del juego usando las
    /// APIs públicas de Windows (Process.PriorityClass / ProcessorAffinity).
    /// IMPORTANTE: esto NO abre el proceso para leer/escribir su memoria, no
    /// inyecta código y no interactúa con el anti-cheat. Es exactamente lo
    /// mismo que hacer clic derecho -> Detalles -> Establecer prioridad en el
    /// Administrador de tareas, solo que automatizado.
    /// </summary>
    public static class ProcessManager
    {
        public static Process? FindProcess(string processName)
        {
            var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
            return Process.GetProcessesByName(name).FirstOrDefault();
        }

        public class PriorityResult
        {
            public bool Success;
            public string Message = "";
        }

        /// <summary>
        /// Por defecto dejamos los hilos lógicos disponibles sin tocar la afinidad.
        /// que Windows use todos los hilos (afinidad completa = más seguro y
        /// generalmente más rápido que restringir núcleos manualmente); solo
        /// se sube la prioridad de planificación del hilo del juego.
        /// </summary>
        public static PriorityResult ApplyGamePriority(string processName, bool useHighInsteadOfAboveNormal)
        {
            var result = new PriorityResult();
            try
            {
                var proc = FindProcess(processName);
                if (proc == null)
                {
                    result.Success = false;
                    result.Message = $"'{processName}' no está en ejecución todavía. " +
                                      "Abre el juego y vuelve a aplicar, o activa el modo automático.";
                    return result;
                }

                proc.PriorityClass = useHighInsteadOfAboveNormal
                    ? ProcessPriorityClass.High
                    : ProcessPriorityClass.AboveNormal;

                // Afinidad: todos los 12 hilos lógicos disponibles del 5600H.
                long allCores = (1L << Environment.ProcessorCount) - 1;
                proc.ProcessorAffinity = (IntPtr)allCores;

                result.Success = true;
                result.Message = $"Prioridad establecida a " +
                    $"{(useHighInsteadOfAboveNormal ? "Alta" : "Sobre lo normal")} " +
                    $"para PID {proc.Id} ({Environment.ProcessorCount} hilos habilitados).";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"No se pudo ajustar: {ex.Message}";
            }
            return result;
        }

        // Servicios de Windows que consumen CPU/disco en segundo plano y que
        // pueden pausarse temporalmente durante sesiones de juego intensivas.
        // Se documentan sus efectos para que el usuario decida con información real.
        public static readonly Dictionary<string, string> OptionalServices = new()
        {
            { "SysMain", "Superfetch/Prefetch: precarga apps en RAM. Pausarlo libera CPU/disco pero apps abrirán algo más lento después." },
            { "WSearch", "Indexado de búsqueda de Windows. Pausarlo libera E/S de disco en segundo plano." },
        };

        public class ServiceToggleReport
        {
            public List<string> Success = new();
            public List<string> Failed = new();
        }

        public static ServiceToggleReport StopOptionalServices()
        {
            var report = new ServiceToggleReport();
            foreach (var svcName in OptionalServices.Keys)
            {
                try
                {
                    using var sc = new ServiceController(svcName);
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        report.Success.Add(svcName);
                    }
                }
                catch
                {
                    report.Failed.Add(svcName);
                }
            }
            return report;
        }

        public static ServiceToggleReport StartOptionalServices()
        {
            var report = new ServiceToggleReport();
            foreach (var svcName in OptionalServices.Keys)
            {
                try
                {
                    using var sc = new ServiceController(svcName);
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                        report.Success.Add(svcName);
                    }
                }
                catch
                {
                    report.Failed.Add(svcName);
                }
            }
            return report;
        }
    }
}
