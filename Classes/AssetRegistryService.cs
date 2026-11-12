using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AssetRegistryModMigrator.Classes
{
    public enum LogVerbosity
    {
        Warn = 0,
        Info = 1,
        Debug = 2
    }

    public class AssetRegistryService
    {
        private readonly string _executablePath;

        /// <summary>
        /// Initializes the wrapper pointing to the bundled hexviewer.exe.
        /// </summary>
        /// <param name="executablePath">Optional custom path. Defaults to hexviewer\hexviewer.exe.</param>
        public AssetRegistryService(string? executablePath = null)
        {
            _executablePath = executablePath ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "hexviewer",
                "hexviewer.exe"
            );
        }

        /// <summary>
        /// Converts a binary Unreal Engine AssetRegistry file into an editable JSON file.
        /// </summary>
        /// <param name="inputBinPath">Path to the source .bin file.</param>
        /// <param name="outputJsonPath">Optional output path. Defaults to [input_stem]_parsed.json.</param>
        /// <param name="verbosity">Python logging verbosity level.</param>
        /// <returns>The full path to the generated JSON file.</returns>
        public async Task<string> ConvertBinToJsonAsync(
            string inputBinPath,
            string? outputJsonPath = null,
            LogVerbosity verbosity = LogVerbosity.Warn,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(inputBinPath))
                throw new FileNotFoundException("Input binary file was not found.", inputBinPath);

            // Match Python's default naming behavior if outputJsonPath is omitted
            string resolvedOutputPath = outputJsonPath ?? Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(inputBinPath))!,
                $"{Path.GetFileNameWithoutExtension(inputBinPath)}_parsed.json"
            );

            await RunCliCommandAsync("bin_to_json", inputBinPath, resolvedOutputPath, verbosity, cancellationToken);
            return resolvedOutputPath;
        }

        /// <summary>
        /// Converts an editable JSON file back into a binary Unreal Engine AssetRegistry file.
        /// </summary>
        /// <param name="inputJsonPath">Path to the edited .json file.</param>
        /// <param name="outputBinPath">Optional output path. Defaults to [input_stem]_encoded.bin.</param>
        /// <param name="verbosity">Python logging verbosity level.</param>
        /// <returns>The full path to the generated binary file.</returns>
        public async Task<string> ConvertJsonToBinAsync(
            string inputJsonPath,
            string? outputBinPath = null,
            LogVerbosity verbosity = LogVerbosity.Warn,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(inputJsonPath))
                throw new FileNotFoundException("Input JSON file was not found.", inputJsonPath);

            // Match Python's default naming behavior if outputBinPath is omitted
            string resolvedOutputPath = outputBinPath ?? Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(inputJsonPath))!,
                $"{Path.GetFileNameWithoutExtension(inputJsonPath)}_encoded.bin"
            );

            await RunCliCommandAsync("json_to_bin", inputJsonPath, resolvedOutputPath, verbosity, cancellationToken);
            return resolvedOutputPath;
        }

        private async Task<string> RunCliCommandAsync(
            string subCommand,
            string inputFile,
            string outputFile,
            LogVerbosity verbosity,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(_executablePath))
                throw new FileNotFoundException($"Bundled Python executable not found at: {_executablePath}");

            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true // Prevents a black console window from flashing in WPF
            };

            // Add group-level verbosity flags (-v or -vv) before the subcommand
            if (verbosity == LogVerbosity.Info)
                startInfo.ArgumentList.Add("-v");
            else if (verbosity == LogVerbosity.Debug)
                startInfo.ArgumentList.Add("-vv");

            // Add subcommand, positional input_file, and -o output_path
            startInfo.ArgumentList.Add(subCommand);
            startInfo.ArgumentList.Add(inputFile);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputFile);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Read stdout and stderr concurrently to avoid buffer deadlocks
            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            string stdOut = await stdOutTask;
            string stdErr = await stdErrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"hexviewer.exe failed with exit code {process.ExitCode}.\nDetails: {stdErr}".Trim()
                );
            }

            return stdOut;
        }
    }
#if DEBUG
    public record SemanticDiffResult(bool IsEquivalent, string? FirstDifferencePath, string? Details);

    public static class JsonSemanticComparer
    {
        public static SemanticDiffResult CompareJsonStrings(string originalJson, string roundtripJson)
        {
            JsonNode? origNode = JsonNode.Parse(originalJson);
            JsonNode? roundNode = JsonNode.Parse(roundtripJson);

            JsonNode? normOrig = Canonicalize(origNode);
            JsonNode? normRound = Canonicalize(roundNode);

            return FindFirstDifference(normOrig, normRound, "$");
        }

        /// <summary>
        /// Recursively sorts object properties and array elements so unordered tables compare identically.
        /// </summary>
        private static JsonNode? Canonicalize(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                var sortedObj = new JsonObject();
                foreach (var kvp in obj.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    sortedObj[kvp.Key] = Canonicalize(kvp.Value);
                }
                return sortedObj;
            }

            if (node is JsonArray arr)
            {
                // Canonicalize each element, then sort by its serialized representation
                var canonicalItems = arr
                    .Select(item => Canonicalize(item))
                    .OrderBy(item => item?.ToJsonString() ?? "null", StringComparer.Ordinal)
                    .ToList();

                var sortedArr = new JsonArray();
                foreach (var item in canonicalItems)
                {
                    sortedArr.Add(item);
                }
                return sortedArr;
            }

            // Primitive value (string, number, bool, null)
            return node?.DeepClone();
        }

        private static SemanticDiffResult FindFirstDifference(JsonNode? a, JsonNode? b, string path)
        {
            if (a is null && b is null)
                return new SemanticDiffResult(true, null, null);

            if (a is null || b is null)
                return new SemanticDiffResult(false, path, $"Null mismatch: '{a?.ToJsonString() ?? "null"}' vs '{b?.ToJsonString() ?? "null"}'");

            if (a is JsonObject objA && b is JsonObject objB)
            {
                var keysA = objA.Select(k => k.Key).ToHashSet();
                var keysB = objB.Select(k => k.Key).ToHashSet();

                string? missingInB = keysA.Except(keysB).FirstOrDefault();
                if (missingInB != null)
                    return new SemanticDiffResult(false, $"{path}.{missingInB}", "Property missing in roundtrip output.");

                string? extraInB = keysB.Except(keysA).FirstOrDefault();
                if (extraInB != null)
                    return new SemanticDiffResult(false, $"{path}.{extraInB}", "Unexpected extra property in roundtrip output.");

                foreach (var kvp in objA)
                {
                    var diff = FindFirstDifference(kvp.Value, objB[kvp.Key], $"{path}.{kvp.Key}");
                    if (!diff.IsEquivalent)
                        return diff;
                }

                return new SemanticDiffResult(true, null, null);
            }

            if (a is JsonArray arrA && b is JsonArray arrB)
            {
                if (arrA.Count != arrB.Count)
                    return new SemanticDiffResult(false, path, $"Array length mismatch: {arrA.Count} vs {arrB.Count}");

                for (int i = 0; i < arrA.Count; i++)
                {
                    var diff = FindFirstDifference(arrA[i], arrB[i], $"{path}[{i}]");
                    if (!diff.IsEquivalent)
                        return diff;
                }

                return new SemanticDiffResult(true, null, null);
            }

            // Compare primitive values
            string valA = a.ToJsonString();
            string valB = b.ToJsonString();
            if (valA != valB)
                return new SemanticDiffResult(false, path, $"Value mismatch: {valA} vs {valB}");

            return new SemanticDiffResult(true, null, null);
        }
    }
#endif
}