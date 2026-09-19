using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Approved agent tool proposal and process step that admitted a workflow run. Internal lineage recorded in the
/// launch origin.
/// </summary>
public sealed record WorkflowProcessToolBinding {
    [JsonConstructor]
    public WorkflowProcessToolBinding(AgentToolSessionReference session, AgentToolProfileBinding profile,
        AgentToolBatchId batchId, AgentToolBusinessIntentId intentId, string toolName, AgentToolSemanticDigest proposalFingerprint,
        Guid executorAgentId, Guid launchCapabilityId, WorkflowProcessRunId processRun, WorkflowProcessAssignmentId stepInstance,
        string readinessHash) {
        Session = session;
        Profile = profile;
        BatchId = batchId;
        IntentId = intentId;
        ToolName = toolName;
        ProposalFingerprint = proposalFingerprint;
        ExecutorAgentId = executorAgentId;
        LaunchCapabilityId = launchCapabilityId;
        ProcessRun = processRun;
        StepInstance = stepInstance;
        ReadinessHash = readinessHash;
        Validate();
    }

    /// <summary>Background agent session in which the tool was proposed.</summary>
    public AgentToolSessionReference Session { get; }
    /// <summary>Profile binding of the agent tool.</summary>
    public AgentToolProfileBinding Profile { get; }
    /// <summary>Batch of tool calls the proposal belongs to.</summary>
    public AgentToolBatchId BatchId { get; }
    /// <summary>Business intent of the proposal; it also becomes the workflow run identifier.</summary>
    public AgentToolBusinessIntentId IntentId { get; }
    /// <summary>Name of the tool, at most 128 characters.</summary>
    public string ToolName { get; }
    /// <summary>Fingerprint of the approved proposal.</summary>
    public AgentToolSemanticDigest ProposalFingerprint { get; }
    /// <summary>Identifier of the agent that executes the tool.</summary>
    public Guid ExecutorAgentId { get; }
    /// <summary>Identifier of the capability that allows the agent to launch workflows.</summary>
    public Guid LaunchCapabilityId { get; }
    /// <summary>Process run that owns the step.</summary>
    public WorkflowProcessRunId ProcessRun { get; }
    /// <summary>Process step instance that made the proposal.</summary>
    public WorkflowProcessAssignmentId StepInstance { get; }
    /// <summary>Hash of the step readiness at approval; internal.</summary>
    public string ReadinessHash { get; }

    [JsonIgnore]
    public WorkflowRunId PreparedRunId => new(IntentId.Value);

    public void Validate() {
        if (Session?.BackgroundSource is null || Session.ChatSessionId != Guid.Empty || Session.AuthorityId.Value != Guid.Empty ||
                Profile is null || Profile.ProfileId == Guid.Empty || BatchId.Value == Guid.Empty || IntentId.Value == Guid.Empty ||
                ExecutorAgentId == Guid.Empty || LaunchCapabilityId == Guid.Empty || ProcessRun.Value == Guid.Empty || StepInstance.Value == Guid.Empty ||
                string.IsNullOrWhiteSpace(ToolName) || ToolName.Length > 128 || string.IsNullOrWhiteSpace(ProposalFingerprint.Value) ||
                string.IsNullOrWhiteSpace(ReadinessHash)) {
            throw new ArgumentException("A Process Workflow tool requires its original background session, profile, approved proposal and exact Process claim.");
        }
    }
}
