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
            //Debug.WriteLine(string.Format("Adding Subfolder {1}/{0}",folder,this.Name));
            var node = new FolderNode(folder, this);
            Children.Add(node);
            return node;
        }
        public FileNode? EndChain(Asset asset)
        {
            
            if (Children.ToList().Exists(x => x.Name == asset.PackageName.Split("/", StringSplitOptions.RemoveEmptyEntries).Last()))
            {
                FileNode? file = ((FileNode?)Children.ToList().Find(x => x.Name == asset.PackageName.Split("/", StringSplitOptions.RemoveEmptyEntries).Last()));
                //Debug.WriteLine(string.Format("Combining {0} into {1}",asset.AssetName,file?.ToString()));

                file?.AddAsset(asset);
                return file;
            }

            var node = new FileNode(asset, this);
            Children.Add(node);
            //Debug.WriteLine("New Asset!.. " + asset.AssetName);

            return node;
        }
        public List<FileNode> GetAllFiles()
        {
            var files = new List<FileNode>();
            //var test = Children.Where(x => x is FolderNode).SelectMany(x => (x as FolderNode).GetAllFiles());
            foreach (var child in Children)
            {
                if (child is FileNode)
                {
                    //Debug.WriteLine(child.Name);
                    files.Add((FileNode)child);
                    continue;
                }
                files.AddRange(((FolderNode)child).GetAllFiles());
            }

            return files;

            return Children
                .Where(x=>x is FolderNode)
                .SelectMany(x => (x as FolderNode)
                .GetAllFiles())
                .ToList();
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
