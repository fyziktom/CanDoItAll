using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Process assignment claim that dispatched a workflow run for a process step. Internal lineage recorded in the
/// launch origin.
/// </summary>
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

    /// <summary>Identifier of the database profile of the process.</summary>
    public Guid ProfileId { get; }
    /// <summary>Root process run of the process tree.</summary>
    public WorkflowProcessRunId RootRun { get; }
    /// <summary>Process run that owns the step.</summary>
    public WorkflowProcessRunId ProcessRun { get; }
    /// <summary>Process step assignment that dispatched the workflow.</summary>
    public WorkflowProcessAssignmentId Assignment { get; }
    /// <summary>Token of the process claim; internal.</summary>
    public Guid ClaimToken { get; }
    /// <summary>Fingerprint of the claim owner; internal.</summary>
    public string OwnerFingerprint { get; }
    /// <summary>Hash of the assignment contract; internal.</summary>
    public string ContractHash { get; }
    /// <summary>Workflow the step runs.</summary>
    public WorkflowId WorkflowId { get; }
    /// <summary>Exact version the step requested; null for the latest Active version.</summary>
    public WorkflowVersionId? RequestedVersionId { get; }
    /// <summary>Fingerprint of the run input; internal.</summary>
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
