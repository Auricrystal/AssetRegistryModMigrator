using AssetRegistryModMigrator.Classes;
using AssetRegistryModMigrator.Model;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.CodeDom;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AssetRegistryModMigrator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        AssetRegistry? DonorRegistry { get; set; }
        List<Asset>? SelectedAssets { get; set; }
        Dictionary<Item, bool>? CheckedItems { get; set; }

        static Properties.Settings SaveData { get => Properties.Settings.Default; }
        public MainWindow()
        {
            InitializeComponent();
            SelectedAssets = new();

        }

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            FileDialog dialog = new OpenFileDialog() { DefaultExt = ".json", FileName = "DonorRegistry", Filter = "Json Files (.json)|*.json" };
            Nullable<bool> result = dialog.ShowDialog();
            if (!result.Value) return;


            DonorRegistry = ItemProvider.DeserializeFile(dialog.FileName);
            SaveData.DonorAssetRegistry = dialog.FileName;
            SaveData.Save();

            var temp = ItemProvider.GetItems(DonorRegistry?.State.Assets);
            AssetList.ItemsSource = temp;
            Debug.WriteLine("Done!");
        }
        private void AssetList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var selecteditem = AssetList.SelectedItem;

            Debug.WriteLine("Item: " + ((Item)selecteditem).ToString());
            if (selecteditem is FileItem)
                JsonBox.Text = ((FileItem)selecteditem)?.GetAllAssets().Select(x => JsonConvert.SerializeObject(x, Formatting.Indented, new JsonSerializerSettings() { })).Aggregate((first, second) => first + ",\n" + second);
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            
            CheckBox Clicked = ((CheckBox)sender);
            TreeViewItem? tree = (TreeViewItem?)Clicked.FindParentByClass(typeof(TreeViewItem));
            Item test = (Item)Clicked.DataContext;
            Debug.WriteLine("ToString: " + test.ToString());
            if (tree == null) return;
            tree.IsSelected = true;

            
            Debug.WriteLine(((CheckBox)sender).TemplatedParent is TreeViewItem);
            Debug.WriteLine(string.Join("\n", ((Item)AssetList.SelectedItem).GetAllAssets().Select(x => x.PackagePath)));
            //((Item)AssetList.SelectedItem).PropagateChecks((bool)(Clicked.IsChecked ?? false));
            return;
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Tree Item Selected");
            if (((TreeView)sender).SelectedItem is FileItem)
                return;
            foreach (var item in ((DirectoryItem)((TreeView)sender).SelectedItem).Children)
                Debug.WriteLine(((Item)item).Name);
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox Clicked = ((CheckBox)sender);

            TreeViewItem? tree = (TreeViewItem?)Clicked.FindParentByClass(typeof(TreeViewItem));

            if (tree.DataContext is DirectoryItem)
                return;
            FileItem item = (FileItem)tree.DataContext;

            Debug.WriteLine(string.Format("{0}: Hash:{1} Add:{2}", item.Name, item.GetHashCode(), Clicked.IsChecked));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            DonorRegistry = ItemProvider.DeserializeFile(SaveData.DonorAssetRegistry);
            if (DonorRegistry is null)
                return;
            AssetList.ItemsSource = DonorRegistry.GetItems();
            Debug.WriteLine(DonorRegistry.State.Assets[0]);

        }

    }
    public class ListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is List<int?> list)
            {
                return string.Join(", ", list); // Joins items with a comma and space
            }
            return "null";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public static class ItemProvider
    {
        public static IEnumerable<Item> GetItems(string file) => GetItems(DeserializeFile(file));
        public static IEnumerable<Item> GetItems(this AssetRegistry registry) => GetItems(registry.State.Assets);
        public static IEnumerable<Item> GetItems(IReadOnlyList<Asset> Assets)
        {
            var items = new List<Item>();

            foreach (Asset asset in Assets)
            {
                var folders = asset.PackagePath.Split("/", StringSplitOptions.RemoveEmptyEntries);
                //Debug.WriteLine(asset.PackageName);
                DirectoryItem? CrawlNode = null;

                foreach (var folder in folders)
                {
                    if (CrawlNode == null)
                    {
                        if (items.Exists(x => x.Name == folder))
                        {
                            CrawlNode = (DirectoryItem?)items.Find(x => x.Name == folder);
                        }
                        else
                        {
                            CrawlNode = (DirectoryItem?)new DirectoryItem(folder);
                            //CrawlNode.Path = asset.PackagePath;
                            items.Add(CrawlNode);
                        }

                        continue;
                    }


                    if (CrawlNode.Children.Exists(x => x.Name == folder))
                    {
                        CrawlNode = (DirectoryItem?)CrawlNode.Children.Find(x => x.Name == folder);
                    }
                    else
                    {
                        CrawlNode = CrawlNode.Chain(folder);
                        //items.Add(CrawlNode);
                    }
                }
                CrawlNode?.EndChain(asset);
            }
            return items;
        }
        public static AssetRegistry? DeserializeFile(string path)
        {
            if (!File.Exists(path))
                return null;
            // Open document
            string? json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<AssetRegistry>(json);
        }
        public static AssetRegistry? GenerateAssetTree(string path)
        {
            if (!File.Exists(path))
                return null;
            // Open document
            string? json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<AssetRegistry>(json);
        }
        public static FrameworkElement? FindParentByClass(this FrameworkElement control, Type Class){

            if (control.TemplatedParent is null)
                return null;

            Debug.WriteLine("Type: "+(control.TemplatedParent.GetType() == Class) +"\n"+ control.TemplatedParent.GetType().ToString());
            if (control.TemplatedParent.GetType()==Class)
                return (FrameworkElement?)control.TemplatedParent;

        return ((FrameworkElement?)control.TemplatedParent).FindParentByClass(Class);
        }

    }
}