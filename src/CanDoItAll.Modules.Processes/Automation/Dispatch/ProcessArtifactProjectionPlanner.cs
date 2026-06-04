using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactProjectionPlan(
    ProcessArtifactProjectionSourceKind SourceKind,
    string SourceExternalReferenceKey,
    string ExternalReferenceKey,
    Guid? ArtifactExpectationId,
    ProcessArtifactKind ArtifactKind,
    string Title,
    ProcessArtifactTrustStatus TrustStatus,
    ProcessSensitivityLevel SensitivityLevel,
    string ProvenanceSummary,
    string AllowedFutureUsageSummary,
    string ReviewSummary,
    ProcessArtifactProjectionLineage ProjectionLineage);

internal static class ProcessArtifactProjectionPlanner
{
    public static ProcessArtifactProjectionPlan PlanExecutionArtifact(
        Guid executionRunId,
        ProcessAutomationExecutionArtifact artifact,
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation? matchedExpectation,
        ProcessArtifactKind fallbackArtifactKind,
        ProcessStepRunStatus completionStatus,
        string runResultSummary,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        var sourceExternalReferenceKey = BuildExecutionArtifactExternalReferenceKey(artifact.Id);

        return new ProcessArtifactProjectionPlan(
            ProcessArtifactProjectionSourceKind.AgentExecutionArtifact,
            sourceExternalReferenceKey,
            ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
                sourceExternalReferenceKey,
                executionRunId,
                recoveryContext),
            matchedExpectation?.Id,
            matchedExpectation?.ArtifactKind ?? fallbackArtifactKind,
            matchedExpectation?.Title ?? BuildArtifactTitle(artifact),
            matchedExpectation is null
                ? ProcessArtifactTrustStatus.ReviewRequired
                : ResolveProjectedArtifactTrustStatus(matchedExpectation, completionStatus),
            matchedExpectation?.SensitivityLevel ?? ProcessSensitivityLevel.Internal,
            ProcessArtifactProjectionLineageBuilder.BuildProvenance(
                $"Projected from AgentFramework execution run {executionRunId:D} artifact '{artifact.RelativePath}'.",
                executionRunId,
                recoveryContext),
            "Process evidence and audit review.",
            string.IsNullOrWhiteSpace(artifact.Summary) ? runResultSummary : artifact.Summary,
            ProcessArtifactProjectionLineageBuilder.BuildLineage(
                ProcessArtifactProjectionSourceKind.AgentExecutionArtifact,
                executionRunId,
                recoveryContext,
                sourceArtifactId: artifact.Id,
                sourceExternalReferenceKey: sourceExternalReferenceKey));
    }

    public static string BuildExecutionArtifactExternalReferenceKey(Guid artifactId)
        => $"agentframework-artifact:{artifactId:D}";

    public static string BuildProcessMockArtifactExternalReferenceKey(
        Guid stepRunId,
        Guid artifactExpectationId,
        string relativePath)
        => $"process-mock-artifact:{stepRunId:D}:{artifactExpectationId:D}:{NormalizeManagedRelativePathForComparison(relativePath)}";

    public static string BuildWorkspaceWrittenArtifactExternalReferenceKey(
        Guid executionRunId,
        Guid artifactExpectationId,
        string relativePath)
        => $"workspace-written-artifact|{executionRunId:D}|{artifactExpectationId:D}|{NormalizeManagedRelativePathForComparison(relativePath)}";

    public static string BuildExistingManagedArtifactExternalReferenceKey(
        Guid executionRunId,
        Guid artifactExpectationId,
        string relativePath)
        => $"existing-managed-artifact|{executionRunId:D}|{artifactExpectationId:D}|{NormalizeManagedRelativePathForComparison(relativePath)}";

    public static string BuildResponseTextArtifactExternalReferenceKey(Guid executionRunId, string relativePath)
        => $"assistant-response|{executionRunId:D}|{NormalizeManagedRelativePathForComparison(relativePath)}";

    public static string BuildProviderNativeBrowserArtifactExternalReferenceKey(Guid executionRunId, string relativePath)
        => $"agentframework-browser-artifact:{executionRunId:D}:{WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath)}";

    public static ProcessArtifactTrustStatus ResolveProjectedArtifactTrustStatus(
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact,
        ProcessStepRunStatus completionStatus)
    {
        ArgumentNullException.ThrowIfNull(expectedArtifact);

        return completionStatus == ProcessStepRunStatus.Completed &&
               expectedArtifact.ArtifactKind is ProcessArtifactKind.Decision or ProcessArtifactKind.DecisionRecord
            ? ResolveCompletedDecisionArtifactTrustStatus(expectedArtifact.TrustRequirement)
            : ProcessArtifactTrustStatus.ReviewRequired;
    }

    public static string BuildArtifactTitle(ProcessAutomationExecutionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        return string.IsNullOrWhiteSpace(artifact.DisplayName)
            ? Path.GetFileName(artifact.RelativePath)
            : artifact.DisplayName.Trim();
    }

    private static ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.HumanApproved or ProcessArtifactTrustRequirement.ApprovalRequired => ProcessArtifactTrustStatus.Approved,
            _ => ProcessArtifactTrustStatus.ReviewRequired
        };
    }

    private static string NormalizeManagedRelativePathForComparison(string relativePath)
        => WorkspaceScopeDescriptor
            .NormalizeRelativePath(relativePath)
            .Trim()
            .TrimStart('/');
}
