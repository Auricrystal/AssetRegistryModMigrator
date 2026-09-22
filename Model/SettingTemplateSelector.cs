using IronPython.Compiler.Ast;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace AssetRegistryModMigrator.Model
{

    public class SettingTemplateSelector : DataTemplateSelector
    {
        public required DataTemplate FolderTemplate { get; set; }
        public required DataTemplate FilePathTemplate { get; set; }
        public required DataTemplate StringTemplate { get; set; }
        public required DataTemplate BooleanTemplate { get; set; }
        public required DataTemplate EnumTemplate { get; set; }
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            // 1. Check if the item is our new grouped setting payload structure
            if (item is SettingPayload payload)
            {
                // Rule A: Check for File Path settings based on key name rules
                // (e.g., if the setting key ends with "Path", "File", or "Dir")
                if (payload.FullSettingsKey != null &&
                    (payload.FullSettingsKey.EndsWith("Path", StringComparison.OrdinalIgnoreCase) ||
                     payload.FullSettingsKey.EndsWith("Dir", StringComparison.OrdinalIgnoreCase) ||
                     payload.FullSettingsKey.EndsWith("File", StringComparison.OrdinalIgnoreCase)))
                {
                    return FilePathTemplate;
                }

                // Rule B: Check the runtime type of the underlying Value object
                if (payload.Value is bool)
                {
                    return BooleanTemplate;
                }

                if (payload.Value is string)
                {
                    return StringTemplate;
                }

                // Fallback: If Value is null, check the default settings type definition
                // via the Settings property entry if needed. Defaulting to String template.
                return StringTemplate;
            }

            // 2. Fallback for the TreeView itself if any raw elements leak through
            if (item is SettingsNode)
            {
                return FolderTemplate;
            }

            return base.SelectTemplate(item, container);
        }
        //    public override DataTemplate SelectTemplate(object item, DependencyObject container)
        //    {
        //        if (item is SettingPayload node)
        //        {
        //            // 1. If it has children, it's a folder/category
        //            if (!node.IsSetting)
        //                return FolderTemplate;

        //            // 2. Otherwise, check the underlying data type
        //            Type type = node.Settings?.PropertyType ?? typeof(string);

        //            if (type == typeof(bool))
        //                return BooleanTemplate;

        //            if (type.IsEnum)
        //                return EnumTemplate;

        //            if (type == typeof(string))
        //            {
        //                // Option A: Check if the setting name ends with "Path" or "File"
        //                if (node.Name.EndsWith("Path") || node.Name.EndsWith("File"))
        //                {
        //                    return FilePathTemplate;
        //                }

        //                // Default fallback for strings, numbers, etc.
        //                return StringTemplate;
        //            }
        //        }

        //        return base.SelectTemplate(item, container);
        //    }
        //}
    }
    public class SettingsNode : INotifyPropertyChanged
    {
        public required string Name { get; set; }

        public ObservableCollection<SettingsNode> Children { get; set; } = [];

        public ObservableCollection<SettingPayload> Settings { get; set; } = [];
        // Shortcut to identify if this is a leaf node (an actual setting)
        public bool IsSetting => Settings != null;

        public IEnumerable<SettingPayload> AllDescendantPayloads
        {
            get
            {
                // First gather the concrete settings tied directly to this folder level
                foreach (var setting in Settings)
                {
                    yield return setting;
                }

                foreach (var childFolder in Children)
                {
                    foreach (var descendantSetting in childFolder.AllDescendantPayloads)
                    {
                        yield return descendantSetting;
                    }
                }
            }
        }

        //// Directly bind your UI controls to this property for live updates
        //public object? Value
        //{
        //    get => IsSetting ? AssetRegistryModMigrator.Properties.Settings.Default[Settings?.Name] : null;
        //    set
        //    {
        //        if (IsSetting)
        //        {
        //            AssetRegistryModMigrator.Properties.Settings.Default[Settings?.Name] = value;
        //            AssetRegistryModMigrator.Properties.Settings.Default.Save(); // Optional: Auto-save on change

        //            // Notify the UI that the value changed
        //            OnPropertyChanged();
        //        }
        //    }
        //}

        public static ObservableCollection<SettingsNode> BuildSettingsTree()
        {
            // Virtual root anchor to hold all paths, will discard at the end
            SettingsNode virtualRoot = new() { Name = "Root" };

            foreach (SettingsProperty prop in AssetRegistryModMigrator.Properties.Settings.Default.Properties)
            {
                string[] tokens = prop.Name.Split('_');
                if (tokens[0].lower() == "hidden") continue;//ignore hidden prefixed settings e.g. Hidden_SomeSetting

                SettingsNode currentFolder = virtualRoot;

                for (int i = 0; i < tokens.Length - 1; i++)
                {
                    string token = tokens[i];

                    //bool isLast = (i == tokens.Length - 1);
                    // Check if a child node with the same value already exists
                    SettingsNode? existingChild = currentFolder?.Children?.FirstOrDefault(c => c.Name == token);

                    if (existingChild == null)
                    {
                        // If it doesn't exist, create it and link it
                        SettingsNode newChild = new() { Name = token };
                        currentFolder?.Children!.Add(newChild);
                        currentFolder = newChild;
                    }
                    else
                    {
                        // Move into the existing node prop
                        currentFolder = existingChild;
                    }

                }
                // 2. Attach the setting payload into the terminal folder
                if (currentFolder != null)
                {
                    var payload = new SettingPayload
                    {
                        FullSettingsKey = prop.Name,       // "Graphics_Textures_Quality" -> for backend storage
                        Name = tokens.Last()     // "Quality" -> for UI layout text labels
                    };
                    Debug.WriteLine($"{payload.FullSettingsKey} : {payload.Name}");
                    currentFolder.Settings.Add(payload);
                }


            }

            // Discard the root and return the list of primary nodes
            return virtualRoot.Children;
        }
        // Settings Changed Event required to force the TextBox UI to refresh
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    // Simple RelayCommand implementation if you aren't using a MVVM framework
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    public class SettingPayload : INotifyPropertyChanged
    {
        // Name is just the final word (e.g., "Quality") shown next to the input box
        public required string Name { get; set; }

        // FullSettingsKey stores the original string (e.g., "Graphics_Textures_Quality")
        // used to pull/save from Properties.Settings.Default
        public required string FullSettingsKey { get; set; }

        // The dynamic property your CheckBoxes and TextBoxes bind to
        public object? Value
        {
            get
            {
                if (string.IsNullOrEmpty(FullSettingsKey)) return null;
                return Properties.Settings.Default[FullSettingsKey];
            }
            set
            {
                if (!string.IsNullOrEmpty(FullSettingsKey))
                {
                    Properties.Settings.Default[FullSettingsKey] = value;
                    Properties.Settings.Default.Save(); // Auto-saves changes instantly
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ICommand? _browseFileCommand;
        private string _lastdirectory { get; set; } = Directory.GetCurrentDirectory();
        public ICommand BrowseFileCommand
        {
            get
            {
                return _browseFileCommand ??= new RelayCommand(_ =>
                {
                    OpenFileDialog openFileDialog = new OpenFileDialog() { FileName = "AssetRegistry", Filter = "Json Files (.json)|*.json|Bin Files (.bin)|*.bin", DefaultDirectory = _lastdirectory };
                    // Set the currentFolder value as the initial file if it's already a valid path
                    if (Value is string currentPath && !string.IsNullOrEmpty(currentPath))
                    {
                        openFileDialog.FileName = currentPath;
                    }

                    if (openFileDialog.ShowDialog() == true)
                    {
                        Value = openFileDialog.FileName;
                        // Force the UI to refresh if you implement INotifyPropertyChanged
                    }
                });
            }
        }

    }
    public class FolderIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int childCount)
            {
                // If it has children, it's a folder container.
                // If it has 0 children, it's the penultimate selectable setting leaf.
                return childCount > 0 ? "📁" : "⚙️";
            }
            return "📁";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

