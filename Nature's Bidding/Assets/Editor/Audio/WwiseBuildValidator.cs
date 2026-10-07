#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class WwiseBuildValidator : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string AuthoringManifestFileName = "AuthoringSourceHashes.txt";
    private const string GeneratedManifestFileName = "GeneratedOutputHashes.txt";

    private static readonly Regex ObjectNamePattern = new Regex(
        @"^\s*objectName:\s*(?<name>.+?)\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex MediaPathPattern = new Regex(
        "\\\"Path\\\"\\s*:\\s*\\\"(?<path>Media/[^\\\"]+\\.wem)\\\"",
        RegexOptions.Compiled);

    private static string stagedSoundBankFolder;

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        string platformName = AkBuildPreprocessor.GetPlatformName(report.summary.platform);
        if (string.IsNullOrWhiteSpace(platformName))
            throw new BuildFailedException($"Wwise has no platform mapping for {report.summary.platform}.");

        if (!AkBasePathGetter.GetSoundBankPaths(platformName, out string sourceFolder, out string destinationFolder))
            throw new BuildFailedException($"Wwise could not resolve SoundBank paths for {platformName}.");

        sourceFolder = Path.GetFullPath(sourceFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        destinationFolder = Path.GetFullPath(destinationFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        ValidateSourceBanks(platformName, sourceFolder);
        ValidateStagingDestination(platformName, sourceFolder, destinationFolder);

        try
        {
            if (Directory.Exists(destinationFolder))
                Directory.Delete(destinationFolder, true);

            CopyDirectory(sourceFolder, destinationFolder);
            stagedSoundBankFolder = destinationFolder;
        }
        catch (Exception exception)
        {
            stagedSoundBankFolder = null;
            throw new BuildFailedException(
                $"Wwise SoundBanks could not be staged for {platformName}: {exception.Message}");
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (string.IsNullOrEmpty(stagedSoundBankFolder))
            return;

        if (Directory.Exists(stagedSoundBankFolder))
            Directory.Delete(stagedSoundBankFolder, true);

        stagedSoundBankFolder = null;
    }

    private static void ValidateSourceBanks(string platformName, string sourceFolder)
    {
        var failures = new List<string>();

        RequireFile(Path.Combine(sourceFolder, "Init.bnk"), failures);
        RequireFile(Path.Combine(sourceFolder, "PlatformInfo.json"), failures);
        RequireFile(Path.Combine(sourceFolder, "PluginInfo.json"), failures);

        string eventAssetFolder = Path.Combine(Application.dataPath, "Wwise/ScriptableObjects/Event");
        foreach (string assetPath in Directory.EnumerateFiles(eventAssetFolder, "*.asset"))
        {
            string assetContents = File.ReadAllText(assetPath);
            if (assetContents.Contains("IsInUserDefinedSoundBank: 1"))
            {
                failures.Add(
                    $"{assetPath} uses a user-defined SoundBank, which this validator cannot resolve safely.");
                continue;
            }

            Match objectNameMatch = ObjectNamePattern.Match(assetContents);
            if (!objectNameMatch.Success)
            {
                failures.Add($"Cannot read the Wwise event name from {assetPath}.");
                continue;
            }

            string eventName = objectNameMatch.Groups["name"].Value;
            string eventBankPath = Path.Combine(sourceFolder, "Event", eventName + ".bnk");
            string eventMetadataPath = Path.Combine(sourceFolder, "Event", eventName + ".json");
            RequireFile(eventBankPath, failures);
            RequireFile(eventMetadataPath, failures);

            if (File.Exists(eventMetadataPath))
                ValidateMediaReferences(sourceFolder, eventMetadataPath, failures);
        }

        ValidateGenerationIsCurrent(sourceFolder, failures);

        if (failures.Count > 0)
        {
            string details = string.Join(Environment.NewLine + "  - ", failures);
            throw new BuildFailedException(
                $"Wwise SoundBank validation failed for {platformName}:{Environment.NewLine}  - {details}");
        }
    }

    private static void ValidateMediaReferences(
        string sourceFolder,
        string eventMetadataPath,
        ICollection<string> failures)
    {
        string metadata = File.ReadAllText(eventMetadataPath);
        foreach (Match match in MediaPathPattern.Matches(metadata))
        {
            string relativePath = match.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar);
            RequireFile(Path.Combine(sourceFolder, relativePath), failures);
        }
    }

    private static void ValidateGenerationIsCurrent(string sourceFolder, ICollection<string> failures)
    {
        string wwiseProjectPath = AkWwiseEditorSettings.WwiseProjectAbsolutePath;
        string wwiseProjectFolder = Path.GetDirectoryName(wwiseProjectPath);
        if (string.IsNullOrEmpty(wwiseProjectFolder) || !Directory.Exists(wwiseProjectFolder))
        {
            failures.Add("The Wwise authoring project cannot be found.");
            return;
        }

        string outputRoot = Directory.GetParent(sourceFolder)?.FullName;
        if (string.IsNullOrEmpty(outputRoot))
        {
            failures.Add("The Wwise generated-bank root cannot be resolved.");
            return;
        }

        string manifestPath = Path.Combine(outputRoot, AuthoringManifestFileName);
        if (!File.Exists(manifestPath))
        {
            failures.Add(
                $"Missing {manifestPath}. Run scripts/generate-wwise-banks.sh before building.");
            return;
        }

        string recordedManifest = NormalizeLineEndings(File.ReadAllText(manifestPath));
        string currentManifest = BuildAuthoringManifest(wwiseProjectPath, wwiseProjectFolder);
        if (!string.Equals(recordedManifest, currentManifest, StringComparison.Ordinal))
        {
            failures.Add(
                "Wwise authoring data or original audio differs from the generated-bank manifest. " +
                "Run scripts/generate-wwise-banks.sh before building.");
        }

        string generatedManifestPath = Path.Combine(outputRoot, GeneratedManifestFileName);
        if (!File.Exists(generatedManifestPath))
        {
            failures.Add(
                $"Missing {generatedManifestPath}. Run scripts/generate-wwise-banks.sh before building.");
            return;
        }

        string recordedGeneratedManifest = NormalizeLineEndings(File.ReadAllText(generatedManifestPath));
        string currentGeneratedManifest = BuildGeneratedManifest(outputRoot, currentManifest);
        if (!string.Equals(recordedGeneratedManifest, currentGeneratedManifest, StringComparison.Ordinal))
        {
            failures.Add(
                "One or more Wwise generated banks, metadata files, or media files differ from the " +
                "generated-output manifest. Run scripts/generate-wwise-banks.sh before building.");
        }
    }

    private static string BuildAuthoringManifest(string wwiseProjectPath, string wwiseProjectFolder)
    {
        var sourcePaths = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetFullPath(wwiseProjectPath)] = true
        };

        foreach (string workUnitPath in Directory.EnumerateFiles(
                     wwiseProjectFolder,
                     "*.wwu",
                     SearchOption.AllDirectories))
        {
            sourcePaths[Path.GetFullPath(workUnitPath)] = true;
        }

        foreach (string sourceListPath in Directory.EnumerateFiles(
                     wwiseProjectFolder,
                     "*.wsources",
                     SearchOption.AllDirectories))
        {
            sourcePaths[Path.GetFullPath(sourceListPath)] = true;
        }

        string originalsFolder = Path.Combine(wwiseProjectFolder, "Originals");
        if (Directory.Exists(originalsFolder))
        {
            foreach (string originalPath in Directory.EnumerateFiles(
                         originalsFolder,
                         "*",
                         SearchOption.AllDirectories))
            {
                if (!Path.GetFileName(originalPath).StartsWith(".", StringComparison.Ordinal))
                    sourcePaths[Path.GetFullPath(originalPath)] = ShouldNormalizeTextLineEndings(originalPath);
            }
        }

        var manifest = new StringBuilder("# SHA-256 hashes for the Wwise project used to generate all SoundBanks.\n");
        foreach (string sourcePath in sourcePaths.Keys.OrderBy(
                     path => GetRelativeWwisePath(wwiseProjectFolder, path),
                     StringComparer.Ordinal))
        {
            string relativePath = GetRelativeWwisePath(wwiseProjectFolder, sourcePath);
            manifest.Append(ComputeSha256(sourcePath, sourcePaths[sourcePath]));
            manifest.Append("  ");
            manifest.Append(relativePath);
            manifest.Append('\n');
        }

        return manifest.ToString();
    }

    private static string BuildGeneratedManifest(string outputRoot, string authoringManifest)
    {
        var outputPaths = new List<string>();
        foreach (string outputPath in Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(outputPath);
            if (fileName.StartsWith(".", StringComparison.Ordinal) ||
                string.Equals(fileName, AuthoringManifestFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, GeneratedManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            outputPaths.Add(Path.GetFullPath(outputPath));
        }

        var manifest = new StringBuilder("# SHA-256 hashes for every generated Wwise output.\n");
        manifest.Append("# Authoring manifest SHA-256: ");
        manifest.Append(ComputeStringSha256(authoringManifest));
        manifest.Append('\n');
        foreach (string outputPath in outputPaths.OrderBy(
                     path => GetRelativeWwisePath(outputRoot, path),
                     StringComparer.Ordinal))
        {
            string relativePath = GetRelativeWwisePath(outputRoot, outputPath);
            manifest.Append(ComputeSha256(outputPath, ShouldNormalizeTextLineEndings(outputPath)));
            manifest.Append("  ");
            manifest.Append(relativePath);
            manifest.Append('\n');
        }

        return manifest.ToString();
    }

    private static string ComputeStringSha256(string value)
    {
        using (var sha256 = SHA256.Create())
            return FormatSha256(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static bool ShouldNormalizeTextLineEndings(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".h":
            case ".json":
            case ".txt":
            case ".xml":
            case ".wproj":
            case ".wsources":
            case ".wwu":
                return true;
            default:
                return false;
        }
    }

    private static string ComputeSha256(string path, bool normalizeTextLineEndings)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] hash;
            if (normalizeTextLineEndings)
            {
                byte[] sourceBytes = File.ReadAllBytes(path);
                using (var normalizedBytes = new MemoryStream(sourceBytes.Length))
                {
                    for (int index = 0; index < sourceBytes.Length; index++)
                    {
                        byte value = sourceBytes[index];
                        if (value == '\r')
                        {
                            normalizedBytes.WriteByte((byte)'\n');
                            if (index + 1 < sourceBytes.Length && sourceBytes[index + 1] == '\n')
                                index++;
                        }
                        else
                        {
                            normalizedBytes.WriteByte(value);
                        }
                    }

                    normalizedBytes.Position = 0;
                    hash = sha256.ComputeHash(normalizedBytes);
                }
            }
            else
            {
                using (FileStream stream = File.OpenRead(path))
                    hash = sha256.ComputeHash(stream);
            }

            return FormatSha256(hash);
        }
    }

    private static string FormatSha256(byte[] hash)
    {
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string GetRelativeWwisePath(string wwiseProjectFolder, string path)
    {
        string root = Path.GetFullPath(wwiseProjectFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Wwise source lies outside the project: {fullPath}");

        return fullPath.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static void ValidateStagingDestination(
        string platformName,
        string sourceFolder,
        string destinationFolder)
    {
        string streamingAssetsFolder = Path.GetFullPath(Application.streamingAssetsPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string streamingAssetsRoot = streamingAssetsFolder + Path.DirectorySeparatorChar;

        string sourceRoot = sourceFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string destinationRoot = destinationFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (sourceRoot.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase) ||
            destinationRoot.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new BuildFailedException(
                "The Wwise source and staging folders overlap, so staging would risk deleting source banks.");
        }

        if (!destinationFolder.StartsWith(streamingAssetsRoot, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetFileName(destinationFolder.TrimEnd(Path.DirectorySeparatorChar)),
                platformName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BuildFailedException(
                $"Refusing to replace the unexpected Wwise staging path: {destinationFolder}");
        }

        string wwiseProjectFolder = Path.GetDirectoryName(AkWwiseEditorSettings.WwiseProjectAbsolutePath);
        ValidateNoLinkedDirectories(sourceFolder, wwiseProjectFolder, "source");
        ValidateNoLinkedDirectories(destinationFolder, streamingAssetsFolder, "staging");
    }

    private static void ValidateNoLinkedDirectories(string path, string allowedRoot, string label)
    {
        if (string.IsNullOrEmpty(allowedRoot))
            throw new BuildFailedException($"The Wwise {label} root cannot be resolved.");

        string normalizedRoot = Path.GetFullPath(allowedRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var directory = new DirectoryInfo(path); directory != null; directory = directory.Parent)
        {
            string currentPath = directory.FullName
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new BuildFailedException(
                    $"The Wwise {label} path contains a symbolic link or junction: {directory.FullName}");
            }

            if (string.Equals(currentPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return;
        }

        throw new BuildFailedException($"The Wwise {label} path lies outside its expected root: {path}");
    }

    private static void RequireFile(string path, ICollection<string> failures)
    {
        if (!File.Exists(path))
            failures.Add($"Missing {path}.");
    }

    private static void CopyDirectory(string sourceFolder, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);

        foreach (string sourceFile in Directory.EnumerateFiles(sourceFolder))
        {
            string destinationFile = Path.Combine(destinationFolder, Path.GetFileName(sourceFile));
            File.Copy(sourceFile, destinationFile, true);
        }

        foreach (string sourceSubdirectory in Directory.EnumerateDirectories(sourceFolder))
        {
            string destinationSubdirectory = Path.Combine(
                destinationFolder,
                Path.GetFileName(sourceSubdirectory));
            CopyDirectory(sourceSubdirectory, destinationSubdirectory);
        }
    }
}
#endif
