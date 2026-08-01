using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Drivers.Abstractions;

public interface IProcessExecutionAdapter
{
    ProcessExecutionAdapterDescriptor Descriptor { get; }

    ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessExecutionAdapterDescriptor(
    ProcessExecutionAdapterId AdapterId,
    ProcessExecutionAdapterKind Kind,
    string AdapterVersion,
    ProcessStrategyDescriptor Strategy,
    IReadOnlySet<CapabilityTag> CapabilityTags);

public sealed record ProcessExecutionAdapterRequest(
    ProcessRunId RunId,
    ProcessStepInstanceId? StepId,
    ProcessExecutionAdapterKind Kind,
    ProcessExecutionAdapterOperationKey OperationKey,
    ProcessStrategyBindingSnapshot Binding,
    IReadOnlyList<StrategyBindingInput> Inputs,
    IReadOnlyList<ProcessExecutionContextFacet> ContextFacets)
{
    public ProcessStepExecutionContract StepContract { get; init; } = ProcessStepExecutionContract.Empty;

    public required ProcessDispatchClaimIdentity DispatchClaimIdentity { get; init; }
}

public sealed record ProcessExecutionContextFacet(
    ProcessExecutionContextFacetKey Key,
    string ValueHash,
    StrategyDiagnosticSensitivity Sensitivity);

public sealed record ProcessExecutionAdapterResult(
    StrategyOutcome Outcome,
    IReadOnlyList<ProducedArtifactRef> ProducedArtifacts,
    IReadOnlyList<RequestedArtifactRef> RequestedArtifacts,
    IReadOnlyList<ProcessExecutionAdapterDiagnostic> Diagnostics,
    IReadOnlyList<ManagerSignal> ManagerSignals,
    string UserSafeSummary,
    string ResultHash)
{
    public ProcessExecutionRunId? ExecutionRunId { get; init; }

    public static ProcessExecutionAdapterResult Succeeded(
        string userSafeSummary,
        string resultHash)
    {
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.Succeeded,
            [],
            [],
            [],
            [],
            userSafeSummary,
            resultHash);
    }
}

public sealed record ProcessExecutionAdapterDiagnostic(
    StrategyDiagnosticCode Code,
    StrategyDiagnosticSensitivity Sensitivity,
    string EvidenceHash,
    string SafeSummary,
    string? RestrictedEvidenceReference,
    ProcessDiagnosticRetrySafety RetrySafety,
    ProcessDiagnosticIdempotencyClassification Idempotency)
{
    public ProcessRunId? RelatedChildRunId { get; init; }

    public ProcessExecutionSafetyAttestation? ExecutionSafetyAttestation { get; init; }
}

public enum ProcessExecutionSafetyAttestationKind
{
    None = 0,
    FailedBeforeRecordedSideEffects = 1
}

public enum ProcessExecutionSafetyAttestor
{
    None = 0,
    AgentFrameworkExecutionLedger = 1
}

public sealed record ProcessExecutionSafetyAttestation(
    ProcessExecutionSafetyAttestationKind Kind,
    ProcessExecutionSafetyAttestor Attestor,
    int SchemaVersion,
    ProcessExecutionRunId ExecutionRunId,
    ProcessRunId ProcessRunId,
    ProcessStepInstanceId StepInstanceId,
    ProcessExecutionExecutorId ExecutorId,
    string DurableEvidenceDigest,
    string EvidenceHash)
{
    public const int CurrentSchemaVersion = 1;

    public static ProcessExecutionSafetyAttestation FailedBeforeRecordedSideEffects(
        ProcessExecutionRunId executionRunId,
        ProcessRunId processRunId,
        ProcessStepInstanceId stepInstanceId,
        ProcessExecutionExecutorId executorId,
        string durableEvidenceDigest)
    {
        if (!IsSha256Digest(durableEvidenceDigest))
        {
            throw new ArgumentException(
                "Durable execution-safety evidence digest must be a lowercase sha256 digest.",
                nameof(durableEvidenceDigest));
        }

        return new ProcessExecutionSafetyAttestation(
            ProcessExecutionSafetyAttestationKind.FailedBeforeRecordedSideEffects,
            ProcessExecutionSafetyAttestor.AgentFrameworkExecutionLedger,
            CurrentSchemaVersion,
            executionRunId,
            processRunId,
            stepInstanceId,
            executorId,
            durableEvidenceDigest,
            ComputeEvidenceHash(
                ProcessExecutionSafetyAttestationKind.FailedBeforeRecordedSideEffects,
                ProcessExecutionSafetyAttestor.AgentFrameworkExecutionLedger,
                CurrentSchemaVersion,
                executionRunId,
                processRunId,
                stepInstanceId,
                executorId,
                durableEvidenceDigest));
    }

    public bool IsStructurallyValid()
    {
        return Kind == ProcessExecutionSafetyAttestationKind.FailedBeforeRecordedSideEffects &&
               Attestor == ProcessExecutionSafetyAttestor.AgentFrameworkExecutionLedger &&
               SchemaVersion == CurrentSchemaVersion &&
               ExecutionRunId.Value != Guid.Empty &&
               ProcessRunId.Value != Guid.Empty &&
               StepInstanceId.Value != Guid.Empty &&
               ExecutorId.Value != Guid.Empty &&
               IsSha256Digest(DurableEvidenceDigest) &&
               string.Equals(
                   EvidenceHash,
                   ComputeEvidenceHash(
                       Kind,
                       Attestor,
                       SchemaVersion,
                       ExecutionRunId,
                       ProcessRunId,
                       StepInstanceId,
                       ExecutorId,
                       DurableEvidenceDigest),
                   StringComparison.Ordinal);
    }

    private static string ComputeEvidenceHash(
        ProcessExecutionSafetyAttestationKind kind,
        ProcessExecutionSafetyAttestor attestor,
        int schemaVersion,
        ProcessExecutionRunId executionRunId,
        ProcessRunId processRunId,
        ProcessStepInstanceId stepInstanceId,
        ProcessExecutionExecutorId executorId,
        string durableEvidenceDigest)
    {
        var evidence = string.Join(
            ':',
            "process-execution-safety-attestation",
            schemaVersion,
            kind,
            attestor,
            executionRunId.Value.ToString("D"),
            processRunId.Value.ToString("D"),
            stepInstanceId.Value.ToString("D"),
            executorId.Value.ToString("D"),
            durableEvidenceDigest);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(evidence));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool IsSha256Digest(string? value)
    {
        return value is { Length: 71 } &&
               value.StartsWith("sha256:", StringComparison.Ordinal) &&
               value.AsSpan(7).IndexOfAnyExcept("0123456789abcdef") < 0;
    }
}

public enum ProcessExecutionAdapterKind
{
    Workflow,
    SingleAgent,
    AgentGroup,
    Handoff,
    SchedulerTrigger,
    ScopedContext,
    Plugin
}
