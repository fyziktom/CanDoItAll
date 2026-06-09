using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Core.Diagnostics;
using CanDoItAll.Processes.Core.Execution;
using CanDoItAll.Processes.Core.Finalization;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.OfficeEvidence;
using CoreArtifactExpectationSnapshot = CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot;
using CoreArtifactRecordSnapshot = CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessReadOnlyVerificationPayloadBuilder
{
    public static ProcessTranscriptVerificationReadOnlyEvidencePayload CreateTranscriptPayload(
        ProcessTranscriptVerificationPayloadFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var identity = NormalizeIdentity(facts.Identity);
        var transcriptText = RequireContent(facts.TranscriptText, nameof(facts.TranscriptText));
        var transcriptUri = RequireText(facts.TranscriptUri, nameof(facts.TranscriptUri));
        var transcriptReference = new ProcessDriverTranscriptReference(
            transcriptUri,
            ProcessDriverEvidencePolicy.ComputeSha256(transcriptText),
            facts.Language,
            RequireText(facts.ToolchainName, nameof(facts.ToolchainName)),
            RequireText(facts.TargetFramework, nameof(facts.TargetFramework)));
        var evidenceReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            transcriptUri,
            transcriptText,
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);

        return new ProcessTranscriptVerificationReadOnlyEvidencePayload(
            identity.ProcessRunId,
            identity.StepRunId,
            identity.ArtifactId,
            identity.CallerContext,
            ProcessDriverPermissionMode.VerificationOnly,
            CreateScope(
                ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
                ProcessDriverPermissionMode.VerificationOnly),
            transcriptReference,
            transcriptText,
            [evidenceReference],
            ProcessReadOnlyVerificationOperationPolicy.Normalize(
                facts.RequestedOperations,
                ProcessReadOnlyVerificationOperationPolicy.TranscriptVerificationDefaults),
            identity.RequestedAt);
    }

    public static ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload CreateRuntimeEvidencePayload(
        ProcessRuntimeEvidenceVerificationPayloadFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var identity = NormalizeIdentity(facts.Identity);
        var projectionSourceOrder = CreateReadonlyList(facts.ProjectionSourceOrder);
        var descriptorPayload = CreateRuntimeDescriptorPayloadMaterial(
            facts.ExecutionEvidence,
            facts.FinalizerEvidence,
            facts.RetryDiagnostic,
            facts.NoProgressDiagnostic,
            facts.ProviderRepairDiagnostic,
            projectionSourceOrder);
        var evidenceReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            RequireText(facts.EvidenceUri, nameof(facts.EvidenceUri)),
            descriptorPayload,
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);

        return new ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload(
            identity.ProcessRunId,
            identity.StepRunId,
            identity.ArtifactId,
            identity.CallerContext,
            ProcessDriverPermissionMode.ManagerReadonly,
            CreateScope(
                ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
                ProcessDriverPermissionMode.ManagerReadonly),
            [evidenceReference],
            ProcessReadOnlyVerificationOperationPolicy.Normalize(
                facts.RequestedOperations,
                ProcessReadOnlyVerificationOperationPolicy.RuntimeEvidenceDefaults),
            facts.ExecutionEvidence,
            facts.FinalizerEvidence,
            facts.RetryDiagnostic,
            facts.NoProgressDiagnostic,
            facts.ProviderRepairDiagnostic,
            projectionSourceOrder,
            identity.RequestedAt);
    }

    public static ProcessArtifactEvidenceReadOnlyPayload CreateArtifactEvidencePayload(
        ProcessArtifactEvidencePayloadFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var identity = NormalizeIdentity(facts.Identity);
        var projectionPayload = RequireContent(facts.ProjectionEvidencePayload, nameof(facts.ProjectionEvidencePayload));
        var validationPayload = RequireContent(facts.ValidationEvidencePayload, nameof(facts.ValidationEvidencePayload));
        var projectionReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            RequireText(facts.ProjectionEvidenceUri, nameof(facts.ProjectionEvidenceUri)),
            projectionPayload,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
        var validationReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            RequireText(facts.ValidationEvidenceUri, nameof(facts.ValidationEvidenceUri)),
            validationPayload,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);

        return new ProcessArtifactEvidenceReadOnlyPayload(
            identity.ProcessRunId,
            identity.StepRunId,
            identity.ArtifactId,
            identity.CallerContext,
            ProcessDriverPermissionMode.VerificationOnly,
            CreateScope(
                ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [projectionReference, validationReference],
            ProcessReadOnlyVerificationOperationPolicy.Normalize(
                facts.RequestedOperations,
                ProcessReadOnlyVerificationOperationPolicy.ArtifactEvidenceDefaults),
            ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
                projectionReference,
                projectionPayload),
            CreateReadonlyList(facts.ProjectionLineage),
            CreateReadonlyList(facts.ProjectionSourceOrder),
            CreateReadonlyList(facts.ProviderNativeBrowserEvidence),
            CreateReadonlyList(facts.ValidationRequirements),
            CreateReadonlyList(facts.ExpectedArtifacts),
            CreateReadonlyList(facts.ArtifactRecords),
            identity.RequestedAt);
    }

    public static ProcessOfficeEvidenceReadOnlyPayload CreateOfficeEvidencePayload(
        ProcessOfficeEvidencePayloadFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var identity = NormalizeIdentity(facts.Identity);
        var payload = RequireContent(facts.EvidencePayload, nameof(facts.EvidencePayload));
        var evidenceReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
            RequireText(facts.EvidenceUri, nameof(facts.EvidenceUri)),
            payload,
            coreDescriptorFamily: null);

        return new ProcessOfficeEvidenceReadOnlyPayload(
            identity.ProcessRunId,
            identity.StepRunId,
            identity.ArtifactId,
            identity.CallerContext,
            ProcessDriverPermissionMode.VerificationOnly,
            CreateScope(
                ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            ProcessReadOnlyVerificationOperationPolicy.Normalize(
                facts.RequestedOperations,
                ProcessReadOnlyVerificationOperationPolicy.OfficeEvidenceDefaults),
            ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload(
                evidenceReference,
                payload),
            CreateReadonlyList(facts.Items),
            identity.RequestedAt);
    }

    public static ProcessBusinessAnalysisReadOnlyPayload CreateBusinessAnalysisPayload(
        ProcessBusinessAnalysisPayloadFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var identity = NormalizeIdentity(facts.Identity);
        var payload = RequireContent(facts.EvidencePayload, nameof(facts.EvidencePayload));
        var evidenceReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
            RequireText(facts.EvidenceUri, nameof(facts.EvidenceUri)),
            payload,
            coreDescriptorFamily: null);

        return new ProcessBusinessAnalysisReadOnlyPayload(
            identity.ProcessRunId,
            identity.StepRunId,
            identity.ArtifactId,
            identity.CallerContext,
            ProcessDriverPermissionMode.VerificationOnly,
            CreateScope(
                ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            ProcessReadOnlyVerificationOperationPolicy.Normalize(
                facts.RequestedOperations,
                ProcessReadOnlyVerificationOperationPolicy.BusinessAnalysisDefaults),
            ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload(
                evidenceReference,
                payload),
            CreateReadonlyList(facts.Items),
            identity.RequestedAt);
    }

    private static string CreateRuntimeDescriptorPayloadMaterial(
        ProcessExecutionEvidenceDescriptor? executionEvidence,
        ProcessFinalizerEvidenceDescriptor? finalizerEvidence,
        ProcessRetryDiagnosticDescriptor? retryDiagnostic,
        ProcessNoProgressRetryDiagnosticDescriptor? noProgressDiagnostic,
        ProcessProviderRepairDiagnosticDescriptor? providerRepairDiagnostic,
        IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor> projectionSourceOrder)
    {
        return string.Join(
            "|",
            executionEvidence?.Run.ExecutionRunId,
            finalizerEvidence?.Intent.ProcessRunId,
            retryDiagnostic?.AttemptNumber,
            noProgressDiagnostic?.Fingerprint,
            providerRepairDiagnostic?.FailureSummary,
            projectionSourceOrder.Count);
    }

    private static ProcessDriverCapabilityScope CreateScope(
        ProcessDriverCapabilityScopeKind kind,
        ProcessDriverPermissionMode permissionMode)
    {
        return new ProcessDriverCapabilityScope(
            kind,
            permissionMode,
            AllowsProcessMutation: false,
            AllowsExternalCalls: false,
            AllowsWorkspaceWrites: false,
            AllowsStorageWrites: false);
    }

    private static ProcessDriverEvidenceReference CreateEvidenceReference(
        ProcessDriverEvidenceReferenceKind kind,
        string uri,
        string content,
        ProcessDriverCoreDescriptorFamily? coreDescriptorFamily)
    {
        return new ProcessDriverEvidenceReference(
            kind,
            uri,
            ProcessDriverEvidencePolicy.ComputeSha256(content),
            coreDescriptorFamily);
    }

    private static ProcessReadOnlyPayloadIdentity NormalizeIdentity(ProcessReadOnlyPayloadIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return identity with
        {
            CallerContext = RequireText(identity.CallerContext, nameof(identity.CallerContext))
        };
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string RequireContent(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Supplied evidence content is required.", parameterName);
        }

        return value;
    }

    private static IReadOnlyList<T> CreateReadonlyList<T>(IReadOnlyList<T>? values)
    {
        return Array.AsReadOnly((values ?? []).ToArray());
    }
}

