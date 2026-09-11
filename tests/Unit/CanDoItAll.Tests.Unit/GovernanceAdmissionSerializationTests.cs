using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class GovernanceAdmissionSerializationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_admission_and_intent_identifiers_round_trip_as_the_exact_nondefault_values(bool webDefaults) {
        var options = new JsonSerializerOptions(webDefaults ? JsonSerializerDefaults.Web : JsonSerializerDefaults.General);
        var admission = new ProcessLaunchAdmissionId(Guid.NewGuid());
        var intent = new ProcessLaunchIntentId(Guid.NewGuid());
        var readAdmission = JsonSerializer.Deserialize<ProcessLaunchAdmissionId>(JsonSerializer.Serialize(admission, options), options);
        var readIntent = JsonSerializer.Deserialize<ProcessLaunchIntentId>(JsonSerializer.Serialize(intent, options), options);
        Assert.NotEqual(Guid.Empty, readAdmission.Value);
        Assert.NotEqual(Guid.Empty, readIntent.Value);
        Assert.Equal(admission.Value, readAdmission.Value);
        Assert.Equal(intent.Value, readIntent.Value);
    }

    [Fact]
    public void Scheduler_snapshot_preserves_exact_authority_and_has_canonical_bytes_and_hash_for_equivalent_grant_sets() {
        var fixture = new Fixture();
        var now = new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);
        var snapshot = new SchedulerFireSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now,
            now.AddMinutes(5), "Saved source", SchedulerPlanTargetKind.Workflow, Guid.NewGuid(), Guid.NewGuid(), "Saved target",
            "{}", fixture.Authority(false));
        var reordered = snapshot with { Authority = fixture.Authority(true) };
        Assert.Equal(snapshot.ToJson(), reordered.ToJson());
        Assert.Equal(snapshot.Fingerprint(), reordered.Fingerprint());
        AssertCanonicalSets(JsonNode.Parse(snapshot.ToJson())!["authority"]!["agentGovernance"]!);
        var saved = SchedulerFireSnapshot.Parse(snapshot.ToJson());
        Assert.NotEqual(Guid.Empty, saved.FireId);
        Assert.NotEqual(Guid.Empty, saved.PlanRunId);
        Assert.NotEqual(Guid.Empty, saved.CorrelationId);
        Assert.Equal(snapshot.FireId, saved.FireId);
        Assert.Equal(snapshot.PlanRunId, saved.PlanRunId);
        Assert.Equal(snapshot.CorrelationId, saved.CorrelationId);
        Assert.Equal(snapshot.TargetId, saved.TargetId);
        Assert.Equal(snapshot.TargetVersionId, saved.TargetVersionId);
        Assert.Equal(snapshot.ToJson(), saved.ToJson());
        Assert.Equal(snapshot.Fingerprint(), saved.Fingerprint());
        fixture.AssertAuthority(saved.Authority!);
    }

    [Fact]
    public void Workflow_origin_round_trip_retains_preallocated_run_fire_source_and_all_nonempty_authority_grants() {
        var fixture = new Fixture();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var original = new WorkflowLaunchOrigin.SchedulerPlanRun(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid()),
            new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero), new(Guid.NewGuid())) {
            PreparedRunId = WorkflowRunId.New(), StructureAuthority = fixture.Authority(false)
        };
        var json = JsonSerializer.Serialize<WorkflowLaunchOrigin>(original, options);
        var saved = Assert.IsType<WorkflowLaunchOrigin.SchedulerPlanRun>(JsonSerializer.Deserialize<WorkflowLaunchOrigin>(json, options));
        Assert.Equal(original.PlanId, saved.PlanId);
        Assert.Equal(original.PlanRunId, saved.PlanRunId);
        Assert.Equal(original.FireId.Value, saved.FireId.Value);
        Assert.NotEqual(Guid.Empty, saved.FireId.Value);
        Assert.Equal(original.CorrelationId.Value, saved.CorrelationId.Value);
        Assert.False(string.IsNullOrWhiteSpace(saved.CorrelationId.Value));
        Assert.Equal(original.PreparedRunId!.Value.Value, saved.PreparedRunId!.Value.Value);
        Assert.NotEqual(Guid.Empty, saved.PreparedRunId.Value.Value);
        fixture.AssertAuthority(saved.StructureAuthority!);
        Assert.Equal(json, JsonSerializer.Serialize<WorkflowLaunchOrigin>(saved, options));
        var reordered = original with { StructureAuthority = fixture.Authority(true) };
        Assert.Equal(json, JsonSerializer.Serialize<WorkflowLaunchOrigin>(reordered, options));
    }

    [Fact]
    public void Process_project_admission_preserves_all_three_exact_nondefault_identity_components() {
        var original = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var saved = JsonSerializer.Deserialize<ProcessProjectAdmission>(JsonSerializer.Serialize(original));
        Assert.NotNull(saved);
        Assert.NotEqual(Guid.Empty, saved.DatabaseProfileId);
        Assert.NotEqual(Guid.Empty, saved.ProjectId);
        Assert.NotEqual(Guid.Empty, saved.LifetimeId);
        Assert.Equal(original, saved);
    }

    private static void AssertCanonicalSets(JsonNode governance) {
        foreach (var property in new[] { "allowedOperations", "allowedCapabilityKeys", "writableExternalTargetAliases",
                     "readOnlyExternalTargetAliases", "allowedManagedArtifactReadRefs" }) {
            var values = governance[property]!.AsArray().Select(item => item!.GetValue<string>()).ToArray();
            Assert.True(values.Length > 1);
            Assert.Equal(values.Order(StringComparer.Ordinal), values);
        }
    }

    private sealed class Fixture {
        private readonly Guid authorityId = Guid.NewGuid();
        private readonly Guid agentId = Guid.NewGuid();
        private readonly Guid profileId = Guid.NewGuid();
        private readonly Guid projectId = Guid.NewGuid();
        private static readonly string[] Operations = ["z.operation", "a.operation", "m.operation"];
        private static readonly string[] Capabilities = ["z.capability", "a.capability", "m.capability"];
        private static readonly string[] WritableAliases = ["z-target", "a-target", "m-target"];
        private static readonly string[] ReadOnlyAliases = ["z-read", "a-read", "m-read"];
        private static readonly string[] ManagedRefs = ["managed:z/item.txt", "managed:a/item.txt", "managed:m/item.txt"];

        public WorkflowStructureAuthority Authority(bool reverse) => new(WorkflowStructureAuthorityChannel.AgentExecution,
            new(WorkflowLaunchActorKind.Agent, agentId.ToString("D")), profileId, projectId, true, false, null, "policy-fingerprint") {
            AgentGovernance = new(new(authorityId), agentId, profileId, new(7), WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
                true, true, "policy-version", "policy-fingerprint", Ordered(Operations, reverse), Ordered(Capabilities, reverse),
                Ordered(WritableAliases, reverse), Ordered(ReadOnlyAliases, reverse), Ordered(ManagedRefs, reverse))
        };

        public void AssertAuthority(WorkflowStructureAuthority authority) {
            Assert.Equal(WorkflowStructureAuthorityChannel.AgentExecution, authority.Channel);
            Assert.Equal(WorkflowLaunchActorKind.Agent, authority.Principal.Kind);
            Assert.Equal(agentId.ToString("D"), authority.Principal.SubjectId);
            Assert.Equal(profileId, authority.DatabaseProfileId);
            Assert.Equal(projectId, authority.ProjectId);
            Assert.True(authority.CanCreateTasks);
            Assert.False(authority.CanCreateAssets);
            var governance = Assert.IsType<AgentExecutionGovernanceSnapshot>(authority.AgentGovernance);
            Assert.NotEqual(Guid.Empty, governance.AuthorityId.Value);
            Assert.Equal(authorityId, governance.AuthorityId.Value);
            Assert.Equal(agentId, governance.AgentId);
            Assert.Equal(profileId, governance.DatabaseProfileId);
            Assert.Equal(7, governance.DatabaseProfileGeneration.Value);
            Assert.Equal(WorkspaceScopeDescriptor.Project(projectId.ToString("D")), governance.WorkspaceScope);
            Assert.True(governance.ReadAllowed);
            Assert.True(governance.MutationAllowed);
            Assert.Equal("policy-version", governance.PolicyVersion);
            Assert.Equal("policy-fingerprint", governance.PolicyFingerprint);
            Assert.True(governance.AllowedOperations.SetEquals(Operations));
            Assert.True(governance.AllowedCapabilityKeys.SetEquals(Capabilities));
            Assert.True(governance.WritableExternalTargetAliases.SetEquals(WritableAliases));
            Assert.True(governance.ReadOnlyExternalTargetAliases.SetEquals(ReadOnlyAliases));
            Assert.True(governance.AllowedManagedArtifactReadRefs.SetEquals(ManagedRefs));
            Assert.True(governance.AllowedOperations.Contains("A.OPERATION"));
        }

        private static string[] Ordered(string[] values, bool reverse) => reverse ? values.Reverse().ToArray() : [.. values];
    }
}
