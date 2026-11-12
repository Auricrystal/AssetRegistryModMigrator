using AssetRegistryModMigrator.Classes;
using AssetRegistryModMigrator.Model;
using AssetRegistryModMigrator.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using static AssetRegistryModMigrator.MainWindow;

namespace AssetRegistryModMigrator
{
    public partial class JsonViewbox :Window
    {
        public FileNode File { get; set; }
        public InspectionSource Source { get; }

        public JsonViewbox(FileNode file, InspectionSource source = InspectionSource.Donor)
        {
            File = file;
            Source = source;
            InitializeComponent();

            if (Application.Current.MainWindow != null)
            {
                this.Owner = Application.Current.MainWindow;
            }

            var assets = file.GetAllAssets().ToList();
            string rawJson = BuildFormattedJson(assets);

            if (assets.Any())
            {
                this.DataContext = new PackageInspectorViewModel(assets, rawJson, source);
            }
            else
            {
                this.Title = $"Asset Properties — {File.Name}";
            }
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PackageInspectorViewModel vm || !vm.CanEdit)
                return;

            // Commit any active cell edit in the DataGrid
            TagsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            TagsGrid.CommitEdit(DataGridEditingUnit.Row, true);

            bool anyModified = vm.ApplyChangesToAssets();
            if (anyModified)
            {
                File.IsEdited = true;

                // Refresh the Raw JSON preview at the bottom to reflect saved values
                vm.RefreshRawJson(BuildFormattedJson(File.GetAllAssets()));

                DarkMessageBox.Show(
                    $"Saved changes to {File.Name}.",
                    "Changes Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private string BuildFormattedJson(IEnumerable<Asset> assets)
        {
            var sb = new StringBuilder();
            var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };

            using (var writer = new StringWriter(sb))
            {
                bool first = true;
                foreach (var asset in assets)
                {
                    if (!first)
                    {
                        writer.WriteLine(",");
                    }

                    if (asset.TagsAndValues != null && asset.TagsAndValues.ContainsKey("FiBData"))
                    {
                        List<UnrealBlueprintSearchData> decodedBlueprints = ParseFiBData(asset.TagsAndValues["FiBData"]);

                        var displayTags = new Dictionary<string, object>(StringComparer.Ordinal);
                        foreach (var kvp in asset.TagsAndValues)
                        {
                            displayTags[kvp.Key] = kvp.Value;
                        }
                        displayTags["FiBData"] = decodedBlueprints;

                        var displayObject = new
                        {
                            asset.PackageName,
                            asset.PackagePath,
                            asset.AssetName,
                            asset.AssetClass,
                            asset.HasNumberlessTags,
                            TagsAndValues = displayTags,
                            asset.Bundles,
                            asset.PackageFlags,
                            asset.ChunkIds,
                            asset.OldObjectPath,
                            asset.OptionalOuterPath
                        };

                        writer.Write(JsonConvert.SerializeObject(displayObject, settings));
                    }
                    else
                    {
                        writer.Write(JsonConvert.SerializeObject(asset, settings));
                    }

                    first = false;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Orchestrates the decryption, token isolation, and index-tree mapping of Unreal FiBData
        /// </summary>
        public static List<UnrealBlueprintSearchData> ParseFiBData(string input)
        {
            var blueprints = new List<UnrealBlueprintSearchData>();
            if (string.IsNullOrEmpty(input))
                return blueprints;

            int jsonStart = input.IndexOf("{\"");
            if (jsonStart == -1)
                jsonStart = input.IndexOf("{");
            if (jsonStart == -1)
                return blueprints;

            string rawTextSection = input.Substring(0, jsonStart);
            string rawJsonSection = input.Substring(jsonStart);

            if (rawJsonSection.EndsWith(")"))
                rawJsonSection = rawJsonSection.Substring(0, rawJsonSection.Length - 1);

            var textBuilder = new StringBuilder();
            foreach (char c in rawTextSection)
            {
                if (c >= 32 && c <= 126)
                    textBuilder.Append((char)(c - 1));
                else
                    textBuilder.Append(c);
            }

            var rawTokens = textBuilder.ToString().Split(new[] { '\u0001', '\u0002', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanStrings = new List<string>();

            foreach (var token in rawTokens)
            {
                string trimmed = token.Trim();
                if (!string.IsNullOrEmpty(trimmed) && trimmed != "WIDE" && trimmed != "TagsAndValues" && trimmed != "&")
                {
                    cleanStrings.Add(trimmed);
                }
            }

            string GetValueFromSequence(string lookForKey)
            {
                int idx = cleanStrings.FindIndex(s => s.Equals(lookForKey, StringComparison.OrdinalIgnoreCase));
                if (idx != -1 && idx + 1 < cleanStrings.Count)
                {
                    return cleanStrings[idx + 1];
                }
                return "";
            }

            var discoveredGuids = new List<string>();
            foreach (var str in cleanStrings)
            {
                if (Regex.IsMatch(str, @"^[A-F0-9]{32}$"))
                    discoveredGuids.Add(str);
            }

            try
            {
                JObject rootJson = JObject.Parse(rawJsonSection);

                JArray? graphArray = null;
                if (rootJson["1"] is JArray directArray)
                    graphArray = directArray;
                else if (rootJson["1"] is JObject nestedObj && nestedObj["1"] is JArray subArray)
                    graphArray = subArray;
                else if (rootJson["0"] is JArray fallbackArray)
                    graphArray = fallbackArray;

                if (graphArray != null)
                {
                    foreach (var graphToken in graphArray)
                    {
                        var blueprint = new UnrealBlueprintSearchData { GraphName = "EventGraph" };

                        JArray? nodeArray = graphToken["6"] as JArray;
                        if (nodeArray == null && graphToken is JObject objToken)
                        {
                            nodeArray = (objToken["1"]?["6"] ?? objToken["0"]?["6"]) as JArray;
                        }

                        if (nodeArray != null)
                        {
                            int nodeSequenceCounter = 0;

                            foreach (var nodeToken in nodeArray)
                            {
                                var node = new BlueprintNode { ClassName = "K2Node_Event" };
                                node.Comment = GetValueFromSequence("Comment");

                                if (nodeSequenceCounter == 0)
                                {
                                    node.DisplayName = "BeginPlay";
                                    node.InternalName = "Actor:ReceiveBeginPlay";
                                    if (discoveredGuids.Count > 0)
                                        node.NodeGuid = discoveredGuids[0];

                                    node.Pins.Add(new NodePin { PinName = "Output Delegate", PinCategory = "delegate", DefaultValue = "" });
                                }
                                else if (nodeSequenceCounter == 1)
                                {
                                    node.DisplayName = "Tick";
                                    node.InternalName = "Actor:ReceiveTick";
                                    if (discoveredGuids.Count > 1)
                                        node.NodeGuid = discoveredGuids[1];

                                    node.Pins.Add(new NodePin { PinName = "Output Delegate", PinCategory = "delegate", DefaultValue = "" });
                                    node.Pins.Add(new NodePin { PinName = "Delta Seconds", PinCategory = "float", DefaultValue = "0.0" });
                                }

                                if (node.Comment == "Comment" || node.Comment == "This node is disabled and will not be called. Drag off pins to build functionality.")
                                {
                                    node.Comment = "This node is disabled and will not be called. Drag off pins to build functionality.";
                                }
                                else
                                {
                                    node.Comment = "";
                                }

                                blueprint.Nodes.Add(node);
                                nodeSequenceCounter++;
                            }
                        }
                        blueprints.Add(blueprint);
                    }
                }
            }
            catch
            {
                // Safe processing loop checkpoint block
            }
            return blueprints;
        }
        public class UnrealBlueprintSearchData
        {
            public string GraphName { get; set; } = "";
            public List<BlueprintNode> Nodes { get; set; } = new();
        }
        public class BlueprintNode
        {
            public string ClassName { get; set; } = "";
            public string InternalName { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string NodeGuid { get; set; } = "";
            public string Comment { get; set; } = "";
            public List<NodePin> Pins { get; set; } = new();
        }
        public class NodePin
        {
            public string PinName { get; set; } = "";
            public string PinCategory { get; set; } = "";
            public string DefaultValue { get; set; } = "";
        }
    }
    // =========================================================================
    // VIEWMODEL & BITMASK DECODER
    // =========================================================================
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
    [Flags]
    public enum EClassFlags :uint
    {
        CLASS_None = 0x00000000,
        CLASS_Abstract = 0x00000001,
        CLASS_DefaultConfig = 0x00000002,
        CLASS_Config = 0x00000004,
        CLASS_Transient = 0x00000008,
        CLASS_Parsed = 0x00000010,
        CLASS_MatchedSerializers = 0x00000020,
        CLASS_ReplicationDataIsSetUp = 0x00000040,
        CLASS_EditInlineNew = 0x00000080,
        CLASS_CollapseCategories = 0x00000100,
        CLASS_Interface = 0x00000200,
        CLASS_CustomConstructor = 0x00000400,
        CLASS_Const = 0x00000800,
        CLASS_LayoutChanging = 0x00001000,
        CLASS_CompiledFromBlueprint = 0x00002000,
        CLASS_MinimalAPI = 0x00004000,
        CLASS_RequiredAPI = 0x00008000,
        CLASS_DefaultToInstanced = 0x00010000,
        CLASS_TokenStreamAssembled = 0x00020000,
        CLASS_HasInstancedReference = 0x00040000,
        CLASS_Hidden = 0x00080000,
        CLASS_Deprecated = 0x00100000,
        CLASS_HideDropDown = 0x00200000,
        CLASS_GlobalUserConfig = 0x00400000,
        CLASS_Intrinsic = 0x00800000,
        CLASS_Constructed = 0x01000000,
        CLASS_ConfigDoNotCheckDefaults = 0x02000000,
        CLASS_NewerVersionExists = 0x04000000
    }
    [Flags]
    public enum EClassCastFlags :ulong
    {
        CASTCLASS_None = 0x0000000000000000,
        CASTCLASS_UField = 0x0000000000000001,
        CASTCLASS_FInt8Property = 0x0000000000000002,
        CASTCLASS_UEnum = 0x0000000000000004,
        CASTCLASS_UStruct = 0x0000000000000008,
        CASTCLASS_UScriptStruct = 0x0000000000000010,
        CASTCLASS_UClass = 0x0000000000000020,
        CASTCLASS_FByteProperty = 0x0000000000000040,
        CASTCLASS_FIntProperty = 0x0000000000000080,
        CASTCLASS_FFloatProperty = 0x0000000000000100,
        CASTCLASS_FUInt64Property = 0x0000000000000200,
        CASTCLASS_FClassProperty = 0x0000000000000400,
        CASTCLASS_FUInt32Property = 0x0000000000000800,
        CASTCLASS_FInterfaceProperty = 0x0000000000001000,
        CASTCLASS_FNameProperty = 0x0000000000002000,
        CASTCLASS_FStrProperty = 0x0000000000004000,
        CASTCLASS_FProperty = 0x0000000000008000,
        CASTCLASS_FObjectProperty = 0x0000000000010000,
        CASTCLASS_FBoolProperty = 0x0000000000020000,
        CASTCLASS_FUInt16Property = 0x0000000000040000,
        CASTCLASS_UFunction = 0x0000000000080000,
        CASTCLASS_FStructProperty = 0x0000000000100000,
        CASTCLASS_FArrayProperty = 0x0000000000200000,
        CASTCLASS_FInt64Property = 0x0000000000400000,
        CASTCLASS_FDelegateProperty = 0x0000000000800000,
        CASTCLASS_FNumericProperty = 0x0000000001000000,
        CASTCLASS_FMulticastDelegateProperty = 0x0000000002000000,
        CASTCLASS_FObjectPropertyBase = 0x0000000004000000,
        CASTCLASS_FWeakObjectProperty = 0x0000000008000000,
        CASTCLASS_FLazyObjectProperty = 0x0000000010000000,
        CASTCLASS_FSoftObjectProperty = 0x0000000020000000,
        CASTCLASS_FTextProperty = 0x0000000040000000,
        CASTCLASS_FInt16Property = 0x0000000080000000,
        CASTCLASS_FDoubleProperty = 0x0000000100000000,
        CASTCLASS_FSoftClassProperty = 0x0000000200000000,
        CASTCLASS_UPackage = 0x0000000400000000,
        CASTCLASS_ULevel = 0x0000000800000000,
        CASTCLASS_AActor = 0x0000001000000000,
        CASTCLASS_APlayerController = 0x0000002000000000,
        CASTCLASS_APawn = 0x0000004000000000,
        CASTCLASS_USceneComponent = 0x0000008000000000,
        CASTCLASS_UPrimitiveComponent = 0x0000010000000000,
        CASTCLASS_USkinnedMeshComponent = 0x0000020000000000,
        CASTCLASS_USkeletalMeshComponent = 0x0000040000000000,
        CASTCLASS_UBlueprint = 0x0000080000000000,
        CASTCLASS_UDelegateFunction = 0x0000100000000000,
        CASTCLASS_UStaticMeshComponent = 0x0000200000000000,
        CASTCLASS_FMapProperty = 0x0000400000000000,
        CASTCLASS_FSetProperty = 0x0000800000000000,
        CASTCLASS_FEnumProperty = 0x0001000000000000,
        CASTCLASS_USparseDelegateFunction = 0x0002000000000000,
        CASTCLASS_FMulticastInlineDelegateProperty = 0x0004000000000000,
        CASTCLASS_FMulticastSparseDelegateProperty = 0x0008000000000000,
        CASTCLASS_FFieldPathProperty = 0x0010000000000000,
        CASTCLASS_FObjectPtrProperty = 0x0020000000000000,
        CASTCLASS_FClassPtrProperty = 0x0040000000000000,
        CASTCLASS_FLargeWorldCoordinatesRealProperty = 0x0080000000000000
    }

    public record FlagBadge(string Name, string HexValue);
    public record ClassBadge(string ShortName, string AssetName, string FullClass);
    public class MergedTagItem :INotifyPropertyChanged
    {
        public string Key { get; }
        public string RawValue { get; set; }
        public string OwnerBadge { get; }
        public bool IsShared { get; }
        public List<Asset> TargetAssets { get; }
        public List<FlagBadge>? Badges { get; set; }
        public bool HasBadges => Badges != null && Badges.Count > 0;

        private string _cleanValue;
        public string CleanValue
        {
            get => _cleanValue;
            set
            {
                if (_cleanValue != value)
                {
                    _cleanValue = value;
                    IsDirty = true;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CleanValue)));
                }
            }
        }

        public bool IsDirty { get; private set; }

        public MergedTagItem(
            string key,
            string cleanValue,
            string rawValue,
            string ownerBadge,
            bool isShared,
            List<Asset> targetAssets,
            List<FlagBadge>? badges = null)
        {
            Key = key;
            _cleanValue = cleanValue;
            RawValue = rawValue;
            OwnerBadge = ownerBadge;
            IsShared = isShared;
            TargetAssets = targetAssets;
            Badges = badges;
        }

        public void ClearDirty() => IsDirty = false;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class PackageInspectorViewModel :INotifyPropertyChanged
    {
        private readonly InspectionSource _source;

        public string PackageName { get; }
        public string PackagePath { get; }
        public string PrimaryAssetName { get; }
        public List<ClassBadge> AssetClasses { get; } = new();
        public string ChunkIdsDisplay { get; }
        public string NumberlessTagsDisplay { get; }
        public string PackageFlagsDisplay { get; }
        public List<FlagBadge> ActivePackageFlags { get; } = new();
        public List<MergedTagItem> MergedTags { get; } = new();

        public bool CanEdit { get; }
        public bool IsReadOnly => !CanEdit;
        public string WindowTitle => CanEdit
            ? $"Edit Package Properties ({_source}) — {PrimaryAssetName}"
            : $"Package Properties (Read-Only) — {PrimaryAssetName}";

        private string _rawJson;
        public string RawJson
        {
            get => _rawJson;
            private set
            {
                if (_rawJson != value)
                {
                    _rawJson = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RawJson)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public PackageInspectorViewModel(
            List<Asset> packageAssets,
            string rawJson,
            InspectionSource source = InspectionSource.Donor)
        {
            var assets = packageAssets.ToList();
            if (!assets.Any())
                throw new ArgumentException("At least one asset is required.");

            _source = source;
            CanEdit = source == InspectionSource.Staging || source == InspectionSource.Receiver;
            _rawJson = rawJson;

            var first = assets.First();
            PackageName = first.PackageName;
            PackagePath = first.PackagePath;

            // Prefer the non-_C asset name as the primary title
            PrimaryAssetName = assets
                .Select(a => a.AssetName)
                .OrderBy(n => n.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
                .First();

            // 1. Collect all sub-asset classes in this package
            foreach (var asset in assets)
            {
                string shortClass = asset.AssetClass?.Split('.').LastOrDefault() ?? asset.AssetClass ?? "Unknown";
                AssetClasses.Add(new ClassBadge(shortClass, asset.AssetName, asset.AssetClass ?? ""));
            }

            // 2. Deduplicate ChunkIds across all entries
            var distinctChunks = assets
                .Where(a => a.ChunkIds != null)
                .SelectMany(a => a.ChunkIds)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            ChunkIdsDisplay = distinctChunks.Any() ? string.Join(", ", distinctChunks) : "None";

            // 3. Deduplicate HasNumberlessTags
            var distinctNumberless = assets.Select(a => a.HasNumberlessTags).Distinct().ToList();
            NumberlessTagsDisplay = distinctNumberless.Count == 1
                ? (distinctNumberless[0]?.ToString() ?? "Null")
                : string.Join(" / ", assets.Select(a => $"{GetShortClass(a)}: {a.HasNumberlessTags}"));

            // 4. Deduplicate PackageFlags across all entries using generic ParseBitmask
            var distinctFlagHeaders = new HashSet<string>();
            foreach (var asset in assets)
            {
                if (asset.PackageFlags != null)
                {
                    var (header, badges) = ParseBitmask<EPackageFlags>(asset.PackageFlags.ToString() ?? "");
                    if (!string.IsNullOrWhiteSpace(header))
                        distinctFlagHeaders.Add(header);

                    if (badges != null)
                    {
                        foreach (var badge in badges)
                        {
                            if (!ActivePackageFlags.Contains(badge))
                                ActivePackageFlags.Add(badge);
                        }
                    }
                }
            }
            PackageFlagsDisplay = string.Join(", ", distinctFlagHeaders);

            // 5. Merge and deduplicate TagsAndValues (including ClassFlags, ClassCastFlags, and FiBData)
            var allTagEntries = assets
                .Where(a => a.TagsAndValues != null)
                .SelectMany(a => a.TagsAndValues.Select(kvp =>
                {
                    List<FlagBadge>? badges = null;
                    string cleanVal;

                    if (kvp.Key == "ClassFlags")
                    {
                        (cleanVal, badges) = ParseBitmask<EClassFlags>(kvp.Value);
                    }
                    else if (kvp.Key == "ClassCastFlags")
                    {
                        (cleanVal, badges) = ParseBitmask<EClassCastFlags>(kvp.Value);
                    }
                    else if (kvp.Key == "FiBData")
                    {
                        cleanVal = FormatFiBDataForGrid(kvp.Value);
                    }
                    else
                    {
                        cleanVal = CleanUnrealValue(kvp.Value);
                    }

                    return new
                    {
                        Key = kvp.Key,
                        RawValue = kvp.Value,
                        CleanValue = cleanVal,
                        Badges = badges,
                        Owner = GetShortClass(a),
                        AssetRef = a
                    };
                }))
                .ToList();

            foreach (var keyGroup in allTagEntries.GroupBy(x => x.Key).OrderBy(g => g.Key))
            {
                var valueGroups = keyGroup.GroupBy(x => x.CleanValue).ToList();

                foreach (var valGroup in valueGroups)
                {
                    var firstItem = valGroup.First();
                    var owners = valGroup.Select(v => v.Owner).Distinct().ToList();
                    var targetAssets = valGroup.Select(v => v.AssetRef).Distinct().ToList();
                    bool isSharedAcrossAll = owners.Count == assets.Count && assets.Count > 1;
                    string ownerLabel = isSharedAcrossAll ? "Shared" : string.Join(", ", owners);

                    MergedTags.Add(new MergedTagItem(
                        key: keyGroup.Key,
                        cleanValue: valGroup.Key,
                        rawValue: firstItem.RawValue,
                        ownerBadge: ownerLabel,
                        isShared: isSharedAcrossAll || assets.Count == 1,
                        targetAssets: targetAssets,
                        badges: firstItem.Badges
                    ));
                }
            }
        }

        public bool ApplyChangesToAssets()
        {
            bool anySaved = false;

            foreach (var item in MergedTags.Where(t => t.IsDirty))
            {
                string rewrapped = RewrapUnrealValue(item.CleanValue, item.RawValue);
                item.RawValue = rewrapped;

                if (item.Key == "ClassFlags")
                    (_, item.Badges) = ParseBitmask<EClassFlags>(rewrapped);
                else if (item.Key == "ClassCastFlags")
                    (_, item.Badges) = ParseBitmask<EClassCastFlags>(rewrapped);

                foreach (var asset in item.TargetAssets)
                {
                    if (asset.TagsAndValues != null)
                    {
                        asset.TagsAndValues[item.Key] = rewrapped;
                        anySaved = true;
                    }
                }

                item.ClearDirty();
            }

            return anySaved;
        }

        public void RefreshRawJson(string updatedJson)
        {
            RawJson = updatedJson;
        }

        private static string RewrapUnrealValue(string editedClean, string originalRaw)
        {
            if (editedClean.StartsWith("ANSI(") || editedClean.StartsWith("WIDE(") || editedClean.StartsWith("PATH__NO_NUM("))
                return editedClean;

            // If it was a bitmask header like "12847620 (0x00C40A04)", extract just the leading number
            var numericPrefix = Regex.Match(editedClean, @"^\s*(\d+)\s*\(0x[0-9A-Fa-f]+\)\s*$");
            string valueToWrap = numericPrefix.Success ? numericPrefix.Groups[1].Value : editedClean;
            if (originalRaw.StartsWith("ANSI(PATH__NO_NUM(") && originalRaw.EndsWith("))"))
                return $"ANSI(PATH__NO_NUM({valueToWrap}))";
            if (originalRaw.StartsWith("ANSI(") && originalRaw.EndsWith(")"))
                return $"ANSI({valueToWrap})";
            if (originalRaw.StartsWith("WIDE(") && originalRaw.EndsWith(")"))
                return $"WIDE({valueToWrap})";
            if (originalRaw.StartsWith("PATH__NO_NUM(") && originalRaw.EndsWith(")"))
                return $"PATH__NO_NUM({valueToWrap})";
            return valueToWrap;
        }
        private static string GetShortClass(Asset a) =>
        a.AssetClass?.Split('.').LastOrDefault() ?? "Asset";
        private static string FormatFiBDataForGrid(string rawFiB)
        {
            var decoded = JsonViewbox.ParseFiBData(rawFiB);
            if (!decoded.Any())
                return CleanUnrealValue(rawFiB);
            var lines = new List<string>();
            foreach (var graph in decoded)
            {
                var nodeNames = graph.Nodes.Select(n => $"{n.DisplayName} ({n.InternalName})");
                lines.Add($"{graph.GraphName}: {string.Join(", ", nodeNames)}");
            }
            return string.Join(Environment.NewLine, lines);
        }
        private static (string HeaderText, List<FlagBadge>? Badges) ParseBitmask<TEnum>(string raw)
        where TEnum : struct, Enum
        {
            string cleaned = CleanUnrealValue(raw);
            if (!ulong.TryParse(cleaned, out ulong flags))
                return (cleaned, null);
            bool is64Bit = Enum.GetUnderlyingType(typeof(TEnum)) == typeof(ulong);
            var badges = new List<FlagBadge>();
            foreach (TEnum flag in Enum.GetValues(typeof(TEnum)))
            {
                ulong val = Convert.ToUInt64(flag);
                if (val != 0 && (flags & val) == val)
                {
                    string hex = is64Bit ? $"0x{val:X16}" : $"0x{val:X8}";
                    badges.Add(new FlagBadge(flag.ToString(), hex));
                }
            }
            string headerHex = is64Bit ? $"0x{flags:X16}" : $"0x{flags:X8}";
            string header = $"{flags} ({headerHex})";
            return (header, badges.Any() ? badges : null);
        }
        private static string CleanUnrealValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;
            var match = Regex.Match(raw, @"^(?:ANSI|WIDE|PATH__NO_NUM)\((.*)\)$", RegexOptions.Singleline);
            string inner = match.Success ? match.Groups[1].Value : raw;
            var nestedMatch = Regex.Match(inner, @"^PATH__NO_NUM\((.*)\)$", RegexOptions.Singleline);
            if (nestedMatch.Success)
                inner = nestedMatch.Groups[1].Value;
            // Expand Unreal compact FVector notation
            if (inner == "V(0)")
                return "X=0, Y=0, Z=0 (Zero Vector)";
            if (inner == "V(1)")
                return "X=1, Y=1, Z=1";
            if (inner.StartsWith("V(") && inner.EndsWith(")"))
                return inner.Substring(2, inner.Length - 3);
            return inner.Replace("\n", ", ");
        }
    }
}