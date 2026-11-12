using AssetRegistryModMigrator.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;namespace AssetRegistryModMigrator.Classes
{
    public static class ItemProvider
    {
        public static IEnumerable<TreeNode> BuildTree(this AssetRegistry registry) => registry.BuildTree(out _);
        public static IEnumerable<TreeNode> BuildTree(this AssetRegistry registry, out List<FileNode>? leaves) => BuildTree(registry.State.Assets, out leaves);        // Explicitly pre-type the inner collections to hold structured subclasses
        private static readonly Dictionary<string, List<TreeNode>> _donorRegistrySubMaps = new Dictionary<string, List<TreeNode>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<TreeNode>> _receiverRegistrySubMaps = new Dictionary<string, List<TreeNode>>(StringComparer.OrdinalIgnoreCase);        public static IEnumerable<TreeNode> BuildLazyTree(IReadOnlyList<Asset> assets, bool isDonorRegistry, out List<FileNode> leaves)
        {
            var rootNodes = new List<TreeNode>();
            var files = new List<FileNode>(assets.Count);            var subMaps = isDonorRegistry ? _donorRegistrySubMaps : _receiverRegistrySubMaps;
            subMaps.Clear();            var folderCache = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
            var fileCache = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);            foreach (Asset asset in assets)
            {
                string packagePath = asset.PackagePath;
                if (string.IsNullOrWhiteSpace(packagePath)) continue;                // Ensure packagePath always has a single standardized leading slash format
                if (!packagePath.StartsWith("/")) packagePath = "/" + packagePath;                // 1. Properly isolate the top-level base root folder (e.g., "/ACLPlugin")
                int nextSlashIdx = packagePath.IndexOf('/', 1);
                string rootFolderName = nextSlashIdx == -1 ? packagePath : packagePath.Substring(0, nextSlashIdx);                if (!folderCache.TryGetValue(rootFolderName, out FolderNode? rootFolder))
                {
                    // Set root folder display name to clean text (strip the leading slash for display only)
                    string cleanDisplayName = rootFolderName.TrimStart('/');
                    rootFolder = new FolderNode(cleanDisplayName, rootFolderName);
                    rootNodes.Add(rootFolder);
                    folderCache[rootFolderName] = rootFolder;
                }                // 2. Isolate the asset filename leaf cleanly as a typed FileNode
                string packageName = asset.PackageName;
                int lastSlashIdx = packageName.LastIndexOf('/');
                string assetLeafName = lastSlashIdx == -1 ? packageName : packageName.Substring(lastSlashIdx + 1);                string uniqueFileKey = $"{packagePath}/{assetLeafName}";                if (!fileCache.TryGetValue(uniqueFileKey, out FileNode? fileNode))
                {
                    fileNode = new FileNode(asset, packagePath, rootFolder);
                    fileCache[uniqueFileKey] = fileNode;
                    files.Add(fileNode);                    if (!subMaps.ContainsKey(packagePath))
                    {
                        subMaps[packagePath] = new List<TreeNode>();
                    }
                    subMaps[packagePath].Add(fileNode);
                }
                else
                {
                    fileNode.AddAsset(asset);
                }                // 3. Build parent-child branch associations ensuring paths match FullPath perfectly
                string[] parts = packagePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string workingPath = "";                for (int i = 0; i < parts.Length - 1; i++)
                {
                    // FIX: Always build the keys with a leading slash to match folderNode.FullPath contracts!
                    string parentPath = i == 0 ? $"/{parts[i]}" : $"{workingPath}/{parts[i]}";
                    string currentSubPath = $"{parentPath}/{parts[i + 1]}";
                    workingPath = parentPath;                    if (!subMaps.ContainsKey(parentPath))
                    {
                        subMaps[parentPath] = new List<TreeNode>();
                    }                    if (!subMaps[parentPath].Any(x => x.FullPath.Equals(currentSubPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        var subFolderNode = new FolderNode(parts[i + 1], currentSubPath);
                        subMaps[parentPath].Add(subFolderNode);
                    }
                }
            }            leaves = files;
            return rootNodes;
        }
        public static IEnumerable<TreeNode> GetChildrenOnDemand(string parentFullPath, bool isDonorRegistry)
        {
            var subMaps = isDonorRegistry ? _donorRegistrySubMaps : _receiverRegistrySubMaps;
            if (subMaps.TryGetValue(parentFullPath, out var children))
            {
                return children;
            }
            return Enumerable.Empty<TreeNode>();
        }        public static void Filter(this TreeView self, Predicate<object> predicate)
        {
            if (self == null) return;
            ICollectionView view = CollectionViewSource.GetDefaultView(self.ItemsSource);
            if (view == null) return;
            view.Filter = predicate;
        }        public static IEnumerable<TreeNode> BuildTree(IReadOnlyList<Asset> Assets, out List<FileNode>? leaves)
        {
            var rootNodes = new List<TreeNode>();
            var files = new List<FileNode>(Assets.Count);            var folderCache = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
            var fileCache = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);            foreach (Asset asset in Assets)
            {
                string packagePath = asset.PackagePath;
                if (string.IsNullOrWhiteSpace(packagePath)) continue;                FolderNode? currentParent = null;
                int start = 0;
                int len = packagePath.Length;                while (start < len)
                {
                    if (packagePath[start] == '/')
                    {
                        start++;
                        continue;
                    }                    int nextSlash = packagePath.IndexOf('/', start);
                    int count = (nextSlash == -1) ? len - start : nextSlash - start;                    string folderName = packagePath.Substring(start, count);
                    string currentAccumulatedPath = (currentParent == null) ? folderName : $"{currentParent.FullPath}/{folderName}";                    if (!folderCache.TryGetValue(currentAccumulatedPath, out FolderNode? cachedFolder))
                    {
                        if (currentParent == null)
                        {
                            cachedFolder = new FolderNode(folderName, folderName);
                            rootNodes.Add(cachedFolder);
                        }
                        else
                        {
                            cachedFolder = new FolderNode(folderName, currentAccumulatedPath, currentParent);
                            currentParent.Children.Add(cachedFolder);
                        }
                        folderCache[currentAccumulatedPath] = cachedFolder;
                    }                    currentParent = cachedFolder;
                    start += count;
                }                if (currentParent != null)
                {
                    string packageName = asset.PackageName;
                    int lastSlashIdx = packageName.LastIndexOf('/');
                    string assetLeafName = (lastSlashIdx == -1) ? packageName : packageName.Substring(lastSlashIdx + 1);                    string uniqueFileKey = $"{currentParent.FullPath}/{assetLeafName}";                    if (fileCache.TryGetValue(uniqueFileKey, out FileNode? existingFileNode))
                    {
                        existingFileNode.AddAsset(asset);
                    }
                    else
                    {
                        var newFileNode = new FileNode(asset, currentParent.FullPath, currentParent);
                        currentParent.Children.Add(newFileNode);                        fileCache[uniqueFileKey] = newFileNode;
                        files.Add(newFileNode);
                    }
                }
            }            leaves = files;
            return rootNodes;
        }
    }
}