internal sealed record ProcessReadOnlyPayloadIdentity(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    string CallerContext,
    DateTimeOffset RequestedAt);

internal sealed record ProcessTranscriptVerificationPayloadFacts(
    ProcessReadOnlyPayloadIdentity Identity,
    ProcessDriverTranscriptLanguage Language,
    string ToolchainName,
    string TargetFramework,
    string TranscriptUri,
    string TranscriptText,
    IReadOnlyList<ProcessDriverOperation>? RequestedOperations = null);

internal sealed record ProcessRuntimeEvidenceVerificationPayloadFacts(
    ProcessReadOnlyPayloadIdentity Identity,
    string EvidenceUri,
    ProcessExecutionEvidenceDescriptor? ExecutionEvidence,
    ProcessFinalizerEvidenceDescriptor? FinalizerEvidence,
    ProcessRetryDiagnosticDescriptor? RetryDiagnostic,
    ProcessNoProgressRetryDiagnosticDescriptor? NoProgressDiagnostic,
    ProcessProviderRepairDiagnosticDescriptor? ProviderRepairDiagnostic,
    IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor>? ProjectionSourceOrder,
    IReadOnlyList<ProcessDriverOperation>? RequestedOperations = null);

internal sealed record ProcessArtifactEvidencePayloadFacts(
    ProcessReadOnlyPayloadIdentity Identity,
    string ProjectionEvidenceUri,
    string ProjectionEvidencePayload,
    string ValidationEvidenceUri,
    string ValidationEvidencePayload,
    IReadOnlyList<ProcessArtifactProjectionLineageDescriptor>? ProjectionLineage,
    IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor>? ProjectionSourceOrder,
    IReadOnlyList<ProcessProviderNativeBrowserEvidenceDescriptor>? ProviderNativeBrowserEvidence,
    IReadOnlyList<ProcessArtifactValidationRequirementDescriptor>? ValidationRequirements,
    IReadOnlyList<CoreArtifactExpectationSnapshot>? ExpectedArtifacts,
    IReadOnlyList<CoreArtifactRecordSnapshot>? ArtifactRecords,
    IReadOnlyList<ProcessDriverOperation>? RequestedOperations = null);

internal sealed record ProcessOfficeEvidencePayloadFacts(
    ProcessReadOnlyPayloadIdentity Identity,
    string EvidenceUri,
    string EvidencePayload,
    IReadOnlyList<OfficeEvidenceItem>? Items,
    IReadOnlyList<ProcessDriverOperation>? RequestedOperations = null);

internal sealed record ProcessBusinessAnalysisPayloadFacts(
    ProcessReadOnlyPayloadIdentity Identity,
    string EvidenceUri,
    string EvidencePayload,
    IReadOnlyList<BusinessAnalysisEvidenceItem>? Items,
    IReadOnlyList<ProcessDriverOperation>? RequestedOperations = null);
