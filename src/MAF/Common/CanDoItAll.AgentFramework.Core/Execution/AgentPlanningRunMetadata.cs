using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentPlanningRunMetadata {
    private const string Key = "agentPlanningRuntimeContract";
    private const int Version = 2;
    private const int InheritedVersion = 3;
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

    internal static string ApplyInherited(string metadataJson, AgentPlanningOwner owner, AgentToolSemanticDigest digest,
        WorkspaceScopeDescriptor scope) {
        Require(owner, digest);
        RequireScope(scope);
        if (AgentTurnContextMetadata.ContainsTurnContextReference(metadataJson) ||
            AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(metadataJson) is not null) {
            throw Failure("Inherited planning cannot substitute interactive authority.");
        }
        var metadata = JsonNode.Parse(metadataJson) as JsonObject
            ?? throw Failure("Planning requires object run metadata.");
        if (metadata.ContainsKey(Key)) {
            throw Failure("A new inherited run cannot replace an existing planning runtime contract.");
        }
        metadata[Key] = JsonSerializer.SerializeToNode(new Binding(InheritedVersion, owner, digest, scope));
        return metadata.ToJsonString();
    }

    private static Binding ReadBinding(ExecutionRunRecord run) {
        var journal = run.ToolAdmission;
        var decision = journal?.PlanningDecision
            ?? throw Failure("The original run has no planning decision.");
        journal!.Validate();
        using var metadata = JsonDocument.Parse(run.MetadataJson);
        if (metadata.RootElement.ValueKind != JsonValueKind.Object || !metadata.RootElement.TryGetProperty(Key, out var value) ||
            value.Deserialize<Binding>() is not { } contract || contract.Owner != decision.Owner ||
            contract.Scope is null) {
            throw Failure("The original planning runtime contract is unavailable or incompatible.");
        }
        Require(contract.Owner, contract.Digest);
        if (decision.InheritedOrigin is { } origin) {
            RequireScope(contract.Scope);
            if (contract.Version != InheritedVersion || journal.SchemaVersion != AgentToolJournalRecord.InheritedPlanningSchemaVersion ||
                decision.Session != journal.Session.Reference || decision.Session.ExecutionRunId != run.Id || run.ChatSessionId is not null ||
                decision.Session.ChatSessionId != Guid.Empty || decision.Session.AuthorityId.Value != Guid.Empty ||
                journal.Session.AgentId != run.AgentId || origin.AgentId != run.AgentId || origin.Profile != journal.Session.Profile ||
                origin.Source != decision.Session.BackgroundSource || origin.Source.SourceKind != run.SourceKind || origin.Source.SourceId != run.SourceId ||
                AgentTurnContextMetadata.ContainsTurnContextReference(run.MetadataJson) ||
                AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson) is not null) {
                throw Failure("The inherited planning contract no longer matches its original admitted run.");
            }
            return contract;
        }
        if (contract.Version != Version || journal.SchemaVersion != AgentToolJournalRecord.PlanningSchemaVersion) {
            throw Failure("The original planning runtime contract is unavailable or incompatible.");
        }
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

    private static void RequireScope(WorkspaceScopeDescriptor scope) {
        if (scope is null || !Enum.IsDefined(scope.Kind) ||
            scope.Kind != WorkspaceScopeKind.Sandbox && string.IsNullOrWhiteSpace(scope.Key) ||
            scope != new WorkspaceScopeDescriptor(scope.Kind, scope.Key)) {
            throw Failure("The inherited planning runtime contract requires its canonical workspace scope.");
        }
    }

    private static AgentToolAdmissionException Failure(string message) => new(InvalidContractCode, message);
    private sealed record Binding(int Version, AgentPlanningOwner Owner, AgentToolSemanticDigest Digest, WorkspaceScopeDescriptor Scope);
}
