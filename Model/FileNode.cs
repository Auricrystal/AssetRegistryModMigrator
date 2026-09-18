using AssetRegistryModMigrator.Classes;
using System.Runtime.CompilerServices;

namespace AssetRegistryModMigrator.Model
{
    public class FileNode : TreeNode
    {
        public FileNode(Asset asset, TreeNode parent) : base(asset)
        {
            Assets = [asset];
            ParentNode = parent;
        }
        private List<Asset> Assets { get; set; }
        public void AddAsset(Asset asset) => Assets.Add(asset);

        public override List<Asset> GetAllAssets() => Assets;

        public override int GetHashCode()
        {
            return Assets.GetHashCode();
        }
    }
}
