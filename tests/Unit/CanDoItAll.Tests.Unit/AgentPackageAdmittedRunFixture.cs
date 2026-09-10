using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

internal static class AgentPackageAdmittedRunFixture {
    public const string PrivateContent = "test-only-admitted-private-settings-and-context";

    public static ExecutionRunRecord Create(Guid agentId) {
        var now = new DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var reference = new AgentToolSessionReference(runId, sessionId, new(Guid.NewGuid()));
        var session = new AgentToolSessionAdmission(reference, agentId, AgentRuntimeContextPurpose.InteractiveChat,
            new(Guid.NewGuid(), "original-profile-fingerprint", new(1)));
        var protocol = AgentToolProtocolEnvelope.Create("package-test-protocol", 1, "{}");
        var arguments = System.Text.Json.JsonSerializer.Serialize(new { settings = PrivateContent });
        var payload = new AgentToolPreparedPayload(AgentToolInvocationPolicyMetadata.HrSimpleChatCreate, 1,
            AgentToolProtocolEnvelope.ComputeDigest(arguments), arguments, AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt);
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, "original-call", payload, true,
            AgentToolProposalState.Completed, ExecutionApprovalStatus.Approved, payload.Digest,
            ApprovalId: "original-approval", DispatchClaimId: Guid.NewGuid(), Result: protocol,
            EffectState: AgentToolEffectState.Committed);
        var journal = new AgentToolJournalRecord(AgentToolJournalRecord.CurrentSchemaVersion, 1, session,
            [new(Guid.NewGuid(), 0, 0, protocol, PendingApprovals: [])],
            [new(new(Guid.NewGuid()), 0, protocol.Digest, protocol, [proposal])],
            OriginalInput: new(Guid.NewGuid(), PrivateContent),
            RuntimeContext: new(PrivateContent, WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"))));
        journal.Validate();
        return new ExecutionRunRecord(runId, agentId, sessionId, "Portable historical run", "chat-session", sessionId.ToString("N"),
            string.Empty, string.Empty, "test", "interactive", "{}", string.Empty, "Historical result",
            "test-provider", "test-model", ExecutionState.Completed, RunOutcome.Succeeded, now, now, now, now,
            PrivateContent, System.Text.Json.JsonSerializer.Serialize(new { checkpoint = PrivateContent }), []) {
            ToolAdmission = journal
        };
    }
}
