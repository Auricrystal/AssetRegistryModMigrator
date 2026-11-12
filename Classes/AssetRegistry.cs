using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;namespace AssetRegistryModMigrator.Classes
{
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
        }        public string PackageName { get; set; }
        public string PackagePath { get; set; }
        public string AssetName { get; set; }
        public string AssetClass { get; set; }
        public bool? HasNumberlessTags { get; set; }
        public Dictionary<string, string> TagsAndValues { get; set; }
        public IReadOnlyList<object> Bundles { get; set; }
        public object PackageFlags { get; set; }
        public IReadOnlyList<int?> ChunkIds { get; set; }
        public object OldObjectPath { get; set; }
        public object OptionalOuterPath { get; set; }
        [JsonIgnore]
        public string UniqueKey => $"{PackageName}_{AssetClass}";

        // Add to Asset class in AssetRegistry.cs:
        public Asset DeepClone()
        {
            return new Asset(
                PackageName: this.PackageName,
                PackagePath: this.PackagePath,
                AssetName: this.AssetName,
                AssetClass: this.AssetClass,
                HasNumberlessTags: this.HasNumberlessTags,
                TagsAndValues: this.TagsAndValues != null
                    ? new Dictionary<string, string>(this.TagsAndValues, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal),
                Bundles: this.Bundles != null ? new List<object>(this.Bundles) : new List<object>(),
                PackageFlags: this.PackageFlags,
                ChunkIds: this.ChunkIds != null ? new List<int?>(this.ChunkIds) : new List<int?>(),
                OldObjectPath: this.OldObjectPath,
                OptionalOuterPath: this.OptionalOuterPath
            );
        }

    }    public class Header
    {
        [JsonConstructor]
        public Header(List<object> VersionGUID, int? VersionNumber, bool? FilterEditorOnly)
        {
            this.VersionGUID = VersionGUID;
            this.VersionNumber = VersionNumber;
            this.FilterEditorOnly = FilterEditorOnly;
        }
        public IReadOnlyList<object> VersionGUID { get; }
        public int? VersionNumber { get; }
        public bool? FilterEditorOnly { get; }
    }    public class Options
    {
        [JsonConstructor]
        public Options(bool? TextTagsFirst) { this.TextTagsFirst = TextTagsFirst; }
        public bool? TextTagsFirst { get; }
    }    public class State
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
            this.Options = Options;            var indexMap = new Dictionary<string, int>(Assets.Count, StringComparer.Ordinal);
            for (int i = 0; i < Assets.Count; i++)
            {
                indexMap[Assets[i].UniqueKey] = i;
            }
            KeyIndex = indexMap;
        }        public List<Asset> Assets { get; }
        public IReadOnlyList<object> Dependencies { get; }
        public IReadOnlyList<object> Packages { get; }
        public Options Options { get; }        internal readonly IReadOnlyDictionary<string, int> KeyIndex;
    }    public class AssetRegistry
    {
        [JsonConstructor]
        public AssetRegistry(Header Header, State State)
        {
            this.Header = Header;
            this.State = State;
        }        public Header Header { get; }
        public State State { get; }        // Optimized string-key target lookups
        public Asset? FindAsset(string uniqueKey)
        {
            return State.KeyIndex.TryGetValue(uniqueKey, out int index) ? State.Assets[index] : null;
        }        public static AssetRegistry? CreateRegistry(string path)
        {
            if (!File.Exists(path) || Path.GetExtension(path) != ".json")
                return null;            // FIX: Stream directly from the disk surface instead of downloading the whole string to memory
            var serializer = new JsonSerializer();
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536))
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return serializer.Deserialize<AssetRegistry>(jsonReader);
            }
        }        public static AssetRegistry? PickRegistryDialog(out string filename)
        {
            filename = "";
            FileDialog dialog = new OpenFileDialog() { DefaultExt = ".json", FileName = "AssetRegistry", Filter = "Json Files (.json)|*.json" };
            bool? result = dialog.ShowDialog();
            if (result != true) return null;            filename = dialog.FileName;
            return CreateRegistry(dialog.FileName);
        }
        public static async Task<AssetRegistry?> CreateRegistryAsync(string path, AssetRegistryService registryService)
        {
            if (!File.Exists(path))
                return null;

            string ext = Path.GetExtension(path);

            if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                // Run streaming deserialization on a background thread so large JSON files don't freeze WPF
                return await Task.Run(() => CreateRegistry(path));
            }

            if (ext.Equals(".bin", StringComparison.OrdinalIgnoreCase))
            {
                string tempJsonPath = Path.Combine(
                    Path.GetTempPath(),
                    $"AssetRegistry_{Path.GetFileNameWithoutExtension(path)}_{Guid.NewGuid():N}.json"
                );

                try
                {
                    await registryService.ConvertBinToJsonAsync(path, tempJsonPath);
                    return await Task.Run(() => CreateRegistry(tempJsonPath));
                }
                finally
                {
                    if (File.Exists(tempJsonPath))
                        File.Delete(tempJsonPath);
                }
            }

            return null;
        }

        public static async Task<(AssetRegistry? Registry, string FileChosen)> PickRegistryDialogAsync(AssetRegistryService registryService)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select AssetRegistry File (.bin or .json)",
                FileName = "AssetRegistry",
                Filter = "AssetRegistry Files (*.bin;*.json)|*.bin;*.json|Binary Files (*.bin)|*.bin|JSON Files (*.json)|*.json"
            };

            if (dialog.ShowDialog() != true)
                return (null, "");

            string fileChosen = dialog.FileName;
            AssetRegistry? registry = await CreateRegistryAsync(fileChosen, registryService);
            return (registry, fileChosen);
        }


    }
    [Flags]
    public enum EPackageFlags :uint
    {
        PKG_None = 0x00000000,
        PKG_NewlyCreated = 0x00000001,
        PKG_ClientOptional = 0x00000002,
        PKG_ServerSideOnly = 0x00000004,
        PKG_CompiledIn = 0x00000010,
        PKG_ForDiffing = 0x00000020,
        PKG_EditorOnly = 0x00000040,
        PKG_Developer = 0x00000080,
        PKG_UncookedOnly = 0x00000100,
        PKG_Cooked = 0x00000200,
        PKG_ContainsNoAsset = 0x00000400,
        PKG_NotExternallyReferenceable = 0x00000800,
        PKG_UnversionedProperties = 0x00002000,
        PKG_ContainsMapData = 0x00004000,
        PKG_IsSaving = 0x00008000,
        PKG_Compiling = 0x00010000,
        PKG_ContainsMap = 0x00020000,
        PKG_RequiresLocalizationGather = 0x00040000,
        PKG_PlayInEditor = 0x00100000,
        PKG_ContainsScript = 0x00200000,
        PKG_DisallowExport = 0x00400000,
        PKG_DynamicImports = 0x10000000,
        PKG_RuntimeGenerated = 0x20000000,
        PKG_ReloadingForReinstancing = 0x40000000,
        PKG_FilterEditorOnly = 0x80000000
    }

    public record FlagBadge(string Name, string HexValue);
    public record TagItem(string Key, string CleanValue, string RawValue);

    public class AssetInspectorViewModel
    {
        public Asset Source { get; }
        public string ShortAssetClass => Source.AssetClass?.Split('.').LastOrDefault() ?? Source.AssetClass ?? "";
        public string ChunkIdsDisplay => Source.ChunkIds != null && Source.ChunkIds.Any()
            ? string.Join(", ", Source.ChunkIds)
            : "None";

        public uint RawPackageFlags { get; }
        public string PackageFlagsHex => $"0x{RawPackageFlags:X8}";
        public List<FlagBadge> ActivePackageFlags { get; } = new();
        public List<TagItem> CleanedTags { get; } = new();
        public string RawJson { get; }

        public AssetInspectorViewModel(Asset asset, string rawJson)
        {
            Source = asset;
            RawJson = rawJson;

            // 1. Parse PackageFlags (handles long/uint/string from Newtonsoft)
            if (asset.PackageFlags != null && uint.TryParse(asset.PackageFlags.ToString(), out uint flags))
            {
                RawPackageFlags = flags;
                foreach (EPackageFlags flag in Enum.GetValues(typeof(EPackageFlags)))
                {
                    if (flag != EPackageFlags.PKG_None && (flags & (uint)flag) == (uint)flag)
                    {
                        ActivePackageFlags.Add(new FlagBadge(flag.ToString(), $"0x{(uint)flag:X8}"));
                    }
                }
            }

            // 2. Clean ANSI(...) and PATH__NO_NUM(...) wrappers in TagsAndValues
            if (asset.TagsAndValues != null)
            {
                foreach (var kvp in asset.TagsAndValues.OrderBy(k => k.Key))
                {
                    CleanedTags.Add(new TagItem(kvp.Key, CleanUnrealValue(kvp.Value), kvp.Value));
                }
            }
        }

        private static string CleanUnrealValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            // Strip outer ANSI(...) or PATH__NO_NUM(...)
            var match = Regex.Match(raw, @"^(?:ANSI|PATH__NO_NUM)\((.*)\)$", RegexOptions.Singleline);
            string inner = match.Success ? match.Groups[1].Value : raw;

            // Unwrap nested PATH__NO_NUM(...) if it was inside ANSI(...)
            var nestedMatch = Regex.Match(inner, @"^PATH__NO_NUM\((.*)\)$", RegexOptions.Singleline);
            if (nestedMatch.Success)
                inner = nestedMatch.Groups[1].Value;

            return inner.Replace("\\n", ", ");
        }
    }
}
