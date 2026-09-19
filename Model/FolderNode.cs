using AssetRegistryModMigrator.Classes;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace AssetRegistryModMigrator.Model
{
    public class FolderNode : TreeNode
    {
        public FolderNode(string name) : base(name)
        {
            Children = [];
        }

        public FolderNode(string name, TreeNode parent) : base(name, parent)
        {
            Children = [];
        }

        public ObservableCollection<TreeNode> Children { get; set; }

        public FolderNode Chain(string folder)
        {
            var node = new FolderNode(folder, this);
            Children.Add(node);
            return node;
        }
        public FileNode? EndChain(Asset asset)
        {
            
            if (Children.ToList().Exists(x => x.Name == asset.PackageName.Split("/", StringSplitOptions.RemoveEmptyEntries).Last()))
            {
                FileNode? file = ((FileNode?)Children.ToList().Find(x => x.Name == asset.PackageName.Split("/", StringSplitOptions.RemoveEmptyEntries).Last()));

                file?.AddAsset(asset);
                return file;
            }

            var node = new FileNode(asset, this);
            Children.Add(node);

            return node;
        }
        public List<FileNode> GetAllFiles()
        {
            var files = new List<FileNode>();
            foreach (var child in Children)
            {
                if (child is FileNode)
                {
                    files.Add((FileNode)child);
                    continue;
                }
                files.AddRange(((FolderNode)child).GetAllFiles());
            }

            return files;
        }

        public override List<Asset> GetAllAssets()
        {
            return Children.SelectMany(x => x.GetAllAssets()).ToList();
        }

        public void SetExpansionAll(bool isExpanded)
        {
            IsExpanded = isExpanded;
            foreach (var child in Children)
            {
                if (child is not FolderNode)
                    continue;
                (child as FolderNode)?.SetExpansionAll(isExpanded);
            }
        }
    }
}
