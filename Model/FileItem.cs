using AssetRegistryModMigrator.Classes;

namespace AssetRegistryModMigrator.Model
{
    public class FileItem : Item
    {
        public FileItem(Asset asset, Item parent) : base(asset)
        {
            Assets = [asset];
            Parent = parent;
        }

        private List<Asset> Assets { get; set; }
        public void AddAsset(Asset asset) => Assets.Add(asset);

        public override List<Asset> GetAllAssets() => Assets;
    }
}
