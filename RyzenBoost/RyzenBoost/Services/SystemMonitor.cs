using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

namespace RyzenBoost.Services
{
    public class SystemSnapshot
    {
        public float CpuUsagePercent = -1f;
        public float RamUsedGb = -1f;
        public float RamTotalGb = -1f;
        public string CpuName = "";
        public string GpuName = "";
        public float GpuUsagePercent = -1f;
        public int LogicalProcessors = 0;
    }

    /// <summary>
    /// Lee datos reales del sistema usando API nativa para CPU y memoria,
    /// y WMI como respaldo cuando no haya otra opción.
    /// </summary>
    public class SystemMonitor : IDisposable
    {
        private readonly PerformanceCounter? _cpuCounter;
        private readonly PerformanceCounter? _availableMemoryCounter;
        private string? _cpuName;
        private string? _gpuName;
        private readonly bool _systemTimesAvailable;
        private ulong _lastIdleTime;
        private ulong _lastKernelTime;
        private ulong _lastUserTime;

        public SystemMonitor()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // primera lectura siempre da 0, se descarta
            }
            catch
            {
                _cpuCounter = null;
            }

            try
            {
                _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                _availableMemoryCounter.NextValue();
            }
            catch
            {
                _availableMemoryCounter = null;
            }

            _systemTimesAvailable = TryInitializeCpuTimes(out _lastIdleTime, out _lastKernelTime, out _lastUserTime);
        }

        public SystemSnapshot Read()
        {
            _cpuName ??= QueryWmi("Win32_Processor", "Name");
            _gpuName ??= QueryWmi("Win32_VideoController", "Name");

            float cpuPercent = GetCpuUsagePercent();
            (float totalGb, float availGb) = ReadMemoryGb();

            return new SystemSnapshot
            {
                CpuUsagePercent = cpuPercent,
                RamUsedGb = totalGb - availGb,
                RamTotalGb = totalGb,
                CpuName = _cpuName,
                GpuName = _gpuName,
                GpuUsagePercent = GetGpuUsagePercent(),
                LogicalProcessors = Environment.ProcessorCount
            };
        }

        private string QueryWmi(string wmiClass, string property)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var val = obj[property]?.ToString();
                    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                }
            }
            catch { }
            return "No disponible";
        }

        private float GetCpuUsagePercent()
        {
            if (_systemTimesAvailable)
            {
                var value = GetCpuUsageFromSystemTimes();
                if (value >= 0) return value;
            }

            if (_cpuCounter != null)
            {
                try
                {
                    var value = _cpuCounter.NextValue();
                    return Math.Clamp(value, 0, 100);
                }
                catch { }
            }

            return GetCpuLoadPercentageFromWmi();
        }

        private float GetCpuUsageFromSystemTimes()
        {
            if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            {
                return -1;
            }

            ulong idle = FileTimeToUInt64(idleTime);
            ulong kernel = FileTimeToUInt64(kernelTime);
            ulong user = FileTimeToUInt64(userTime);
            ulong system = kernel + user;

            ulong prevSystem = _lastKernelTime + _lastUserTime;
            ulong systemDelta = system - prevSystem;
            ulong idleDelta = idle - _lastIdleTime;

            _lastIdleTime = idle;
            _lastKernelTime = kernel;
            _lastUserTime = user;

            if (systemDelta == 0) return -1;

            var usage = (float)((systemDelta - idleDelta) * 100.0 / systemDelta);
            return Math.Clamp(usage, 0, 100);
        }

        private bool TryInitializeCpuTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime)
        {
            idleTime = kernelTime = userTime = 0;
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                idleTime = FileTimeToUInt64(idle);
                kernelTime = FileTimeToUInt64(kernel);
                userTime = FileTimeToUInt64(user);
                return true;
            }
            return false;
        }

        private static ulong FileTimeToUInt64(FILETIME time)
        {
            return ((ulong)time.dwHighDateTime << 32) | time.dwLowDateTime;
        }

        private (float totalGb, float availGb) ReadMemoryGb()
        {
            float totalGb = GetTotalPhysicalMemoryGb();
            float availGb = GetAvailableMemoryGb();

            if (IsValidMemory(totalGb, availGb))
            {
                return (totalGb, availGb);
            }

            var fallback = ReadMemoryFromOperatingSystem();
            if (IsValidMemory(fallback.totalGb, fallback.availGb))
            {
                return fallback;
            }

            return (totalGb, Math.Max(0, availGb));
        }

        private bool IsValidMemory(float totalGb, float availGb)
        {
            return totalGb > 0 && availGb >= 0 && availGb <= totalGb;
        }

        private (float totalGb, float availGb) ReadMemoryFromOperatingSystem()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var totalKb = obj["TotalVisibleMemorySize"]?.ToString();
                    var freeKb = obj["FreePhysicalMemory"]?.ToString();
                    if (double.TryParse(totalKb, out var totalKbValue) && double.TryParse(freeKb, out var freeKbValue))
                    {
                        float totalGbFallback = (float)(totalKbValue / 1024.0 / 1024.0);
                        float availGbFallback = (float)(freeKbValue / 1024.0 / 1024.0);
                        return (totalGbFallback, availGbFallback);
                    }
                }
            }
            catch { }
            return (0f, -1f);
        }

        private float GetTotalPhysicalMemoryGb()
        {
            if (TryGetMemoryStatus(out var totalBytes, out _))
            {
                return totalBytes / 1024f / 1024f / 1024f;
            }

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var value = obj["TotalPhysicalMemory"]?.ToString();
                    if (double.TryParse(value, out var bytes))
                    {
                        return (float)(bytes / 1024.0 / 1024.0 / 1024.0);
                    }
                }
            }
            catch { }
            return 0;
        }

        private float GetAvailableMemoryGb()
        {
            if (_availableMemoryCounter != null)
            {
                try
                {
                    float availableMb = _availableMemoryCounter.NextValue();
                    if (availableMb > 0)
                    {
                        return availableMb / 1024f;
                    }
                }
                catch { }
            }

            if (TryGetMemoryStatus(out var totalBytes, out var availBytes))
            {
                return availBytes / 1024f / 1024f / 1024f;
            }

            return -1;
        }

        private bool TryGetMemoryStatus(out ulong totalBytes, out ulong availBytes)
        {
            totalBytes = 0;
            availBytes = 0;

            try
            {
                var mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
                if (GlobalMemoryStatusEx(ref mem))
                {
                    totalBytes = mem.ullTotalPhys;
                    availBytes = mem.ullAvailPhys;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private float GetCpuLoadPercentageFromWmi()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var value = obj["LoadPercentage"]?.ToString();
                    if (float.TryParse(value, out var percent))
                    {
                        return Math.Clamp(percent, 0, 100);
                    }
                }
            }
            catch { }
            return -1;
        }

        private float GetGpuUsagePercent()
        {
            float gpuPercent = GetGpuPerfCounterUsage();
            if (gpuPercent >= 0) return gpuPercent;

            gpuPercent = GetGpuUsageFromNvidiaSmi();
            if (gpuPercent >= 0) return gpuPercent;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT UtilizationPercentage FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var value = obj["UtilizationPercentage"]?.ToString();
                    if (float.TryParse(value, out var percent))
                    {
                        return Math.Clamp(percent, 0, 100);
                    }
                }
            }
            catch { }
            return -1;
        }

        private float GetGpuUsageFromNvidiaSmi()
        {
            foreach (var candidate in GetNvidiaSmiCandidates())
            {
                try
                {
                    using var process = new Process();
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = "--query-gpu=utilization.gpu --format=csv,noheader,nounits",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    if (!process.Start())
                    {
                        continue;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);

                    if (process.ExitCode != 0)
                    {
                        continue;
                    }

                    foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length == 0) continue;

                        var firstValue = trimmed.Split(',')[0].Trim();
                        if (float.TryParse(firstValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                        {
                            return Math.Clamp(percent, 0, 100);
                        }
                    }
                }
                catch
                {
                    // Ignore and try the next candidate.
                }
            }

            return -1;
        }

        private static IEnumerable<string> GetNvidiaSmiCandidates()
        {
            yield return "nvidia-smi.exe";
            yield return Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        }

        private float GetGpuPerfCounterUsage()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
                float totalUsage = 0;
                int count = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    var value = obj["UtilizationPercentage"]?.ToString();
                    if (float.TryParse(value, out var percent))
                    {
                        totalUsage += percent;
                        count++;
                    }
                }
                if (count > 0) return Math.Clamp(totalUsage / count, 0, 100);
            }
            catch { }
            return -1;
        }

        public void Dispose()
        {
            _cpuCounter?.Dispose();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }
    }
}
