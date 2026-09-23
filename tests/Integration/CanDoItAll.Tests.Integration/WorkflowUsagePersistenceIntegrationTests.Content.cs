using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class WorkflowUsagePersistenceIntegrationTests {
    private static readonly JsonSerializerOptions ContentJsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostgreSql_replay_preserves_current_origin_and_ordered_attempts_after_owner_restart(bool nativeAdmission) {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        var original = CreateScopedObservation(history, nativeAdmission);
        var restoredInput = RoundTripContent(original);
        Assert.NotSame(original.Origin!.StructureAuthority!.ProjectScope, restoredInput.Origin!.StructureAuthority!.ProjectScope);
        Assert.NotSame(original.Origin.StructureAuthority.AgentGovernance, restoredInput.Origin.StructureAuthority.AgentGovernance);
        Assert.Equal(original.HistoryEvidence, restoredInput.HistoryEvidence);

        await CreateContentStore(history).AppendRangeAsync([original, restoredInput]);
        var before = await ReadContentRowsAsync(history);
        var row = Assert.Single(before);
        Assert.Equal(JsonSerializer.Serialize(original.Origin, ContentJsonOptions), row.OriginJson);
        Assert.Equal(RecordedAtUtc, row.RecordedAtUtc);
        Assert.Equal(RecordedAtUtc.AddSeconds(-1), row.StartedAtUtc);
        Assert.Equal(RecordedAtUtc, row.CompletedAtUtc);

        var restarted = CreateContentStore(history);
        await restarted.AppendAsync(original);
        await restarted.AppendAsync(restoredInput);
        var persisted = Assert.Single(await restarted.ListAsync(new() { RunIds = [original.RunId!.Value] }));
        await CreateContentStore(history).AppendAsync(persisted);
        Assert.Equal(before, await ReadContentRowsAsync(history));
        Assert.Equal(original.HistoryEvidence, persisted.HistoryEvidence);
        Assert.Equal(1, await history.Processor.ProcessAsync(history.Partition, 20, default));
        await using (var db = history.Factory.CreateDbContext()) {
            var entries = await db.Set<HistoryEntryRow>().AsNoTracking().ToListAsync();
            Assert.Equal(original.HistoryEvidence!.Attempts.Select(attempt => attempt.Id.Value).Order(),
                entries.Select(entry => entry.Id).Order());
        }
        await CreateContentStore(history).AppendAsync(RoundTripContent(persisted));
        Assert.Equal(0, await history.Processor.ProcessAsync(history.Partition, 20, default));
        Assert.Equal(before, await ReadContentRowsAsync(history));
    }

    [Theory]
    [InlineData(ContentChange.Tokens)]
    [InlineData(ContentChange.Cost)]
    [InlineData(ContentChange.ProjectLifetime)]
    [InlineData(ContentChange.AgentGrants)]
    [InlineData(ContentChange.AdmissionTargets)]
    [InlineData(ContentChange.NativeAdmissionIdentity)]
    [InlineData(ContentChange.AttemptOrder)]
    [InlineData(ContentChange.AttemptUsage)]
    public async Task PostgreSql_rejects_changed_fact_after_restart_without_partial_usage_or_history(ContentChange change) {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        var original = CreateScopedObservation(history, nativeAdmission: true);
        await CreateContentStore(history).AppendAsync(original);
        var before = await ReadContentRowsAsync(history);
        await using var db = history.Factory.CreateDbContext();
        var outboxBefore = await db.Set<HistoryOutboxRow>().AsNoTracking().Select(row => row.Id).ToArrayAsync();
        var changed = ChangeContent(RoundTripContent(original), change);
        var additional = original with { Id = WorkflowUsageObservationId.New() };

        await Assert.ThrowsAsync<WorkflowUsageObservationConflictException>(() =>
            CreateContentStore(history).AppendRangeAsync([additional, changed]));

        Assert.Equal(before, await ReadContentRowsAsync(history));
        Assert.Equal(outboxBefore, await db.Set<HistoryOutboxRow>().AsNoTracking().Select(row => row.Id).ToArrayAsync());
        var retained = Assert.Single(await CreateContentStore(history).ListAsync(new() { RunIds = [original.RunId!.Value] }));
        Assert.Equal(original.HistoryEvidence, retained.HistoryEvidence);
        Assert.Equal(JsonSerializer.Serialize(original.Origin, ContentJsonOptions),
            JsonSerializer.Serialize(retained.Origin, ContentJsonOptions));
    }

    [Fact]
    public async Task PostgreSql_rejects_conflicting_clones_in_one_new_batch_before_any_owner_write() {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        var original = CreateScopedObservation(history, nativeAdmission: true);
        var changed = ChangeContent(RoundTripContent(original), ContentChange.ProjectLifetime);

        await Assert.ThrowsAsync<WorkflowUsageObservationConflictException>(() =>
            CreateContentStore(history).AppendRangeAsync([original, changed]));

        Assert.Empty(await ReadContentRowsAsync(history));
        await using var db = history.Factory.CreateDbContext();
        Assert.Empty(await db.Set<HistoryOutboxRow>().ToListAsync());
    }

    public enum ContentChange {
        Tokens,
        Cost,
        ProjectLifetime,
        AgentGrants,
        AdmissionTargets,
        NativeAdmissionIdentity,
        AttemptOrder,
        AttemptUsage
    }

    private static PersistentWorkflowUsageObservationStore CreateContentStore(HistoryPersistenceTestDatabase history) =>
        new(WorkflowOwnerPersistenceTestFactory.FromCanonical(history.Factory),
            new(history.Partitions, history.Outbox), history.Transactions);

    private static WorkflowUsageObservation RoundTripContent(WorkflowUsageObservation observation) =>
        JsonSerializer.Deserialize<WorkflowUsageObservation>(JsonSerializer.Serialize(observation, ContentJsonOptions), ContentJsonOptions)!;

    private static async Task<ContentRow[]> ReadContentRowsAsync(HistoryPersistenceTestDatabase history) {
        await using var db = history.Factory.CreateDbContext();
        return await db.Set<WorkflowUsageObservationRecordEntity>().AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new ContentRow(row.Id, row.OriginJson, row.HistoryEvidenceJson,
                row.StartedAtUtc, row.CompletedAtUtc, row.RecordedAtUtc, row.InputTokens, row.TotalTokens, row.CostUsd))
            .ToArrayAsync();
    }

    private sealed record ContentRow(Guid Id, string OriginJson, string HistoryEvidenceJson,
        DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, DateTimeOffset RecordedAtUtc,
        int InputTokens, int TotalTokens, decimal? CostUsd);

    private static WorkflowUsageObservation CreateScopedObservation(HistoryPersistenceTestDatabase history, bool nativeAdmission) {
        var profileId = history.Profile.Profile.Id;
        var project = new WorkflowProjectLifetime(profileId, Guid.NewGuid(), Guid.NewGuid());
        var anotherProject = new WorkflowProjectLifetime(profileId, Guid.NewGuid(), Guid.NewGuid());
        var agentId = Guid.NewGuid();
        var runId = WorkflowRunId.New();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, agentId.ToString("D"));
        var governance = new AgentExecutionGovernanceSnapshot(new(Guid.NewGuid()), agentId, profileId,
            new(4), WorkspaceScopeDescriptor.Organization(profileId.ToString("N")), true, true, "fixture-v1", "original-policy",
            ["read-alpha", "read-beta"], ["fixture-a", "fixture-b"], ["write-a", "write-b"],
            ["read-a", "read-b"], ["artifact-a", "artifact-b"]);
        var authority = new WorkflowStructureAuthority(WorkflowStructureAuthorityChannel.AgentExecution,
            actor, profileId, project.ProjectId, true, true, RecordedAtUtc.AddHours(1), "original-source") {
            AgentGovernance = governance,
            ProjectIds = [anotherProject.ProjectId, project.ProjectId],
            ProjectScope = new([anotherProject, project], [project.ProjectId, anotherProject.ProjectId],
                workflowStartCapabilityId: Guid.NewGuid())
        };
        WorkflowLaunchOrigin origin = nativeAdmission
            ? new WorkflowLaunchOrigin.ProjectStructureNode(project.ProjectId, new("workflow-parent"), actor,
                new("original-session"), new("original-correlation")) {
                StructureAdmission = new(Guid.NewGuid(), runId, 9, Guid.NewGuid(), "original-binding",
                    new("workflow-parent"), WorkflowStructureOutputRole.RequiredResult, authority) {
                    LeaseOwner = new(agentId.ToString("D"), "Fixture Agent", "fixture-host", "fixture-root", "fixture-branch", "fixture-session")
                }
            }
            : new WorkflowLaunchOrigin.AgentRuntimeInvocation(actor, new("original-session"), "fixture-read", new("original-correlation"));
        origin = origin with {
            StructureAuthority = authority,
            AuthorizationScope = governance.WorkspaceScope,
            AuthorizationPolicyFingerprint = "original-read-policy",
            HistoryCaller = new(HistoryAuthenticationKind.ManagedCredential, new(Guid.NewGuid()), "fixture-issuer", "fixture-subject")
        };
        var start = history.Start();
        var first = HistoryAttemptEvidence.Create(start, history.Completion());
        var second = first with {
            Id = HistoryEntryId.New(),
            AttemptId = ProviderAttemptId.New(),
            Usage = first.Usage with { InputTokens = 12 }
        };
        return CreateObservation(WorkflowUsageObservationId.New(), runId, WorkflowId.New(),
            WorkflowVersionId.New(), "model", WorkflowPricingStatus.Known, 0.01m, 22) with {
                Origin = origin,
                StartedAtUtc = RecordedAtUtc.AddSeconds(-1).AddTicks(7).ToOffset(TimeSpan.FromHours(2)),
                CompletedAtUtc = RecordedAtUtc.AddTicks(7).ToOffset(TimeSpan.FromHours(2)),
                RecordedAtUtc = RecordedAtUtc.AddTicks(7).ToOffset(TimeSpan.FromHours(2)),
                HistoryEvidence = new(start.RequestId, true, [first, second])
            };
    }

    private static WorkflowUsageObservation ChangeContent(WorkflowUsageObservation observation, ContentChange change) {
        var authority = observation.Origin!.StructureAuthority!;
        var scope = authority.ProjectScope!;
        var evidence = observation.HistoryEvidence!;
        return change switch {
            ContentChange.Tokens => observation with { InputTokens = observation.InputTokens + 1, TotalTokens = observation.TotalTokens + 1 },
            ContentChange.Cost => observation with { CostUsd = observation.CostUsd + 0.01m },
            ContentChange.ProjectLifetime => observation with {
                Origin = observation.Origin with {
                    StructureAuthority = authority with {
                        ProjectScope = new(scope.Projects.Select(project => project.ProjectId == authority.ProjectId
                                ? new WorkflowProjectLifetime(project.DatabaseProfileId, project.ProjectId, Guid.NewGuid()) : project).ToArray(),
                            scope.AdmissionProjectIds, scope.SchemaVersion, scope.WorkflowStartCapabilityId)
                    }
                }
            },
            ContentChange.AgentGrants => observation with {
                Origin = observation.Origin with { StructureAuthority = authority with { AgentGovernance = WithoutOperation(authority.AgentGovernance!) } }
            },
            ContentChange.AdmissionTargets => observation with {
                Origin = observation.Origin with {
                    StructureAuthority = authority with {
                        ProjectScope = new(scope.Projects, [authority.ProjectId], scope.SchemaVersion, scope.WorkflowStartCapabilityId)
                    }
                }
            },
            ContentChange.NativeAdmissionIdentity => observation with {
                Origin = ((WorkflowLaunchOrigin.ProjectStructureNode)observation.Origin) with {
                    StructureAdmission = ((WorkflowLaunchOrigin.ProjectStructureNode)observation.Origin).StructureAdmission! with { IntentId = Guid.NewGuid() }
                }
            },
            ContentChange.AttemptOrder => observation with { HistoryEvidence = new(evidence.RequestId, true, evidence.Attempts.Reverse().ToArray()) },
            ContentChange.AttemptUsage => observation with {
                HistoryEvidence = new(evidence.RequestId, true,
                    [evidence.Attempts[0] with { Usage = evidence.Attempts[0].Usage with { OutputTokens = 99 } }, evidence.Attempts[1]])
            },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
    }

    private static AgentExecutionGovernanceSnapshot WithoutOperation(AgentExecutionGovernanceSnapshot original) =>
        new(original.AuthorityId, original.AgentId, original.DatabaseProfileId, original.DatabaseProfileGeneration,
            original.WorkspaceScope, original.ReadAllowed, original.MutationAllowed, original.PolicyVersion, original.PolicyFingerprint,
            ["read-alpha"], original.AllowedCapabilityKeys.ToArray(), original.WritableExternalTargetAliases.ToArray(),
            original.ReadOnlyExternalTargetAliases.ToArray(), original.AllowedManagedArtifactReadRefs.ToArray());
}
