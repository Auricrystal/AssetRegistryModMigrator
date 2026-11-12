using AssetRegistryModMigrator.Classes;
using AssetRegistryModMigrator.Model;
using AssetRegistryModMigrator.Properties;
using AssetRegistryModMigrator.Windows;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;namespace AssetRegistryModMigrator
{
    public partial class MainWindow :Window
    {
        private AssetRegistry? DonorRegistry { get; set; }
        private AssetRegistry? ReceiverRegistry { get; set; }
        private StagingSet StagingQueue { get; set; } = new StagingSet();

        private readonly AssetRegistryService _registryService = new();
        private static AppSettingsData SaveData => AppSettingsManager.Current;
        private readonly Dictionary<string, Window> _openPropertyWindows = new(StringComparer.OrdinalIgnoreCase);
        public enum InspectionSource
        {
            Donor,
            Staging,
            Receiver
        }
        public MainWindow()
        {
            InitializeComponent();

#if DEBUG
            VerifyBinaryRoundtripMenuItem.Click += VerifyBinaryRoundtrip_Click;
            VerifyJsonRoundtripMenuItem.Click += VerifyJsonRoundtrip_Click;
#else
            DebugVerifyMenu.Visibility = Visibility.Collapsed;
#endif
        }        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. Show the Loading Panel overlay layout block and freeze control frames
            // LoadingOverlay.Visibility = Visibility.Visible;
            AssetList.IsEnabled = false;
            ReceiverAssetList.IsEnabled = false;            string donorPath = SaveData.DonorAssetRegistryFile;
            string receiverPath = SaveData.ReceiverAssetRegistryFile;
            StagingDataGrid.ItemsSource = StagingQueue.Items;            IEnumerable<TreeNode>? donorTreeSource = null;
            IEnumerable<TreeNode>? receiverTreeSource = null;            try
            {
                // 2. Load and Build the Donor Registry tree lazily
                LoadingStatusText.Text = "Parsing Donor Registry Manifest...";
                await Task.Run(() =>
                {
                    DonorRegistry = AssetRegistry.CreateRegistry(donorPath);
                    if (DonorRegistry != null)
                    {
                        // FIX: Pass 'true' for the second parameter to flag this data for the Donor dictionary cache
                        donorTreeSource = ItemProvider.BuildLazyTree(DonorRegistry.State.Assets, true, out _);
                    }
                });                if (donorTreeSource != null)
                {
                    AssetList.ItemsSource = donorTreeSource;
                }                // 3. Load and Build the Receiver Registry tree lazily
                LoadingStatusText.Text = "Parsing Target Receiver Registry Manifest...";
                await Task.Run(() =>
                {
                    ReceiverRegistry = AssetRegistry.CreateRegistry(receiverPath);
                    if (ReceiverRegistry != null)
                    {
                        // FIX: Pass 'false' for the second parameter to flag this data for the Receiver dictionary cache
                        receiverTreeSource = ItemProvider.BuildLazyTree(ReceiverRegistry.State.Assets, false, out _);
                    }
                });                if (receiverTreeSource != null)
                {
                    ReceiverAssetList.ItemsSource = receiverTreeSource;
                }
            }
            catch(NullReferenceException nr){ DarkMessageBox.Show($"An error occurred while loading files: {nr.Message}", "Loading Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            catch (Exception ex)
            {
                DarkMessageBox.Show($"An error occurred while loading files: {ex.Message}", "Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            finally
            {
                // 4. Hide overlay and reactivate workspace controls when execution completes
                LoadingOverlay.Visibility = Visibility.Collapsed;
                AssetList.IsEnabled = true;
                ReceiverAssetList.IsEnabled = true;
            }            //PM = new PoetryManager("C:/Users/AuriCrystal/Desktop/R2Modding/SchattenTool/AssetRegistryEditor");
        }

        private async void DonorRegistryDialog_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn)
                btn.IsEnabled = false;

            try
            {
                var (registry, filechosen) = await AssetRegistry.PickRegistryDialogAsync(_registryService);
                if (registry == null)
                    return;

                DonorRegistry = registry;
                SaveData.DonorAssetRegistryFile = filechosen;
                AppSettingsManager.Save();


                // FIX: Fast-load via lazy tree builder
                AssetList.ItemsSource = ItemProvider.BuildLazyTree(DonorRegistry.State.Assets, true, out _);
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show($"Failed to load Donor AssetRegistry:\n\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is FrameworkElement b)
                    b.IsEnabled = true;
            }
        }

        private async void ReceiverRegistryDialog_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn)
                btn.IsEnabled = false;

            try
            {
                var (registry, filechosen) = await AssetRegistry.PickRegistryDialogAsync(_registryService);
                if (registry == null)
                    return;

                ReceiverRegistry = registry;
                SaveData.ReceiverAssetRegistryFile = filechosen;
                AppSettingsManager.Save();


                // FIX: Fast-load via lazy tree builder
                ReceiverAssetList.ItemsSource = ItemProvider.BuildLazyTree(ReceiverRegistry.State.Assets, false, out _);
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show($"Failed to load Receiver AssetRegistry:\n\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is FrameworkElement b)
                    b.IsEnabled = true;
            }
        }

#if DEBUG        private async void VerifyBinaryRoundtrip_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Binary AssetRegistry File",
                Filter = "Binary Files (*.bin)|*.bin|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            string originalBinPath = dialog.FileName;
            string tempDir = Path.Combine(Path.GetTempPath(), "AssetRegistryRoundtrip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            if (sender is FrameworkElement btn)
                btn.IsEnabled = false;

            try
            {
                string step1JsonPath = Path.Combine(tempDir, "step1_parsed.json");
                string roundtripBinPath = Path.Combine(tempDir, "step2_encoded.bin");
                string step2JsonPath = Path.Combine(tempDir, "step3_reparsed.json");

                // 1. Convert .bin -> .json -> .bin -> .json
                await _registryService.ConvertBinToJsonAsync(originalBinPath, step1JsonPath);
                await _registryService.ConvertJsonToBinAsync(step1JsonPath, roundtripBinPath);
                await _registryService.ConvertBinToJsonAsync(roundtripBinPath, step2JsonPath);

                // 2. Check byte-level identity AND semantic JSON equivalence
                string originalHash = await ComputeFileHashAsync(originalBinPath);
                string roundtripHash = await ComputeFileHashAsync(roundtripBinPath);

                string json1 = await File.ReadAllTextAsync(step1JsonPath);
                string json2 = await File.ReadAllTextAsync(step2JsonPath);
                SemanticDiffResult diff = await Task.Run(() => JsonSemanticComparer.CompareJsonStrings(json1, json2));

                if (diff.IsEquivalent)
                {
                    string byteNote = originalHash == roundtripHash
                        ? "Byte-for-byte SHA-256: Exact match"
                        : "Byte-for-byte SHA-256: Differs (internal FName/table order only)";

                    DarkMessageBox.Show(
                        $"Binary roundtrip is SEMANTICALLY EQUIVALENT!\n\n" +
                        $"All asset entries, tags, and dependencies survived intact.\n" +
                        $"{byteNote}",
                        "Binary Roundtrip Verified",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    DarkMessageBox.Show(
                        $"Semantic mismatch detected after binary roundtrip!\n\n" +
                        $"Path: {diff.FirstDifferencePath}\n" +
                        $"Reason: {diff.Details}",
                        "Binary Roundtrip Mismatch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show($"Binary roundtrip failed:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is FrameworkElement b)
                    b.IsEnabled = true;
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        private async void VerifyJsonRoundtrip_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Editable JSON AssetRegistry File",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            string originalJsonPath = dialog.FileName;
            string tempDir = Path.Combine(Path.GetTempPath(), "AssetRegistryRoundtrip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            if (sender is FrameworkElement btn)
                btn.IsEnabled = false;

            try
            {
                string tempBinPath = Path.Combine(tempDir, "step1_encoded.bin");
                string roundtripJsonPath = Path.Combine(tempDir, "step2_parsed.json");

                // 1. Convert .json -> .bin -> .json
                await _registryService.ConvertJsonToBinAsync(originalJsonPath, tempBinPath);
                await _registryService.ConvertBinToJsonAsync(tempBinPath, roundtripJsonPath);

                // 2. Run semantic comparison off the UI thread (important for ~7 MB JSON files)
                string originalJson = await File.ReadAllTextAsync(originalJsonPath);
                string roundtripJson = await File.ReadAllTextAsync(roundtripJsonPath);
                SemanticDiffResult diff = await Task.Run(() => JsonSemanticComparer.CompareJsonStrings(originalJson, roundtripJson));

                if (diff.IsEquivalent)
                {
                    DarkMessageBox.Show(
                        "JSON roundtrip is SEMANTICALLY EQUIVALENT!\n\nEvery asset, property, and array item matches after binary encoding and decoding.",
                        "JSON Roundtrip Verified",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    DarkMessageBox.Show(
                        $"Semantic mismatch detected after JSON roundtrip!\n\n" +
                        $"Path: {diff.FirstDifferencePath}\n" +
                        $"Reason: {diff.Details}",
                        "JSON Roundtrip Mismatch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show($"JSON roundtrip failed:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is FrameworkElement b)
                    b.IsEnabled = true;
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }


        private static async Task<string> ComputeFileHashAsync(string filePath)
        {
            await using FileStream stream = File.OpenRead(filePath);
            byte[] hashBytes = await SHA256.HashDataAsync(stream);
            return Convert.ToHexString(hashBytes);
        }

#endif
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox clickedBox && clickedBox.DataContext is FileNode donorNode)
            {
                if (clickedBox.IsChecked == true)
                {
                    if (!StagingQueue.Items.Any(x => x.FullPath.Equals(donorNode.FullPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        StagingQueue.Add(donorNode.CreateStagedClone());
                    }
                }
                else
                {
                    var stagedMatch = StagingQueue.Items.FirstOrDefault(x => x.FullPath.Equals(donorNode.FullPath, StringComparison.OrdinalIgnoreCase));
                    if (stagedMatch != null)
                    {
                        StagingQueue.Remove(stagedMatch);
                    }
                }
            }
        }
        private void ClearStagingQueue_Click(object sender, RoutedEventArgs e)
        {
            var selectedInGrid = StagingDataGrid.SelectedItems.Cast<FileNode>().ToList();
            var nodesToRemove = selectedInGrid.Count == 0
                ? StagingQueue.Items.ToList()
                : selectedInGrid;

            foreach (var stagedNode in nodesToRemove)
            {
                UncheckDonorNodeByPath(AssetList.ItemsSource as IEnumerable<TreeNode>, stagedNode.FullPath);
                StagingQueue.Remove(stagedNode);
            }
        }

        private static void UncheckDonorNodeByPath(IEnumerable<TreeNode>? nodes, string fullPath)
        {
            if (nodes == null)
                return;
            foreach (var node in nodes)
            {
                if (node is FileNode file && file.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    file.Selected = false;
                    return;
                }
                if (node is FolderNode folder && folder.Children != null)
                {
                    UncheckDonorNodeByPath(folder.Children, fullPath);
                }
            }
        }
        private void InjectToReceiver_Click(object sender, RoutedEventArgs e)
        {
            if (StagingQueue.Items.Count == 0 || ReceiverRegistry == null)
                return;            // Extract core array entities from staging setup boundaries
            var assetsToMigrate = StagingQueue.Items.SelectMany(x => x.GetAllAssets()).ToList();            // Insert objects safely into Receiver data list parameters
            foreach (var asset in assetsToMigrate)
            {
                if (!ReceiverRegistry.State.Assets.Any(x => x.UniqueKey == asset.UniqueKey))
                {
                    ReceiverRegistry.State.Assets.Add(asset);
                }
            }            // Instantly redraw the target column destination interface hierarchy layout
            ReceiverAssetList.ItemsSource = ReceiverRegistry.BuildTree();            DarkMessageBox.Show($"Successfully injected {assetsToMigrate.Count} assets into target collection!", "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }        private void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            // Only execute the search if the user hits the Enter key
            if (e.Key == Key.Enter && sender is TextBox textBox && AssetList.ItemsSource != null)
            {
                // Suppress the default Windows "ding" sound when pressing enter in a single-line textbox
                e.Handled = true;                string[] filterTokens = textBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);                // Execute deep structural search filtering pass across all root tree items
                foreach (TreeNode rootNode in AssetList.ItemsSource)
                {
                    rootNode.ApplyDeepSearchFilter(filterTokens);
                }
            }
        }        private void ReceiverSearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox && ReceiverAssetList.ItemsSource != null)
            {
                e.Handled = true;                string[] filterTokens = textBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);                foreach (TreeNode rootNode in ReceiverAssetList.ItemsSource)
                {
                    rootNode.ApplyDeepSearchFilter(filterTokens);
                }
            }
        }
        private void OpenPropertyWindow(FileNode node, InspectionSource source)
        {
            string windowKey = $"{source}:{node.FullPath}";

            if (_openPropertyWindows.TryGetValue(windowKey, out Window? existingWindow))
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                    existingWindow.WindowState = WindowState.Normal;

                existingWindow.Activate();
                existingWindow.Focus();
                return;
            }

            var propsWindow = new JsonViewbox(node, source);
            _openPropertyWindows[windowKey] = propsWindow;
            propsWindow.Closed += (_, _) => _openPropertyWindows.Remove(windowKey);
            propsWindow.Show();
        }

        private void View_OnClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if ((sender as FrameworkElement)?.DataContext is not FileNode node)
                return;

            // Determine whether this TreeView item belongs to Donor (AssetList) or Receiver (ReceiverAssetList)
            InspectionSource source = InspectionSource.Donor;
            if (node.OwningTreeViewItem != null && FindParentTreeView(node.OwningTreeViewItem)?.Name == "ReceiverAssetList")
            {
                source = InspectionSource.Receiver;
            }

            OpenPropertyWindow(node, source);
        }

        private void StagingEdit_Click(object sender, RoutedEventArgs e)
        {
            if (StagingDataGrid.SelectedItem is FileNode node)
                OpenPropertyWindow(node, InspectionSource.Staging);
        }

        private void StagingDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (StagingDataGrid.SelectedItem is FileNode node)
                OpenPropertyWindow(node, InspectionSource.Staging);
        }


        private void Window_Closing(object sender, CancelEventArgs e)
        {
            foreach (Window window in OwnedWindows.OfType<Window>().ToList())
            {
                window.Close();
            }
        }        private async void JsonToBin_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Select Editable JSON File to Convert",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() != true)
                return;

            string inputJsonPath = openDialog.FileName;
            string defaultName = Path.GetFileNameWithoutExtension(inputJsonPath) + "_encoded.bin";

            var saveDialog = new SaveFileDialog
            {
                Title = "Save Encoded Binary File As",
                Filter = "Binary Files (*.bin)|*.bin|All Files (*.*)|*.*",
                InitialDirectory = Path.GetDirectoryName(inputJsonPath),
                FileName = defaultName
            };

            if (saveDialog.ShowDialog() != true)
                return;

            string outputBinPath = saveDialog.FileName;

            if (sender is FrameworkElement btn)
                btn.IsEnabled = false;

            try
            {
                await _registryService.ConvertJsonToBinAsync(inputJsonPath, outputBinPath);

                long outputSize = new FileInfo(outputBinPath).Length;
                var result = DarkMessageBox.Show(
                    $"Successfully converted JSON to Binary!\n\n" +
                    $"Saved to: {outputBinPath}\n" +
                    $"Size: {outputSize:N0} bytes\n\n" +
                    $"Would you like to open the output folder?",
                    "Conversion Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start("explorer.exe", $"/select,\"{outputBinPath}\"");
                }
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show(
                    $"Failed to convert JSON to Binary:\n\n{ex.Message}",
                    "Conversion Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (sender is FrameworkElement b)
                    b.IsEnabled = true;
            }
        }

        private async void BinToJson_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Select Binary AssetRegistry File to Convert",
                Filter = "Binary Files (*.bin)|*.bin|All Files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() != true)
                return;

            string inputBinPath = openDialog.FileName;
            string defaultName = Path.GetFileNameWithoutExtension(inputBinPath) + "_parsed.json";

            var saveDialog = new SaveFileDialog
            {
                Title = "Save Parsed JSON File As",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                InitialDirectory = Path.GetDirectoryName(inputBinPath),
                FileName = defaultName
            };

            if (saveDialog.ShowDialog() != true)
                return;

            string outputJsonPath = saveDialog.FileName;

            if (sender is FrameworkElement btn)
                btn.IsEnabled = false;

            try
            {
                await _registryService.ConvertBinToJsonAsync(inputBinPath, outputJsonPath);

                long outputSize = new FileInfo(outputJsonPath).Length;
                var result = DarkMessageBox.Show(
                    $"Successfully converted Binary to JSON!\n\n" +
                    $"Saved to: {outputJsonPath}\n" +
                    $"Size: {outputSize:N0} bytes\n\n" +
                    $"Would you like to open the output folder?",
                    "Conversion Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start("explorer.exe", $"/select,\"{outputJsonPath}\"");
                }
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show(
                    $"Failed to convert Binary to JSON:\n\n{ex.Message}",
                    "Conversion Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (sender is FrameworkElement b)
                    b.IsEnabled = true;
            }
        }
        private void StackPanel_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is StackPanel panel && panel.DataContext is TreeNode node && node.OwningTreeViewItem != null)
            {
                node.OwningTreeViewItem.IsSelected = true;
            }
        }        private void TreeViewItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && item.DataContext is TreeNode node)
            {
                node.OwningTreeViewItem = item;
            }
        }
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item && item.DataContext is FolderNode folderNode)
            {
                if (folderNode.IsLazyInitialized)
                    return;                // Determine if this event is coming from the Donor Tree or the Receiver Tree
                bool isDonor = FindParentTreeView(item)?.Name == "AssetList";                // Pull calculated child nodes on-demand in 0 milliseconds!
                var realChildren = ItemProvider.GetChildrenOnDemand(folderNode.FullPath, isDonor);                // Reparent items safely back onto the main thread space
                foreach (var child in realChildren)
                {
                    child.ParentNode = folderNode;
                    if (child is FileNode file)
                        file.ParentNode = folderNode;
                }                folderNode.PerformLazyInitialization(realChildren);
            }
        }        private TreeView? FindParentTreeView(DependencyObject child)
        {
            if (child == null)
                return null;
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent is TreeView treeView)
                return treeView;
            return FindParentTreeView(parent);
        }        private void Settings_Click(object sender, RoutedEventArgs e) => _ = new Settings();

        private void StackPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                View_OnClick(sender, e);
            }
        }

    }
}
