using AssetRegistryModMigrator.Classes;
using AssetRegistryModMigrator.Properties;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace AssetRegistryModMigrator.Windows
{
    public enum JsonSettingKind { Boolean, FilePath, FolderPath, String }

    public class JsonSettingItem :INotifyPropertyChanged
    {
        public string Key { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public JsonSettingKind Kind { get; init; }
        public string FileFilter { get; init; } = "All Files (*.*)|*.*";

        private bool _boolValue;
        public bool BoolValue
        {
            get => _boolValue;
            set { _boolValue = value; OnPropertyChanged(); }
        }

        private string _stringValue = string.Empty;
        public string StringValue
        {
            get => _stringValue;
            set { _stringValue = value; OnPropertyChanged(); }
        }

        public Action<AppSettingsData>? ApplyToModel { get; set; }
        public Action<AppSettingsData>? ReadFromModel { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SettingsCategoryNode
    {
        public string Icon { get; init; } = "📁";
        public string Name { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public ObservableCollection<SettingsCategoryNode> SubCategories { get; } = new();
        public List<JsonSettingItem> Items { get; } = new();
    }

    public class JsonSettingTemplateSelector :DataTemplateSelector
    {
        public DataTemplate? BooleanTemplate { get; set; }
        public DataTemplate? PathTemplate { get; set; }
        public DataTemplate? StringTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is JsonSettingItem setting)
            {
                return setting.Kind switch
                {
                    JsonSettingKind.Boolean => BooleanTemplate,
                    JsonSettingKind.FilePath or JsonSettingKind.FolderPath => PathTemplate,
                    _ => StringTemplate
                };
            }
            return base.SelectTemplate(item, container);
        }
    }

    public partial class Settings :Window, INotifyPropertyChanged
    {
        public ObservableCollection<SettingsCategoryNode> Categories { get; } = new();
        public string SettingsFilePath => AppSettingsManager.SettingsFilePath;

        private IEnumerable<JsonSettingItem>? _visibleSettings;
        public IEnumerable<JsonSettingItem>? VisibleSettings
        {
            get => _visibleSettings;
            set { _visibleSettings = value; OnPropertyChanged(); }
        }

        private string _activeCategoryTitle = string.Empty;
        public string ActiveCategoryTitle
        {
            get => _activeCategoryTitle;
            set { _activeCategoryTitle = value; OnPropertyChanged(); }
        }

        private string _activeCategorySubtitle = string.Empty;
        public string ActiveCategorySubtitle
        {
            get => _activeCategorySubtitle;
            set { _activeCategorySubtitle = value; OnPropertyChanged(); }
        }

        private SettingsCategoryNode? _selectedCategory;

        public Settings()
        {
            BuildCategories();
            InitializeComponent();
            DataContext = this;
            LoadValuesFromDisk();
            Show();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Owner = Application.Current.MainWindow;

            if (Categories.Any())
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (SettingsTree.ItemContainerGenerator.ContainerFromItem(Categories[0]) is TreeViewItem tvi)
                    {
                        tvi.IsSelected = true;
                        tvi.Focus();
                    }
                });
            }
        }

        private void BuildCategories()
        {
            var pathsCategory = new SettingsCategoryNode
            {
                Icon = "📂",
                Name = "Registry Paths",
                Subtitle = "Default AssetRegistry.bin files and export directories"
            };
            var donorPathItem = new JsonSettingItem
            {
                Key = nameof(AppSettingsData.DonorAssetRegistryFile),
                Title = "Default Donor AssetRegistry",
                Description = "Path to the source AssetRegistry.bin loaded automatically on startup.",
                Kind = JsonSettingKind.FilePath,
                FileFilter = "AssetRegistry Binary (*.bin)|*.bin|All Files (*.*)|*.*"
            };
            donorPathItem.ReadFromModel = m => donorPathItem.StringValue = m.DonorAssetRegistryFile;
            donorPathItem.ApplyToModel = m => m.DonorAssetRegistryFile = donorPathItem.StringValue;

            var receiverPathItem = new JsonSettingItem
            {
                Key = nameof(AppSettingsData.ReceiverAssetRegistryFile),
                Title = "Default Receiver AssetRegistry",
                Description = "Path to the target mod AssetRegistry.bin that receives injected assets.",
                Kind = JsonSettingKind.FilePath,
                FileFilter = "AssetRegistry Binary (*.bin)|*.bin|All Files (*.*)|*.*"
            };
            receiverPathItem.ReadFromModel = m => receiverPathItem.StringValue = m.ReceiverAssetRegistryFile;
            receiverPathItem.ApplyToModel = m => m.ReceiverAssetRegistryFile = receiverPathItem.StringValue;

            var exportDirItem = new JsonSettingItem
            {
                Key = nameof(AppSettingsData.DefaultExportDirectory),
                Title = "Default Export Folder",
                Description = "Initial directory used when compiling .json back into .bin.",
                Kind = JsonSettingKind.FolderPath
            };
            exportDirItem.ReadFromModel = m => exportDirItem.StringValue = m.DefaultExportDirectory;
            exportDirItem.ApplyToModel = m => m.DefaultExportDirectory = exportDirItem.StringValue;

            pathsCategory.Items.AddRange(new[] { donorPathItem, receiverPathItem, exportDirItem });

            var migrationCategory = new SettingsCategoryNode
            {
                Icon = "🛡️",
                Name = "Migration & Safety",
                Subtitle = "Backup, validation, and queue confirmation preferences"
            };

            var autoBackup = new JsonSettingItem
            {
                Key = nameof(AppSettingsData.AutoBackupBeforeInject),
                Title = "Auto-Backup Receiver Before Export",
                Description = "Creates a timestamped .bak copy of the Receiver AssetRegistry before overwriting.",
                Kind = JsonSettingKind.Boolean
            };
            autoBackup.ReadFromModel = m => autoBackup.BoolValue = m.AutoBackupBeforeInject;
            autoBackup.ApplyToModel = m => m.AutoBackupBeforeInject = autoBackup.BoolValue;

            var verifyExport = new JsonSettingItem
            {
                Key = nameof(AppSettingsData.VerifyRoundtripOnExport),
                Title = "Verify Binary Roundtrip on Export",
                Description = "Runs a semantic validation check after compiling .json to .bin.",
                Kind = JsonSettingKind.Boolean
            };
            verifyExport.ReadFromModel = m => verifyExport.BoolValue = m.VerifyRoundtripOnExport;
            verifyExport.ApplyToModel = m => m.VerifyRoundtripOnExport = verifyExport.BoolValue;

            var confirmClear = new JsonSettingItem
            {
                Key = nameof(AppSettingsData.ConfirmClearStagingQueue),
                Title = "Confirm Before Clearing Edited Staging Items",
                Description = "Prompts for confirmation if you clear staged items that have unsaved or local edits.",
                Kind = JsonSettingKind.Boolean
            };
            confirmClear.ReadFromModel = m => confirmClear.BoolValue = m.ConfirmClearStagingQueue;
            confirmClear.ApplyToModel = m => m.ConfirmClearStagingQueue = confirmClear.BoolValue;

            migrationCategory.Items.AddRange(new[] { autoBackup, verifyExport, confirmClear });

            var uiCategory = new SettingsCategoryNode
            {
                Icon = "🎨",
                Name = "Interface & Inspector",
                Subtitle = "Tree navigation and package property window defaults"
            };
            var expandSearch = new JsonSettingItem
            {
                Key = nameof(AppSettingsData.ExpandTreeOnSearch),
                Title = "Auto-Expand Matching Folders on Search",
                Description = "Automatically expands folder branches containing matching search tokens.",
                Kind = JsonSettingKind.Boolean
            };
            expandSearch.ReadFromModel = m => expandSearch.BoolValue = m.ExpandTreeOnSearch;
            expandSearch.ApplyToModel = m => m.ExpandTreeOnSearch = expandSearch.BoolValue;

            var showJson = new JsonSettingItem
            {
                Key = nameof(AppSettingsData.ShowRawJsonByDefault),
                Title = "Expand Raw JSON Payload by Default",
                Description = "Opens the Raw JSON expander automatically when inspecting a package.",
                Kind = JsonSettingKind.Boolean
            };
            showJson.ReadFromModel = m => showJson.BoolValue = m.ShowRawJsonByDefault;
            showJson.ApplyToModel = m => m.ShowRawJsonByDefault = showJson.BoolValue;

            uiCategory.Items.AddRange(new[] { expandSearch, showJson });
            Categories.Add(pathsCategory);
            Categories.Add(migrationCategory);
            Categories.Add(uiCategory);
        }
        private IEnumerable<JsonSettingItem> GetAllSettings() =>
            Categories.SelectMany(c => c.Items.Concat(c.SubCategories.SelectMany(s => s.Items)));

        private void LoadValuesFromDisk()
        {
            var data = AppSettingsManager.Load();
            foreach (var item in GetAllSettings())
                item.ReadFromModel?.Invoke(data);
        }
        private void SettingsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is SettingsCategoryNode node)
            {
                _selectedCategory = node;
                CategorySearchBox.Text = string.Empty;
                ActiveCategoryTitle = node.Name;
                ActiveCategorySubtitle = node.Subtitle;
                VisibleSettings = node.Items;
            }
        }
        private void CategorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = CategorySearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                if (_selectedCategory != null)
                {
                    ActiveCategoryTitle = _selectedCategory.Name;
                    ActiveCategorySubtitle = _selectedCategory.Subtitle;
                    VisibleSettings = _selectedCategory.Items;
                }
                return;
            }
            ActiveCategoryTitle = $"Search Results for \"{query}\"";
            ActiveCategorySubtitle = "Matching settings across all categories";
            VisibleSettings = GetAllSettings()
            .Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            s.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        }
        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not JsonSettingItem setting)
                return;
            if (setting.Kind == JsonSettingKind.FilePath)
            {
                var dialog = new OpenFileDialog
                {
                    Title = $"Select {setting.Title}",
                    Filter = setting.FileFilter,
                    FileName = File.Exists(setting.StringValue) ? Path.GetFileName(setting.StringValue) : string.Empty,
                    InitialDirectory = File.Exists(setting.StringValue) ? Path.GetDirectoryName(setting.StringValue) : null
                };
                if (dialog.ShowDialog() == true)
                    setting.StringValue = dialog.FileName;
            }
            else if (setting.Kind == JsonSettingKind.FolderPath)
            {
                var dialog = new OpenFolderDialog
                {
                    Title = $"Select {setting.Title}",
                    InitialDirectory = Directory.Exists(setting.StringValue) ? setting.StringValue : null
                };
                if (dialog.ShowDialog() == true)
                    setting.StringValue = dialog.FolderName;
            }
        }
        private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
        {
            AppSettingsManager.Save();
            Process.Start("explorer.exe", $"/select,\"{AppSettingsManager.SettingsFilePath}\"");
        }
        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = DarkMessageBox.Show(
            "Reset all settings to their default values?",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                AppSettingsManager.ResetToDefaults();
                LoadValuesFromDisk();
            }
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var data = AppSettingsManager.Current;
            foreach (var item in GetAllSettings())
                item.ApplyToModel?.Invoke(data);
            AppSettingsManager.Save();
            Close();
        }
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}