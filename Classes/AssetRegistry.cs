using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AssetRegistryModMigrator.Classes
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Asset
    {
        [JsonConstructor]
        public Asset(
            string PackageName,
            string PackagePath,
            string AssetName,
            string AssetClass,
            bool? HasNumberlessTags,
            Dictionary<string, string> TagsAndValues,
            List<object> Bundles,
            object PackageFlags,
            List<int?> ChunkIds,
            object OldObjectPath,
            object OptionalOuterPath
        )
        {
            this.PackageName = PackageName;
            this.PackagePath = PackagePath;
            this.AssetName = AssetName;
            this.AssetClass = AssetClass;
            this.HasNumberlessTags = HasNumberlessTags;
            this.TagsAndValues = TagsAndValues;
            this.Bundles = Bundles;
            this.PackageFlags = PackageFlags;
            this.ChunkIds = ChunkIds;
            this.OldObjectPath = OldObjectPath;
            this.OptionalOuterPath = OptionalOuterPath;
        }

        public string PackageName { get; }
        public string PackagePath { get; }
        public string AssetName { get; }
        public string AssetClass { get; }
        public bool? HasNumberlessTags { get; }
        public Dictionary<string,string> TagsAndValues { get; }
        public IReadOnlyList<object> Bundles { get; }
        public object PackageFlags { get; }
        public IReadOnlyList<int?> ChunkIds { get; }
        public object OldObjectPath { get; }
        public object OptionalOuterPath { get; }
    }

    public class Header
    {
        [JsonConstructor]
        public Header(
            List<object> VersionGUID,
            int? VersionNumber,
            bool? FilterEditorOnly
        )
        {
            this.VersionGUID = VersionGUID;
            this.VersionNumber = VersionNumber;
            this.FilterEditorOnly = FilterEditorOnly;
        }

        public IReadOnlyList<object> VersionGUID { get; }
        public int? VersionNumber { get; }
        public bool? FilterEditorOnly { get; }
    }

    public class Options
    {
        [JsonConstructor]
        public Options(
            bool? TextTagsFirst
        )
        {
            this.TextTagsFirst = TextTagsFirst;
        }

        public bool? TextTagsFirst { get; }
    }

    public class AssetRegistry
    {
        [JsonConstructor]
        public AssetRegistry(
            Header Header,
            State State
        )
        {
            this.Header = Header;
            this.State = State;
        }

        public Header Header { get; }
        public State State { get; }
    }

    public class State
    {
        [JsonConstructor]
        public State(
            List<Asset> Assets,
            List<object> Dependencies,
            List<object> Packages,
            Options Options
        )
        {
            this.Assets = Assets;
            this.Dependencies = Dependencies;
            this.Packages = Packages;
            this.Options = Options;
        }

        public IReadOnlyList<Asset> Assets { get; }
        public IReadOnlyList<object> Dependencies { get; }
        public IReadOnlyList<object> Packages { get; }
        public Options Options { get; }
    }

}
