using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactLineageValidationContext(
    Guid ProcessRunId,
    Guid? StepRunId = null,
    Guid? ExecutionRunId = null,
    Guid? WorkflowRunId = null,
    Guid? SubprocessRunId = null,
    Guid? RecoveryExecutionRunId = null,
    Guid? RecoveredForExecutionRunId = null);

internal sealed record ProcessArtifactLineageValidationResult(
    bool IsCurrentRun,
    string Diagnostic)
{
    public static ProcessArtifactLineageValidationResult Valid { get; } = new(true, string.Empty);

    public static ProcessArtifactLineageValidationResult Invalid(string diagnostic)
        => new(false, diagnostic);
}

internal static class ProcessArtifactLineageValidator
{
    public static ProcessArtifactLineageValidationResult ValidateCurrentRunArtifact(
        ProcessArtifactRecord artifact,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind,
        ProcessArtifactLineageValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(context);

        if (artifact.ProcessRunId != context.ProcessRunId)
        {
            return ProcessArtifactLineageValidationResult.Invalid(
                $"Artifact '{artifact.Title}' belongs to process run {artifact.ProcessRunId:D}, not current process run {context.ProcessRunId:D}.");
        }

        if (context.StepRunId.HasValue && artifact.StepRunId != context.StepRunId)
        {
            var actualStepRunId = artifact.StepRunId?.ToString("D") ?? "<none>";
            return ProcessArtifactLineageValidationResult.Invalid(
                $"Artifact '{artifact.Title}' belongs to step run {actualStepRunId}, not current step run {context.StepRunId.Value:D}.");
        }

        var boundaryResult = ValidateManagedStorageBoundary(artifact, context.ProcessRunId);
        if (!boundaryResult.IsCurrentRun)
        {
            return boundaryResult;
        }

        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
        if (lineage is not null && (lineage.SourceKind != ProcessArtifactProjectionSourceKind.Unknown || IsManagerRecoveryLineage(lineage)))
        {
            return ValidateProjectionLineage(lineage, producerKind, context);
        }

        return ValidateExternalReferenceLineage(artifact, producerKind, context);
    }

