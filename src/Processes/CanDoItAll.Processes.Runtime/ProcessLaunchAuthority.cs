using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public readonly record struct ProcessLaunchAdmissionId {
    [JsonConstructor]
    public ProcessLaunchAdmissionId(Guid value) {
        if (value == Guid.Empty) {
            throw new ArgumentException("A process launch admission identifier is required.", nameof(value));
        }
        Value = value;
    }
    public Guid Value { get; }
}

public readonly record struct ProcessLaunchIntentId {
    [JsonConstructor]
    public ProcessLaunchIntentId(Guid value) {
        if (value == Guid.Empty) {
            throw new ArgumentException("A process launch intent identifier is required.", nameof(value));
        }
        Value = value;
    }
    public Guid Value { get; }
}

public enum ProcessLaunchOperatorSurface {
    UserInterface,
    Api
}

public enum ProcessLaunchSourceScopeKind {
    Sandbox,
    Project,
    Organization
}

public enum ProcessLaunchAgentOperation {
    Unspecified,
    StructureStart,
    SubprocessLaunch
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$source")]
[JsonDerivedType(typeof(ProcessLaunchPrincipal.LocalOperator), "local-operator")]
[JsonDerivedType(typeof(ProcessLaunchPrincipal.AuthenticatedOperator), "authenticated-operator")]
[JsonDerivedType(typeof(ProcessLaunchPrincipal.AgentExecution), "agent-execution")]
public abstract record ProcessLaunchPrincipal {
    public sealed record LocalOperator(ProcessLaunchOperatorSurface Surface) : ProcessLaunchPrincipal;
    public sealed record AuthenticatedOperator(string SubjectId, DateTimeOffset ExpiresAtUtc) : ProcessLaunchPrincipal;
    public sealed record AgentExecution(ProcessLaunchAgentCeiling Ceiling,
        ProcessLaunchAgentOperation Operation = ProcessLaunchAgentOperation.Unspecified) : ProcessLaunchPrincipal;
}

public sealed record ProcessLaunchAgentCeiling(
    Guid AuthorityId,
    Guid AgentId,
    long DatabaseProfileGeneration,
    ProcessLaunchSourceScopeKind WorkspaceScopeKind,
    string WorkspaceScopeKey,
    bool ReadAllowed,
    bool MutationAllowed,
    string PolicyVersion,
    string PolicyFingerprint,
    IReadOnlyList<string> AllowedOperations,
    IReadOnlyList<string> AllowedCapabilityKeys,
    IReadOnlyList<string> WritableExternalTargetAliases,
    IReadOnlyList<string> ReadOnlyExternalTargetAliases,
    IReadOnlyList<string> AllowedManagedArtifactReadRefs);

public sealed record ProcessLaunchAuthority(
    ProcessLaunchPrincipal Principal,
    Guid DatabaseProfileId,
    ProcessProjectAdmission? ProjectAdmission,
    bool CanCreateTasks,
    bool CanCreateAssets,
    string PolicyFingerprint) {
    public void Validate() {
        if (DatabaseProfileId == Guid.Empty || string.IsNullOrWhiteSpace(PolicyFingerprint) || PolicyFingerprint.Length > 256 ||
                ProjectAdmission is { } admission && admission.DatabaseProfileId != DatabaseProfileId) {
            throw new InvalidOperationException("The saved process launch authority has an invalid profile or policy binding.");
        }
        switch (Principal) {
            case ProcessLaunchPrincipal.LocalOperator local when Enum.IsDefined(local.Surface):
                break;
            case ProcessLaunchPrincipal.AuthenticatedOperator authenticated when
                    !string.IsNullOrWhiteSpace(authenticated.SubjectId) && authenticated.SubjectId.Length <= 256 &&
                    authenticated.ExpiresAtUtc.Offset == TimeSpan.Zero:
                break;
            case ProcessLaunchPrincipal.AgentExecution { Ceiling: { } ceiling } source:
                if (!Enum.IsDefined(source.Operation) || ceiling.AuthorityId == Guid.Empty || ceiling.AgentId == Guid.Empty ||
                        !Enum.IsDefined(ceiling.WorkspaceScopeKind) || ceiling.DatabaseProfileGeneration < 0 ||
                        !ceiling.ReadAllowed ||
                        string.IsNullOrWhiteSpace(ceiling.PolicyVersion) || string.IsNullOrWhiteSpace(ceiling.PolicyFingerprint) ||
                        !ValidEntries(ceiling.AllowedOperations) || !ValidEntries(ceiling.AllowedCapabilityKeys) ||
                        !ValidEntries(ceiling.WritableExternalTargetAliases) || !ValidEntries(ceiling.ReadOnlyExternalTargetAliases) ||
                        ceiling.AllowedManagedArtifactReadRefs is null || ceiling.AllowedManagedArtifactReadRefs.Any(string.IsNullOrWhiteSpace) ||
                        (CanCreateTasks || CanCreateAssets) && !ceiling.MutationAllowed ||
                        ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Project &&
                            (ProjectAdmission is null || !Guid.TryParse(ceiling.WorkspaceScopeKey, out var sourceProject) ||
                                sourceProject != ProjectAdmission.ProjectId)) {
                    throw new InvalidOperationException("The saved process agent authority has an invalid or wider grant ceiling.");
                }
                break;
            default:
                throw new InvalidOperationException("The process launch has no supported trusted authority channel.");
        }
    }

    private static bool ValidEntries(IReadOnlyList<string>? entries)
        => entries is { Count: <= 500 } && entries.All(entry => !string.IsNullOrWhiteSpace(entry) && entry.Length <= 200);
}

public sealed record ProcessLaunchAdmissionReference(
    ProcessLaunchAdmissionId AdmissionId,
    string PreparationFingerprint,
    bool Execute) {
    [JsonIgnore]
    public ProcessLaunchAuthority? CurrentCallerAuthority { get; init; }
}

public sealed record ProcessExecutionProjectAuthority(
    ProcessLaunchAdmissionId AdmissionId,
    string PreparationFingerprint,
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    string ReadinessHash);



public sealed class ProcessLaunchAuthorityRejectedException(string message) : InvalidOperationException(message);
