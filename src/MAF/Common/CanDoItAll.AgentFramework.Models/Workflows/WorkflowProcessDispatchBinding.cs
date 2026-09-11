using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed record WorkflowProcessDispatchBinding {
    [JsonConstructor]
    public WorkflowProcessDispatchBinding(Guid profileId, WorkflowProcessRunId rootRun, WorkflowProcessRunId processRun,
        WorkflowProcessAssignmentId assignment, Guid claimToken, string ownerFingerprint, string contractHash,
        WorkflowId workflowId, WorkflowVersionId? requestedVersionId, string inputFingerprint) {
        ProfileId = profileId;
        RootRun = rootRun;
        ProcessRun = processRun;
        Assignment = assignment;
        ClaimToken = claimToken;
        OwnerFingerprint = ownerFingerprint;
        ContractHash = contractHash;
        WorkflowId = workflowId;
        RequestedVersionId = requestedVersionId;
        InputFingerprint = inputFingerprint;
        Validate();
    }

    public Guid ProfileId { get; }
    public WorkflowProcessRunId RootRun { get; }
    public WorkflowProcessRunId ProcessRun { get; }
    public WorkflowProcessAssignmentId Assignment { get; }
    public Guid ClaimToken { get; }
    public string OwnerFingerprint { get; }
    public string ContractHash { get; }
    public WorkflowId WorkflowId { get; }
    public WorkflowVersionId? RequestedVersionId { get; }
    public string InputFingerprint { get; }

    public void Validate() {
        if (ProfileId == Guid.Empty || RootRun.Value == Guid.Empty || ProcessRun.Value == Guid.Empty ||
                Assignment.Value == Guid.Empty || ClaimToken == Guid.Empty || WorkflowId.Value == Guid.Empty ||
                RequestedVersionId is { Value: var version } && version == Guid.Empty) {
            throw new ArgumentException("Mapped Workflow dispatch requires its exact profile, Process assignment, claim and Workflow identity.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(OwnerFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(ContractHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(InputFingerprint);
    }

    public bool HasSameIntent(WorkflowProcessDispatchBinding other) => other is not null &&
        ProfileId == other.ProfileId && RootRun == other.RootRun && ProcessRun == other.ProcessRun && Assignment == other.Assignment &&
        OwnerFingerprint == other.OwnerFingerprint && ContractHash == other.ContractHash && WorkflowId == other.WorkflowId &&
        RequestedVersionId == other.RequestedVersionId && InputFingerprint == other.InputFingerprint;
}
