using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentPlanningRunMetadata {
    private const string Key = "agentPlanningRuntimeContract";
    private const int Version = 2;
    private const string InvalidContractCode = "planning.original-contract";

    internal static string Apply(string metadataJson, AgentPlanningOwner owner, AgentToolSemanticDigest digest) {
        Require(owner, digest);
        var governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(metadataJson)
            ?? throw Failure("Planning requires its originally admitted authority and workspace scope.");
        var metadata = JsonNode.Parse(metadataJson) as JsonObject
            ?? throw Failure("Planning requires object run metadata.");
        metadata[Key] = JsonSerializer.SerializeToNode(new Binding(Version, owner, digest, governance.WorkspaceScope));
        return metadata.ToJsonString();
    }

    internal static AgentToolSemanticDigest Read(ExecutionRunRecord run) => ReadBinding(run).Digest;

    internal static WorkspaceScopeDescriptor ReadScope(ExecutionRunRecord run) => ReadBinding(run).Scope;

    private static Binding ReadBinding(ExecutionRunRecord run) {
        var journal = run.ToolAdmission;
        var decision = journal?.PlanningDecision
            ?? throw Failure("The original run has no planning decision.");
        journal!.Validate();
        using var metadata = JsonDocument.Parse(run.MetadataJson);
        if (metadata.RootElement.ValueKind != JsonValueKind.Object || !metadata.RootElement.TryGetProperty(Key, out var value) ||
            value.Deserialize<Binding>() is not { } contract || contract.Version != Version || contract.Owner != decision.Owner ||
            contract.Scope is null) {
            throw Failure("The original planning runtime contract is unavailable or incompatible.");
        }
        Require(contract.Owner, contract.Digest);
        var governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson)
            ?? throw Failure("The original planning authority is unavailable or incompatible.");
        if (decision.Session != journal.Session.Reference || decision.Session.ExecutionRunId != run.Id ||
            decision.Session.ChatSessionId != run.ChatSessionId || decision.Session.AuthorityId != governance.AuthorityId ||
            journal.Session.AgentId != run.AgentId || governance.AgentId != run.AgentId ||
            governance.DatabaseProfileId != journal.Session.Profile.ProfileId ||
            governance.DatabaseProfileGeneration != journal.Session.Profile.Generation || governance.WorkspaceScope != contract.Scope) {
            throw Failure("The planning scope or authority no longer matches its original admitted run.");
        }
        return contract;
    }

    private static void Require(AgentPlanningOwner owner, AgentToolSemanticDigest digest) {
        if (!Enum.IsDefined(owner) || string.IsNullOrWhiteSpace(digest.Value)) {
            throw Failure("The planning runtime contract requires an assigned owner and digest.");
        }
    }

    private static AgentToolAdmissionException Failure(string message) => new(InvalidContractCode, message);
    private sealed record Binding(int Version, AgentPlanningOwner Owner, AgentToolSemanticDigest Digest, WorkspaceScopeDescriptor Scope);
}
