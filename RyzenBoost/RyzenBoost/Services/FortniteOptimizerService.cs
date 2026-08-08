using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RyzenBoost.Services
{
    public sealed class FortniteOptimizationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string BackupPath { get; init; } = string.Empty;
    }

    public class FortniteOptimizerService
    {
        private readonly string? _overridePath;

        public FortniteOptimizerService(string? overridePath = null)
        {
            _overridePath = overridePath;
        }

        public Task<FortniteOptimizationResult> ApplyOptimizationsAsync(CancellationToken cancellationToken = default)
        {
            return ApplyOptimizationsAsync(ResolveTargetPath(), cancellationToken);
        }

        public async Task<FortniteOptimizationResult> ApplyOptimizationsAsync(string? targetPath, CancellationToken cancellationToken = default)
        {
            string path = targetPath ?? ResolveTargetPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return new FortniteOptimizationResult
                {
                    Success = false,
                    Message = "No se pudo determinar la ruta de GameUserSettings.ini."
                };
            }

            try
            {
                if (IsFortniteRunning())
                {
                    return new FortniteOptimizationResult
                    {
                        Success = false,
                        Message = "Fortnite está abierto en este momento. Se omite la edición para evitar conflictos y se aplicará al siguiente arranque."
                    };
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (!File.Exists(path))
                {
                    await File.WriteAllTextAsync(path, string.Empty, Encoding.UTF8, cancellationToken);
                }

                var originalAttributes = File.GetAttributes(path);
                var readOnly = (originalAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
                if (readOnly)
                {
                    File.SetAttributes(path, originalAttributes & ~FileAttributes.ReadOnly);
                }

                var backupPath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".bak");
                if (File.Exists(path) && !File.Exists(backupPath))
                {
                    File.Copy(path, backupPath, overwrite: false);
                }

                var originalContent = await File.ReadAllTextAsync(path, cancellationToken);
                var updatedContent = UpdateIniContent(originalContent);

                await File.WriteAllTextAsync(path, updatedContent, Encoding.UTF8, cancellationToken);

                if (readOnly)
                {
                    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                }

                return new FortniteOptimizationResult
                {
                    Success = true,
                    Message = $"Se aplicaron optimizaciones de Fortnite en {Path.GetFileName(path)}.",
                    FilePath = path,
                    BackupPath = backupPath
                };
            }
            catch (Exception ex)
            {
                return new FortniteOptimizationResult
                {
                    Success = false,
                    Message = $"No se pudieron aplicar las optimizaciones de Fortnite: {ex.Message}"
                };
            }
        }

        public async Task<FortniteOptimizationResult> RestoreDefaultsAsync(CancellationToken cancellationToken = default)
        {
            string path = _overridePath ?? ResolveTargetPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return new FortniteOptimizationResult
                {
                    Success = false,
                    Message = "No se pudo determinar la ruta de GameUserSettings.ini."
                };
            }

            try
            {
                var backupPath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".bak");
                if (File.Exists(backupPath))
                {
                    var originalAttributes = File.GetAttributes(path);
                    if ((originalAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(path, originalAttributes & ~FileAttributes.ReadOnly);
                    }

                    using var source = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                    await source.CopyToAsync(destination, cancellationToken);

                    if ((originalAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                    }

                    return new FortniteOptimizationResult
                    {
                        Success = true,
                        Message = "Se restauraron los valores originales de Fortnite desde la copia de seguridad.",
                        FilePath = path,
                        BackupPath = backupPath
                    };
                }

                return new FortniteOptimizationResult
                {
                    Success = false,
                    Message = "No se encontró una copia de seguridad previa para restaurar."
                };
            }
            catch (Exception ex)
            {
                return new FortniteOptimizationResult
                {
                    Success = false,
                    Message = $"No se pudieron restaurar los valores de Fortnite: {ex.Message}"
                };
            }
        }

        private static string ResolveTargetPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");
        }

        private static bool IsFortniteRunning()
        {
            try
            {
                return Process.GetProcessesByName("FortniteClient-Win64-Shipping").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string UpdateIniContent(string originalContent)
        {
            var lineEnding = originalContent.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = originalContent.Replace("\r\n", "\n").Replace("\r", "\n").Split(new[] { "\n" }, StringSplitOptions.None);
            var output = new List<string>();
            var targetSection = "/Script/FortniteGame.FortniteGameUserSettings";
            var scalabilitySection = "ScalabilityGroups";

            var targetValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bEnableAnisotropicFiltering"] = "False",
                ["bShowNetDebugStats"] = "False",
                ["bShowVoiceChatNotification"] = "False",
                ["bMotionBlur"] = "False",
                ["bShowGrass"] = "False"
            };

            var scalabilityValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sg.ViewDistanceQuality"] = "0",
                ["sg.AntiAliasingQuality"] = "0",
                ["sg.ShadowQuality"] = "0",
                ["sg.GlobalIlluminationQuality"] = "0",
                ["sg.ReflectionQuality"] = "0",
                ["sg.PostProcessQuality"] = "0",
                ["sg.TextureQuality"] = "0",
                ["sg.EffectsQuality"] = "0",
                ["sg.FoliageQuality"] = "0",
                ["sg.ShadingQuality"] = "0"
            };

            var currentSection = string.Empty;
            var targetSectionSeen = false;
            var scalabilitySectionSeen = false;
            var handledTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var handledScalability = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in lines)
            {
                var line = rawLine;
                if (TryParseSectionHeader(line, out var section))
                {
                    if (currentSection.Equals(targetSection, StringComparison.OrdinalIgnoreCase) && !targetSectionSeen)
                    {
                        targetSectionSeen = true;
                    }
                    if (currentSection.Equals(scalabilitySection, StringComparison.OrdinalIgnoreCase) && !scalabilitySectionSeen)
                    {
                        scalabilitySectionSeen = true;
                    }

                    if (currentSection.Equals(targetSection, StringComparison.OrdinalIgnoreCase) && targetValues.Count > 0)
                    {
                        AppendMissingValues(output, targetValues, handledTargets, lineEnding);
                    }
                    else if (currentSection.Equals(scalabilitySection, StringComparison.OrdinalIgnoreCase) && scalabilityValues.Count > 0)
                    {
                        AppendMissingValues(output, scalabilityValues, handledScalability, lineEnding);
                    }

                    currentSection = section;
                    output.Add(line);
                    continue;
                }

                if (currentSection.Equals(targetSection, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseKeyValue(line, out var key, out var value))
                    {
                        if (targetValues.TryGetValue(key, out var newValue))
                        {
                            output.Add($"{key}={newValue}");
                            handledTargets.Add(key);
                            continue;
                        }
                    }
                }
                else if (currentSection.Equals(scalabilitySection, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseKeyValue(line, out var key, out var value))
                    {
                        if (scalabilityValues.TryGetValue(key, out var newValue))
                        {
                            output.Add($"{key}={newValue}");
                            handledScalability.Add(key);
                            continue;
                        }
                    }
                }

                output.Add(line);
            }

            if (currentSection.Equals(targetSection, StringComparison.OrdinalIgnoreCase))
            {
                AppendMissingValues(output, targetValues, handledTargets, lineEnding);
            }
            else if (currentSection.Equals(scalabilitySection, StringComparison.OrdinalIgnoreCase))
            {
                AppendMissingValues(output, scalabilityValues, handledScalability, lineEnding);
            }

            if (!targetSectionSeen)
            {
                if (output.Count > 0 && output[^1] != string.Empty)
                {
                    output.Add(string.Empty);
                }
                output.Add($"[{targetSection}]");
                AppendMissingValues(output, targetValues, handledTargets, lineEnding);
            }

            if (!scalabilitySectionSeen)
            {
                if (output.Count > 0 && output[^1] != string.Empty)
                {
                    output.Add(string.Empty);
                }
                output.Add($"[{scalabilitySection}]");
                AppendMissingValues(output, scalabilityValues, handledScalability, lineEnding);
            }

            return string.Join(lineEnding, output.Where(x => x != null))
                .TrimEnd('\r', '\n');
        }

        private static void AppendMissingValues(List<string> output, Dictionary<string, string> targetValues, HashSet<string> handled, string lineEnding)
        {
            foreach (var kvp in targetValues)
            {
                if (handled.Contains(kvp.Key)) continue;
                output.Add($"{kvp.Key}={kvp.Value}");
                handled.Add(kvp.Key);
            }
        }

        private static bool TryParseSectionHeader(string line, out string section)
        {
            section = string.Empty;
            var trimmed = line.Trim();
            if (trimmed.Length >= 2 && trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                section = trimmed.Substring(1, trimmed.Length - 2);
                return true;
            }
            return false;
        }

        private static bool TryParseKeyValue(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(";", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0)
            {
                return false;
            }

            key = trimmed[..equalsIndex].Trim();
            value = trimmed[(equalsIndex + 1)..].Trim();
            return true;
        }
    }
}
