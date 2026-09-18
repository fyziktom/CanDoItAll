using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class GovernanceAdmissionSerializationTests {
    private const string LegacySchedulerSnapshotJson = """
        {"planId":"66666666-6666-6666-6666-666666666666","planRunId":"77777777-7777-7777-7777-777777777777","fireId":"88888888-8888-8888-8888-888888888888","correlationId":"99999999-9999-9999-9999-999999999999","firedAtUtc":"2026-09-10T10:00:00+00:00","nextPlannedFireAtUtc":"2026-09-10T10:05:00+00:00","planName":"Saved source","targetKind":1,"targetId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","targetVersionId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","targetName":"Saved target","inputJson":"{}","authority":{"channel":0,"principal":{"kind":1,"subjectId":"44444444-4444-4444-4444-444444444444"},"databaseProfileId":"11111111-1111-1111-1111-111111111111","projectId":"22222222-2222-2222-2222-222222222222","canCreateTasks":true,"canCreateAssets":true,"expiresAtUtc":null,"policyFingerprint":"policy-fixture","operatorSurface":0,"agentGovernance":{"authorityId":{"value":"77777777-7777-7777-7777-777777777777","isEmpty":false},"agentId":"44444444-4444-4444-4444-444444444444","databaseProfileId":"11111111-1111-1111-1111-111111111111","databaseProfileGeneration":{"value":12},"workspaceScope":{"kind":4,"key":"11111111111111111111111111111111","isDefaultSandbox":false,"displayName":"organization/11111111111111111111111111111111","partitionRelativePath":"scopes/organization/11111111111111111111111111111111","dataRootRelativePath":"data/scopes/organization/11111111111111111111111111111111","artifactRootRelativePath":"artifacts/scopes/organization/11111111111111111111111111111111","integrationMapRootRelativePath":"integration-map/scopes/organization/11111111111111111111111111111111","outputRootRelativePath":"output/scopes/organization/11111111111111111111111111111111","managedRootRelativePaths":["data/scopes/organization/11111111111111111111111111111111","artifacts/scopes/organization/11111111111111111111111111111111","integration-map/scopes/organization/11111111111111111111111111111111","output/scopes/organization/11111111111111111111111111111111"]},"readAllowed":true,"mutationAllowed":true,"policyVersion":"fixture-v1","policyFingerprint":"policy-fixture","allowedOperations":["alpha","omega"],"allowedCapabilityKeys":["cap-a","cap-z"],"writableExternalTargetAliases":["write-a","write-z"],"readOnlyExternalTargetAliases":["read-a","read-z"],"allowedManagedArtifactReadRefs":["artifact-a","artifact-z"]},"processAuthority":null,"schedulerAuthority":null,"allProjects":false,"projectIds":["22222222-2222-2222-2222-222222222222"]},"legacyObservationOnly":false}
        """;

    [Fact]
    public void Retained_scheduler_row_and_workflow_origin_keep_frozen_legacy_governance_hashes() {
        const string expectedHash = "07098CB35C081134B3F1DDF8A91AD55A2322095850C1052F3E97B2043B69700E";
        Assert.Equal(expectedHash, SchedulerFireSnapshot.Hash(LegacySchedulerSnapshotJson));
        var preparedRunId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var saved = SchedulerFireAdmissionStore.ReadSnapshot(new() {
            PlanId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"), PreparedWorkflowRunId = preparedRunId,
            SnapshotJson = LegacySchedulerSnapshotJson, SnapshotFingerprint = expectedHash
        });
        Assert.Equal(LegacySchedulerSnapshotJson, saved.ToJson());
        Assert.Equal(expectedHash, saved.Fingerprint());
        Assert.Null(saved.Authority!.AgentGovernance!.SchemaVersion);
        Assert.Null(saved.Authority.AgentGovernance.SourceProjectLifetime);
        Assert.Equal("6AB2AE8A76AADD86B843C4B71121B3588FCF2967B47C7A3F07C71701A85A8AAF",
            SchedulerFireSnapshot.Hash(SchedulerFireSnapshot.SerializeAuthority(saved.Authority)!));
        var origin = new WorkflowLaunchOrigin.SchedulerPlanRun(saved.PlanId, saved.PlanRunId, new(saved.FireId),
            saved.FiredAtUtc, new(saved.CorrelationId)) { PreparedRunId = new(preparedRunId), StructureAuthority = saved.Authority };
        Assert.Equal("68C2438F9A26ACFB354482DBC2C0DBE5C75B84CD3F1F34E2A6554E9F20395C95",
            WorkflowProviderDisclosureContent.Source(origin).Value);
    }

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
        private readonly Guid lifetimeId = Guid.NewGuid();
        private static readonly string[] Operations = ["z.operation", "a.operation", "m.operation"];
        private static readonly string[] Capabilities = ["z.capability", "a.capability", "m.capability"];
        private static readonly string[] WritableAliases = ["z-target", "a-target", "m-target"];
        private static readonly string[] ReadOnlyAliases = ["z-read", "a-read", "m-read"];
        private static readonly string[] ManagedRefs = ["managed:z/item.txt", "managed:a/item.txt", "managed:m/item.txt"];

        public WorkflowStructureAuthority Authority(bool reverse) => new(WorkflowStructureAuthorityChannel.AgentExecution,
            new(WorkflowLaunchActorKind.Agent, agentId.ToString("D")), profileId, projectId, true, false, null, "policy-fingerprint") {
            AgentGovernance = new(new(authorityId), agentId, profileId, new(7), WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
                true, true, "policy-version", "policy-fingerprint", Ordered(Operations, reverse), Ordered(Capabilities, reverse),
                Ordered(WritableAliases, reverse), Ordered(ReadOnlyAliases, reverse), Ordered(ManagedRefs, reverse),
                AgentExecutionAuthorityRecord.CurrentSchemaVersion, new(profileId, projectId, lifetimeId))
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
            Assert.Equal(AgentExecutionAuthorityRecord.CurrentSchemaVersion, governance.EffectiveSchemaVersion);
            Assert.Equal(new AgentProjectStructureLifetime(profileId, projectId, lifetimeId), governance.SourceProjectLifetime);
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
