using AssetRegistryModMigrator.Classes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Linq;namespace AssetRegistryModMigrator.Model
{
    public class FileNode : TreeNode
    {
        public FileNode(Asset asset, string currentFolderFullPath, TreeNode parent) : base(asset, currentFolderFullPath)
        {
            Assets = [asset];
            ParentNode = parent;
            Class = asset.AssetClass;
            asset.TagsAndValues.TryGetValue("NativeParentClass", out var NativeParent);
            if (NativeParent != null)
            {
                NativeParentClass = CleanNativeClassPath(NativeParent);
            }
            else
            {
                NativeParentClass = "No Native Parent";
            }
        }
        // Add using System.Linq; at the top of FileNode.cs

        public FileNode(string name, string fullPath, List<Asset> assets) : base(name, fullPath)
        {
            Assets = assets ?? new List<Asset>();

            if (Assets.Count > 0)
            {
                var primary = Assets[0];
                Class = primary.AssetClass;

                if (primary.TagsAndValues != null &&
                    primary.TagsAndValues.TryGetValue("NativeParentClass", out var nativeParent) &&
                    nativeParent != null)
                {
                    NativeParentClass = CleanNativeClassPath(nativeParent);
                }
                else
                {
                    NativeParentClass = "No Native Parent";
                }
            }
            else
            {
                Class = string.Empty;
                NativeParentClass = string.Empty;
            }
        }

        public FileNode CreateStagedClone()
        {
            var clonedAssets = Assets.Select(a => a.DeepClone()).ToList();

            return new FileNode(this.Name, this.FullPath, clonedAssets)
            {
                Selected = true,
                Class = this.Class,
                NativeParentClass = this.NativeParentClass
            };
        }

        public List<Asset> Assets { get; set; } = new();        // FIX: Satisfies WPF's recursive template engine scans 
        // by providing an empty list instead of throwing missing property errors
        public IEnumerable<object> Children => System.Array.Empty<object>();
        public string Class { get; set; }
        public string NativeParentClass { get; set; }
        public void AddAsset(Asset asset) => Assets.Add(asset);        public override List<Asset> GetAllAssets() => Assets;        public override int GetHashCode()
        {
            return Assets?.GetHashCode() ?? base.GetHashCode();
        }
        private bool _isEdited;
        public bool IsEdited
        {
            get => _isEdited;
            set
            {
                if (_isEdited != value)
                {
                    _isEdited = value;
                    OnPropertyChanged(nameof(IsEdited));
                }
            }
        }
        /// <summary>
        /// Extract the inner script path from string formats like ANSI('/Script/...''/Script/Target')
        /// </summary>
        private string CleanNativeClassPath(string rawClass)
        {
            if (string.IsNullOrWhiteSpace(rawClass)) return string.Empty;            // Find the last index position of the string sequence '/Script/'
            int lastScriptIdx = rawClass.LastIndexOf("'/Script/");
            if (lastScriptIdx >= 0)
            {
                // Trims off the leading parts and trailing parenthesis wrapper symbols
                string trimmed = rawClass.Substring(lastScriptIdx).TrimEnd(')', '\'');
                return trimmed; // Returns: '/Script/GunfireRuntime.Equipment'
            }            // Fallback string matching alternative case
            int fallbackIdx = rawClass.LastIndexOf("/Script/");
            if (fallbackIdx >= 0)
            {
                return rawClass.Substring(fallbackIdx).TrimEnd(')', '\'');
            }            return rawClass;
        }
    }
}
