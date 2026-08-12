using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
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
using CanDoItAll.SharedKernel;
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

internal enum ProcessProductRootResolutionKind
{
    NotConfigured,
    Resolved,
    Invalid
}

internal readonly record struct ProcessProductRootResolution(
    ProcessProductRootResolutionKind Kind,
    string ProductRoot,
    string InvalidReason);

internal static class ProcessProductRootResolver
{
    private static readonly IPhysicalFileSystemPathPolicyFactory PhysicalPathPolicyFactory =
        new PhysicalFileSystemPathPolicyFactory();

    internal static ProcessProductRootResolution ResolveInspectableProductRoot(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var productRoot = FirstNonEmpty(
            ResolveLaunchVariable(launchVariables, "ProductRootAlias"),
            ResolveLaunchVariable(launchVariables, "OutputRootAlias"),
            ResolveLaunchVariable(launchVariables, "OutputFolder"),
            ResolveLaunchVariable(launchVariables, "OutputRoot"),
            ResolveLaunchVariable(launchVariables, "ProductRoot"),
            ResolveLaunchVariable(launchVariables, "ExternalTargetRoot"));
        if (string.IsNullOrWhiteSpace(productRoot))
        {
            return new ProcessProductRootResolution(
                ProcessProductRootResolutionKind.NotConfigured,
                string.Empty,
                string.Empty);
        }

        if (ExternalTargetAliasCodec.IsAnyAlias(productRoot))
        {
            var normalizedAlias = ExternalTargetAliasCodec.NormalizeVersionedAlias(productRoot);
            if (normalizedAlias is null &&
                !ExternalTargetAliasCodec.TryNormalizeLegacyAlias(productRoot, out normalizedAlias))
            {
                return new ProcessProductRootResolution(
                    ProcessProductRootResolutionKind.Invalid,
                    string.Empty,
                    "invalid external-target alias");
            }

            return new ProcessProductRootResolution(
                ProcessProductRootResolutionKind.Resolved,
                normalizedAlias,
                string.Empty);
        }

        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(productRoot, "process product root");
            if (!Path.IsPathFullyQualified(productRoot))
            {
                return new ProcessProductRootResolution(
                    ProcessProductRootResolutionKind.Invalid,
                    string.Empty,
                    "product root is not fully qualified");
            }

            productRoot = Path.GetFullPath(productRoot);
            PhysicalPathPolicyFactory.Create(productRoot).EnsureSafePath(
                productRoot,
                allowMissingLeaf: true);
            return new ProcessProductRootResolution(
                ProcessProductRootResolutionKind.Resolved,
                productRoot,
                string.Empty);
        }
        catch (Exception exception) when (exception is PhysicalPathValidationException or ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            return new ProcessProductRootResolution(
                ProcessProductRootResolutionKind.Invalid,
                string.Empty,
                "product root could not be validated safely");
        }
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

        if (ExternalTargetAliasCodec.IsAnyAlias(productRoot))
        {
            return TryResolveAliasProductPath(
                productRoot,
                candidate,
                out resolvedPath,
                out invalidReason);
        }

        if (ExternalTargetAliasCodec.IsAnyAlias(candidate))
        {
            var normalizedAlias = ExternalTargetAliasCodec.NormalizeVersionedAlias(candidate);
            if (normalizedAlias is null &&
                !ExternalTargetAliasCodec.TryNormalizeLegacyAlias(candidate, out normalizedAlias))
            {
                invalidReason = "external-target alias is invalid";
                return false;
            }

            resolvedPath = normalizedAlias;
            return true;
        }

        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(productRoot, "process product root");
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(candidate, "required process product path");
            var rootPathPolicy = PhysicalPathPolicyFactory.Create(productRoot);
            resolvedPath = rootPathPolicy.ResolveContainedPath(candidate);
        }
        catch (Exception exception) when (exception is PhysicalPathValidationException or ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
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

    private static bool TryResolveAliasProductPath(
        string productRoot,
        string candidate,
        out string resolvedPath,
        out string invalidReason)
    {
        resolvedPath = string.Empty;
        invalidReason = string.Empty;
        var normalizedRoot = ExternalTargetAliasCodec.NormalizeVersionedAlias(productRoot);
        var isVersionedRoot = normalizedRoot is not null;
        if (normalizedRoot is null)
        {
            if (!ExternalTargetAliasCodec.TryNormalizeLegacyAlias(productRoot, out var legacyRoot))
            {
                invalidReason = "product root external-target alias is invalid";
                return false;
            }

            normalizedRoot = legacyRoot;
        }

        if (ExternalTargetAliasCodec.IsAnyAlias(candidate))
        {
            var normalizedCandidate = ExternalTargetAliasCodec.NormalizeVersionedAlias(candidate);
            if (normalizedCandidate is null)
            {
                if (!ExternalTargetAliasCodec.TryNormalizeLegacyAlias(candidate, out var legacyCandidate))
                {
                    invalidReason = "required external-target alias is invalid";
                    return false;
                }

                normalizedCandidate = legacyCandidate;
            }

            if (!ExternalTargetAliasCodec.IsAliasWithinRoot(normalizedCandidate, normalizedRoot))
            {
                invalidReason = "outside product root";
                return false;
            }

            resolvedPath = normalizedCandidate;
            return true;
        }

        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(candidate, "required process product path");
        }
        catch (Exception exception) when (exception is PhysicalPathValidationException or ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            invalidReason = "required product path has invalid syntax";
            return false;
        }

        if (Path.IsPathFullyQualified(candidate))
        {
            invalidReason = "outside product root";
            return false;
        }

        var candidateSegments = candidate
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.None);
        if (candidateSegments.Length == 0 ||
            candidateSegments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            invalidReason = "required product path contains invalid traversal segments";
            return false;
        }

        if (isVersionedRoot)
        {
            ExternalTargetAliasCodec.TryParseVersionedAlias(
                normalizedRoot,
                out var rootId,
                out var rootSegments,
                out _);
            resolvedPath = ExternalTargetAliasCodec.BuildAlias(
                rootId,
                rootSegments.Concat(candidateSegments).ToArray());
            return true;
        }

        resolvedPath = $"{normalizedRoot}/{string.Join('/', candidateSegments)}";
        return true;
    }

    internal static bool IsSameOrChildPath(string root, string candidate)
    {
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(root, "process product containment root");
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(candidate, "process product containment candidate");
        return PhysicalPathPolicyFactory.Create(root).IsWithinRoot(candidate);
    }

    internal static bool IsProductFileReference(string path)
    {
        var segments = path.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(IsIgnoredProductPathSegment))
        {
            return false;
        }

        var fileName = segments.LastOrDefault() ?? string.Empty;
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
