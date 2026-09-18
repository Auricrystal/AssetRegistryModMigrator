using AssetRegistryModMigrator.Model;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AssetRegistryModMigrator.Classes
{
    internal static class ItemCollectionExtensions
    {
        internal static void Do<T>(this ItemCollection collection,Action<T> onItem)
        {
            foreach (var item in collection)
            {
                Debug.WriteLine("Item Is: " + item.GetType());
                if(item is T castItem)
                    onItem(castItem);
            }
        }
        internal static IEnumerable<T> Where<T>(this ItemCollection collection,Func<T,bool> predicate)
        {
            foreach (var item in collection)
            {
                if (item is T)
                   if (item is T castItem && predicate(castItem))
                        yield return castItem;
            }
        }

        internal static void FilterBy(this ItemCollection collection, Func<TreeViewItem,bool> filter)
        {
            collection.Do<TreeViewItem>(item => item.Collapse()); //FolderNode
            collection.Where<TreeViewItem>(filter).Do(item => item.Expand());
        }

        internal static void Do<T>(this IEnumerable<T> items,Action<T> onItem)
        {
            foreach (var item in items)
                onItem(item);
        }
        internal static void Collapse(this TreeViewItem treeViewItem) => treeViewItem.Height = 0;
        internal static void Expand(this TreeViewItem treeViewItem)
        {
            treeViewItem.Height = Double.NaN;
            treeViewItem.IsExpanded = true;
        }
        internal static string GetHeaderOrEmpty(this TreeViewItem treeViewItem)
        {
            string? header = treeViewItem.Header as string;
            if (header == null) return string.Empty;
            return header;
        }
    }
}
