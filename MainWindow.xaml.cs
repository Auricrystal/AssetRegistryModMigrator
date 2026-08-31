using AssetRegistryModMigrator.Classes;
using Microsoft.Win32;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using AssetRegistryModMigrator.Model;

namespace AssetRegistryModMigrator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        AssetRegistry? AssetRegistry { get; set; }
        List<Asset>? SelectedAssets { get; set; }
        Dictionary<Item, bool>? CheckedItems { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            SelectedAssets = new();
        }

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            FileDialog dialog = new OpenFileDialog() { DefaultExt = ".json", FileName = "AssetRegistry", Filter = "Json Files (.json)|*.json" };

            Nullable<bool> result = dialog.ShowDialog();
            if (!result.Value) return;

            // Open document
            string? json = File.ReadAllText(dialog.FileName);

            AssetRegistry = JsonConvert.DeserializeObject<AssetRegistry>(json);
            if (AssetRegistry is null) return;
            //var assets = AssetRegistry?.State.Assets.GroupBy(x => x.PackageName).ToDictionary(y => y.Key, y => y.ToList());
            var temp = ItemProvider.GetItems(AssetRegistry?.State.Assets);

            //CheckedItems = ItemProvider.GetItems(AssetRegistry?.State.Assets).ToDictionary(x => x, x => false);
            AssetList.ItemsSource = temp;
            Debug.WriteLine("Done!");



        }

        private void AssetList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var selecteditem = AssetList.SelectedItem;

            Debug.WriteLine("Item: " + ((Item)selecteditem).ToString());
            if (selecteditem is FileItem)
                JsonBox.Text = ((FileItem)selecteditem)?.Assets.Select(x => JsonConvert.SerializeObject(x, Formatting.Indented, new JsonSerializerSettings() { })).Aggregate((first, second) => first + ",\n" + second);
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox Clicked = ((CheckBox)sender);
            TreeViewItem tree= (TreeViewItem)((ContentPresenter)Clicked.TemplatedParent).TemplatedParent;
            Item test = (Item)Clicked.DataContext;
            Debug.WriteLine("ToString: "+test.ToString());
            if (tree == null) return;
            tree.IsSelected = true;

            Debug.WriteLine(string.Join("\n", ((Item)AssetList.SelectedItem).GetAllAssets().Select(x => x.PackagePath)));
            ((Item)AssetList.SelectedItem).PropagateChecks((bool)(Clicked.IsChecked));
            return;



        }

        private void TextBlock_MouseEnter(object sender, MouseEventArgs e)
        {

        }

        private void HierarchicalDataTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Test");
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
        public static List<Item> GetItems(IReadOnlyList<Asset> Assets)
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
    }
}