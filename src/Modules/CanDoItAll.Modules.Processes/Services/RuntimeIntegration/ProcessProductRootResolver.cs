using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProductRootResolver
{
    internal static bool TryResolveInspectableProductRoot(
        IReadOnlyDictionary<string, string> launchVariables,
        out string productRoot)
    {
        productRoot = FirstNonEmpty(
            ResolveLaunchVariable(launchVariables, "OutputFolder"),
            ResolveLaunchVariable(launchVariables, "OutputRoot"),
            ResolveLaunchVariable(launchVariables, "ProductRoot"),
            ResolveLaunchVariable(launchVariables, "ExternalTargetRoot"));
        if (string.IsNullOrWhiteSpace(productRoot) ||
            productRoot.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathFullyQualified(productRoot))
        {
            productRoot = string.Empty;
            return false;
        }

        productRoot = Path.GetFullPath(productRoot);
        return true;
    }

    internal static bool TryResolveRequiredProductPath(
        string productRoot,
        string requiredPath,
        out string resolvedPath,
        out string invalidReason)
    {
        resolvedPath = string.Empty;
        invalidReason = string.Empty;
        var candidate = requiredPath.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            invalidReason = "empty path";
            return false;
        }

        if (TryConvertExternalTargetAliasToNativePath(candidate, out var nativePath))
        {
            candidate = nativePath;
        }

        try
        {
            resolvedPath = Path.GetFullPath(Path.IsPathFullyQualified(candidate)
                ? candidate
                : Path.Combine(productRoot, candidate));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            invalidReason = exception.Message;
            return false;
        }

        if (!IsSameOrChildPath(productRoot, resolvedPath))
        {
            invalidReason = "outside product root";
            return false;
        }

        return true;
    }

    internal static bool TryConvertExternalTargetAliasToNativePath(string value, out string nativePath)
    {
        nativePath = string.Empty;
        var normalized = value.Trim().Replace('\\', '/');
        if (!normalized.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 ||
            segments[1].Length != 1 ||
            !char.IsLetter(segments[1][0]))
        {
            return false;
        }

        var driveRoot = $"{char.ToUpperInvariant(segments[1][0])}:{Path.DirectorySeparatorChar}";
        nativePath = segments.Length == 2
            ? driveRoot
            : Path.Combine(new[] { driveRoot }.Concat(segments.Skip(2)).ToArray());
        return true;
    }

    internal static bool IsSameOrChildPath(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCandidate = Path.GetFullPath(candidate)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normalizedRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(
                   normalizedRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(
                   normalizedRoot + Path.AltDirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    internal static ProductRootInspection InspectProductRoot(string productRoot)
    {
        try
        {
            if (!Directory.Exists(productRoot))
            {
                return new ProductRootInspection(false, "the directory does not exist");
            }

            return Directory
                .EnumerateFiles(productRoot, "*", SearchOption.AllDirectories)
                .Any(file => IsProductFile(productRoot, file))
                ? new ProductRootInspection(true, string.Empty)
                : new ProductRootInspection(false, "no product files were found");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
        {
            return new ProductRootInspection(false, exception.Message);
        }
    }

    internal static bool IsProductFile(string productRoot, string file)
    {
        var relativePath = Path.GetRelativePath(productRoot, file);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(IsIgnoredProductPathSegment))
        {
            return false;
        }

        var fileName = Path.GetFileName(file);
        return !string.Equals(fileName, ".gitkeep", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsIgnoredProductPathSegment(string segment)
        => string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, ".vs", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "packages", StringComparison.OrdinalIgnoreCase);
}
