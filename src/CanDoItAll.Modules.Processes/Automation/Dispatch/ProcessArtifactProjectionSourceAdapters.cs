using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessMockArtifactProjectionSource(
    Guid StepRunId,
    Guid ExecutionRunId,
    string RelativePath,
    string ScopedRelativePath,
    string RoleKey);

internal sealed record WorkspaceWrittenArtifactProjectionSource(
    Guid ExecutionRunId,
    string ProjectedRelativePath,
    string SourceRelativePath);

internal sealed record ExistingManagedArtifactProjectionSource(
    Guid ExecutionRunId,
    string ProjectedRelativePath);

internal sealed record ResponseTextArtifactProjectionSource(
    Guid ExecutionRunId,
    string ProjectedRelativePath);

internal sealed record ProviderNativeBrowserArtifactProjectionSource(
    Guid ExecutionRunId,
    string ProjectedRelativePath,
    string SourceOutputPath,
    string ProducedByToolName);

internal static class ProcessMockArtifactProjectionSourceAdapter
{
    public static string BuildExternalReferenceKey(
        ProcessMockArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedArtifact);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        return ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
            BuildSourceExternalReferenceKey(source, expectedArtifact),
            source.ExecutionRunId,
            recoveryContext);
    }

    public static ProcessArtifactProjectionPlan Plan(
        ProcessMockArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessStepRunStatus completionStatus,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedArtifact);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        var sourceExternalReferenceKey = BuildSourceExternalReferenceKey(source, expectedArtifact);

        return new ProcessArtifactProjectionPlan(
            ProcessArtifactProjectionSourceKind.ProcessMock,
            sourceExternalReferenceKey,
            BuildExternalReferenceKey(source, expectedArtifact, recoveryContext),
            expectedArtifact.Id,
            expectedArtifact.ArtifactKind,
            expectedArtifact.Title,
            ProcessArtifactProjectionPlanner.ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
            expectedArtifact.SensitivityLevel,
            $"Projected from deterministic process mock artifact '{source.RelativePath}' at scoped workspace path '{source.ScopedRelativePath}' for AgentFramework execution run {source.ExecutionRunId:D}.",
            ProcessArtifactProjectionSourceAdapterDefaults.ResolveAllowedFutureUsage(
                expectedArtifact,
                "Process mock evidence and regression audit review."),
            $"Process mock role '{source.RoleKey}' produced '{Path.GetFileName(source.RelativePath)}'.",
            ProcessArtifactProjectionLineageBuilder.BuildLineage(
                ProcessArtifactProjectionSourceKind.ProcessMock,
                source.ExecutionRunId,
                recoveryContext,
                sourceExternalReferenceKey: sourceExternalReferenceKey));
    }

    private static string BuildSourceExternalReferenceKey(
        ProcessMockArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact)
        => ProcessArtifactProjectionPlanner.BuildProcessMockArtifactExternalReferenceKey(
            source.StepRunId,
            expectedArtifact.Id,
            source.RelativePath);
}

internal static class WorkspaceWrittenArtifactProjectionSourceAdapter
{
    public static string BuildExternalReferenceKey(
        WorkspaceWrittenArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedArtifact);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        return ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
            BuildSourceExternalReferenceKey(source, expectedArtifact),
            source.ExecutionRunId,
            recoveryContext);
    }

    public static ProcessArtifactProjectionPlan Plan(
        WorkspaceWrittenArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessStepRunStatus completionStatus,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedArtifact);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        var sourceExternalReferenceKey = BuildSourceExternalReferenceKey(source, expectedArtifact);

        return new ProcessArtifactProjectionPlan(
            ProcessArtifactProjectionSourceKind.WorkspaceWrite,
            sourceExternalReferenceKey,
            BuildExternalReferenceKey(source, expectedArtifact, recoveryContext),
            expectedArtifact.Id,
            expectedArtifact.ArtifactKind,
            expectedArtifact.Title,
            ProcessArtifactProjectionPlanner.ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
            expectedArtifact.SensitivityLevel,
            ProcessArtifactProjectionLineageBuilder.BuildProvenance(
                $"Projected from workspace file write '{source.SourceRelativePath}' for AgentFramework execution run {source.ExecutionRunId:D}.",
                source.ExecutionRunId,
                recoveryContext),
            ProcessArtifactProjectionSourceAdapterDefaults.ResolveAllowedFutureUsage(
                expectedArtifact,
                "Process evidence and audit review."),
            string.Equals(source.SourceRelativePath, source.ProjectedRelativePath, StringComparison.OrdinalIgnoreCase)
                ? $"Workspace file write produced '{source.ProjectedRelativePath}'."
                : $"Workspace file write produced '{source.SourceRelativePath}' and was imported as '{source.ProjectedRelativePath}'.",
            ProcessArtifactProjectionLineageBuilder.BuildLineage(
                ProcessArtifactProjectionSourceKind.WorkspaceWrite,
                source.ExecutionRunId,
                recoveryContext,
                sourceExternalReferenceKey: sourceExternalReferenceKey));
    }

    private static string BuildSourceExternalReferenceKey(
        WorkspaceWrittenArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact)
        => ProcessArtifactProjectionPlanner.BuildWorkspaceWrittenArtifactExternalReferenceKey(
            source.ExecutionRunId,
            expectedArtifact.Id,
            source.ProjectedRelativePath);
}

