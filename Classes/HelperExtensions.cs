using AssetRegistryModMigrator.Model;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AssetRegistryModMigrator.Classes
{
    public static class ItemProvider
    {
        //public static IEnumerable<TreeNode> BuildTree(string path) => BuildTree(path, out _);
       // public static IEnumerable<TreeNode> BuildTree(string path, out List<FileNode>? leaves) => AssetRegistry.CreateRegistry(path).BuildTree(out leaves);
        public static IEnumerable<TreeNode> BuildTree(this AssetRegistry registry) => registry.BuildTree(out _);
        public static IEnumerable<TreeNode> BuildTree(this AssetRegistry registry, out List<FileNode>? leaves) => BuildTree(registry.State.Assets, out leaves);
        //public static IEnumerable<TreeNode> BuildTree(IReadOnlyList<Asset> Assets) => BuildTree(Assets, out _);
        public static IEnumerable<TreeNode> BuildTree(IReadOnlyList<Asset> Assets, out List<FileNode>? leaves)
        {
            var items = new List<TreeNode>();
            var files = new List<FileNode>();
            FolderNode? CrawlNode = null;
            FileNode? temp = null;
            foreach (Asset asset in Assets)
            {
                if (CrawlNode?.Path == asset.PackagePath)
                {
                    temp = CrawlNode?.EndChain(asset);
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
            }
            leaves = files;
            return items;
        }

        //public static FrameworkElement? FindParentByClass(this FrameworkElement control, Type Class)
        //{

        //    if (control.TemplatedParent is null)
        //        return null;

        //    //Debug.WriteLine("Type: " + (control.TemplatedParent.GetType() == Class) + "\n" + control.TemplatedParent.GetType().ToString());
        //    if (control.TemplatedParent.GetType() == Class)
        //        return (FrameworkElement?)control.TemplatedParent;

        //    return ((FrameworkElement?)control.TemplatedParent).FindParentByClass(Class);
        //}

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

        /// <summary>
        /// Grabs and auto-casts DataContext on a <see cref="TreeViewItem"/> into a subclass of <see cref="TreeNode"/> (<see cref="FileNode"/> or <see cref="FolderNode"/>).
        /// </summary>
        /// <typeparam name="T"><see cref="FileNode"/> or <see cref="FolderNode"/></typeparam>
        /// <param name="item"><see cref="TreeViewItem"/> to attempt a cast on DataContext.</param>
        /// <returns><see cref="FileNode"/>/<see cref="FolderNode"/></returns>
        /// <exception cref="InvalidCastException">Will throw exception if DataContext doesnt inherit from <see cref="TreeNode"/>.</exception>
        public static T? GetStoredNode<T>(this TreeViewItem item)
        {
            if (item.DataContext is null)
                 return default; 

            if(item.DataContext is not TreeNode n)
                throw new InvalidCastException($"{item.DataContext.GetType().FullName} does not inherit from Base Class 'TreeNode'");

            if (n is not T)
            {
                Debug.WriteLine($"Cast mismatch, was expecting {typeof(T).FullName} but got {item.DataContext.GetType().FullName}", "GetStoredNode.CastFailure");
                return default;
            }

            return (T)Convert.ChangeType(n, typeof(T));
        }

    }
}