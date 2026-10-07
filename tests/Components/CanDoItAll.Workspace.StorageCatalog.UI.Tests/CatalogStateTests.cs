using System.Collections.Immutable;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;
using CanDoItAll.Workspace.StorageCatalog.UI;
using CanDoItAll.Workspace.StorageCatalog.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceStorageCatalogUi;

public sealed class CatalogStateTests {
    [Theory]
    [InlineData(CatalogEffect.Save)]
    [InlineData(CatalogEffect.Test)]
    public async Task Older_operation_readback_cannot_replace_a_newer_test_for_the_unchanged_draft(CatalogEffect firstEffect) {
        var store = new CatalogScenarioStore();
        var owner = new DelayedObservationOwner(store);
        using var session = new CatalogSession(owner, new());
        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        owner.HoldNextRead = true;
        var first = session.MutateAsync(firstEffect);
        await owner.Entered.Task;
        await session.MutateAsync(CatalogEffect.Test);
        var latest = session.Draft!.Health;
        Assert.Equal(CatalogHealth.Healthy, latest.Status);
        Assert.Equal(CatalogScenarioData.TestedAt.AddSeconds(store.DriverCalls), latest.TestedAtUtc);
        owner.Release.SetResult();
        await first;
        Assert.Equal(latest, session.Draft.Health);
        Assert.Equal(2, session.Receipts.Count());
        Assert.Equal(firstEffect == CatalogEffect.Test ? 2 : 1, store.DriverCalls);
    }

