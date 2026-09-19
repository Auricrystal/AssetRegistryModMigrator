using AssetRegistryModMigrator.Classes;
using AssetRegistryModMigrator.Model;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.CodeDom;
using System.ComponentModel;
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
        //List<Asset>? SelectedAssets { get; set; }
        List<FileNode> CheckedItems { get; set; }
        //TreeTrunk Trunk { get; set; }
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


        }

        private void DonorRegistryDialog_Click(object sender, RoutedEventArgs e)
        {
            //string filechosen = "";
            DonorRegistry = AssetRegistry.PickRegistryDialog(out string filechosen);
            SaveData.DonorAssetRegistry = filechosen;
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

            //var tree = Clicked.FindParentTest<TreeViewItem>();

            //if (tree == null) return;

            //tree.IsSelected = Clicked.IsChecked ?? false;
            //try
            //{
            //    FileNode? TreeNode2 = Trunk.Leaves.Single(x => { return x.FullPath == test?.FullPath; });
            //    if (TreeNode2 == null) return;
            //}
            //catch (InvalidOperationException) { Debug.WriteLine("More than one element, be more specific"); }



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

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            DonorRegistry = AssetRegistry.CreateRegistry(SaveData.DonorAssetRegistry);
            ReceiverRegistry = AssetRegistry.CreateRegistry(SaveData.ReceiverAssetRegistry);
            //if (DonorRegistry is null)
            //    return;
            //List<FileNode> leaves;
            //DonorRegistry.BuildTree();
            //Trunk.FullTree = temp;
            //Trunk.Leaves = leaves;
            AssetList.ItemsSource = DonorRegistry.BuildTree();

        }

        private void View_OnClick(object sender, RoutedEventArgs e)
        {
            var selecteditem = AssetList.SelectedItem;
            //var text = "";
            if (selecteditem is not FileNode node)
                return;

                //text = node?.GetAllAssets().Select(x => JsonConvert.SerializeObject(x, Formatting.Indented, new JsonSerializerSettings() { })).Aggregate((first, second) => first + ",\n" + second);

            JsonViewbox newWindow = new JsonViewbox(selecteditem as FileNode);
            //newWindow.DataContext = this;
            
            newWindow.Show();
        }

        private void Edit_OnClick(object sender, RoutedEventArgs e)
        {

        }

        private void Delete_OnClick(object sender, RoutedEventArgs e)
        {

        }

        //Dispatcher dispatcher = Application.Current.Dispatcher;
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
        private void AssetList_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

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
    }
}