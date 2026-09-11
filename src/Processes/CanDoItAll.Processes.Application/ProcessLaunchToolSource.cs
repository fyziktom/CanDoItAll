using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessLaunchToolSource(
    int SchemaVersion,
    ProcessExecutionDispatchAuthority Execution,
    string SourceKind,
    string SourceId,
    string ExecutionFingerprint,
    ProcessLaunchIntentId IntentId,
    string ProposalFingerprint) {
    public const int CurrentSchemaVersion = 1;

    [JsonIgnore]
    public string SemanticFingerprint => ProcessLaunchIntentFingerprint.Hash(JsonSerializer.Serialize(new {
        SchemaVersion, SourceKind, SourceId, ExecutionFingerprint, Execution.Evidence.ExecutionRunId,
        Execution.Evidence.ExecutorAgentId, Execution.Evidence.RunId, Execution.Evidence.StepInstanceId,
        Execution.Evidence.DispatchClaimToken, Execution.Evidence.StepKey, Execution.Evidence.ExecutionCreatedAtUtc,
        Execution.OwnerFingerprint, IntentId, ProposalFingerprint
    }));

    public void Validate() {
        if (SchemaVersion != CurrentSchemaVersion || Execution is null || Execution.Evidence is null ||
                Execution.Evidence.ExecutionRunId == Guid.Empty || Execution.Evidence.ExecutorAgentId == Guid.Empty ||
                Execution.Evidence.RunId.Value == Guid.Empty || Execution.Evidence.StepInstanceId.Value == Guid.Empty ||
                Execution.Evidence.DispatchClaimToken == Guid.Empty || Execution.RootRunId.Value == Guid.Empty ||
                Execution.ProjectReference is null || Execution.SourceAuthority?.ProjectAdmission is not { } project ||
                Execution.ProjectId != project.ProjectId || Execution.ProjectReference.RunId != Execution.Evidence.RunId ||
                Execution.ProjectReference.StepInstanceId != Execution.Evidence.StepInstanceId ||
                string.IsNullOrWhiteSpace(SourceKind) || SourceKind.Length > 128 || string.IsNullOrWhiteSpace(SourceId) ||
                SourceId != Execution.Evidence.StepKey ||
                !IsDigest(ExecutionFingerprint) || !IsDigest(ProposalFingerprint) || IntentId.Value == Guid.Empty ||
                Execution.OwnerFingerprint is not { Length: 71 } owner || !owner.StartsWith("sha256:", StringComparison.Ordinal) ||
                !IsDigest(owner[7..])) {
            throw new InvalidOperationException("The prepared Process tool launch has an invalid source, claim or proposal binding.");
        }
        Execution.SourceAuthority.Validate();
    }

    public void RequirePreparation(ProcessPreparedLaunch preparation) {
        Validate();
        if (preparation.CallerIntentId != IntentId || preparation.Request.CallerIntentId != IntentId ||
                preparation.Request.ProducerInputFingerprint != ProposalFingerprint ||
                preparation.InitialCommit.Mutation.State.ProjectAdmission != Execution.SourceAuthority!.ProjectAdmission ||
                preparation.Request.ProjectId != Execution.ProjectId ||
                JsonSerializer.Serialize(preparation.Authority) != JsonSerializer.Serialize(Execution.SourceAuthority) ||
                preparation.RequestFingerprint != ProcessLaunchIntentFingerprint.Compute(preparation.Request with {
                    Authority = preparation.Authority, ProjectAdmission = preparation.Authority?.ProjectAdmission,
                    LinkTarget = preparation.LinkTarget, ToolSource = this
                })) {
            throw new ProcessLaunchIntentConflictException(IntentId,
                "The prepared Process launch differs from its original tool proposal or source authority.");
        }
    }

    private static bool IsDigest(string? value)
        => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}

public interface IProcessToolLaunchAdmissionPolicy {
    Task<IProcessLaunchAuthorityLease> AcquireAsync(ProcessPreparedLaunch preparation,
        CancellationToken cancellationToken = default);
}
