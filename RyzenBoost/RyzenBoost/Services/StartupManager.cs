using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using RyzenBoost.Models;

namespace RyzenBoost.Services
{
    public static class StartupManager
    {
        private static readonly (RegistryHive Hive, string Path)[] StartupLocations =
        {
            (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
            (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
        };

        private static string BackupRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RyzenBoost",
            "StartupBackups");

        public static List<StartupEntry> DisableStartupEntries(out string message)
        {
            var disabled = new List<StartupEntry>();
            try
            {
                Directory.CreateDirectory(BackupRoot);

                foreach (var (hive, path) in StartupLocations)
                {
                    foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    {
                        using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(path, writable: true);
                        if (key == null) continue;

                        foreach (var valueName in key.GetValueNames())
                        {
                            var valueData = key.GetValue(valueName)?.ToString() ?? string.Empty;
                            if (!ShouldDisableRegistryValue(valueData)) continue;

                            var valueKind = key.GetValueKind(valueName).ToString();
                            disabled.Add(new StartupEntry
                            {
                                EntryType = "Registry",
                                Hive = hive.ToString(),
                                View = view.ToString(),
                                KeyPath = path,
                                ValueName = valueName,
                                ValueData = valueData,
                                ValueKind = valueKind,
                            });
                            key.DeleteValue(valueName, false);
                        }
                    }
                }

                foreach (var folder in GetStartupFolders())
                {
                    if (!Directory.Exists(folder)) continue;

                    foreach (var file in Directory.GetFiles(folder))
                    {
                        if (!ShouldDisableStartupFile(file)) continue;

                        var fileName = Path.GetFileName(file);
                        var backupPath = GetUniqueBackupPath(fileName);
                        File.Move(file, backupPath);

                        disabled.Add(new StartupEntry
                        {
                            EntryType = "StartupFolder",
                            KeyPath = folder,
                            ValueName = fileName,
                            ValueData = file,
                            ValueKind = Path.GetExtension(file),
                            SourcePath = file,
                            BackupPath = backupPath,
                        });
                    }
                }

                message = disabled.Count > 0 ?
                    $"Se deshabilitaron {disabled.Count} entrada(s) de inicio automático (registro y carpeta de inicio)." :
                    "No se encontraron entradas de inicio automático que había preconfiguradas para deshabilitar.";
                return disabled;
            }
            catch (Exception ex)
            {
                message = $"No se pudo modificar el inicio automático: {ex.Message}";
                return disabled;
            }
        }

        public static List<StartupEntry> RestoreStartupEntries(List<StartupEntry> entries, out string message)
        {
            var restored = new List<StartupEntry>();
            try
            {
                foreach (var entry in entries)
                {
                    if (entry.EntryType.Equals("StartupFolder", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(entry.BackupPath) && File.Exists(entry.BackupPath))
                        {
                            if (!File.Exists(entry.ValueData))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(entry.ValueData)!);
                                File.Move(entry.BackupPath, entry.ValueData);
                            }
                            restored.Add(entry);
                        }
                        continue;
                    }

                    if (!Enum.TryParse<RegistryHive>(entry.Hive, out var hive)) continue;
                    if (!Enum.TryParse<RegistryView>(entry.View, out var view)) continue;
                    using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(entry.KeyPath, writable: true);
                    if (key == null) continue;

                    if (entry.ValueKind.Equals("String", StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(entry.ValueName, entry.ValueData, RegistryValueKind.String);
                    }
                    else if (entry.ValueKind.Equals("ExpandString", StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(entry.ValueName, entry.ValueData, RegistryValueKind.ExpandString);
                    }
                    else if (entry.ValueKind.Equals("DWord", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(entry.ValueData, out var intValue))
                        {
                            key.SetValue(entry.ValueName, intValue, RegistryValueKind.DWord);
                        }
                    }
                    else
                    {
                        key.SetValue(entry.ValueName, entry.ValueData);
                    }
                    restored.Add(entry);
                }

                message = restored.Count > 0 ?
                    $"Se restauraron {restored.Count} entrada(s) de inicio automático." :
                    "No había entradas de inicio automático para restaurar.";
                return restored;
            }
            catch (Exception ex)
            {
                message = $"No se pudo restaurar el inicio automático: {ex.Message}";
                return restored;
            }
        }

        private static IEnumerable<string> GetStartupFolders()
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        }

        private static bool ShouldDisableRegistryValue(string valueData)
        {
            if (string.IsNullOrWhiteSpace(valueData)) return false;

            var lower = valueData.ToLowerInvariant();
            if (lower.Contains(@"\windows\") || lower.Contains(@"%systemroot%") || lower.Contains(@"%windir%"))
            {
                return false;
            }

            return lower.Contains(".exe") || lower.Contains(".cmd") || lower.Contains(".bat") || lower.Contains(".ps1") || lower.Contains(".lnk") || lower.Contains(".appref-ms");
        }

        private static bool ShouldDisableStartupFile(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension is ".lnk" or ".url" or ".cmd" or ".bat" or ".ps1" or ".exe" or ".appref-ms";
        }

        private static string GetUniqueBackupPath(string fileName)
        {
            var candidate = Path.Combine(BackupRoot, fileName);
            var index = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(BackupRoot, $"{Path.GetFileNameWithoutExtension(fileName)}_{index}{Path.GetExtension(fileName)}");
                index++;
            }
            return candidate;
        }
    }
}
