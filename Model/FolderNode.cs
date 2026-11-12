using AssetRegistryModMigrator.Classes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;namespace AssetRegistryModMigrator.Model
{
    public class FolderNode : TreeNode
    {
        private bool _isLazyInitialized = false;
        public bool IsLazyInitialized => _isLazyInitialized;        public FolderNode(string name, string fullPath) : base(name, fullPath)
        {
            Children = new ObservableCollection<TreeNode>();
            // Add a temporary hidden dummy item so WPF renders the expansion arrow
            Children.Add(new DummyNode());
        }        public FolderNode(string name, string fullPath, TreeNode parent) : base(name, fullPath, parent)
        {
            Children = new ObservableCollection<TreeNode>();
            Children.Add(new DummyNode());
        }        public ObservableCollection<TreeNode> Children { get; set; }        /// <summary>
        /// Instantly swaps out the placeholder dummy tracking nodes with actual live sub-assets on demand
        /// </summary>
        public void PerformLazyInitialization(IEnumerable<TreeNode> realChildren)
        {
            if (_isLazyInitialized) return;            Children.Clear();
            foreach (var child in realChildren)
            {
                Children.Add(child);
            }
            _isLazyInitialized = true;
        }        public List<FileNode> GetAllFiles()
        {
            var files = new List<FileNode>();
            AccumulateFiles(this, files);
            return files;
        }        private static void AccumulateFiles(FolderNode folder, List<FileNode> files)
        {
            var items = folder.Children;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is FileNode file)
                {
                    files.Add(file);
                }
                else if (items[i] is FolderNode subFolder)
                {
                    AccumulateFiles(subFolder, files);
                }
            }
        }        public override List<Asset> GetAllAssets()
        {
            var sharedResult = new List<Asset>(256);
            var items = Children;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is FileNode file) sharedResult.AddRange(file.Assets);
                else if (items[i] is FolderNode subFolder) sharedResult.AddRange(subFolder.GetAllAssets());
            }
            return sharedResult;
        }        public void SetExpansionAll(bool isExpanded)
        {
            IsExpanded = isExpanded;
            var items = Children;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is FolderNode folder)
                {
                    folder.SetExpansionAll(isExpanded);
                }
            }
        }
    }    /// <summary>
    /// Placeholder token class to safely trigger WPF layout arrows
    /// </summary>
    public class DummyNode : TreeNode
    {
        // FIX: IsVisible must remain TRUE so the WPF TreeView item style 
        // renders the dropdown expansion arrows correctly on screen!
        public DummyNode() : base("Loading...", "")
        {
            IsVisible = true;
        }
    }}
