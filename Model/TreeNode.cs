using AssetRegistryModMigrator.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;namespace AssetRegistryModMigrator.Model
{
    public abstract class TreeNode : ObservableObject
    {
        private string _name = "";
        private bool _isExpanded;
        private bool? _selected = false;        // Optimizations: Pre-compute paths to eliminate runtime string cloning loops
        private readonly string _cachedPath;
        private readonly string _cachedFullPath;        public TreeNode(string name, string fullPath)
        {
            Name = name; // FIX: Use the public property setter to trigger layout bindings!
            _cachedPath = fullPath;
            _cachedFullPath = fullPath;
        }        public TreeNode(string name, string fullPath, TreeNode parent)
        {
            Name = name; // FIX: Use the public property setter to trigger layout bindings!
            ParentNode = parent;
            _cachedPath = fullPath;
            _cachedFullPath = fullPath;
        }
        protected TreeNode(Asset asset, string currentFolderFullPath)
        {
            string pkg = asset.PackageName;
            int lastSlashIndex = pkg.LastIndexOf('/');
            _name = lastSlashIndex >= 0 ? pkg.Substring(lastSlashIndex + 1) : pkg;            _cachedPath = currentFolderFullPath;
            _cachedFullPath = $"{currentFolderFullPath}/{_name}";
        }        public int Depth => ParentNode != null ? ParentNode.Depth + 1 : 1;
        public int HashCode => GetHashCode();        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }        public string Path => _cachedPath;
        public string FullPath => _cachedFullPath;
        public TreeNode? ParentNode { get; set; }
        public bool IsRoot => ParentNode == null;
        public bool IsLeaf => this is FileNode;        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }        public bool? Selected
        {
            get => _selected;
            set
            {
                if (value != _selected)
                {
                    _selected = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        /// <summary>
        /// High-performance recursive pass to update visibility states and expand matching folder branches.
        /// </summary>
        public bool ApplyDeepSearchFilter(string[] tokens)
        {
            // If search text field is completely empty, reset all items to visible and collapse them
            if (tokens == null || tokens.Length == 0)
            {
                IsVisible = true;
                IsExpanded = false;                if (this is FolderNode folder)
                {
                    var children = folder.Children;
                    for (int i = 0; i < children.Count; i++)
                    {
                        children[i].ApplyDeepSearchFilter(tokens);
                    }
                }
                return true;
            }            if (IsLeaf)
            {
                // OPTIMIZATION: Replaced tokens.All() with raw indexing loop to bypass heap allocations
                bool isMatch = true;
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (!_cachedFullPath.Contains(tokens[i], StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = false;
                        break;
                    }
                }
                IsVisible = isMatch;
                return isMatch;
            }            if (this is FolderNode folderNode)
            {
                bool anyChildVisible = false;
                var children = folderNode.Children;                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i].ApplyDeepSearchFilter(tokens))
                    {
                        anyChildVisible = true; break;
                    }
                }                // Folder is visible ONLY if it contains matching child nodes
                IsVisible = anyChildVisible;                // Auto-expand the folder if it leads directly down to matching target nodes
                IsExpanded = anyChildVisible;                return IsVisible;
            }            return false;
        }        public System.Windows.Controls.TreeViewItem? OwningTreeViewItem { get; set; }        public override string ToString() => _cachedPath;        public virtual List<Asset> GetAllAssets() => [];        public bool TreeHasMatchingName(string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;            // Fast direct string context evaluation
            if (IsLeaf) return _cachedFullPath.Contains(search, StringComparison.OrdinalIgnoreCase);            if (this is FolderNode folder)
            {
                // Accessing underlying fields directly removes property lookup overheads inside massive deep loop sweeps
                var items = folder.Children;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].TreeHasMatchingName(search)) return true;
                }
            }            return false;
        }
    }    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;        /// <summary>
        /// Modernized compiler-driven Property Change Handler
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName == null) return;            #if DEBUG
            VerifyPropertyName(propertyName);
            #endif            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }        [Conditional("DEBUG")]
        private void VerifyPropertyName(string propertyName)
        {
            if (TypeDescriptor.GetProperties(this)[propertyName] == null)
            {
                throw new ArgumentException($"Invalid property structure contract: {propertyName}");
            }
        }
    }

    public class FolderIconConverter :IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int childCount)
            {
                // If it has children, it's a folder container.
                // If it has 0 children, it's the penultimate selectable setting leaf.
                return childCount > 0 ? "📁" : "⚙️";
            }
            return "📁";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class TreeViewTemplateSelector :DataTemplateSelector
    {
        public DataTemplate? FolderTemplate { get; set; }
        public DataTemplate? FileTemplate { get; set; }        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is FolderNode)
                return FolderTemplate;            if (item is FileNode)
                return FileTemplate;            return base.SelectTemplate(item, container);
        }
    }
}