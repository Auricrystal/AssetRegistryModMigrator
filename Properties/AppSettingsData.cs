using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetRegistryModMigrator.Properties
{
    public class AppSettingsData
    {
        // Paths
        public string DonorAssetRegistryFile { get; set; } = string.Empty;
        public string ReceiverAssetRegistryFile { get; set; } = string.Empty;
        public string DefaultExportDirectory { get; set; } = string.Empty;

        // Behavior & Safety
        public bool AutoBackupBeforeInject { get; set; } = true;
        public bool VerifyRoundtripOnExport { get; set; } = false;
        public bool ConfirmClearStagingQueue { get; set; } = true;

        // UI & Display
        public bool ExpandTreeOnSearch { get; set; } = true;
        public bool ShowRawJsonByDefault { get; set; } = false;
    }

    public static class AppSettingsManager
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AssetRegistryModMigrator");

        public static string SettingsFilePath { get; } = Path.Combine(SettingsFolder, "settings.json");

        private static AppSettingsData? _current;
        public static AppSettingsData Current => _current ??= Load();

        public static AppSettingsData Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var loaded = JsonConvert.DeserializeObject<AppSettingsData>(json);
                    if (loaded != null)
                    {
                        _current = loaded;
                        return _current;
                    }
                }
            }
            catch
            {
                // Fall back to defaults if JSON is corrupted
            }

            _current = new AppSettingsData();
            Save();
            return _current;
        }

        public static void Save()
        {
            Directory.CreateDirectory(SettingsFolder);
            string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json);
        }

        public static void ResetToDefaults()
        {
            _current = new AppSettingsData();
            Save();
        }
    }
}