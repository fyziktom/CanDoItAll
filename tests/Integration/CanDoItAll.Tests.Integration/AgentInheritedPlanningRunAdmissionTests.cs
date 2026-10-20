using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedPlanningRunAdmissionTests {
    private static readonly AgentToolSemanticDigest RuntimeContract = AgentPlanningDecisionTestData.Envelope("{\"runtime\":1}").Digest;

    [Fact]
    public async Task New_admission_binds_the_original_scope_contract_input_and_parent_before_publication() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        ExecutionRunRecord? validated = null;
        var admitted = await Owner(fixture).AdmitAsync(id, (run, _) => {
            Assert.NotNull(run.ToolAdmission?.PlanningDecision?.InheritedOrigin);
            validated = run;
            return Task.CompletedTask;
        }, _ => Task.FromResult(Preparation(fixture, id)));

        Assert.True(admitted.Created);
        Assert.Equal(id, admitted.Run.Id);
        Assert.Equal(RuntimeContract, AgentPlanningRunMetadata.Read(admitted.Run));
        Assert.Equal(fixture.Files.StorageScope, AgentPlanningRunMetadata.ReadScope(admitted.Run));
        Assert.Equal(JsonSerializer.Serialize(validated!.ToolAdmission!), JsonSerializer.Serialize(admitted.Run.ToolAdmission!));
        Assert.Equal("Prepare the original bounded plan.", admitted.Run.ToolAdmission!.BackgroundInput!.Content);
        Assert.Null(admitted.Run.ChatSessionId);
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(admitted.Run.MetadataJson));
        Assert.Empty((await fixture.Files.NewStore().GetExecutionRunDetailAsync(id))!.ExecutionLog);
    }

    [Fact]
    public async Task Independent_same_child_race_prepares_once_while_another_child_can_progress() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparations = 0;
        var first = Owner(fixture).AdmitAsync(id, RequireParent(fixture), async _ => {
            Interlocked.Increment(ref preparations);
            entered.SetResult();
            await release.Task;
            return Preparation(fixture, id);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var second = Owner(fixture).AdmitAsync(id, RequireParent(fixture), _ => {
            Interlocked.Increment(ref preparations);
            return Task.FromResult(Preparation(fixture, id));
        });
        try {
            var otherId = Guid.NewGuid();
            var other = await Owner(fixture).AdmitAsync(otherId, RequireParent(fixture),
                _ => Task.FromResult(Preparation(fixture, otherId))).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(other.Created);
            Assert.False(second.IsCompleted);
        } finally {
            release.SetResult();
        }
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(1, preparations);
        Assert.Single(results, result => result.Created);
        Assert.Equal(results[0].Run.ToolAdmission!.Session, results[1].Run.ToolAdmission!.Session);
        Assert.Equal(results[0].Run.ToolAdmission.PlanningDecision!.InheritedOrigin, results[1].Run.ToolAdmission.PlanningDecision!.InheritedOrigin);
    }

    [Theory]
    [InlineData((int)GenericNewRunCommitStage.JournalPersisted)]
    [InlineData((int)GenericNewRunCommitStage.ExecutionSlicesPersisted)]
    [InlineData((int)GenericNewRunCommitStage.ExecutionIndexPersisted)]
    [InlineData((int)GenericNewRunCommitStage.UsageIndexPersisted)]
    [InlineData((int)GenericNewRunCommitStage.WorkspaceIndexPersisted)]
    [InlineData((int)GenericNewRunCommitStage.ChatIndexPersisted)]
    public async Task Lost_initial_ack_recovers_the_original_child_without_preparing_a_new_input_or_grant(int stageValue) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var failure = new IOException("inherited-initial-ack");
        var store = new FileSandboxWorkspaceStore(fixture.Files.WorkspaceRoot, fixture.Files.StorageScope,
            chatBackedRunCommitBoundary: null, existingRunDetailCommitBoundary: null,
            genericNewRunCommitBoundary: stage => {
                if (stage == (GenericNewRunCommitStage)stageValue) {
                    throw failure;
                }
            }, jsonReadDiagnostics: null);
        ExecutionRunRecord? original = null;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => Owner(fixture, store).AdmitAsync(id, (run, _) => {
            original = run;
            return Task.CompletedTask;
        }, _ => Task.FromResult(Preparation(fixture, id)))));

        var recovered = await Owner(fixture).AdmitAsync(id, RequireParent(fixture), NoPreparation);
        Assert.False(recovered.Created);
        Assert.Equal(original!.MetadataJson, recovered.Run.MetadataJson);
        Assert.Equal(JsonSerializer.Serialize(original.ToolAdmission!), JsonSerializer.Serialize(recovered.Run.ToolAdmission!));
        Assert.Equal(RuntimeContract, AgentPlanningRunMetadata.Read(recovered.Run));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Parent_denial_prevents_new_publication_and_cannot_replace_an_original(bool retained) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var original = retained ? await Owner(fixture).AdmitAsync(id, RequireParent(fixture),
            _ => Task.FromResult(Preparation(fixture, id))) : null;
        var denial = new AgentToolAdmissionException("parent-denied", "The canonical parent refused this child.");
        Assert.Same(denial, await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Owner(fixture).AdmitAsync(id,
            (_, _) => throw denial, retained ? NoPreparation : _ => Task.FromResult(Preparation(fixture, id)))));
        var saved = await fixture.Files.NewStore().GetExecutionRunAsync(id);
        if (original is null) {
            Assert.Null(saved);
        } else {
            Assert.NotNull(saved);
            Assert.Equal(JsonSerializer.Serialize(original.Run.ToolAdmission!.PlanningDecision), JsonSerializer.Serialize(saved.ToolAdmission!.PlanningDecision));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retained_read_requires_the_original_source_but_not_new_dispatch_permission(bool readDenied) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var original = await Owner(fixture).AdmitAsync(id, RequireParent(fixture), _ => Task.FromResult(Preparation(fixture, id)));
        fixture.Policy.DispatchAllowed = false;
        fixture.Policy.ReadAllowed = !readDenied;
        if (readDenied) {
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => Owner(fixture).AdmitAsync(id, RequireParent(fixture), NoPreparation));
        } else {
            var recovered = await Owner(fixture).AdmitAsync(id, RequireParent(fixture), NoPreparation);
            Assert.Equal(JsonSerializer.Serialize(original.Run.ToolAdmission!.PlanningDecision), JsonSerializer.Serialize(recovered.Run.ToolAdmission!.PlanningDecision));
        }
    }

    [Fact]
    public async Task Retained_open_work_remains_unsettled_and_cannot_start_another_interval() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var admitted = await Owner(fixture).AdmitAsync(id, RequireParent(fixture), _ => Task.FromResult(Preparation(fixture, id)));
        var live = new AgentPlanningDecisionOwner(fixture.NewJournal());
        await using (var lease = await live.StartInheritedAsync(AgentPlanningDecisionExpectation.From(admitted.Run), AgentInheritedPlanningFixture.AllowRead, default)) {
            Assert.NotNull(lease.PlanningWork);
        }
        var original = (await fixture.Files.NewStore().GetExecutionRunAsync(id))!;
        var retained = await Owner(fixture).AdmitAsync(id, RequireParent(fixture), NoPreparation);
        Assert.Equal(JsonSerializer.Serialize(original.ToolAdmission!), JsonSerializer.Serialize(retained.Run.ToolAdmission!));
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => live.StartInheritedAsync(
            AgentPlanningDecisionExpectation.From(retained.Run), AgentInheritedPlanningFixture.AllowRead, default));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(96)]
    public async Task Retained_lookup_reads_the_exact_child_without_unrelated_history(int historyCount) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        await Owner(fixture).AdmitAsync(id, RequireParent(fixture), _ => Task.FromResult(Preparation(fixture, id)));
        var history = Enumerable.Range(0, historyCount).Select(_ => Preparation(fixture, Guid.NewGuid()).Candidate).ToArray();
        await fixture.Files.Store.UpdateExecutionAsync(current => current with { ExecutionRuns = [.. current.ExecutionRuns, .. history] });
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
        var store = new FileSandboxWorkspaceStore(fixture.Files.WorkspaceRoot, fixture.Files.StorageScope, null, null,
            new FileSandboxWorkspaceJsonReadDiagnostics(reads.Add));
        var retained = await Owner(fixture, store).AdmitAsync(id, RequireParent(fixture), NoPreparation);
        Assert.False(retained.Created);
        Assert.Single(reads, read => read.PayloadType == typeof(ExecutionRunRecord));
        Assert.DoesNotContain(reads, read => history.Any(run => read.FullPath.Contains(run.Id.ToString("N"), StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Cancelled_lease_wait_never_prepares_or_creates_the_child() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        await using var held = await fixture.Files.NewStore().AcquireToolDispatchLeaseAsync(id);
        using var cancellation = new CancellationTokenSource();
        var waiting = Owner(fixture).AdmitAsync(id, RequireParent(fixture), NoPreparation, cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Null(await fixture.Files.NewStore().GetExecutionRunAsync(id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lease_release_failure_preserves_any_primary_failure_and_allows_exact_recovery(bool denyParent) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var store = fixture.Files.NewStore();
        var releaseFailure = new IOException("inherited-release-failed");
        var parentFailure = new AgentToolAdmissionException("parent-denied", "The canonical parent refused this child.");
        var owner = new AgentInheritedPlanningRunAdmission(store, new ReleaseFault(store, releaseFailure), fixture.NewJournal(store));
        var attempted = owner.AdmitAsync(id, denyParent ? (_, _) => throw parentFailure : RequireParent(fixture),
            _ => Task.FromResult(Preparation(fixture, id)));
        if (denyParent) {
            var failure = await Assert.ThrowsAsync<AggregateException>(() => attempted);
            Assert.Equal(new Exception[] { parentFailure, releaseFailure }, failure.InnerExceptions);
            Assert.Null(await store.GetExecutionRunAsync(id));
        } else {
            Assert.Same(releaseFailure, await Assert.ThrowsAsync<IOException>(() => attempted));
            Assert.False((await Owner(fixture).AdmitAsync(id, RequireParent(fixture), NoPreparation)).Created);
        }
    }

    [Theory]
    [InlineData("wrong-id")]
    [InlineData("chat")]
    [InlineData("auto-approve")]
    [InlineData("completed")]
    [InlineData("session")]
    [InlineData("contract")]
    public async Task Invalid_new_candidate_is_refused_before_initial_publication(string substitution) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var preparation = Preparation(fixture, id);
        preparation = preparation with { Candidate = substitution switch {
            "wrong-id" => preparation.Candidate with { Id = Guid.NewGuid() },
            "chat" => preparation.Candidate with { ChatSessionId = Guid.NewGuid() },
            "auto-approve" => preparation.Candidate with { AutoApprovePendingToolCalls = true },
            "completed" => preparation.Candidate with { CompletedAtUtc = fixture.Clock.Now },
            "session" => preparation.Candidate with { SerializedSessionStateJson = "{}" },
            "contract" => preparation.Candidate with { MetadataJson = AgentPlanningRunMetadata.ApplyInherited("{}",
                preparation.Owner, RuntimeContract, preparation.Scope) },
            _ => throw new ArgumentOutOfRangeException(nameof(substitution))
        } };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Owner(fixture).AdmitAsync(id, RequireParent(fixture), _ => Task.FromResult(preparation)));
        Assert.Null(await fixture.Files.NewStore().GetExecutionRunAsync(id));
    }

    [Fact]
    public async Task Existing_unrelated_execution_is_not_promoted_to_inherited_planning() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Owner(fixture).AdmitAsync(fixture.Files.Detail.Run.Id,
            RequireParent(fixture), NoPreparation));
        Assert.Equal(fixture.Files.Detail.Run.ToolAdmission!.SchemaVersion,
            (await fixture.Files.NewStore().GetExecutionRunAsync(fixture.Files.Detail.Run.Id))!.ToolAdmission!.SchemaVersion);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("digest")]
    [InlineData("scope")]
    public async Task Changed_retained_runtime_metadata_is_not_authorized_for_read(string substitution) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        await Owner(fixture).AdmitAsync(id, RequireParent(fixture), _ => Task.FromResult(Preparation(fixture, id)));
        var detail = (await fixture.Files.NewStore().GetExecutionRunDetailAsync(id))!;
        var metadata = JsonNode.Parse(detail.Run.MetadataJson)!;
        var contract = metadata["agentPlanningRuntimeContract"]!;
        if (substitution == "version") {
            contract["Version"] = 2;
        } else if (substitution == "digest") {
            contract["Digest"] = JsonNode.Parse("{\"Value\":\"" + new string('a', 64) + "\"}");
        } else {
            contract["Scope"]!["Kind"] = (int)WorkspaceScopeKind.Project;
            contract["Scope"]!["Key"] = "another-project";
        }
        await fixture.Files.NewStore().SaveExecutionRunDetailAsync(detail with { Run = detail.Run with { MetadataJson = metadata.ToJsonString() } });
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Owner(fixture).AdmitAsync(id, RequireParent(fixture), NoPreparation));
    }

    private static AgentInheritedPlanningRunAdmission Owner(AgentInheritedPlanningFixture fixture, FileSandboxWorkspaceStore? store = null) {
        store ??= fixture.Files.NewStore();
        return new(store, store, fixture.NewJournal(store));
    }

    private static AgentInheritedPlanningRunPreparation Preparation(AgentInheritedPlanningFixture fixture, Guid id) {
        var decision = fixture.Original.ToolAdmission!.PlanningDecision!;
        return new(fixture.Original with { Id = id, MetadataJson = "{}", ToolAdmission = null }, "Prepare the original bounded plan.",
            decision.Owner, RuntimeContract, fixture.Files.StorageScope, session => new(decision.OriginalTarget,
                decision.InheritedOrigin! with { Source = session.Reference.BackgroundSource! }));
    }

    private static Func<ExecutionRunRecord, CancellationToken, Task> RequireParent(AgentInheritedPlanningFixture fixture)
        => (run, _) => {
            var original = fixture.Original.ToolAdmission!.PlanningDecision!;
            var decision = run.ToolAdmission!.PlanningDecision!;
            Assert.Equal(original.OriginalTarget, decision.OriginalTarget);
            Assert.Equal(original.InheritedOrigin! with { Source = decision.InheritedOrigin!.Source }, decision.InheritedOrigin);
            return Task.CompletedTask;
        };

    private static Task<AgentInheritedPlanningRunPreparation> NoPreparation(CancellationToken _)
        => throw new InvalidOperationException("An original child must be resolved before preparation.");

    private sealed class ReleaseFault(ISandboxWorkspaceExecutionRunLeaseStore store, Exception failure) : ISandboxWorkspaceExecutionRunLeaseStore {
        public async ValueTask<IAsyncDisposable> AcquireToolDispatchLeaseAsync(Guid id, CancellationToken cancellationToken)
            => new Handle(await store.AcquireToolDispatchLeaseAsync(id, cancellationToken), failure);

        private sealed class Handle(IAsyncDisposable lease, Exception failure) : IAsyncDisposable {
            public async ValueTask DisposeAsync() {
                await lease.DisposeAsync();
                throw failure;
            }
        }
    }
}
