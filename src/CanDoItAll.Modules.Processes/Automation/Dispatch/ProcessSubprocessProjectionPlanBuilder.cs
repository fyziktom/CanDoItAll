using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessSubprocessArtifactProjectionPlan(
    Guid ArtifactExpectationId,
    ProcessArtifactKind ArtifactKind,
    string Title,
    ProcessArtifactTrustStatus TrustStatus,
    ProcessSensitivityLevel SensitivityLevel,
    string ProvenanceSummary,
    string AllowedFutureUsageSummary,
    string ReviewSummary,
    string ManagedStoragePath,
    string ExternalReferenceKey,
    ProcessArtifactProjectionLineage ProjectionLineage,
    string MarkdownContent);

internal static class ProcessSubprocessProjectionPlanBuilder
{
    public static ProcessSubprocessArtifactProjectionPlan Build(
        ProcessDispatchSubprocessRuntimeInput input,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactExpectation expectation,
        ProcessArtifactRecord sourceArtifact,
        string projectionDiagnostic,
        string scopedProfileId)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(subprocessRun);
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(sourceArtifact);

        var projectionLineage = ProcessArtifactProjectionLineageJson.Normalize(
            new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.SubprocessArtifact,
                SubprocessRunId = subprocessRun.RunId,
                SourceArtifactId = sourceArtifact.Id,
                SourceExternalReferenceKey = sourceArtifact.ExternalReferenceKey
            })!;
        var managedStoragePath = BuildManagedStoragePath(input, expectation, scopedProfileId);

        return new ProcessSubprocessArtifactProjectionPlan(
            expectation.Id,
            expectation.ArtifactKind,
            expectation.Title,
            ProcessArtifactTrustStatus.ReviewRequired,
            ResolveSensitivity(expectation, sourceArtifact),
            BuildProvenance(input, subprocessRun, sourceArtifact),
            expectation.AllowedFutureUsageSummary,
            BuildReviewSummary(subprocessRun, sourceArtifact, projectionDiagnostic),
            managedStoragePath,
            BuildExternalReferenceKey(subprocessRun.RunId, sourceArtifact.Id),
            projectionLineage,
            BuildMarkdown(input, subprocessRun, expectation, sourceArtifact));
    }

    public static bool SatisfiesCurrentArtifactExpectation(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation,
        Guid subprocessRunId)
    {
        return SatisfiesArtifactExpectation(artifact, expectation) &&
               artifact.ExternalReferenceKey.StartsWith("subprocess-run:", StringComparison.OrdinalIgnoreCase) &&
               artifact.ExternalReferenceKey.Contains(subprocessRunId.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildManagedStoragePath(
        ProcessDispatchSubprocessRuntimeInput input,
        ProcessArtifactExpectation expectation,
        string scopedProfileId)
    {
        var fileSlug = FileSafeSlugBuilder.Build(expectation.Title);
        if (string.IsNullOrWhiteSpace(fileSlug))
        {
            fileSlug = "subprocess-artifact-projection";
        }

        return WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(
            "artifacts",
            "scopes",
            "organization",
            scopedProfileId,
            "process-runs",
            input.Run.Id.ToString("D"),
            input.StepRun.Id.ToString("D"),
            $"{fileSlug}.md"));
    }

    private static bool SatisfiesArtifactExpectation(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return false;
        }

        if (artifact.SensitivityLevel < expectation.SensitivityLevel)
        {
            return false;
        }

        if (!SatisfiesTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement))
        {
            return false;
        }

        return artifact.ArtifactExpectationId.HasValue
            ? artifact.ArtifactExpectationId.Value == expectation.Id
            : string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SatisfiesTrustRequirement(
        ProcessArtifactTrustStatus trustStatus,
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.None => true,
            ProcessArtifactTrustRequirement.ReviewRequired => trustStatus is
                ProcessArtifactTrustStatus.ReviewRequired or
                ProcessArtifactTrustStatus.Approved or
                ProcessArtifactTrustStatus.TrustedSource,
            ProcessArtifactTrustRequirement.HumanApproved => trustStatus == ProcessArtifactTrustStatus.Approved,
            ProcessArtifactTrustRequirement.ApprovalRequired => trustStatus == ProcessArtifactTrustStatus.Approved,
            ProcessArtifactTrustRequirement.TrustedSource => trustStatus == ProcessArtifactTrustStatus.TrustedSource,
            _ => false
        };
    }

    private static ProcessSensitivityLevel ResolveSensitivity(
        ProcessArtifactExpectation expectation,
        ProcessArtifactRecord sourceArtifact)
    {
        return sourceArtifact.SensitivityLevel < expectation.SensitivityLevel
            ? expectation.SensitivityLevel
            : sourceArtifact.SensitivityLevel;
    }

    private static string BuildProvenance(
        ProcessDispatchSubprocessRuntimeInput input,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactRecord sourceArtifact)
    {
        return $"Auto-projected from completed subprocess run '{subprocessRun.RunName}' ({subprocessRun.RunId:D}) for parent subprocess step '{input.StepRun.Title}'. Source subprocess artifact '{sourceArtifact.Title}' ({sourceArtifact.Id:D}).";
    }

    private static string BuildReviewSummary(
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactRecord sourceArtifact,
        string projectionDiagnostic)
    {
        var diagnosticSuffix = string.IsNullOrWhiteSpace(projectionDiagnostic)
            ? string.Empty
            : $" Mapping diagnostic: {projectionDiagnostic}";
        var summary = string.IsNullOrWhiteSpace(sourceArtifact.ReviewSummary)
            ? $"Subprocess run '{subprocessRun.RunName}' completed. Source artifact: {sourceArtifact.Title}."
            : $"Subprocess run '{subprocessRun.RunName}' completed. Source artifact: {sourceArtifact.Title}. {sourceArtifact.ReviewSummary}";

        return $"{summary}{diagnosticSuffix}";
    }

    private static string BuildExternalReferenceKey(Guid subprocessRunId, Guid artifactId)
    {
        return $"subprocess-run:{subprocessRunId:D}:artifact:{artifactId:D}";
    }

    private static string BuildMarkdown(
        ProcessDispatchSubprocessRuntimeInput input,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactExpectation expectation,
        ProcessArtifactRecord sourceArtifact)
    {
        return $"""
            # {expectation.Title}

            Parent process run: {input.Run.Id:D}
            Parent subprocess step: {input.StepRun.Id:D}
            Subprocess run: {subprocessRun.RunId:D}
            Subprocess artifact: {sourceArtifact.Id:D}
            Subprocess artifact title: {sourceArtifact.Title}
            Subprocess managed path: {sourceArtifact.ManagedStoragePath}

            This parent-scoped artifact is a durable projection of the completed subprocess output. The child run artifact ledger remains the source of detailed runtime evidence.
            """;
    }
}
