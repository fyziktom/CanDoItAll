using System.Text.Json;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CoreArtifactExpectationSnapshot = CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot;
using CoreArtifactRecordSnapshot = CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private const string RuntimeHostReadbackRequestedBy = "process-workspace";
    private const string RuntimeHostReadbackCallerContext = "process-workspace:run-detail-runtime-host-readback";
    private const int RuntimeHostReadbackAuditRecordLimit = 10;

    private async Task LoadRuntimeHostReadbackAsync(CancellationToken cancellationToken)
    {
        ClearRuntimeHostReadback();
        if (!selectedRunId.HasValue)
        {
            return;
        }

        var processRunId = selectedRunId.Value;
        var selectedStep = SelectRuntimeHostReadbackStep();
        if (selectedStep is null)
        {
            runtimeHostReadbackError = "Runtime-host readback requires at least one persisted step run.";
            return;
        }

        runtimeHostReadbackLoading = true;
        try
        {
            var requestedAt = DateTimeOffset.UtcNow;
            var payload = BuildRuntimeHostArtifactEvidencePayload(processRunId, selectedStep, requestedAt);
            var readbackTask = ManagerVerificationFacade.VerifyForReadbackAsync(
                new ProcessManagerReadOnlyVerificationReadbackRequest(
                    new ProcessManagerReadOnlyVerificationCommandRequest(
                        ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency,
                        payload,
                        ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
                        RuntimeHostReadbackRequestedBy,
                        requestedAt),
                    RuntimeHostReadbackAuditRecordLimit),
                cancellationToken);
            var statusTask = ManagerVerificationFacade.GetRuntimeHostStatusAsync(
                new ProcessVerificationRuntimeHostStatusRequest(
                    $"run-detail:{processRunId:D}",
                    RuntimeHostReadbackRequestedBy,
                    requestedAt),
                cancellationToken);

            await Task.WhenAll(readbackTask, statusTask);
            if (selectedRunId != processRunId)
            {
                return;
            }

            runtimeHostReadback = MapRuntimeHostReadback(statusTask.Result, readbackTask.Result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || componentLifetimeCts.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (selectedRunId == processRunId)
            {
                runtimeHostReadbackError = $"Runtime-host readback failed: {exception.GetType().Name}: {exception.Message}";
            }
        }
        finally
        {
            if (selectedRunId == processRunId)
            {
                runtimeHostReadbackLoading = false;
            }
        }
    }

    private void ClearRuntimeHostReadback()
    {
        runtimeHostReadback = null;
        runtimeHostReadbackError = string.Empty;
        runtimeHostReadbackLoading = false;
    }

    private ProcessStepRunViewModel? SelectRuntimeHostReadbackStep()
    {
        return stepRuns
            .OrderByDescending(step => artifacts.Any(artifact => artifact.StepRunId == step.Id))
            .ThenByDescending(step => step.ArtifactExpectations.Count)
            .ThenBy(step => step.Sequence)
            .FirstOrDefault();
    }

    private ProcessReadOnlyVerificationBatchPayload BuildRuntimeHostArtifactEvidencePayload(
        Guid processRunId,
        ProcessStepRunViewModel step,
        DateTimeOffset requestedAt)
    {
        var stepArtifacts = artifacts
            .Where(artifact => artifact.StepRunId == step.Id)
            .OrderByDescending(artifact => artifact.CreatedAtUtc)
            .ToArray();
        var expectedArtifacts = BuildRuntimeHostExpectedArtifactSnapshots(step).ToArray();
        var artifactRecords = BuildRuntimeHostArtifactRecordSnapshots(stepArtifacts).ToArray();
        var projectionLineage = BuildRuntimeHostProjectionLineage(stepArtifacts).ToArray();
        var projectionSourceKinds = ResolveRuntimeHostProjectionSourceKinds(stepArtifacts);
        var projectionSourceOrder = ProcessArtifactProjectionEvidenceDescriptorAdapter
            .DescribeProjectionSourceOrder(projectionSourceKinds);
        var validationRequirements = expectedArtifacts
            .Select(ProcessArtifactValidationRequirementDescriptorRules.Describe)
            .ToArray();
        var projectionPayload = JsonSerializer.Serialize(
            BuildRuntimeHostProjectionPayload(processRunId, step, stepArtifacts, projectionSourceKinds),
            JsonOptions);
        var validationPayload = JsonSerializer.Serialize(
            BuildRuntimeHostValidationPayload(processRunId, step, expectedArtifacts, artifactRecords),
            JsonOptions);
        var artifactId = stepArtifacts.FirstOrDefault()?.Id;
        var artifactPayload = ProcessReadOnlyVerificationPayloadBuilder.CreateArtifactEvidencePayload(
            new ProcessArtifactEvidencePayloadFacts(
                new ProcessReadOnlyPayloadIdentity(
                    processRunId,
                    step.Id,
                    artifactId,
                    RuntimeHostReadbackCallerContext,
                    requestedAt),
                $"process-run://{processRunId:D}/runtime-host/artifact-projection",
                projectionPayload,
                $"process-run://{processRunId:D}/runtime-host/artifact-validation",
                validationPayload,
                projectionLineage,
                projectionSourceOrder,
                ProviderNativeBrowserEvidence: null,
                ValidationRequirements: validationRequirements,
                ExpectedArtifacts: expectedArtifacts,
                ArtifactRecords: artifactRecords));

        return new ProcessReadOnlyVerificationBatchPayload(
            processRunId,
            step.Id,
            RuntimeHostReadbackCallerContext,
            requestedAt,
            artifactEvidencePayloads: [artifactPayload]);
    }

    private IEnumerable<CoreArtifactExpectationSnapshot> BuildRuntimeHostExpectedArtifactSnapshots(
        ProcessStepRunViewModel step)
    {
        return step.ArtifactExpectations.Select(expectation => new CoreArtifactExpectationSnapshot(
            expectation.ArtifactExpectationId,
            ProcessCoreArtifactModelAdapters.ToCoreArtifactKind(expectation.ArtifactKind),
            NormalizeRuntimeHostText(expectation.Title, "Runtime artifact expectation"),
            expectation.IsRequired,
            expectation.IsRequired
                ? ProcessCoreArtifactTrustRequirement.ReviewRequired
                : ProcessCoreArtifactTrustRequirement.None,
            ProcessCoreSensitivityLevel.Internal,
            BuildRuntimeHostValidationRequirementSummary(expectation),
            string.Empty));
    }

    private IEnumerable<CoreArtifactRecordSnapshot> BuildRuntimeHostArtifactRecordSnapshots(
        IReadOnlyList<ProcessArtifactViewModel> stepArtifacts)
    {
        return stepArtifacts.Select(artifact => new CoreArtifactRecordSnapshot(
            artifact.Id,
            artifact.ArtifactExpectationId,
            ProcessCoreArtifactModelAdapters.ToCoreArtifactKind(artifact.ArtifactKind),
            NormalizeRuntimeHostText(artifact.Title, "Runtime artifact"),
            ToCoreTrustStatus(artifact.TrustStatus),
            ToCoreSensitivityLevel(artifact.SensitivityLevel),
            artifact.CreatedAtUtc));
    }

    private IEnumerable<ProcessArtifactProjectionLineageDescriptor> BuildRuntimeHostProjectionLineage(
        IReadOnlyList<ProcessArtifactViewModel> stepArtifacts)
    {
        if (stepArtifacts.Count == 0)
        {
            yield return ProcessArtifactProjectionEvidenceDescriptorAdapter.DescribeLineage(new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.Manual,
                SourceExternalReferenceKey = RuntimeHostReadbackCallerContext
            });
            yield break;
        }

        foreach (var artifact in stepArtifacts)
        {
            yield return ProcessArtifactProjectionEvidenceDescriptorAdapter.DescribeLineage(
                ResolveRuntimeHostProjectionLineage(artifact));
        }
    }

    private static ProcessArtifactProjectionLineage ResolveRuntimeHostProjectionLineage(
        ProcessArtifactViewModel artifact)
    {
        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
        if (lineage is not null)
        {
            lineage.SourceArtifactId ??= artifact.Id;
            if (string.IsNullOrWhiteSpace(lineage.ProjectionIdentityHash))
            {
                lineage.ProjectionIdentityHash = artifact.ProjectionIdentityHash;
            }

            return lineage;
        }

        return new ProcessArtifactProjectionLineage
        {
            SourceKind = ResolveRuntimeHostProjectionSourceKind(artifact),
            SourceArtifactId = artifact.Id,
            SourceExternalReferenceKey = artifact.ExternalReferenceKey,
            ProjectionIdentityHash = artifact.ProjectionIdentityHash
        };
    }

    private static IReadOnlyList<ProcessArtifactProjectionSourceKind> ResolveRuntimeHostProjectionSourceKinds(
        IReadOnlyList<ProcessArtifactViewModel> stepArtifacts)
    {
        var sourceKinds = stepArtifacts
            .Select(ResolveRuntimeHostProjectionSourceKind)
            .Where(sourceKind => sourceKind != ProcessArtifactProjectionSourceKind.Unknown)
            .DefaultIfEmpty(ProcessArtifactProjectionSourceKind.Manual)
            .Distinct()
            .OrderBy(ResolveRuntimeHostProjectionSourceOrder)
            .ToArray();

        ProcessArtifactProjectionEvidenceDescriptorAdapter.VerifyProjectionSourceOrder(sourceKinds);
        return sourceKinds;
    }

    private static ProcessArtifactProjectionSourceKind ResolveRuntimeHostProjectionSourceKind(
        ProcessArtifactViewModel artifact)
    {
        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
        if (lineage?.SourceKind is { } sourceKind &&
            sourceKind != ProcessArtifactProjectionSourceKind.Unknown)
        {
            return sourceKind;
        }

        return string.IsNullOrWhiteSpace(artifact.ManagedStoragePath)
            ? ProcessArtifactProjectionSourceKind.Manual
            : ProcessArtifactProjectionSourceKind.ExistingManagedFile;
    }

    private static int ResolveRuntimeHostProjectionSourceOrder(ProcessArtifactProjectionSourceKind sourceKind)
    {
        return sourceKind switch
        {
            ProcessArtifactProjectionSourceKind.AgentExecutionArtifact => 10,
            ProcessArtifactProjectionSourceKind.ProcessMock => 20,
            ProcessArtifactProjectionSourceKind.WorkspaceWrite => 30,
            ProcessArtifactProjectionSourceKind.ExistingManagedFile => 40,
            ProcessArtifactProjectionSourceKind.AssistantResponse => 50,
            ProcessArtifactProjectionSourceKind.ProviderNativeBrowser => 60,
            ProcessArtifactProjectionSourceKind.CompletedDecision => 70,
            ProcessArtifactProjectionSourceKind.WorkflowRun => 80,
            ProcessArtifactProjectionSourceKind.WorkflowArtifact => 90,
            ProcessArtifactProjectionSourceKind.SubprocessArtifact => 100,
            ProcessArtifactProjectionSourceKind.Manual => 110,
            _ => int.MaxValue
        };
    }

    private static RuntimeHostReadbackProjectionPayload BuildRuntimeHostProjectionPayload(
        Guid processRunId,
        ProcessStepRunViewModel step,
        IReadOnlyList<ProcessArtifactViewModel> stepArtifacts,
        IReadOnlyList<ProcessArtifactProjectionSourceKind> projectionSourceKinds)
    {
        return new RuntimeHostReadbackProjectionPayload(
            processRunId,
            step.Id,
            step.Sequence,
            step.Title,
            step.Status.ToString(),
            projectionSourceKinds.Select(sourceKind => sourceKind.ToString()).ToArray(),
            stepArtifacts
                .Select(artifact => new RuntimeHostReadbackArtifactPayload(
                    artifact.Id,
                    artifact.ArtifactExpectationId,
                    artifact.ArtifactKind.ToString(),
                    artifact.Title,
                    artifact.TrustStatus.ToString(),
                    artifact.SensitivityLevel.ToString(),
                    artifact.ManagedStoragePath,
                    artifact.ExternalReferenceKey,
                    artifact.ProjectionIdentityHash))
                .ToArray());
    }

    private static RuntimeHostReadbackValidationPayload BuildRuntimeHostValidationPayload(
        Guid processRunId,
        ProcessStepRunViewModel step,
        IReadOnlyList<CoreArtifactExpectationSnapshot> expectedArtifacts,
        IReadOnlyList<CoreArtifactRecordSnapshot> artifactRecords)
    {
        return new RuntimeHostReadbackValidationPayload(
            processRunId,
            step.Id,
            step.ArtifactExpectations
                .Select(expectation => new RuntimeHostReadbackExpectationPayload(
                    expectation.ArtifactExpectationId,
                    expectation.ArtifactKind.ToString(),
                    expectation.Title,
                    expectation.IsRequired,
                    expectation.Status.ToString(),
                    expectation.SourceKind.ToString(),
                    expectation.ValidationStatus?.ToString() ?? string.Empty,
                    expectation.ValidationAttemptedPath,
                    expectation.ValidationSuggestedAction,
                    expectation.Diagnostic))
                .ToArray(),
            expectedArtifacts.Count,
            artifactRecords.Count);
    }

    private static string BuildRuntimeHostValidationRequirementSummary(
        ProcessArtifactExpectationSatisfactionViewModel expectation)
    {
        if (!string.IsNullOrWhiteSpace(expectation.ValidationSuggestedAction))
        {
            return expectation.ValidationSuggestedAction.Trim();
        }

        if (!string.IsNullOrWhiteSpace(expectation.Diagnostic))
        {
            return expectation.Diagnostic.Trim();
        }

        return $"{NormalizeRuntimeHostText(expectation.Title, "Runtime artifact")} must satisfy {expectation.ArtifactKind}.";
    }

    private static ProcessCoreArtifactTrustStatus ToCoreTrustStatus(ProcessArtifactTrustStatus trustStatus)
    {
        return trustStatus switch
        {
            ProcessArtifactTrustStatus.Draft => ProcessCoreArtifactTrustStatus.Draft,
            ProcessArtifactTrustStatus.ReviewRequired => ProcessCoreArtifactTrustStatus.ReviewRequired,
            ProcessArtifactTrustStatus.Approved => ProcessCoreArtifactTrustStatus.Approved,
            ProcessArtifactTrustStatus.Rejected => ProcessCoreArtifactTrustStatus.Rejected,
            ProcessArtifactTrustStatus.TrustedSource => ProcessCoreArtifactTrustStatus.TrustedSource,
            _ => ProcessCoreArtifactTrustStatus.Draft
        };
    }

    private static ProcessCoreSensitivityLevel ToCoreSensitivityLevel(ProcessSensitivityLevel sensitivityLevel)
    {
        return sensitivityLevel switch
        {
            ProcessSensitivityLevel.Public => ProcessCoreSensitivityLevel.Public,
            ProcessSensitivityLevel.Internal => ProcessCoreSensitivityLevel.Internal,
            ProcessSensitivityLevel.Confidential => ProcessCoreSensitivityLevel.Confidential,
            ProcessSensitivityLevel.Restricted => ProcessCoreSensitivityLevel.Restricted,
            _ => ProcessCoreSensitivityLevel.Internal
        };
    }

    private static ProcessRuntimeHostReadbackPanelViewModel MapRuntimeHostReadback(
        ProcessVerificationRuntimeHostStatusDto status,
        ProcessManagerReadOnlyVerificationReadbackDto readback)
    {
        return new ProcessRuntimeHostReadbackPanelViewModel(
            status.Readiness.ToString(),
            status.AuditStoreKind.ToString(),
            status.Enabled,
            status.EmergencyDisabled,
            status.UsesDurableAuditStore,
            status.SupportsAuditRetentionQuery,
            status.Lanes.Count,
            status.Lanes.Count(lane => lane.Enabled),
            status.Capabilities.Count,
            readback.Status.ToString(),
            readback.CapabilityKey,
            readback.Lane.ToString(),
            readback.ProcessRunId,
            readback.StepRunId,
            readback.CallerContext,
            readback.ProjectionMode.ToString(),
            readback.ProjectionSource?.ToString() ?? string.Empty,
            readback.ProjectionAttached,
            readback.AuditRecordId,
            readback.ResponseCount,
            readback.DiagnosticCount,
            readback.EvidenceReferenceCount,
            readback.AuditRecordObservationHash,
            readback.DenialCategory?.ToString() ?? string.Empty,
            readback.DenialCode?.ToString() ?? string.Empty,
            readback.DenialMessage,
            readback.NoMutationPerformed,
            readback.AllowsProcessMutation,
            readback.AllowsTransitionMutation,
            readback.AllowsFinalizerMutation,
            readback.RequestedAt,
            readback.ObservedAt,
            readback.Diagnostics.Select(MapRuntimeHostReadbackDiagnostic).ToArray(),
            readback.AuditRecords.Select(MapRuntimeHostReadbackAuditRecord).ToArray());
    }

    private static ProcessRuntimeHostReadbackDiagnosticViewModel MapRuntimeHostReadbackDiagnostic(
        ProcessManagerReadOnlyVerificationDiagnosticReadbackDto diagnostic)
    {
        return new ProcessRuntimeHostReadbackDiagnosticViewModel(
            diagnostic.Lane?.ToString() ?? string.Empty,
            diagnostic.Severity.ToString(),
            diagnostic.Category.ToString(),
            diagnostic.Message,
            diagnostic.EvidenceReferenceCount,
            diagnostic.ContractVersion.ToString());
    }

    private static ProcessRuntimeHostReadbackAuditRecordViewModel MapRuntimeHostReadbackAuditRecord(
        ProcessManagerReadOnlyVerificationAuditRecordDto record)
    {
        return new ProcessRuntimeHostReadbackAuditRecordViewModel(
            record.Id,
            record.RecordedAt,
            record.Lane.ToString(),
            record.ResponseCount,
            record.AcceptedCount,
            record.DeniedCount,
            record.NoMutationPerformed,
            record.AllowsProcessMutation,
            record.AllowsTransitionMutation,
            record.AllowsFinalizerMutation,
            record.ObservationHash);
    }

    private static string NormalizeRuntimeHostText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private sealed record RuntimeHostReadbackProjectionPayload(
        Guid ProcessRunId,
        Guid StepRunId,
        int StepSequence,
        string StepTitle,
        string StepStatus,
        IReadOnlyList<string> ProjectionSources,
        IReadOnlyList<RuntimeHostReadbackArtifactPayload> Artifacts);

    private sealed record RuntimeHostReadbackArtifactPayload(
        Guid Id,
        Guid? ArtifactExpectationId,
        string ArtifactKind,
        string Title,
        string TrustStatus,
        string SensitivityLevel,
        string ManagedStoragePath,
        string ExternalReferenceKey,
        string ProjectionIdentityHash);

    private sealed record RuntimeHostReadbackValidationPayload(
        Guid ProcessRunId,
        Guid StepRunId,
        IReadOnlyList<RuntimeHostReadbackExpectationPayload> Expectations,
        int ExpectedArtifactCount,
        int ArtifactRecordCount);

    private sealed record RuntimeHostReadbackExpectationPayload(
        Guid ArtifactExpectationId,
        string ArtifactKind,
        string Title,
        bool IsRequired,
        string SatisfactionStatus,
        string SourceKind,
        string ValidationStatus,
        string ValidationAttemptedPath,
        string ValidationSuggestedAction,
        string Diagnostic);
}
