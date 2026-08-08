namespace RyzenBoost.Models
{
    public class StartupEntry
    {
        public string EntryType { get; set; } = "Registry";
        public string Hive { get; set; } = string.Empty;
        public string View { get; set; } = string.Empty;
        public string KeyPath { get; set; } = string.Empty;
        public string ValueName { get; set; } = string.Empty;
        public string ValueData { get; set; } = string.Empty;
        public string ValueKind { get; set; } = "String";
        public string SourcePath { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
    }
}
