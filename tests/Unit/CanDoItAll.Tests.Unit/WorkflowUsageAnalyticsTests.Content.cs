using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkflowUsageAnalyticsTests {
    private static readonly JsonSerializerOptions ContentJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Memory_usage_accepts_independently_restored_scoped_authority_in_batch_and_replay() {
        var original = CreateScopedUsageObservation();
        var restored = RoundTripUsage(original);
        var store = new InMemoryWorkflowUsageObservationStore();
        Assert.NotSame(original.Origin!.StructureAuthority!.ProjectScope, restored.Origin!.StructureAuthority!.ProjectScope);
        Assert.NotSame(original.Origin.StructureAuthority.AgentGovernance, restored.Origin.StructureAuthority.AgentGovernance);

        await store.AppendRangeAsync([original, restored]);
        await store.AppendAsync(RoundTripUsage(restored));

        var retained = Assert.Single(await store.ListAsync(new() { RunIds = [original.RunId!.Value] }));
        Assert.Same(original, retained);
    }

    [Theory]
    [InlineData(UsageContentChange.Tokens)]
    [InlineData(UsageContentChange.ProjectLifetime)]
    [InlineData(UsageContentChange.RemovedOrigin)]
    public async Task Memory_usage_rejects_changed_restored_fact_without_partial_append(UsageContentChange change) {
        var original = CreateScopedUsageObservation();
        var restored = RoundTripUsage(original);
        var authority = restored.Origin!.StructureAuthority!;
        var project = Assert.Single(authority.ProjectScope!.Projects);
        var changed = change switch {
            UsageContentChange.Tokens => restored with { InputTokens = restored.InputTokens + 1, TotalTokens = restored.TotalTokens + 1 },
            UsageContentChange.ProjectLifetime => restored with {
                Origin = restored.Origin with {
                    StructureAuthority = authority with {
                        ProjectScope = new([new(project.DatabaseProfileId, project.ProjectId, Guid.NewGuid())], [project.ProjectId])
                    }
                }
            },
            UsageContentChange.RemovedOrigin => restored with { Origin = null },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var store = new InMemoryWorkflowUsageObservationStore();
        await store.AppendAsync(original);

        await Assert.ThrowsAsync<WorkflowUsageObservationConflictException>(() =>
            store.AppendRangeAsync([original with { Id = WorkflowUsageObservationId.New() }, changed]));

        Assert.Same(original, Assert.Single(await store.ListAsync(new() { RunIds = [original.RunId!.Value] })));
    }

    [Fact]
    public async Task Memory_usage_keeps_legacy_null_origin_repeat_compatible() {
        var original = CreateObservation(WorkflowUsageObservationId.New(), WorkflowRunId.New());
        var store = new InMemoryWorkflowUsageObservationStore();

        await store.AppendRangeAsync([original, RoundTripUsage(original)]);
        await store.AppendAsync(RoundTripUsage(original));

        Assert.Same(original, Assert.Single(await store.ListAsync(new() { RunIds = [original.RunId!.Value] })));
    }

    public enum UsageContentChange {
        Tokens,
        ProjectLifetime,
        RemovedOrigin
    }

    private static WorkflowUsageObservation RoundTripUsage(WorkflowUsageObservation observation) =>
        JsonSerializer.Deserialize<WorkflowUsageObservation>(JsonSerializer.Serialize(observation, ContentJsonOptions), ContentJsonOptions)!;

    private static WorkflowUsageObservation CreateScopedUsageObservation() {
        var profileId = Guid.NewGuid();
        var project = new WorkflowProjectLifetime(profileId, Guid.NewGuid(), Guid.NewGuid());
        var agentId = Guid.NewGuid();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, agentId.ToString("D"));
        var governance = new AgentExecutionGovernanceSnapshot(new(Guid.NewGuid()), agentId, profileId, new(2),
            WorkspaceScopeDescriptor.Organization(profileId.ToString("N")), true, true, "fixture-v1", "original-policy",
            ["read-a", "read-b"], ["capability-a", "capability-b"]);
        var authority = new WorkflowStructureAuthority(WorkflowStructureAuthorityChannel.AgentExecution,
            actor, profileId, project.ProjectId, true, true, FixedUtcNow.AddHours(1), "original-source") {
            AgentGovernance = governance,
            ProjectIds = [project.ProjectId],
            ProjectScope = new([project], [project.ProjectId])
        };
        return CreateObservation(WorkflowUsageObservationId.New(), WorkflowRunId.New()) with {
            Origin = new WorkflowLaunchOrigin.AgentRuntimeInvocation(actor, new("original-session"), "fixture-read", new("original-correlation")) {
                StructureAuthority = authority,
                AuthorizationScope = governance.WorkspaceScope,
                AuthorizationPolicyFingerprint = "original-policy"
            }
        };
    }
}
