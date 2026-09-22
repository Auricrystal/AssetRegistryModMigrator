using AssetRegistryModMigrator.Classes;
using AssetRegistryModMigrator.Model;
using AssetRegistryModMigrator.Windows;
using IronPython;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.CodeDom;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static Community.CsharpSqlite.Sqlite3;
using static System.Net.WebRequestMethods;


namespace AssetRegistryModMigrator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        AssetRegistry? DonorRegistry { get; set; }
        AssetRegistry? ReceiverRegistry { get; set; }
        PoetryManager? PM { get; set; }
        //List<Asset>? SelectedAssets { get; set; }
        List<FileNode> CheckedItems { get; set; }
        //TreeTrunk Trunk { get; set; }
        //List<Window> ChildWindows { get; set; } = new List<Window>();
        static Properties.Settings SaveData { get => Properties.Settings.Default; }
        private Predicate<object>? _searchFilter { get; set; }
        public Predicate<object>? SearchFilter
        {
            get { return _searchFilter; }
            set
            {
                _searchFilter = value;

                //recreate the tree in order to apply the filter on
                //all currently visible nodes
                //-> of course, this could be optimized, but it does the job
                AssetList.Items.Refresh();
                //Refresh(GetTreeLayout());
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            //SelectedAssets = new();
            CheckedItems = [];
            //Trunk = new TreeTrunk();
            //InitPythonIncludedEnvironment();


        }

        private void DonorRegistryDialog_Click(object sender, RoutedEventArgs e)
        {
            //string filechosen = "";
            DonorRegistry = AssetRegistry.PickRegistryDialog(out string filechosen);
            SaveData.Main_DonorAssetRegistryFile = filechosen;
            SaveData.Save();

            //    List<FileNode>? leaves;
            //    var temp = ItemProvider.BuildTree(DonorRegistry?.State.Assets, out leaves);
            //    Trunk.FullTree = temp;
            //    Trunk.Leaves = leaves;
            //    AssetList.ItemsSource = Trunk.FullTree;
        }


        private void AssetList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var selecteditem = AssetList.SelectedItem;

            if (selecteditem is FileNode node)
                JsonBox.Text = node?.GetAllAssets().Select(x => JsonConvert.SerializeObject(x, Formatting.Indented, new JsonSerializerSettings() { })).Aggregate((first, second) => first + ",\n" + second);
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {

            CheckBox Clicked = ((CheckBox)sender);
            FileNode? test = (FileNode)Clicked.DataContext ?? null;

            switch (Clicked.IsChecked)
            {
                case true:
                    if (!CheckedItems.Contains(test))
                        CheckedItems.Add(test);
                    break;
                case false:
                    CheckedItems.Remove(test);
                    break;
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            DonorRegistry = AssetRegistry.CreateRegistry(SaveData.Main_DonorAssetRegistryFile);
            ReceiverRegistry = AssetRegistry.CreateRegistry(SaveData.Main_ReceiverAssetRegistryFile);

            AssetList.ItemsSource = DonorRegistry?.BuildTree() ?? null;

            PM = new PoetryManager("C:/Users/AuriCrystal/Desktop/R2Modding/SchattenTool/AssetRegistryEditor");
            //PM.JsonToBin();
        }

        private void View_OnClick(object sender, RoutedEventArgs e)
        {
            FileNode? node = null;
            if (sender is MenuItem menu)
                if (menu.DataContext is FileNode)
                    node = (FileNode)menu.DataContext;

            if (node is null)
                return;

            _ = new JsonViewbox(node);
            //Debug.WriteLine(OwnedWindows.Count);
        }

        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBoxName = (TextBox)sender;
            string[] filterText = textBoxName.Text.Split(' ');

            SearchFilter = x => filterText.Select(y => ((TreeNode)x).TreeHasMatchingName(y)).All(x => x == true);

            // Update the UI
            InvokeAction(() => { AssetList.Filter(SearchFilter); });
            return;
        }
        protected static void InvokeAction(Action action)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { action(); }));
        }

        private void SaveList_Click(object sender, RoutedEventArgs e)
        {
            if (CheckedItems.Count > 0)
                JsonBox.Text = CheckedItems.OfType<FileNode>().SelectMany(x => x.GetAllAssets()).Select(x => JsonConvert.SerializeObject(x, Formatting.Indented, new JsonSerializerSettings() { })).Aggregate((first, second) => first + ",\n" + second);
        }

        private void AssetList_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem? ExpandedNode = e.OriginalSource as TreeViewItem;
            if (ExpandedNode is null)
                return;
            try
            {
                // Update the UI
                InvokeAction(() => { ExpandedNode.Items.Filter = SearchFilter; });
            }
            catch (Exception ex) { Debug.WriteLine($"{ex.Message}"); }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            foreach (Window window in OwnedWindows) window.Close();
        }
        private void JsonToBin_Click(object sender, RoutedEventArgs e)
        {
            if (PM is not null)
                PM.JsonToBin();
        }
        private void BinToJson_Click(object sender, RoutedEventArgs e)
        {
            if (PM is not null)
                PM.BinToJson();
        }

        private void StackPanel_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (
                sender is not StackPanel p ||
                p.DataContext is not TreeNode f ||
                f.OwningTreeViewItem is null
                )
                return;

            f.OwningTreeViewItem.IsSelected = true;
        }

        private void TreeViewItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TreeViewItem item) return;
            if (item.DataContext is not TreeNode node) return;

            Debug.WriteLine($"TreeViewItem Loaded: {node.Name}");
            //var test1 = item.GetStoredNode<FolderNode>();
            //var test = item.GetStoredNode<FileNode>();
            node.OwningTreeViewItem = item;
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            _ = new Settings();
        }

    }
}