internal static class ExistingManagedArtifactProjectionSourceAdapter
{
    public static string BuildExternalReferenceKey(
        ExistingManagedArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedArtifact);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        return ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
            BuildSourceExternalReferenceKey(source, expectedArtifact),
            source.ExecutionRunId,
            recoveryContext);
    }

    public static ProcessArtifactProjectionPlan Plan(
        ExistingManagedArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessStepRunStatus completionStatus,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedArtifact);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        var sourceExternalReferenceKey = BuildSourceExternalReferenceKey(source, expectedArtifact);

        return new ProcessArtifactProjectionPlan(
            ProcessArtifactProjectionSourceKind.ExistingManagedFile,
            sourceExternalReferenceKey,
            BuildExternalReferenceKey(source, expectedArtifact, recoveryContext),
            expectedArtifact.Id,
            expectedArtifact.ArtifactKind,
            expectedArtifact.Title,
            ProcessArtifactProjectionPlanner.ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
            expectedArtifact.SensitivityLevel,
            ProcessArtifactProjectionLineageBuilder.BuildProvenance(
                $"Projected from existing managed workspace artifact '{source.ProjectedRelativePath}' for AgentFramework execution run {source.ExecutionRunId:D}.",
                source.ExecutionRunId,
                recoveryContext),
            ProcessArtifactProjectionSourceAdapterDefaults.ResolveAllowedFutureUsage(
                expectedArtifact,
                "Process evidence and audit review."),
            $"Managed workspace artifact '{source.ProjectedRelativePath}' already existed when the step outcome was finalized.",
            ProcessArtifactProjectionLineageBuilder.BuildLineage(
                ProcessArtifactProjectionSourceKind.ExistingManagedFile,
                source.ExecutionRunId,
                recoveryContext,
                sourceExternalReferenceKey: sourceExternalReferenceKey));
    }

    private static string BuildSourceExternalReferenceKey(
        ExistingManagedArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact)
        => ProcessArtifactProjectionPlanner.BuildExistingManagedArtifactExternalReferenceKey(
            source.ExecutionRunId,
            expectedArtifact.Id,
            source.ProjectedRelativePath);
}

internal static class ResponseTextArtifactProjectionSourceAdapter
{
    public static string BuildExternalReferenceKey(
        ResponseTextArtifactProjectionSource source,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        return ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
            BuildSourceExternalReferenceKey(source),
            source.ExecutionRunId,
            recoveryContext);
    }

    public static ProcessArtifactProjectionPlan Plan(
        ResponseTextArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessStepRunStatus completionStatus,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedArtifact);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        var sourceExternalReferenceKey = BuildSourceExternalReferenceKey(source);

        return new ProcessArtifactProjectionPlan(
            ProcessArtifactProjectionSourceKind.AssistantResponse,
            sourceExternalReferenceKey,
            BuildExternalReferenceKey(source, recoveryContext),
            expectedArtifact.Id,
            expectedArtifact.ArtifactKind,
            expectedArtifact.Title,
            ProcessArtifactProjectionPlanner.ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
            expectedArtifact.SensitivityLevel,
            ProcessArtifactProjectionLineageBuilder.BuildProvenance(
                $"Projected from the final assistant response for AgentFramework execution run {source.ExecutionRunId:D}.",
                source.ExecutionRunId,
                recoveryContext),
            ProcessArtifactProjectionSourceAdapterDefaults.ResolveAllowedFutureUsage(
                expectedArtifact,
                "Process evidence and audit review."),
            "Projected the final assistant response into the required managed text artifact path.",
            ProcessArtifactProjectionLineageBuilder.BuildLineage(
                ProcessArtifactProjectionSourceKind.AssistantResponse,
                source.ExecutionRunId,
                recoveryContext,
                sourceExternalReferenceKey: sourceExternalReferenceKey));
    }

    private static string BuildSourceExternalReferenceKey(ResponseTextArtifactProjectionSource source)
        => ProcessArtifactProjectionPlanner.BuildResponseTextArtifactExternalReferenceKey(
            source.ExecutionRunId,
            source.ProjectedRelativePath);
}