    public static ProcessArtifactLineageValidationResult ValidateManagedStorageBoundary(
        ProcessArtifactRecord artifact,
        Guid processRunId)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        if (string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
        {
            return ProcessArtifactLineageValidationResult.Valid;
        }

        var normalizedPath = artifact.ManagedStoragePath.Replace('\\', '/').Trim().TrimStart('/');
        if (StorageJson.TryParseReference(normalizedPath, out _))
        {
            return ProcessArtifactLineageValidationResult.Valid;
        }

        if (normalizedPath.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            return IsExternalArtifactDestinationAlias(normalizedPath)
                ? ProcessArtifactLineageValidationResult.Valid
                : ProcessArtifactLineageValidationResult.Invalid(
                    $"Artifact '{artifact.Title}' points at external target path '{artifact.ManagedStoragePath}', which is not an artifact, evidence, output, or report destination.");
        }

        if (normalizedPath.StartsWith("src/", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactLineageValidationResult.Invalid(
                $"Artifact '{artifact.Title}' points at product path '{artifact.ManagedStoragePath}', not a managed artifact root.");
        }

        if (IsWrongProcessRunRoot(normalizedPath, "artifacts", processRunId) ||
            IsWrongProcessRunRoot(normalizedPath, "output", processRunId))
        {
            return ProcessArtifactLineageValidationResult.Invalid(
                $"Artifact '{artifact.Title}' points at stale or unrelated run root '{artifact.ManagedStoragePath}', not current process run {processRunId:D}.");
        }

        if (string.Equals(normalizedPath, "output", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("output/", StringComparison.OrdinalIgnoreCase))
        {
            if (HasCurrentProcessRunBoundary(normalizedPath, "output", processRunId) ||
                HasCurrentProcessMockBoundary(normalizedPath, processRunId))
            {
                return ProcessArtifactLineageValidationResult.Valid;
            }

            return ProcessArtifactLineageValidationResult.Invalid(
                $"Artifact '{artifact.Title}' points at output path '{artifact.ManagedStoragePath}' without the current process-run boundary.");
        }

        return ProcessArtifactLineageValidationResult.Valid;
    }

    private static ProcessArtifactLineageValidationResult ValidateProjectionLineage(
        ProcessArtifactProjectionLineage lineage,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind,
        ProcessArtifactLineageValidationContext context)
    {
        if (producerKind == ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ManagerRecovery ||
            IsManagerRecoveryLineage(lineage))
        {
            return IsCurrentManagerRecoveryArtifact(lineage, context.ExecutionRunId, context.RecoveryExecutionRunId, context.RecoveredForExecutionRunId)
                ? ProcessArtifactLineageValidationResult.Valid
                : ProcessArtifactLineageValidationResult.Invalid("Manager recovery artifact lineage is not bound to the current recovery execution and recovered execution run.");
        }

        var current = producerKind switch
        {
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AgentExecutionArtifact or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkspaceWrite or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ExistingManagedFile or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AssistantResponse or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProviderNativeBrowser => context.ExecutionRunId.HasValue &&
                lineage.SourceExecutionRunId == context.ExecutionRunId.Value,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowRun or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact => context.WorkflowRunId.HasValue &&
                lineage.WorkflowRunId == context.WorkflowRunId.Value,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.SubprocessArtifact => context.SubprocessRunId.HasValue &&
                lineage.SubprocessRunId == context.SubprocessRunId.Value,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.CompletedDecision or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProcessMock or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Manual => true,
            _ => false
        };

        return current
            ? ProcessArtifactLineageValidationResult.Valid
            : ProcessArtifactLineageValidationResult.Invalid("Artifact projection lineage is not bound to the current execution, workflow, or subprocess run.");
    }

    private static ProcessArtifactLineageValidationResult ValidateExternalReferenceLineage(
        ProcessArtifactRecord artifact,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind,
        ProcessArtifactLineageValidationContext context)
    {
        var key = artifact.ExternalReferenceKey;
        var provenance = artifact.ProvenanceSummary;
        var current = producerKind switch
        {
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AgentExecutionArtifact or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkspaceWrite or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ExistingManagedFile or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AssistantResponse or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProviderNativeBrowser => context.ExecutionRunId.HasValue &&
                (ContainsGuidToken(key, context.ExecutionRunId.Value) ||
                 ContainsGuidToken(provenance, context.ExecutionRunId.Value)),
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowRun or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact => context.WorkflowRunId.HasValue &&
                ContainsGuidToken(key, context.WorkflowRunId.Value),
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.SubprocessArtifact => context.SubprocessRunId.HasValue &&
                ContainsGuidToken(key, context.SubprocessRunId.Value),
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ManagerRecovery => IsCurrentManagerRecoveryArtifact(
                key,
                provenance,
                context.ExecutionRunId,
                context.RecoveryExecutionRunId,
                context.RecoveredForExecutionRunId),
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.CompletedDecision or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProcessMock or
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Manual => true,
            _ => string.IsNullOrWhiteSpace(key)
        };

        return current
            ? ProcessArtifactLineageValidationResult.Valid
            : ProcessArtifactLineageValidationResult.Invalid("Artifact external reference key or provenance is not bound to the current execution, workflow, subprocess, or recovery run.");
    }

    private static bool IsWrongProcessRunRoot(
        string normalizedPath,
        string rootSegment,
        Guid processRunId)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3 ||
            !string.Equals(segments[0], rootSegment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var index = 1; index < segments.Length - 1; index++)
        {
            if (!string.Equals(segments[index], "process-runs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var runSegment = segments[index + 1];
            if (IsCurrentRunSegment(runSegment, processRunId))
            {
                return false;
            }

            return LooksLikeRunIdSegment(runSegment);
        }

        return false;
    }

    private static bool HasCurrentProcessRunBoundary(
        string normalizedPath,
        string rootSegment,
        Guid processRunId)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3 ||
            !string.Equals(segments[0], rootSegment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var index = 1; index < segments.Length - 1; index++)
        {
            if (string.Equals(segments[index], "process-runs", StringComparison.OrdinalIgnoreCase) &&
                IsCurrentRunSegment(segments[index + 1], processRunId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCurrentProcessMockBoundary(string normalizedPath, Guid processRunId)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3 ||
            !string.Equals(segments[0], "output", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var processMockRunKey = processRunId.ToString("N")[..16];
        for (var index = 1; index < segments.Length - 1; index++)
        {
            if (string.Equals(segments[index], "process-mock", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[index + 1], processMockRunKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCurrentRunSegment(string value, Guid processRunId)
    {
        return string.Equals(value, processRunId.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, processRunId.ToString("N"), StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeRunIdSegment(string value)
    {
        if (Guid.TryParse(value, out _))
        {
            return true;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.Length == 32 && normalized.All(Uri.IsHexDigit);
    }

    private static bool IsExternalArtifactDestinationAlias(string normalizedPath)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment =>
            string.Equals(segment, "artifact", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "report", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "reports", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsManagerRecoveryLineage(ProcessArtifactProjectionLineage lineage)
    {
        return lineage.RecoveryExecutionRunId.HasValue && lineage.RecoveredForExecutionRunId.HasValue;
    }

    private static bool IsCurrentManagerRecoveryArtifact(
        ProcessArtifactProjectionLineage lineage,
        Guid? executionRunId,
        Guid? recoveryExecutionRunId,
        Guid? recoveredForExecutionRunId)
    {
        var effectiveRecoveryExecutionRunId = recoveryExecutionRunId ?? executionRunId;
        return effectiveRecoveryExecutionRunId.HasValue &&
               lineage.RecoveryExecutionRunId == effectiveRecoveryExecutionRunId.Value &&
               recoveredForExecutionRunId.HasValue &&
               lineage.RecoveredForExecutionRunId == recoveredForExecutionRunId.Value;
    }

    private static bool IsCurrentManagerRecoveryArtifact(
        string key,
        string provenance,
        Guid? executionRunId,
        Guid? recoveryExecutionRunId,
        Guid? recoveredForExecutionRunId)
    {
        var effectiveRecoveryExecutionRunId = recoveryExecutionRunId ?? executionRunId;
        if (!effectiveRecoveryExecutionRunId.HasValue)
        {
            return false;
        }

        if (!ContainsGuidToken(key, effectiveRecoveryExecutionRunId.Value) &&
            !ContainsGuidToken(provenance, effectiveRecoveryExecutionRunId.Value))
        {
            return false;
        }

        if (!recoveredForExecutionRunId.HasValue)
        {
            return false;
        }

        return ContainsGuidToken(key, recoveredForExecutionRunId.Value) ||
               ContainsGuidToken(provenance, recoveredForExecutionRunId.Value);
    }

    private static bool ContainsGuidToken(string? text, Guid value)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.Contains(value.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }
}
