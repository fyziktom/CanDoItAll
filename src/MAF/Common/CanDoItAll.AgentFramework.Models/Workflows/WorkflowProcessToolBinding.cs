using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

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

    public AgentToolSessionReference Session { get; }
    public AgentToolProfileBinding Profile { get; }
    public AgentToolBatchId BatchId { get; }
    public AgentToolBusinessIntentId IntentId { get; }
    public string ToolName { get; }
    public AgentToolSemanticDigest ProposalFingerprint { get; }
    public Guid ExecutorAgentId { get; }
    public Guid LaunchCapabilityId { get; }
    public WorkflowProcessRunId ProcessRun { get; }
    public WorkflowProcessAssignmentId StepInstance { get; }
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