internal static class ProviderNativeBrowserArtifactProjectionSourceAdapter
{
    public static string BuildExternalReferenceKey(
        ProviderNativeBrowserArtifactProjectionSource source,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        return ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
            BuildSourceExternalReferenceKey(source),
            source.ExecutionRunId,
            recoveryContext);
    }

    public static ProcessArtifactProjectionPlan PlanExpectedOutput(
        ProviderNativeBrowserArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessStepRunStatus completionStatus,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedArtifact);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        var sourceExternalReferenceKey = BuildSourceExternalReferenceKey(source);

        return new ProcessArtifactProjectionPlan(
            ProcessArtifactProjectionSourceKind.ProviderNativeBrowser,
            sourceExternalReferenceKey,
            BuildExternalReferenceKey(source, recoveryContext),
            expectedArtifact.Id,
            expectedArtifact.ArtifactKind,
            expectedArtifact.Title,
            ProcessArtifactProjectionPlanner.ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
            expectedArtifact.SensitivityLevel,
            ProcessArtifactProjectionLineageBuilder.BuildProvenance(
                $"Projected from provider-native browser output '{source.SourceOutputPath}' for AgentFramework execution run {source.ExecutionRunId:D}.",
                source.ExecutionRunId,
                recoveryContext),
            "Process evidence and audit review.",
            $"Projected provider-native browser output '{source.SourceOutputPath}' into the required managed artifact path.",
            ProcessArtifactProjectionLineageBuilder.BuildLineage(
                ProcessArtifactProjectionSourceKind.ProviderNativeBrowser,
                source.ExecutionRunId,
                recoveryContext,
                sourceExternalReferenceKey: sourceExternalReferenceKey));
    }

    public static ProcessArtifactProjectionPlan PlanDiscoveredOutput(
        ProviderNativeBrowserArtifactProjectionSource source,
        ProcessArtifactExpectationSnapshot? matchedExpectation,
        ProcessArtifactKind fallbackArtifactKind,
        string fallbackTitle,
        ProcessStepRunStatus completionStatus,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        var sourceExternalReferenceKey = BuildSourceExternalReferenceKey(source);

        return new ProcessArtifactProjectionPlan(
            ProcessArtifactProjectionSourceKind.ProviderNativeBrowser,
            sourceExternalReferenceKey,
            BuildExternalReferenceKey(source, recoveryContext),
            matchedExpectation?.Id,
            matchedExpectation?.ArtifactKind ?? fallbackArtifactKind,
            matchedExpectation?.Title ?? fallbackTitle,
            matchedExpectation is null
                ? ProcessArtifactTrustStatus.ReviewRequired
                : ProcessArtifactProjectionPlanner.ResolveProjectedArtifactTrustStatus(matchedExpectation, completionStatus),
            matchedExpectation?.SensitivityLevel ?? ProcessSensitivityLevel.Internal,
            ProcessArtifactProjectionLineageBuilder.BuildProvenance(
                $"Projected from provider-native browser output '{source.SourceOutputPath}' for AgentFramework execution run {source.ExecutionRunId:D}.",
                source.ExecutionRunId,
                recoveryContext),
            matchedExpectation is not null &&
            !string.IsNullOrWhiteSpace(matchedExpectation.AllowedFutureUsageSummary)
                ? matchedExpectation.AllowedFutureUsageSummary
                : "Process evidence and audit review.",
            $"Projected provider-native browser output '{source.SourceOutputPath}' into the scoped managed artifact path.",
            ProcessArtifactProjectionLineageBuilder.BuildLineage(
                ProcessArtifactProjectionSourceKind.ProviderNativeBrowser,
                source.ExecutionRunId,
                recoveryContext,
                sourceExternalReferenceKey: sourceExternalReferenceKey));
    }

    private static string BuildSourceExternalReferenceKey(ProviderNativeBrowserArtifactProjectionSource source)
        => ProcessArtifactProjectionPlanner.BuildProviderNativeBrowserArtifactExternalReferenceKey(
            source.ExecutionRunId,
            source.ProjectedRelativePath);
}

internal static class ProcessArtifactProjectionSourceAdapterDefaults
{
    public static string ResolveAllowedFutureUsage(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string fallback)
    {
        ArgumentNullException.ThrowIfNull(expectedArtifact);

        return string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
            ? fallback
            : expectedArtifact.AllowedFutureUsageSummary;
    }
}
