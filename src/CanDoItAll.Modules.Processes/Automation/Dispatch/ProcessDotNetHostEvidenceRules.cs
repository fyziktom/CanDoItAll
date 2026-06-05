using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Xml;
using System.Xml.Linq;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDotNetHostEvidenceRules
{
    private const string ExternalTargetAliasRoot = "external-target";

    internal delegate bool TryResolveExternalTargetPath(
        string normalizedRelativePath,
        out string fullPath,
        out string failureReason);

    internal static IReadOnlyList<string> ResolveRunnableDotNetHostProjectPaths(
        IEnumerable<string> allowedExternalTargetAliases,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> successfulReceipts,
        TryResolveExternalTargetPath tryResolveExternalTargetPath)
    {
        var candidatePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in allowedExternalTargetAliases)
        {
            AddResolvedPromptPathCandidates(candidatePaths, alias);
        }

        foreach (var receipt in successfulReceipts)
        {
            foreach (var path in ProcessConcreteProductPathRules.ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary))
            {
                AddResolvedPromptPathCandidates(candidatePaths, path);
            }

            if (ProcessConcreteProductPathRules.TryMapWorkspacePathForPrompt(receipt.WorkingDirectory, out var mappedWorkingDirectory))
            {
                AddResolvedPromptPathCandidates(candidatePaths, mappedWorkingDirectory);
            }
        }

        return candidatePaths
            .SelectMany(path => EnumerateCandidateDotNetProjectFiles(path, tryResolveExternalTargetPath))
            .Where(IsRunnableDotNetHostProjectFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(TryMapAbsolutePathToExternalTargetAlias)
            .ToList();
    }

    internal static void AddResolvedPromptPathCandidates(
        SortedSet<string> candidatePaths,
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (!ProcessConcreteProductPathRules.IsConcreteProductPath(normalized))
        {
            return;
        }

        candidatePaths.Add(normalized);
    }

    internal static IEnumerable<string> EnumerateCandidateDotNetProjectFiles(
        string promptPath,
        TryResolveExternalTargetPath tryResolveExternalTargetPath)
    {
        if (!TryResolvePromptPathToFullPath(promptPath, tryResolveExternalTargetPath, out var fullPath))
        {
            yield break;
        }

        if (File.Exists(fullPath))
        {
            if (string.Equals(Path.GetExtension(fullPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                yield return fullPath;
            }

            yield break;
        }

        var searchRoot = string.Equals(Path.GetExtension(fullPath), ".sln", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(Path.GetExtension(fullPath), ".slnx", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(fullPath)
            : fullPath;
        if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
        {
            yield break;
        }

        IEnumerable<string> projectFiles;
        try
        {
            projectFiles = Directory.EnumerateFiles(searchRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !HasIgnoredProjectPathSegment(path))
                .Take(32)
                .ToList();
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var projectFile in projectFiles)
        {
            yield return projectFile;
        }
    }

    internal static bool TryResolvePromptPathToFullPath(
        string promptPath,
        TryResolveExternalTargetPath tryResolveExternalTargetPath,
        out string fullPath)
    {
        fullPath = string.Empty;
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (ProcessConcreteProductPathRules.IsExternalTargetAliasPath(normalized))
        {
            return tryResolveExternalTargetPath(normalized, out fullPath, out _);
        }

        if (Path.IsPathRooted(promptPath))
        {
            fullPath = Path.GetFullPath(promptPath);
            return true;
        }

        return false;
    }

    internal static bool HasIgnoredProjectPathSegment(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".oldruns", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "oldruns", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "old-runs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "previous-runs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "backup", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "backups", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "archive", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "archives", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsRunnableDotNetHostProjectFile(string fullPath)
    {
        try
        {
            var document = XDocument.Load(fullPath, LoadOptions.None);
            var sdk = document.Root?.Attribute("Sdk")?.Value ?? string.Empty;
            if (sdk.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
                sdk.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return document
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "OutputType", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Value.Trim())
                .Any(value =>
                    string.Equals(value, "Exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "WinExe", StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    internal static string TryMapAbsolutePathToExternalTargetAlias(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath).Replace(Path.DirectorySeparatorChar, '/');
        if (normalized.Length < 3 || normalized[1] != ':' || normalized[2] != '/')
        {
            return fullPath;
        }

        var driveLetter = char.ToUpperInvariant(normalized[0]);
        var suffix = normalized[3..].Trim('/');
        return string.IsNullOrWhiteSpace(suffix)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{suffix}";
    }
}
