using AssetRegistryModMigrator.Classes;
using AssetRegistryModMigrator.Model;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.CodeDom;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        //List<Asset>? SelectedAssets { get; set; }
        List<TreeNode> CheckedItems { get; set; }
        TreeTrunk Trunk { get; set; }
        static Properties.Settings SaveData { get => Properties.Settings.Default; }
        public MainWindow()
        {
            InitializeComponent();
            //SelectedAssets = new();
            CheckedItems = new List<TreeNode>();
            Trunk = new TreeTrunk();

        }

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            FileDialog dialog = new OpenFileDialog() { DefaultExt = ".json", FileName = "DonorRegistry", Filter = "Json Files (.json)|*.json" };
            Nullable<bool> result = dialog.ShowDialog();
            if (!result.Value) return;


            DonorRegistry = AssetRegistry.GenerateRegistry(dialog.FileName);
            SaveData.DonorAssetRegistry = dialog.FileName;
            SaveData.Save();
            List<FileNode> leaves;
            var temp = ItemProvider.GetItems(DonorRegistry?.State.Assets, out leaves);
            Trunk.FullTree = temp;
            Trunk.Leaves = leaves;
            AssetList.ItemsSource = Trunk.FullTree;
            //Debug.WriteLine("Done!");
        }
        private void AssetList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var selecteditem = AssetList.SelectedItem;

            //Debug.WriteLine((selecteditem is FileNode) ? "FileNode: " : "FolderNode: " + ((TreeNode)selecteditem).ToString());
            if (selecteditem is FileNode)
                JsonBox.Text = ((FileNode)selecteditem)?.GetAllAssets().Select(x => JsonConvert.SerializeObject(x, Formatting.Indented, new JsonSerializerSettings() { })).Aggregate((first, second) => first + ",\n" + second);
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {

            CheckBox Clicked = ((CheckBox)sender);
            TreeViewItem? tree = (TreeViewItem?)Clicked.FindParentByClass(typeof(TreeViewItem));
            TreeNode test = (TreeNode)Clicked.DataContext;
            //Debug.WriteLine("ToString Checked: " + test.ToString());
            if (tree == null) return;

            tree.IsSelected = Clicked.IsChecked ?? false;
            //FileNode? TreeNode = Trunk.Leaves.Where(x => x.Equals(test)).First();
            FileNode? TreeNode2 = Trunk.Leaves.Where(x =>
            {
                //Debug.WriteLine(String.Format("Compare: {0}  Test:{1} == {2}   FullPath:{3}", x.GetHashCode() == test.GetHashCode(), test.GetHashCode(), x.GetHashCode(), x.FullPath));
                return x.FullPath == test.FullPath;
            }).First();
            if (TreeNode2 == null)
            {
                //Debug.WriteLine("Didnt Find Match");
                return;
            }
            //Debug.WriteLine("ToString Leaf: " + TreeNode2.ToString());

            switch (Clicked.IsChecked)
            {
                case true:
                    if (!CheckedItems.Contains(test))
                        CheckedItems.Add(test);
                    break;
                case false:
                    if (CheckedItems.Contains(test))
                        CheckedItems.Remove(test);
                    break;

            }
            //Debug.WriteLine(((CheckBox)sender).TemplatedParent is TreeViewItem);
            //Debug.WriteLine(string.Join("\n", ((TreeNode)AssetList.SelectedItem).GetAllAssets().Select(x => x.PackagePath)));
            //((TreeNode)AssetList.SelectedItem).PropagateChecks((bool)(Clicked.IsChecked ?? false));
            return;
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            //Debug.WriteLine("Tree TreeNode Selected");
            //if (((TreeView)sender).SelectedItem is FileNode)
            // return;
            //foreach (var item in ((FolderNode)((TreeView)sender).SelectedItem).Children)
            // Debug.WriteLine(((TreeNode)item).Name);
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox Clicked = ((CheckBox)sender);

            TreeViewItem? tree = (TreeViewItem?)Clicked.FindParentByClass(typeof(TreeViewItem));

            if (tree.DataContext is FolderNode)
                return;
            FileNode item = (FileNode)tree.DataContext;

            //Debug.WriteLine(string.Format("{0}: Hash:{1} Add:{2}", item.Name, item.GetHashCode(), Clicked.IsChecked));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            DonorRegistry = AssetRegistry.GenerateRegistry(SaveData.DonorAssetRegistry);
            if (DonorRegistry is null)
                return;
            List<FileNode> leaves;
            var temp = DonorRegistry.GetItems(out leaves);
            Trunk.FullTree = temp;
            Trunk.Leaves = leaves;//.DistinctBy(x=>x.GetHashCode());
            AssetList.ItemsSource = Trunk.FullTree;
            //Debug.WriteLine(DonorRegistry.State.Assets[0]);

        }

        private void View_OnClick(object sender, RoutedEventArgs e)
        {

        }

        private void Edit_OnClick(object sender, RoutedEventArgs e)
        {

        }

        private void Delete_OnClick(object sender, RoutedEventArgs e)
        {

        }
        private bool predicate(TreeViewItem item)
        {
            if (item is null)
            {
                Debug.WriteLine("BROKEN");
            }
            return item.GetHeaderOrEmpty().Contains(SearchBar.Text, StringComparison.OrdinalIgnoreCase);
        }
        private void ExpandAll(ItemsControl items, bool expand)
        {
            foreach (object obj in items.Items)
            {
                ItemsControl childControl = items.ItemContainerGenerator.ContainerFromItem(obj) as ItemsControl;
                if (childControl != null)
                {
                    ExpandAll(childControl, expand);
                }
                TreeViewItem item = childControl as TreeViewItem;
                if (item != null)
                    item.IsExpanded = expand;
            }
        }
        public void ExpandAll(TreeView treeView)
        {
            foreach (object item in treeView.Items)
                if (AssetList.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeItem)
                    treeItem?.ExpandSubtree();
        }


        private void btnExpandAll_Click(object sender, RoutedEventArgs e)
        {

            foreach (object item in this.AssetList.Items)
            {
                TreeViewItem? treeItem = this.AssetList.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeItem != null)
                    ExpandAll(treeItem, true);
                treeItem.IsExpanded = true;
            }
        }
        private Predicate<object> _searchFilter { get; set; }
        public Predicate<object> SearchFilter
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
        Dispatcher dispatcher = Application.Current.Dispatcher;
        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            //foreach (FolderNode item in AssetList.Items)
            //{
            //    item.SetExpansionAll(true);
            // }

            TextBox textBoxName = (TextBox)sender;
            string[] filterText = textBoxName.Text.Split(' ');
            //List<TreeNode> list = (AssetList.ItemsSource as IEnumerable<TreeNode>).Where(x => filterText.Select(y => x.TreeHasMatchingName(y)).All(x => x == true)).ToList();
            SearchFilter = x => filterText.Select(y => ((TreeNode)x).TreeHasMatchingName(y)).All(x => x == true);

            dispatcher.BeginInvoke(new Action(() =>
            {
                // Update the UI
                AssetList.Filter(SearchFilter);
            }));
            
            //AssetList.Items.Filter = (x => filterText.Select(y => ((TreeNode)x).TreeHasMatchingName(y)).All(x => x == true));
            //AssetList.Items.FilterBy((x =>x.ItemContainerGenerator.)
            //ExpandAll(AssetList);
            // foreach (FolderNode item in AssetList.Items)
            //{
            //     item.SetExpansionAll(false);
            // }
            return;

            //TextBox textBoxName = (TextBox)sender;
            // string[] filterText = textBoxName.Text.Split(' ');
            ICollectionView[] arr =
                new ICollectionView[] {
                    CollectionViewSource.GetDefaultView(Trunk.FullTree),
                };
            if (!string.IsNullOrEmpty(filterText[0]))
            {
                foreach (ICollectionView icv in arr)
                {
                    icv.Filter = o =>
                    {
                        if (o is null) return false;
                        //Debug.WriteLine("CollectionView: " + (o as TreeNode).Name);
                        //var help = (o as TreeNode).TreeHasMatchingName(filterText[0]);
                        //var test=filterText.Where(x => (o as TreeNode).TreeHasMatchingName(x));

                        var found = filterText.Select(x => (o as TreeNode).TreeHasMatchingName(x)).All(x => x == true);
                        if (found)
                            Debug.WriteLine("Tree Has Child With : " + (o as TreeNode).Name);
                        return found;

                    };

                }
            }
            else
            {
                foreach (ICollectionView icv in arr) { icv.Filter = o => { return true; }; }
            }
        }

        //protected override void OnNodeExpanded(TreeViewItem treeNode)
        //{
        //    //make sure child nodes are being created
        //    base.OnNodeExpanded(treeNode);

        //    //apply filter
        //    foreach (TreeViewItem childNode in treeNode.Items)
        //    {
        //        ApplyFilter(childNode, (ShopCategory)childNode.Header);
        //    }
        //}

        private void AssetList_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //var test=(AssetList.ItemsSource as );
        }

        private void SaveList_Click(object sender, RoutedEventArgs e)
        {
            if (CheckedItems.Count > 0)
                JsonBox.Text = CheckedItems.SelectMany(x => (x as FileNode).GetAllAssets()).Select(x => JsonConvert.SerializeObject(x, Formatting.Indented, new JsonSerializerSettings() { })).Aggregate((first, second) => first + ",\n" + second);
        }

        private void AssetList_Expanded(object sender, RoutedEventArgs e)
        {
            //var treetest = sender;
            //var args = e;
            var ExpandedNode=e.OriginalSource as TreeViewItem;
            try
            {
                //string[] filterText = SearchBar.Text.Split(' ');
                dispatcher.BeginInvoke(new Action(() =>
                {
                    // Update the UI
                    ExpandedNode.Items.Filter = (Predicate<object>)SearchFilter;
                }));
                
                //if (filterText.Length > 0)
                    //AssetList.Filter(x => filterText.Select(y => ((TreeNode)x).TreeHasMatchingName(y)).All(x => x == true));
            }
            catch (Exception ex) { Debug.WriteLine("BOOP"); }
        }
    }
}