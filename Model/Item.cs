using AssetRegistryModMigrator.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AssetRegistryModMigrator.Model
{
    public abstract class Item
    {
        public Item(string name)
        {
            Name = name;
        }
        public Item(string name, Item parent):this(name)
        {
            Name = name;
            Parent = parent;
        }
        protected Item(Asset asset) {
            Name = asset.PackageName.Split("/", StringSplitOptions.RemoveEmptyEntries).Last();
            //Path = asset.PackageName;
        }
       
        public string Name { get; set; }
        public string Path { get{ return (Parent is not null) ? Parent?.ToString() + "/" + Name : "/" + Name; } }
        public Item? Parent { get; set; }


        public override string? ToString()
        {
            return (Parent is not null) ? Parent?.ToString() + "/" + Name : "/" + Name;
        }

        public virtual List<Asset> GetAllAssets()
        {
            return new List<Asset>();
        }

        public abstract void PropagateChecks(bool check);
    }
}
