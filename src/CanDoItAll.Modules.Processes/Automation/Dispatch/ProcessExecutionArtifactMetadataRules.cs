using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;

namespace CanDoItAll.Modules.Processes;

internal delegate bool TryMapAbsoluteExternalPathToAlias(
    string normalizedPath,
    out string mappedAlias);

internal delegate bool TryResolveArtifactFullPathDelegate(
    string workspaceRoot,
    string relativePath,
    out string fullPath,
    out string failureReason);

internal static class ProcessExecutionArtifactMetadataRules
{
    internal static string BuildCompletedDecisionArtifactExternalReferenceKey(Guid stepRunId, Guid artifactExpectationId)
    {
        return $"process-step-decision:{stepRunId:D}:{artifactExpectationId:D}";
    }

    internal static string BuildMissingTechnicalAgentBindingDiagnostic(
        Guid processRunId,
        Guid stepRunId,
        string stepTitle,
        Guid currentExecutorPartyId,
        AiResourceBindingStatus? bindingStatus,
        Guid? technicalAgentId)
    {
        var statusSummary = bindingStatus?.ToString() ?? "MissingDirectorySummary";
        var technicalAgentSummary = technicalAgentId.HasValue
            ? technicalAgentId.Value.ToString("D")
            : "none";
        return $"Process automation dispatch cannot run step '{stepTitle}' ({stepRunId:D}) for process run {processRunId:D} because executor party {currentExecutorPartyId:D} is not bound to an active technical agent. Binding status: {statusSummary}; technical agent ID: {technicalAgentSummary}.";
    }

    internal static string BuildStorageRelativePath(Guid processRunId, Guid stepRunId, string artifactRelativePath)
    {
        var normalizedRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifactRelativePath);
        if (!string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            return normalizedRelativePath;
        }

