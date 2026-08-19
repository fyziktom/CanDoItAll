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
    internal static ProcessProductRootResolution ResolveInspectableProductRoot(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var productRoot = ResolveLaunchVariable(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProductRoot);
        var outputRoot = ResolveLaunchVariable(
            launchVariables,
            ProcessRuntimeLaunchVariables.OutputRoot);
        var outputFolder = ResolveLaunchVariable(launchVariables, "OutputFolder");
        var externalTargetRoot = ResolveLaunchVariable(
            launchVariables,
            ProcessRuntimeLaunchVariables.ExternalTargetRoot);
        var hasProductRootAlias = launchVariables.ContainsKey(
            ProcessRuntimeLaunchVariables.ProductRootAlias);
        var productRootAlias = ResolveLaunchVariable(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProductRootAlias);
        var hasOutputRootAlias = launchVariables.ContainsKey(
            ProcessRuntimeLaunchVariables.OutputRootAlias);
        var outputRootAlias = ResolveLaunchVariable(
            launchVariables,
            ProcessRuntimeLaunchVariables.OutputRootAlias);
        if ((hasProductRootAlias && string.IsNullOrWhiteSpace(productRootAlias)) ||
            (hasOutputRootAlias && string.IsNullOrWhiteSpace(outputRootAlias)))
        {
            return new ProcessProductRootResolution(
                ProcessProductRootResolutionKind.Invalid,
                string.Empty,
                "configured product root alias is empty");
        }

        var configuredAlias = FirstNonEmpty(
            productRootAlias,
            outputRootAlias,
            ExternalTargetAliasCodec.IsAnyAlias(productRoot)
                ? productRoot
                : string.Empty,
            ExternalTargetAliasCodec.IsAnyAlias(outputRoot)
                ? outputRoot
                : string.Empty,
            ExternalTargetAliasCodec.IsAnyAlias(outputFolder)
                ? outputFolder
                : string.Empty,
            ExternalTargetAliasCodec.IsAnyAlias(externalTargetRoot)
                ? externalTargetRoot
                : string.Empty);
        if (string.IsNullOrWhiteSpace(configuredAlias) &&
            launchVariables.ContainsKey(ProcessRuntimeLaunchVariables.ExternalTargetRootBindings))
        {
            return new ProcessProductRootResolution(
                ProcessProductRootResolutionKind.Invalid,
                string.Empty,
                "protected product root bindings require a configured versioned alias");
        }

        if (!string.IsNullOrWhiteSpace(configuredAlias))
        {
            if (!ExternalTargetAliasCodec.IsAnyAlias(configuredAlias))
            {
                return new ProcessProductRootResolution(
                    ProcessProductRootResolutionKind.Invalid,
                    string.Empty,
                    "configured product root alias is invalid");
            }

            var normalizedAlias = ExternalTargetAliasCodec.NormalizeVersionedAlias(configuredAlias);
            if (normalizedAlias is null)
            {
                return new ProcessProductRootResolution(
                    ProcessProductRootResolutionKind.Invalid,
                    string.Empty,
                    "legacy or invalid external-target alias requires explicit rebind");
            }

            return new ProcessProductRootResolution(
                ProcessProductRootResolutionKind.Resolved,
                normalizedAlias,
                string.Empty);
        }

        productRoot = FirstNonEmpty(
            productRoot,
            outputRoot,
            outputFolder,
            externalTargetRoot);
        if (string.IsNullOrWhiteSpace(productRoot))
        {
            return new ProcessProductRootResolution(
                ProcessProductRootResolutionKind.NotConfigured,
                string.Empty,
                string.Empty);
        }

        return new ProcessProductRootResolution(
            ProcessProductRootResolutionKind.Invalid,
            string.Empty,
            "product root requires a persisted versioned alias authority");
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

        if (!ExternalTargetAliasCodec.IsVersionedAlias(productRoot))
        {
            invalidReason = "product root requires a persisted versioned alias authority";
            return false;
        }

        return TryResolveAliasProductPath(
            productRoot,
            candidate,
            out resolvedPath,
            out invalidReason);
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
        if (normalizedRoot is null)
        {
            invalidReason = "product root external-target alias is invalid";
            return false;
        }

        if (ExternalTargetAliasCodec.IsAnyAlias(candidate))
        {
            var normalizedCandidate = ExternalTargetAliasCodec.NormalizeVersionedAlias(candidate);
            if (normalizedCandidate is null)
            {
                invalidReason = "required external-target alias is invalid";
                return false;
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
            try
            {
                resolvedPath = Path.GetFullPath(candidate);
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                invalidReason = "required product path could not be normalized";
                return false;
            }
        }

        var candidateSegments = candidate.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.None);
        if (candidateSegments.Length == 0 ||
            candidateSegments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            invalidReason = "required product path contains invalid traversal segments";
            return false;
        }

        ExternalTargetAliasCodec.TryParseVersionedAlias(
            normalizedRoot,
            out var rootId,
            out var rootSegments,
            out _);
        try
        {
            resolvedPath = ExternalTargetAliasCodec.BuildAlias(
                rootId,
                rootSegments.Concat(candidateSegments).ToArray());
            return true;
        }
        catch (ArgumentException)
        {
            resolvedPath = string.Empty;
            invalidReason = "required product path contains a segment that cannot be represented safely";
            return false;
        }
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
