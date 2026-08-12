using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Drivers.Abstractions;

public interface IProcessStrategyFactory
{
    ProcessStrategyDescriptor Descriptor { get; }

    ValueTask<IProcessStrategy> CreateAsync(
        ProcessStrategyBindingSnapshot binding,
        CancellationToken cancellationToken = default);
}

public interface IProcessStrategy
{
    ValueTask<StrategyResultEnvelope> ExecuteAsync(
        ProcessStrategyExecutionContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessStrategyBindingSnapshot(
    DriverId DriverId,
    StrategyId StrategyId,
    string StrategyVersion,
    string FactoryVersion,
    string MinRuntimeSchema,
    string MaxRuntimeSchema,
    string BindingInputsHash,
    IReadOnlyList<StrategyBindingInput> Inputs)
{
    public ProcessHostProfileId HostProfileId { get; init; } = new("unknown");

    public IReadOnlyList<ProcessHostCapabilityFact> HostCapabilities { get; init; } = [];
}

public sealed record StrategyBindingInput(
    StrategyBindingInputKey Key,
    string ValueHash);

public sealed record ProcessStrategyExecutionContext
{
    public ProcessStrategyExecutionContext(
        ProcessRunId runId,
        ProcessStepInstanceId? stepId,
        ProcessStrategyBindingSnapshot binding,
        IReadOnlyList<StrategyBindingInput> inputs,
        ProcessStepExecutionContract? stepContract = null)
    {
        RunId = runId;
        StepId = stepId;
        Binding = binding;
        Inputs = inputs;
        StepContract = stepContract ?? ProcessStepExecutionContract.Empty;
    }

    public ProcessRunId RunId { get; init; }

    public ProcessStepInstanceId? StepId { get; init; }

    public ProcessStrategyBindingSnapshot Binding { get; init; }

    public IReadOnlyList<StrategyBindingInput> Inputs { get; init; }

    public ProcessStepExecutionContract StepContract { get; init; }

    public required ProcessDispatchClaimIdentity DispatchClaimIdentity { get; init; }

    public ProcessHostCapabilityEvaluationEvidence? DispatchHostCapabilityEvidence { get; init; }
}

public readonly record struct ProcessDispatchClaimIdentity
{
    public ProcessDispatchClaimIdentity(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A dispatch claim identity must be non-empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }
}

public sealed record ProcessStepExecutionContract(
    IReadOnlyList<RequiredArtifactInputRef> RequiredArtifacts,
    IReadOnlyList<ExpectedProducedArtifactRef> ExpectedProducedArtifacts,
    IReadOnlyList<string> RequiredRuntimeToolNames,
    string ContractHash)
{
    public static ProcessStepExecutionContract Empty { get; } = new([], [], [], "sha256:empty-step-contract");

    public IReadOnlyList<ProcessArtifactSlotDescriptor> ArtifactDescriptors { get; init; } = [];

    public IReadOnlyList<SubprocessArtifactMappingDescriptor> SubprocessArtifactMappings { get; init; } = [];

    public IReadOnlyList<BranchOutcomeId> ConfiguredBranchOutcomeIds { get; init; } = [];

    public IReadOnlySet<ProcessHostCapabilityId> RequiredHostCapabilities { get; init; } =
        new HashSet<ProcessHostCapabilityId>();
}

public sealed record RequiredArtifactInputRef(
    ArtifactSlotId SlotId,
    ProcessArtifactInputAvailability Availability,
    ProcessStepInstanceId? ProducerStepId,
    ArtifactInstanceId? ArtifactId,
    string ContentHash,
    string ConnectionHash);

public sealed record ExpectedProducedArtifactRef(
    ArtifactSlotId SlotId);

public sealed record ProcessArtifactSlotDescriptor(
    ArtifactSlotId SlotId,
    string SlotKey,
    string StepKey,
    string ArtifactExpectationKey,
    string ArtifactTitle,
    string ArtifactKind,
    string PrimaryManagedRef,
    ProcessArtifactMaterializationMode MaterializationMode)
{
    public string PayloadSchema { get; init; } = string.Empty;
}

public sealed record SubprocessArtifactMappingDescriptor(
    ArtifactSlotId ParentSlotId,
    string ParentArtifactExpectationKey,
    string ChildProcessDefinitionKey,
    IReadOnlyList<SubprocessChildArtifactMappingDescriptor> AcceptedChildOutputs,
    IReadOnlyList<SubprocessChildArtifactMappingDescriptor> NoGoChildOutputs);

public sealed record SubprocessChildArtifactMappingDescriptor(
    string StepKey,
    string ArtifactExpectationKey,
    string ArtifactTitle,
    string BranchOutcomeKey);

public enum ProcessArtifactMaterializationMode
{
    AgentWritten,
    RuntimeSynthesizedParentHandoff,
    RecoveredExistingProof,
    RuntimeDiagnosticOnly
}

public enum ProcessArtifactInputAvailability
{
    Expected,
    Available,
    Missing
}

public sealed record StrategyResultEnvelope(
    StrategyId StrategyId,
    string StrategyVersion,
    Guid IdempotencyKey,
    StrategyOutcome Outcome,
    IReadOnlyList<ProducedArtifactRef> ProducedArtifacts,
    IReadOnlyList<RequestedArtifactRef> RequestedArtifacts,
    IReadOnlyList<StrategyDiagnosticRef> Diagnostics,
    IReadOnlyList<ManagerSignal> ManagerSignals,
    string ResultHash)
{
    public string UserSafeSummary { get; init; } = string.Empty;

    public ProcessExecutionRunId? ExecutionRunId { get; init; }

    public ProcessHostCapabilityEvaluationEvidence? HostCapabilityEvidence { get; init; }
}

public static class ProcessStrategyResultLimits
{
    public const int MaximumArtifacts = 128;
    public const int MaximumDiagnostics = 32;
    public const int MaximumManagerSignals = 32;
    public const int MaximumIdentifierLength = 128;
    public const int MaximumHashLength = 71;
    public const int MaximumUserSafeSummaryLength = 4096;
    public const int MaximumDiagnosticSummaryLength = 2048;
    public const int MaximumRestrictedEvidenceReferenceLength = 1024;
    public const int MaximumManagerSignalSummaryLength = 2048;
}

public static class ProcessStrategyReceiptValuePolicy
{
    private const string Sha256Prefix = "sha256:";
    private const string RestrictedReferencePrefix = "restricted://";
    private const int Sha256HexLength = 64;
    private const int RestrictedReferenceKindMaximumLength = 64;

    public static bool IsSha256Digest(string? value)
        => IsLowerHexSha256(value);

    public static bool IsStableIdentifier(string? value)
        => IsStableToken(value, allowColon: true, allowPlus: false);

    public static bool IsStableVersion(string? value)
        => IsStableToken(value, allowColon: false, allowPlus: true);

    public static bool IsRestrictedEvidenceReference(string? value)
    {
        if (value is null)
        {
            return true;
        }

        if (!value.StartsWith(RestrictedReferencePrefix, StringComparison.Ordinal) ||
            value.Length > ProcessStrategyResultLimits.MaximumRestrictedEvidenceReferenceLength)
        {
            return false;
        }

        var payload = value.AsSpan(RestrictedReferencePrefix.Length);
        var separatorIndex = payload.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex > RestrictedReferenceKindMaximumLength)
        {
            return false;
        }

        var kind = payload[..separatorIndex];
        var identity = payload[(separatorIndex + 1)..];
        return IsLowerToken(kind) &&
               (IsLowerHex(identity, 32) || IsSha256Digest(identity.ToString()));
    }

    private static bool IsLowerHexSha256(string? value)
        => value is not null &&
           value.Length == Sha256Prefix.Length + Sha256HexLength &&
           value.StartsWith(Sha256Prefix, StringComparison.Ordinal) &&
           IsLowerHex(value.AsSpan(Sha256Prefix.Length), Sha256HexLength);

    private static bool IsLowerHex(ReadOnlySpan<char> value, int requiredLength)
    {
        if (value.Length != requiredLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLowerToken(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value[0] is not (>= 'a' and <= 'z'))
        {
            return false;
        }

        foreach (var character in value)
        {
            if ((character < 'a' || character > 'z') &&
                (character < '0' || character > '9') &&
                character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStableToken(string? value, bool allowColon, bool allowPlus)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > ProcessStrategyResultLimits.MaximumIdentifierLength ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) &&
                character is not '.' and not '_' and not '-' &&
                (!allowColon || character != ':') &&
                (!allowPlus || character != '+'))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record ProducedArtifactRef(
    ArtifactInstanceId ArtifactId,
    ArtifactSlotId SlotId,
    string ContentHash);

public sealed record RequestedArtifactRef(
    ArtifactSlotId SlotId,
    string RequestHash);

public sealed record StrategyDiagnosticRef(
    StrategyDiagnosticCode Code,
    StrategyDiagnosticSensitivity Sensitivity,
    string EvidenceHash,
    string SafeSummary,
    string? RestrictedEvidenceReference = null,
    ProcessDiagnosticRetrySafety RetrySafety = ProcessDiagnosticRetrySafety.Unknown,
    ProcessDiagnosticIdempotencyClassification Idempotency = ProcessDiagnosticIdempotencyClassification.Unknown)
{
    public ProcessRunId? RelatedChildRunId { get; init; }

    public ProcessExecutionSafetyAttestation? ExecutionSafetyAttestation { get; init; }
}

public sealed record ManagerSignal(
    ManagerSignalCode Code,
    string SignalHash,
    string SafeSummary);

public enum StrategyOutcome
{
    Succeeded,
    Failed,
    Waiting,
    NeedsManager,
    Canceled
}

public enum StrategyDiagnosticSensitivity
{
    Normal,
    Restricted
}

public enum ProcessDiagnosticRetrySafety
{
    Unknown,
    SafeToRetry,
    UnsafeToRetry
}

public enum ProcessDiagnosticIdempotencyClassification
{
    Unknown,
    Idempotent,
    NonIdempotent
}