    [Theory]
    [InlineData(CatalogEffect.Test, CatalogScenario.Representative)]
    [InlineData(CatalogEffect.Save, CatalogScenario.PartialRouting)]
    public async Task Readback_preserves_routing_choices_that_the_operation_did_not_acknowledge(CatalogEffect effect, CatalogScenario scenario) {
        var store = new CatalogScenarioStore(scenario);
        using var session = new CatalogSession(store, new());
        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        var draft = session.Draft!;
        draft.TogglePurpose(CatalogPurpose.Evidence, true);
        await session.MutateAsync(effect);
        Assert.Same(draft, session.Draft);
        Assert.Equal(CatalogWrite.Committed, Assert.Single(session.Receipts).Outcome!.Write);
        Assert.Contains(CatalogPurpose.Evidence, draft.DefaultPurposes);
        Assert.DoesNotContain((await store.ReadEditorAsync(draft.Id!.Value, default))!.DefaultPurposes, purpose => purpose == CatalogPurpose.Evidence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_lane_rejects_stale_success_error_and_finally_and_disposes_exact_sources(bool fails) {
        var first = new TaskCompletionSource<int>();
        var second = new TaskCompletionSource<int>();
        CancellationToken successor = default;
        using var lane = new CatalogReadLane<int>(0, () => true, () => { });
        var old = lane.ReadAsync(_ => first.Task);
        var current = lane.ReadAsync(token => {
            successor = token;
            return second.Task;
        });
        if (fails) {
            first.SetException(new InvalidOperationException("stale"));
        } else {
            first.SetResult(7);
        }
        await old;
        Assert.True(lane.IsLoading);
        Assert.Empty(lane.Error);
        lane.Dispose();
        Assert.True(successor.IsCancellationRequested);
        second.SetResult(9);
        await current;
        lane.Dispose();
        Assert.Equal(0, lane.Value);
    }

    [Fact]
    public async Task Exact_missing_and_failed_targets_never_become_new_and_same_id_can_retry() {
        var store = new CatalogScenarioStore(CatalogScenario.ReadFailure);
        using var session = new CatalogSession(store, new());
        await session.LoadAsync();
        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        Assert.Null(session.Draft);
        Assert.False(session.CanMutate);
        Assert.NotEmpty(session.EditorRead.Error);
        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        Assert.Equal(CatalogScenarioData.FileSystemId, session.Draft!.Id);
        await session.SelectAsync(Guid.NewGuid());
        Assert.Null(session.Draft);
        Assert.False(session.CanMutate);
        session.New(CatalogProvider.Ipfs);
        Assert.Null(session.Draft!.Id);
        Assert.True(session.CanMutate);
    }

    [Fact]
    public async Task Selection_A_B_A_and_new_do_not_accept_an_old_completion() {
        var store = new CatalogScenarioStore();
        using var session = new CatalogSession(store, new());
        store.Gates[CatalogScenarioStage.EditorRead].Hold();
        var first = session.SelectAsync(CatalogScenarioData.FileSystemId);
        var second = session.SelectAsync(CatalogScenarioData.FtpId);
        var third = session.SelectAsync(CatalogScenarioData.FileSystemId);
        session.New(CatalogProvider.Ipfs);
        var current = session.Draft;
        store.Gates[CatalogScenarioStage.EditorRead].Release();
        await Task.WhenAll(first, second, third);
        Assert.Same(current, session.Draft);
        Assert.Equal(CatalogProvider.Ipfs, session.Draft!.ProviderKind);
        Assert.False(session.EditorRead.IsLoading);
    }

    [Fact]
    public async Task Save_captures_deep_configuration_and_purposes_and_blocks_shared_routing_effects() {
        var store = new CatalogScenarioStore();
        using var session = new CatalogSession(store, new());
        await session.SelectAsync(CatalogScenarioData.FtpId);
        var draft = session.Draft!;
        draft.Name = "Original FTP";
        draft.EndpointOrRoot = "original.example.test";
        draft.PortText = "2121";
        draft.BasePath = "original/path";
        draft.Username = "original-user";
        draft.UseSsl = false;
        draft.UsePassiveMode = false;
        draft.DisplayOrderText = "42";
        draft.TogglePurpose(CatalogPurpose.Evidence, true);
        store.Gates[CatalogScenarioStage.Admission].Hold();
        var writing = session.MutateAsync(CatalogEffect.Save);
        draft.Name = "Later FTP";
        draft.EndpointOrRoot = "later.example.test";
        draft.PortText = "-";
        draft.TogglePurpose(CatalogPurpose.Evidence, false);
        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        await session.MutateAsync(CatalogEffect.Save);
        Assert.Single(session.Receipts);
        store.Gates[CatalogScenarioStage.Admission].Release();
        await writing;
        var saved = store.Entries[CatalogScenarioData.FtpId];
        Assert.Equal("Original FTP", saved.Name);
        Assert.Equal("original.example.test", saved.EndpointOrRoot);
        Assert.Equal(2121, saved.Port);
        Assert.Equal("original/path", saved.BasePath);
        Assert.Equal("original-user", saved.Username);
        Assert.False(saved.UseSsl);
        Assert.False(saved.UsePassiveMode);
        Assert.Equal(42, saved.DisplayOrder);
        Assert.Equal(CatalogScenarioData.SecretId, saved.CredentialSecretId);
        Assert.Contains(CatalogPurpose.Evidence, saved.DefaultPurposes);
        Assert.Equal(CatalogScenarioData.FileSystemId, session.Draft!.Id);
        Assert.Equal(1, store.CatalogWrites);
    }

    [Fact]
    public async Task Known_create_id_precedes_readback_and_field_revisions_preserve_edit_away_and_back() {
        var store = new CatalogScenarioStore();
        using var session = new CatalogSession(store, new());
        var draft = session.Draft!;
        draft.Name = " raw name ";
        draft.EndpointOrRoot = " /scenario/new ";
        draft.Step = 2;
        var editContext = draft.EditContext;
        store.Gates[CatalogScenarioStage.ReadBack].Hold();
        var writing = session.MutateAsync(CatalogEffect.Save);
        Assert.NotNull(draft.Id);
        Assert.Equal(draft.Id, Assert.Single(session.Receipts).Outcome!.CatalogId);
        Assert.False(writing.IsCompleted);
        draft.Name = "changed";
        draft.Name = " raw name ";
        draft.DisplayOrderText = "-";
        await session.RefreshSecretsAsync();
        store.Gates[CatalogScenarioStage.ReadBack].Release();
        await writing;
        Assert.Equal(" raw name ", draft.Name);
        Assert.Equal("/scenario/new", draft.EndpointOrRoot);
        Assert.Equal("-", draft.DisplayOrderText);
        Assert.Same(editContext, draft.EditContext);
        Assert.Equal(2, draft.Step);
        await session.MutateAsync(CatalogEffect.Save);
        Assert.Single(session.Receipts);
        Assert.Contains(draft.EditContext.GetValidationMessages(), text => text.Contains("complete integer", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Held_test_keeps_original_target_and_health_does_not_certify_edited_configuration(bool existing) {
        var store = new CatalogScenarioStore();
        using var session = new CatalogSession(store, new());
        if (existing) {
            await session.SelectAsync(CatalogScenarioData.FileSystemId);
        }
        var original = session.Draft!;
        original.EndpointOrRoot = "/scenario/tested";
        store.Gates[CatalogScenarioStage.Driver].Hold();
        var testing = session.MutateAsync(CatalogEffect.Test);
        original.ChangeProvider(session.Choices.Providers[1]);
        original.EndpointOrRoot = "https://new.example.test";
        store.Gates[CatalogScenarioStage.Driver].Release();
        await testing;
        Assert.Equal(CatalogHealth.Unknown, original.Health.Status);
        Assert.Equal(CatalogProvider.Ipfs, original.ProviderKind);
        Assert.Equal(existing ? 1 : 0, store.CatalogWrites);
        Assert.Equal(1, store.DriverCalls);
        Assert.Equal(CatalogHealth.Healthy, Assert.Single(session.Receipts).Outcome!.Health!.Status);
        if (existing) {
            Assert.Equal("/scenario/tested", store.Entries[CatalogScenarioData.FileSystemId].EndpointOrRoot);
            Assert.Equal(CatalogProvider.FileSystem, store.Entries[CatalogScenarioData.FileSystemId].ProviderKind);
        } else {
            Assert.Null(original.Id);
        }
    }

    [Theory]
    [InlineData(CatalogScenario.UnknownAcknowledgement, CatalogWrite.Unknown, CatalogRouting.NotAttempted, CatalogActivity.NotAttempted)]
    [InlineData(CatalogScenario.PartialRouting, CatalogWrite.Committed, CatalogRouting.PossiblyPartial, CatalogActivity.NotAttempted)]
    [InlineData(CatalogScenario.ActivityFailure, CatalogWrite.Committed, CatalogRouting.Complete, CatalogActivity.Failed)]
    [InlineData(CatalogScenario.ReadBackFailure, CatalogWrite.Committed, CatalogRouting.Complete, CatalogActivity.Complete)]
    [InlineData(CatalogScenario.Refused, CatalogWrite.NotAttempted, CatalogRouting.NotAttempted, CatalogActivity.NotAttempted)]
    public async Task Receipts_retain_actual_stages_and_observation_never_replays(CatalogScenario scenario, CatalogWrite write, CatalogRouting routing, CatalogActivity activity) {
        var store = new CatalogScenarioStore(scenario);
        using var session = new CatalogSession(store, new());
        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        session.Draft!.TogglePurpose(CatalogPurpose.Evidence, true);
        await session.MutateAsync(CatalogEffect.Save);
        var receipt = Assert.Single(session.Receipts);
        Assert.Equal(write, receipt.Outcome!.Write);
        Assert.Equal(routing, receipt.Outcome.Routing);
        Assert.Equal(activity, receipt.Outcome.Activity);
        var before = (store.CatalogWrites, store.RoutingWrites, store.DriverCalls, store.ActivityCalls);
        await session.ObserveAsync(receipt);
        Assert.Equal(before, (store.CatalogWrites, store.RoutingWrites, store.DriverCalls, store.ActivityCalls));
        if (scenario == CatalogScenario.UnknownAcknowledgement) {
            session.AcknowledgeCorrection(receipt);
            Assert.False(session.CanMutate);
        }
        if (scenario == CatalogScenario.PartialRouting) {
            Assert.False(session.CanMutate);
            session.AcknowledgeCorrection(receipt);
            Assert.True(session.CanMutate);
        }
        if (scenario == CatalogScenario.ReadBackFailure) {
            Assert.Contains("Read-back unavailable", receipt.Observation);
        }
    }

    [Fact]
    public async Task Retired_accepted_work_writes_only_original_store_and_retains_safe_receipt() {
        var old = new CatalogScenarioStore();
        var ledger = new CatalogOperationLedger();
        using var session = new CatalogSession(old, ledger);
        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        session.Draft!.Name = "Original accepted command";
        old.Gates[CatalogScenarioStage.Persistence].Hold();
        var writing = session.MutateAsync(CatalogEffect.Save);
        session.Dispose();
        old.IsCurrent = false;
        var successor = new CatalogScenarioStore();
        using var next = new CatalogSession(successor, ledger);
        old.Gates[CatalogScenarioStage.Persistence].Release();
        await writing;
        Assert.Equal("Original accepted command", old.Entries[CatalogScenarioData.FileSystemId].Name);
        Assert.Equal("Team artifacts", successor.Entries[CatalogScenarioData.FileSystemId].Name);
        Assert.Equal(CatalogWrite.Committed, Assert.Single(ledger.Receipts).Outcome!.Write);
        Assert.Empty(next.Receipts);
        Assert.Null(next.Draft!.Id);
    }

    [Fact]
    public async Task Delete_marks_original_without_recreating_it_and_system_targets_are_protected() {
        var store = new CatalogScenarioStore();
        using var session = new CatalogSession(store, new());
        await session.SelectAsync(CatalogScenarioData.SystemId);
        await session.MutateAsync(CatalogEffect.Delete);
        Assert.Empty(session.Receipts);
        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        await session.MutateAsync(CatalogEffect.Delete);
        Assert.True(session.Draft!.Deleted);
        await session.MutateAsync(CatalogEffect.Save);
        Assert.False(store.Entries.ContainsKey(CatalogScenarioData.FileSystemId));
        Assert.Single(session.Receipts);
    }

    [Fact]
    public void Receipt_capacity_never_evicts_unresolved_operations() {
        var ledger = new CatalogOperationLedger();
        for (var index = 0; index < CatalogOperationLedger.Capacity; index++) {
            Assert.NotNull(ledger.Admit(new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), 0), CatalogEffect.Save, new())));
        }
        Assert.Null(ledger.Admit(new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), 0), CatalogEffect.Save, new())));
        Assert.Equal(CatalogOperationLedger.Capacity, ledger.Receipts.Count);
    }

    private sealed class DelayedObservationOwner(CatalogScenarioStore store) : IStorageCatalogOwner {
        private CatalogHealthFact? latestHealth;
        public bool HoldNextRead { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CatalogContext Context => store.Context;
        public bool IsCurrent => store.IsCurrent;
        public CatalogChoices Choices => store.Choices;
        public Task<ImmutableArray<CatalogRow>> ReadCatalogAsync(CancellationToken cancellationToken) => store.ReadCatalogAsync(cancellationToken);
        public Task<ImmutableArray<CatalogSecret>> ReadSecretsAsync(CancellationToken cancellationToken) => store.ReadSecretsAsync(cancellationToken);
        public Task<ImmutableArray<CatalogRoute>> ReadRoutesAsync(CancellationToken cancellationToken) => store.ReadRoutesAsync(cancellationToken);
        public async Task<CatalogEdit?> ReadEditorAsync(Guid id, CancellationToken cancellationToken) {
            var snapshot = await store.ReadEditorAsync(id, cancellationToken);
            if (snapshot is not null && latestHealth is not null) {
                snapshot = snapshot with { Health = latestHealth };
            }
            if (HoldNextRead) {
                HoldNextRead = false;
                Entered.TrySetResult();
                await Release.Task;
            }
            return snapshot;
        }
        public async Task<CatalogOutcome> ExecuteAsync(CatalogCommand command, CancellationToken cancellationToken) {
            var result = await store.ExecuteAsync(command, cancellationToken);
            if (result.Health is { } health) {
                latestHealth = health with { TestedAtUtc = CatalogScenarioData.TestedAt.AddSeconds(store.DriverCalls) };
                result = result with { Health = latestHealth };
            }
            return result;
        }
    }
}
