using AssetRegistryModMigrator.Classes;

namespace AssetRegistryModMigrator.Model
{
    public abstract class Item //: INotifyPropertyChanged
    {
        public Item(string name)
        {
            Name = name;
            //Checked = false;
        }
        public Item(string name, Item parent) : this(name)
        {
            Name = name;
            Parent = parent;
        }
        protected Item(Asset asset)
        {
            Name = asset.PackageName.Split("/", StringSplitOptions.RemoveEmptyEntries).Last();
            //Checked = false;
            //Path = asset.PackageName;
        }

        public string Name { get; set; }
        public string Path { get { return (Parent is not null) ? Parent?.ToString() + "/" + Name : "/" + Name; } }
        public Item? Parent { get; set; }

        // public abstract event PropertyChangedEventHandler? PropertyChanged;

        //public bool? Checked { get; set; }

        public override string? ToString()
        {
            return (Parent is not null) ? Parent?.ToString() + "/" + Name : "/" + Name;
        }

        public virtual List<Asset> GetAllAssets()
        {
            return new List<Asset>();
        }

        //public virtual void PropagateChecks(bool? check) { Checked = check; }
    }
}
