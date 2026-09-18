using AssetRegistryModMigrator.Model;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AssetRegistryModMigrator.Classes
{
    public static class ItemProvider
    {
        public static IEnumerable<TreeNode> GetItems(string path, out List<FileNode>? leaves) => AssetRegistry.GenerateRegistry(path).GetItems(out leaves);
        public static IEnumerable<TreeNode> GetItems(string path)
        {
            List<FileNode>? unused;
            return GetItems(path, out unused);
        }

        public static IEnumerable<TreeNode> GetItems(this AssetRegistry registry, out List<FileNode>? leaves) => GetItems(registry.State.Assets, out leaves);
        public static IEnumerable<TreeNode> GetItems(this AssetRegistry registry)
        {
            List<FileNode>? unused;
            return registry.GetItems(out unused);
        }
        public static IEnumerable<TreeNode> GetItems(IReadOnlyList<Asset> Assets)
        {
            List<FileNode>? unused;
            return GetItems(Assets, out unused);
        }
        public static IEnumerable<TreeNode> GetItems(IReadOnlyList<Asset> Assets, out List<FileNode>? leaves)
        {
            var items = new List<TreeNode>();
            var files = new List<FileNode>();
            FolderNode? CrawlNode = null;
            FileNode? temp=null;
            foreach (Asset asset in Assets)
            {
                //Debug.WriteLine(string.Format("Compare {0} to {1}",CrawlNode?.Path+"/ F:("+CrawlNode?.Name+")",asset.PackagePath+"/ F:("+asset.AssetName+")"));
                if (CrawlNode?.Path==asset.PackagePath)
                {
                    temp= CrawlNode?.EndChain(asset);
                    files.Add(temp);
                    continue;
                }

                var folders = asset.PackagePath.Split("/", StringSplitOptions.RemoveEmptyEntries);
                CrawlNode = null;
                foreach (var folder in folders)
                {
                    if (CrawlNode == null)
                    {
                        if (items.Exists(x => x.Name == folder))
                        {
                            CrawlNode = (FolderNode?)items.Find(x => x.Name == folder);
                        }
                        else
                        {
                            CrawlNode = (FolderNode?)new FolderNode(folder);
                            items.Add(CrawlNode); //add new root node
                        }

                        continue;
                    }

                    if (CrawlNode.Children.ToList().Exists(x => x.Name == folder))
                    {
                        CrawlNode = (FolderNode?)CrawlNode.Children.ToList().Find(x => x.Name == folder);
                    }
                    else
                    {
                        CrawlNode = CrawlNode.Chain(folder);
                    }
                }
                temp = CrawlNode?.EndChain(asset);

                files.Add(temp);

                //Debug.WriteLine("DepthEnd:" + temp.Depth);
                //Debug.Write(asset.AssetName);
            }
            leaves = files;
            return items;
        }
        
        public static FrameworkElement? FindParentByClass(this FrameworkElement control, Type Class)
        {

            if (control.TemplatedParent is null)
                return null;

            //Debug.WriteLine("Type: " + (control.TemplatedParent.GetType() == Class) + "\n" + control.TemplatedParent.GetType().ToString());
            if (control.TemplatedParent.GetType() == Class)
                return (FrameworkElement?)control.TemplatedParent;

            return ((FrameworkElement?)control.TemplatedParent).FindParentByClass(Class);
        }

        /// <summary>
        /// Applies a search filter to all items of a TreeView recursively
        /// </summary>
        public static void Filter(this TreeView self, Predicate<object> predicate)
        {
            if (self == null) return;
            ICollectionView view = CollectionViewSource.GetDefaultView(self.ItemsSource);
            if (view == null)
                return;
            view.Filter = predicate;
            foreach (var obj in self.ItemContainerGenerator.Items)
            {
                var item = self.ItemContainerGenerator.ContainerFromItem(obj) as TreeViewItem;
                if (item == null)
                {
                    self.UpdateLayout();
                    item = self.ItemContainerGenerator.ContainerFromItem(obj) as TreeViewItem;
                }
                FilterRecursively(item, predicate);
            }
        }

        private static void FilterRecursively(TreeViewItem item, Predicate<object> predicate)
        {
            if (item == null) return;
            ICollectionView view = CollectionViewSource.GetDefaultView(item.ItemsSource);
            if (view == null) return;
            view.Filter = predicate;
            foreach (var obj in item.ItemContainerGenerator.Items)
            {
                var childItem = item.ItemContainerGenerator.ContainerFromItem(obj) as TreeViewItem;
                if (childItem == null)
                {
                    item.UpdateLayout();
                    childItem = item.ItemContainerGenerator.ContainerFromItem(obj) as TreeViewItem;
                }
                FilterRecursively(childItem, predicate);
            }
        }

    }
}