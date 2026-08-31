using AssetRegistryModMigrator.Classes;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AssetRegistryModMigrator.Model
{
    public class DirectoryItem : Item
    {
        public DirectoryItem(string name) : base(name)
        {
            Children = [];
        }

        public DirectoryItem(string name, Item parent) : base(name, parent)
        {
            Children = [];
        }

        public List<Item> Children { get; set; }

        public DirectoryItem Chain(string folder)
        {
            var node = new DirectoryItem(folder, this);
            Children.Add(node);
            return node;
        }
        public FileItem? EndChain(Asset asset)
        {
            if (Children.Exists(x => x.Name == asset.PackageName.Split("/", StringSplitOptions.RemoveEmptyEntries).Last()))
            {
                //Debug.WriteLine("Asset Already Exists!.. " + asset.AssetName);
                FileItem? file = ((FileItem?)Children.Find(x => x.Name == asset.PackageName.Split("/", StringSplitOptions.RemoveEmptyEntries).Last()));
                file?.AddAsset(asset);
                return file;
            }

            var node = new FileItem(asset, this);
            Children.Add(node);
            //Debug.WriteLine("New Asset!.. " + asset.AssetName);

            return node;
        }

        public override List<Asset> GetAllAssets()
        {
            return Children.SelectMany(x => x.GetAllAssets()).ToList();
        }

        public override void PropagateChecks(bool check)
        {
            foreach (var path in Children)
                path.PropagateChecks(check);

        }
    }
}
