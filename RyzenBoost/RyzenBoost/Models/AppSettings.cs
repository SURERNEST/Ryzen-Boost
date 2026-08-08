using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RyzenBoost.Models
{
    /// <summary>
    /// Configuración persistida del usuario. Se guarda en
    /// %AppData%\RyzenBoost\settings.json
    /// </summary>
    public class AppSettings
    {
        public bool SoundEnabled { get; set; } = true;
        public bool DarkTheme { get; set; } = true;
        public string TargetProcessName { get; set; } = "FortniteClient-Win64-Shipping";
        public bool UseHighPriorityInsteadOfAboveNormal { get; set; } = false;

        public bool OptPowerPlan { get; set; } = true;
        public bool OptGameMode { get; set; } = true;
        public bool OptGpuScheduling { get; set; } = true;
        public bool OptProcessPriority { get; set; } = true;
        public bool OptStopBackgroundServices { get; set; } = false;
        public bool OptNetworkTweaks { get; set; } = false;
        public bool OptNetworkWireless { get; set; } = false;
        public bool OptNetworkEthernet { get; set; } = false;
        public bool OptDisableStartupPrograms { get; set; } = false;
        public bool OptMemoryTrim { get; set; } = false;
        public bool OptFortniteSettings { get; set; } = false;
        public List<StartupEntry> DisabledStartupEntries { get; set; } = new();
        // Guarda el estado ANTERIOR de servicios/registro para poder revertir.
        public string? PreviousPowerSchemeGuid { get; set; }
        public bool ServicesWereStopped { get; set; } = false;

        private static string FilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RyzenBoost", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch
            {
                // Si el archivo está corrupto, se ignora y se usan valores por defecto.
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath)!;
                Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // No es crítico si falla el guardado; la app sigue funcionando en memoria.
            }
        }
    }
}
