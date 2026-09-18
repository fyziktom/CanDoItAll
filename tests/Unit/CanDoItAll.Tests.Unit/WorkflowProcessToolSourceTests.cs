using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowProcessToolSourceTests {
    [Fact]
    public void Saved_background_origin_round_trips_every_nondefault_proposal_and_Process_identity() {
        var origin = Origin();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize<WorkflowLaunchOrigin>(origin, options);
        var actual = Assert.IsType<WorkflowLaunchOrigin.ProcessToolInvocation>(JsonSerializer.Deserialize<WorkflowLaunchOrigin>(json, options));
        Assert.Equal(origin.Invocation, actual.Invocation);
        Assert.Equal(origin.Invocation.IntentId.Value, actual.Invocation.PreparedRunId.Value);
        Assert.NotEqual(Guid.Empty, actual.Invocation.Session.ExecutionRunId);
        Assert.NotEqual(Guid.Empty, actual.Invocation.ExecutorAgentId);
        Assert.NotEqual(Guid.Empty, actual.Invocation.BatchId.Value);
        Assert.NotEqual(Guid.Empty, actual.Invocation.ProcessRun.Value);
        Assert.NotEqual(Guid.Empty, actual.Invocation.StepInstance.Value);
        Assert.Equal(17, actual.Invocation.Profile.Generation.Value);
        Assert.Null(actual.StructureAuthority);
        Assert.Equal(Guid.Empty, actual.Invocation.Session.AuthorityId.Value);
        Assert.DoesNotContain("\"preparedRunId\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Pre_origin_reader_rejects_the_new_authority_but_retains_legacy_ProcessAssignment() {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => {
            if (info.Type == typeof(WorkflowLaunchOrigin)) {
                var derived = info.PolymorphismOptions!.DerivedTypes.Single(item => item.DerivedType == typeof(WorkflowLaunchOrigin.ProcessToolInvocation));
                info.PolymorphismOptions.DerivedTypes.Remove(derived);
            }
        });
        var old = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = resolver };
        var current = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize<WorkflowLaunchOrigin>(Origin(), current);
        Assert.Contains("process-tool-invocation-v1", json, StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowLaunchOrigin>(json, old));
        var legacy = new WorkflowLaunchOrigin.ProcessAssignment(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid()));
        var legacyJson = JsonSerializer.Serialize<WorkflowLaunchOrigin>(legacy, current);
        Assert.Equal(legacyJson, JsonSerializer.Serialize<WorkflowLaunchOrigin>(JsonSerializer.Deserialize<WorkflowLaunchOrigin>(legacyJson, old)!, old));
    }

    [Fact]
    public void Canonical_proposal_preserves_nested_JSON_semantics_and_rejects_duplicate_or_unrecognized_fields() {
        var codec = new WorkflowProcessToolProposalCodec();
        var id = Guid.NewGuid();
        var first = codec.Prepare(new WorkflowAgentStartInput(id, WorkflowAgentDefinitionSelectionMode.LatestActive, inputJson: "{\"b\":2,\"a\":1}"));
        var second = codec.Prepare(new WorkflowAgentStartInput(id, WorkflowAgentDefinitionSelectionMode.LatestActive, inputJson: "{\"a\":1,\"b\":2}"));
        Assert.Equal(first, second);
        Assert.Equal(AgentToolProposalRecovery.OwnerReceipt, first.Recovery);
        Assert.Equal(id, codec.Read(first).WorkflowId);
        Assert.Contains("\"A\":1", codec.Read(codec.Prepare(new WorkflowAgentStartInput(id, WorkflowAgentDefinitionSelectionMode.LatestActive,
            inputJson: "{\"a\":2,\"A\":1}"))).InputJson, StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => codec.Prepare(new WorkflowAgentStartInput(id, WorkflowAgentDefinitionSelectionMode.LatestActive, inputJson: "{\"a\":1,\"a\":2}")));
        using var malformed = JsonDocument.Parse("{\"request\":null,\"authority\":\"invented\"}");
        Assert.Throws<JsonException>(() => codec.Prepare(malformed.RootElement));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Changed_selection_input_or_caller_key_is_a_different_approved_semantic_request(int change) {
        var codec = new WorkflowProcessToolProposalCodec();
        var id = Guid.NewGuid();
        var original = codec.Prepare(new WorkflowAgentStartInput(id, WorkflowAgentDefinitionSelectionMode.LatestActive));
        var changed = codec.Prepare(new WorkflowAgentStartInput(change == 0 ? Guid.NewGuid() : id, WorkflowAgentDefinitionSelectionMode.LatestActive,
            inputJson: change == 1 ? "{\"changed\":true}" : "{}", idempotencyKey: change == 2 ? "another" : null));
        Assert.NotEqual(original.Digest, changed.Digest);
    }

    [Fact]
    public void Separate_proposals_in_the_same_Process_occurrence_have_separate_children_and_idempotency_scopes() {
        var first = Origin();
        var saved = first.Invocation;
        var other = new WorkflowLaunchOrigin.ProcessToolInvocation(new(saved.Session, saved.Profile, saved.BatchId, new(Guid.NewGuid()),
            saved.ToolName, saved.ProposalFingerprint, saved.ExecutorAgentId, saved.LaunchCapabilityId, saved.ProcessRun, saved.StepInstance, saved.ReadinessHash), first.CorrelationId) {
            AuthorizationScope = first.AuthorizationScope, AuthorizationPolicyFingerprint = first.AuthorizationPolicyFingerprint
        };
        var workflowId = new WorkflowId(Guid.NewGuid());
        WorkflowLaunchIntent Intent(WorkflowLaunchOrigin origin) => new(new WorkflowDefinitionSelection.LatestActive(workflowId),
            WorkflowLaunchMode.Production, origin, "{}", WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.CallerSupplied(new("same-caller-key")));
        Assert.NotEqual(saved.PreparedRunId, other.Invocation.PreparedRunId);
        Assert.NotEqual(WorkflowLaunchIdempotencyRequestFactory.CreateScope(Intent(first), new("same-caller-key")),
            WorkflowLaunchIdempotencyRequestFactory.CreateScope(Intent(other), new("same-caller-key")));
        Assert.Equal(WorkflowLaunchIdempotencyRequestFactory.CreateScope(Intent(first), new("same-caller-key")),
            WorkflowLaunchIdempotencyRequestFactory.CreateScope(Intent(first), new("same-caller-key")));
    }

    [Fact]
    public void Previous_project_scope_reader_cannot_ignore_Process_tool_authority_on_an_independent_saved_output_plan() {
        var binding = Origin().Invocation;
        var authority = new WorkflowStructureAuthority(WorkflowStructureAuthorityChannel.AgentExecution,
            new(WorkflowLaunchActorKind.Agent, Guid.NewGuid().ToString("D")), binding.Profile.ProfileId, Guid.Empty,
            false, false, null, "saved-policy") {
            ProjectScope = new([]),
            ProcessAuthority = new(binding.ProcessRun.Value, binding.StepInstance.Value, binding.ReadinessHash) {
                ExecutionRunId = binding.Session.ExecutionRunId, OwnerFingerprint = "owner", ToolInvocation = binding
            }
        };
        var current = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var previous = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new PreviousWorkflowProjectScopeReader() } };
        var json = JsonSerializer.Serialize(authority, current);
        Assert.Contains("agent-execution/process-tool-scope-v1", json, StringComparison.Ordinal);
        Assert.Equal(binding, JsonSerializer.Deserialize<WorkflowStructureAuthority>(json, current)!.ProcessAuthority!.ToolInvocation);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowStructureAuthority>(json, previous));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowStructureAuthority>(json.Replace("process-tool-scope-v1", "project-scope-v1", StringComparison.Ordinal), current));
        var legacy = authority with { ProcessAuthority = authority.ProcessAuthority! with { ToolInvocation = null } };
        var oldJson = JsonSerializer.Serialize(legacy, current);
        Assert.Equal(oldJson, JsonSerializer.Serialize(JsonSerializer.Deserialize<WorkflowStructureAuthority>(oldJson, previous), previous));
    }

    private static WorkflowLaunchOrigin.ProcessToolInvocation Origin() {
        var background = new AgentToolBackgroundSourceBinding("process-step", "implementation", new(new string('a', 64)), new(new string('b', 64)));
        var binding = new WorkflowProcessToolBinding(new(Guid.NewGuid(), Guid.Empty, default, background), new(Guid.NewGuid(), "profile-fingerprint", new(17)),
            new(Guid.NewGuid()), new(Guid.NewGuid()), WorkflowToolPolicy.WorkflowsRunStart, new(new string('c', 64)), Guid.NewGuid(), Guid.NewGuid(),
            new(Guid.NewGuid()), new(Guid.NewGuid()), "readiness-fingerprint");
        return new(binding, new(binding.ProcessRun.Value.ToString("D"))) {
            AuthorizationScope = WorkspaceScopeDescriptor.Process(binding.ProcessRun.Value.ToString("D")),
            AuthorizationPolicyFingerprint = "workflow-policy-v1"
        };
    }
}
