using AssetRegistryModMigrator.Model;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Community.CsharpSqlite.Sqlite3;

namespace AssetRegistryModMigrator.Windows
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window, INotifyPropertyChanged
    {
        public ObservableCollection<SettingsNode> RootTree { get; set; }
        private IEnumerable<SettingPayload>? _activeSettingsSource;
        private string? _activeHeaderName;

        // The exact list of items the right panel will draw
        public IEnumerable<SettingPayload>? ActiveSettingsSource
        {
            get => _activeSettingsSource;
            set { _activeSettingsSource = value; OnPropertyChanged(); }
        }

        // The text string displayed at the top of the right panel
        public string? ActiveHeaderName
        {
            get => _activeHeaderName;
            set { _activeHeaderName = value; OnPropertyChanged(); }
        }
        public Settings()
        {
            RootTree = SettingsNode.BuildSettingsTree();

            InitializeComponent();
            this.Show();
            SelectFirstItemOnLoad();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Owner = Application.Current.MainWindow;
            //Application.Current.MainWindow.Closing += Owner_Closing;
            Title = $"ARMM Settings";

        }

        private void SettingsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is SettingsNode selectedFolder)
            {
                // Scenario A: The clicked folder has immediate settings inside it
                if (selectedFolder.Settings != null && selectedFolder.Settings.Any())
                {
                    ActiveSettingsSource = selectedFolder.Settings;
                    ActiveHeaderName = selectedFolder.Name;
                }
                // Scenario B: The clicked folder is an empty structural category. 
                // We hunt down the tree branch for the first nested folder containing settings.
                else
                {
                    SettingsNode? firstTargetWithData = FindFirstFolderWithSettings(selectedFolder);

                    if (firstTargetWithData != null)
                    {
                        ActiveSettingsSource = firstTargetWithData.Settings;
                        ActiveHeaderName = firstTargetWithData.Name;
                    }
                    else
                    {
                        // Clean fallback if the entire folder branch is empty
                        ActiveSettingsSource = null;
                        ActiveHeaderName = selectedFolder.Name;
                    }
                }
            }

        }
        // Helper method to look deep into collapsed sub-folders without forcing the tree to expand
        private SettingsNode? FindFirstFolderWithSettings(SettingsNode node)
        {
            if (node.Settings != null && node.Settings.Any())
            {
                return node;
            }

            foreach (var childFolder in node.Children)
            {
                var match = FindFirstFolderWithSettings(childFolder);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void SelectFirstItemOnLoad()
        {
            // Safety check to ensure the tree actually has data
            if (RootTree != null && RootTree.Count > 0)
            {
                // Grab the absolute first folder at the root level
                SettingsNode firstRootNode = RootTree[0];

                // Wait for the UI thread to finish rendering the TreeView items 
                // so WPF can find the container and select it.
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new System.Action(() =>
                {
                    // Find the visual container for the first item
                    var treeViewItem = SettingsTree.ItemContainerGenerator
                        .ContainerFromItem(firstRootNode) as TreeViewItem;

                    if (treeViewItem != null)
                    {
                        // Focus and Select the item in the UI
                        treeViewItem.IsSelected = true;
                        treeViewItem.Focus();
                    }
                }));
            }
        }

    }
    
}
