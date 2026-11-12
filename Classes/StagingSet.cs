using AssetRegistryModMigrator.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;namespace AssetRegistryModMigrator.Classes
{
    public class StagingSet
    {
        // Expose this directly to your DataGrid's ItemsSource
        public ObservableCollection<FileNode> Items { get; } = new ObservableCollection<FileNode>();        // Internal lightning-fast lookup engine
        private readonly HashSet<FileNode> _lookup = new HashSet<FileNode>();        public bool Contains(FileNode node) => _lookup.Contains(node);        public void Add(FileNode node)
        {
            if (_lookup.Add(node)) // Only adds if it doesn't already exist (O(1))
            {
                Items.Add(node);   // Fires UI update notifications automatically
            }
        }        public void Remove(FileNode node)
        {
            if (_lookup.Remove(node)) // O(1) remove
            {
                Items.Remove(node);   // Removes row instantly from the DataGrid UI
            }
        }        public void Clear()
        {
            _lookup.Clear();
            Items.Clear();
        }
    }
}
