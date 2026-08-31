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

        public List<Asset> Assets { get; set; }
        public bool Checked { get; set; }
        public void AddAsset(Asset asset)
        {
            Assets.Add(asset);
        }

        public override List<Asset> GetAllAssets()
        {
            return Assets;
        }

        public override void PropagateChecks(bool check)
        {
            Checked = check;
        }
    }
}