        return $"process-runs/{processRunId:D}/{stepRunId:D}/{Path.GetFileName(artifactRelativePath)}";
    }

    internal static string ResolveWorkspaceWrittenArtifactRelativePath(
        WorkspaceScopeDescriptor workspaceScope,
        string path,
        Func<string, bool> isExternalTargetAliasPath,
        TryMapAbsoluteExternalPathToAlias tryMapAbsoluteExternalPathToAlias,
        Func<WorkspaceScopeDescriptor, string, string> resolveScopedManagedRelativePath)
    {
        ArgumentNullException.ThrowIfNull(isExternalTargetAliasPath);
        ArgumentNullException.ThrowIfNull(tryMapAbsoluteExternalPathToAlias);
        ArgumentNullException.ThrowIfNull(resolveScopedManagedRelativePath);

        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (isExternalTargetAliasPath(normalized))
        {
            return normalized;
        }

        return tryMapAbsoluteExternalPathToAlias(normalized, out var mappedAlias)
            ? mappedAlias
            : resolveScopedManagedRelativePath(workspaceScope, normalized);
    }

    internal static bool TryResolveWorkspaceWrittenArtifactSourceFullPath(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        string writtenPath,
        string projectedRelativePath,
        Func<string, bool> isExternalTargetAliasPath,
        TryMapAbsoluteExternalPathToAlias tryMapAbsoluteExternalPathToAlias,
        Func<WorkspaceScopeDescriptor, string, string> resolveScopedManagedRelativePath,
        TryResolveArtifactFullPathDelegate tryResolveArtifactFullPath,
        out string fullPath,
        out string sourceRelativePath,
        out string failureReason)
    {
        ArgumentNullException.ThrowIfNull(isExternalTargetAliasPath);
        ArgumentNullException.ThrowIfNull(tryMapAbsoluteExternalPathToAlias);
        ArgumentNullException.ThrowIfNull(resolveScopedManagedRelativePath);
        ArgumentNullException.ThrowIfNull(tryResolveArtifactFullPath);

        fullPath = string.Empty;
        sourceRelativePath = string.Empty;
        failureReason = string.Empty;

        var sourceCandidates = ResolveWorkspaceWrittenArtifactSourceRelativePaths(
            workspaceScope,
            writtenPath,
            projectedRelativePath,
            isExternalTargetAliasPath,
            tryMapAbsoluteExternalPathToAlias,
            resolveScopedManagedRelativePath);
        foreach (var candidatePath in sourceCandidates)
        {
            if (!tryResolveArtifactFullPath(workspaceRoot, candidatePath, out var candidateFullPath, out var candidateFailure))
            {
                failureReason = candidateFailure;
                continue;
            }

            if (!File.Exists(candidateFullPath))
            {
                failureReason = $"File '{candidatePath}' does not exist.";
                continue;
            }

            fullPath = candidateFullPath;
            sourceRelativePath = candidatePath;
            failureReason = string.Empty;
            return true;
        }

        return false;
    }

    internal static IReadOnlyList<string> ResolveWorkspaceWrittenArtifactSourceRelativePaths(
        WorkspaceScopeDescriptor workspaceScope,
        string writtenPath,
        string projectedRelativePath,
        Func<string, bool> isExternalTargetAliasPath,
        TryMapAbsoluteExternalPathToAlias tryMapAbsoluteExternalPathToAlias,
        Func<WorkspaceScopeDescriptor, string, string> resolveScopedManagedRelativePath)
    {
        ArgumentNullException.ThrowIfNull(isExternalTargetAliasPath);
        ArgumentNullException.ThrowIfNull(tryMapAbsoluteExternalPathToAlias);
        ArgumentNullException.ThrowIfNull(resolveScopedManagedRelativePath);

        var paths = new List<string>();
        AddWorkspaceWrittenArtifactSourceRelativePath(paths, writtenPath, tryMapAbsoluteExternalPathToAlias);
        AddWorkspaceWrittenArtifactSourceRelativePath(paths, projectedRelativePath, tryMapAbsoluteExternalPathToAlias);

        var normalizedWrittenPath = WorkspaceScopeDescriptor.NormalizeRelativePath(writtenPath);
        if (!string.IsNullOrWhiteSpace(normalizedWrittenPath) &&
            !isExternalTargetAliasPath(normalizedWrittenPath) &&
            !tryMapAbsoluteExternalPathToAlias(normalizedWrittenPath, out _) &&
            IsManagedWorkspaceArtifactPath(normalizedWrittenPath))
        {
            AddWorkspaceWrittenArtifactSourceRelativePath(
                paths,
                resolveScopedManagedRelativePath(workspaceScope, normalizedWrittenPath),
                tryMapAbsoluteExternalPathToAlias);
        }

        return paths;
    }

    internal static bool ShouldAutoRecordCompletedDecisionArtifact(DispatchArtifactExpectation expectedArtifact)
        => ShouldAutoRecordCompletedDecisionArtifact(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact).ToProjectionExpectation());

    internal static bool ShouldAutoRecordCompletedDecisionArtifact(ProcessProjectionArtifactExpectation expectedArtifact)
    {
        return expectedArtifact.IsRequired &&
               expectedArtifact.ArtifactKind is ProcessArtifactKind.Decision or ProcessArtifactKind.DecisionRecord &&
               expectedArtifact.TrustRequirement is ProcessArtifactTrustRequirement.ReviewRequired or ProcessArtifactTrustRequirement.HumanApproved or ProcessArtifactTrustRequirement.ApprovalRequired;
    }

    internal static ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.HumanApproved or ProcessArtifactTrustRequirement.ApprovalRequired => ProcessArtifactTrustStatus.Approved,
            _ => ProcessArtifactTrustStatus.ReviewRequired
        };
    }

    private static void AddWorkspaceWrittenArtifactSourceRelativePath(
        ICollection<string> paths,
        string path,
        TryMapAbsoluteExternalPathToAlias tryMapAbsoluteExternalPathToAlias)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized) ||
            paths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (tryMapAbsoluteExternalPathToAlias(normalized, out var mappedAlias))
        {
            if (!paths.Contains(mappedAlias, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(mappedAlias);
            }

            return;
        }

        paths.Add(normalized);
    }

    private static bool IsManagedWorkspaceArtifactPath(string path)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var rootSegment = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return rootSegment is not null && ProcessArtifactPathValidationRules.IsManagedRootSegment(rootSegment);
    }
}
