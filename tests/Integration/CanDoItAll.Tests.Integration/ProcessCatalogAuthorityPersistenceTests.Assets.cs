using CanDoItAll.SharedKernel;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Asset_receipt_survives_new_owner_context_human_edit_unlink_and_native_deletion(bool revision) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, revision);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await workbench.PrepareProcessAssetAsync(test.Invocation, default);
        prepared = await workbench.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        var first = await workbench.CommitProcessAssetAsync(prepared, default);
        Assert.False(first.Observation.WasReplay);
        Assert.Equal(1, driver.StableWrites);
        Assert.NotEqual(Guid.Empty, first.Observation.Receipt.IntentId.Value);
        Assert.NotEqual(Guid.Empty, first.Observation.Receipt.StorageIntentId.Value);
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var row = await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(item => item.IntentId == prepared.Plan.Producer.IntentId.Value);
            var restored = ProjectProcessAssetPersistence.ReadPlan(row);
            Assert.Equal(test.Invocation.Execution.Evidence, restored.Execution.Evidence);
            Assert.NotEqual(Guid.Empty, restored.Execution.Evidence.RunId.Value);
            Assert.NotEqual(Guid.Empty, restored.Execution.Evidence.StepInstanceId.Value);
            Assert.NotEqual(Guid.Empty, restored.Execution.Evidence.DispatchClaimToken);
            Assert.Equal(test.Owner.Project.LifetimeId, restored.ProjectAdmission.LifetimeId);
            Assert.Equal(prepared.PlanFingerprint, row.PlanFingerprint);
            var native = await database.Set<ProjectObjectRecord>().SingleAsync(item => item.Id == first.Observation.Receipt.NativeObjectId);
            native.Title = "Human changed the title";
            if (revision) {
                var link = await database.Set<ProjectObjectLinkRecord>().SingleAsync(item => item.SourceNodeKey == first.Node.Id &&
                    item.TargetNodeKey == prepared.Plan.ParentNodeKey && item.LinkKind == ProjectObjectLinkKind.DerivedFrom);
                database.Remove(link);
            }
            await database.SaveChangesAsync();
        }
        await using var restartedScope = app.Services.CreateAsyncScope();
        var restarted = restartedScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var replay = await restarted.CommitProcessAssetAsync(prepared, default);
        Assert.Equal(first.Observation.Receipt, replay.Observation.Receipt);
        Assert.True(replay.Observation.WasReplay);
        Assert.False(replay.Observation.TargetDeleted);
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            Assert.Equal("Human changed the title", (await database.Set<ProjectObjectRecord>().SingleAsync(item => item.Id == prepared.Plan.NativeObjectId)).Title);
            Assert.False(await database.Set<ProjectObjectLinkRecord>().AnyAsync(item => item.SourceNodeKey == first.Node.Id && item.LinkKind == ProjectObjectLinkKind.DerivedFrom));
            var native = await database.Set<ProjectObjectRecord>().SingleAsync(item => item.Id == prepared.Plan.NativeObjectId);
            database.Remove(native);
            await database.SaveChangesAsync();
        }
        var deleted = await restarted.CommitProcessAssetAsync(prepared, default);
        Assert.Equal(first.Observation.Receipt, deleted.Observation.Receipt);
        Assert.True(deleted.Observation.TargetDeleted);
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(services, prepared, 0, 1, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Asset_native_and_revision_link_rollback_or_lost_ack_retains_exact_exception_and_one_Storage_intent(bool afterCommit) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        var failure = new ArgumentException("Injected native asset transaction boundary.");
        var probe = new AssetCommitProbe { AfterCommit = afterCommit, Failure = failure };
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver, probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, true);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await workbench.PrepareProcessAssetAsync(test.Invocation, default);
        prepared = await workbench.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        probe.IntentId = prepared.Plan.Producer.IntentId.Value;
        Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => workbench.CommitProcessAssetAsync(prepared, default)));
        Assert.True(probe.Fired);
        await AssertAssetRowsAsync(services, prepared, afterCommit ? 1 : 0, afterCommit ? 1 : 0, afterCommit ? 1 : 0);
        var stable = await services.GetRequiredService<StorageStablePlacementService>().FindAsync(prepared.Plan.StorageIntentId);
        Assert.Equal(StorageStablePlacementState.Completed, stable!.State);
        Assert.Equal(1, driver.StableWrites);
        probe.Failure = null;
        var retry = await workbench.CommitProcessAssetAsync(prepared, default);
        Assert.Equal(afterCommit, retry.Observation.WasReplay);
        Assert.Equal(prepared.Plan.NativeObjectId, retry.Observation.Receipt.NativeObjectId);
        Assert.Equal(prepared.Plan.StorageIntentId, retry.Observation.Receipt.StorageIntentId);
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(services, prepared, 1, 1, 1);
    }

    [Fact]
    public async Task Same_asset_intent_concurrent_preparation_retains_one_target_and_changed_content_conflicts() {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, false);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var pair = await Task.WhenAll(workbench.PrepareProcessAssetAsync(test.Invocation, default), workbench.PrepareProcessAssetAsync(test.Invocation, default));
        Assert.Equal(pair[0].Plan.NativeObjectId, pair[1].Plan.NativeObjectId);
        Assert.Equal(pair[0].Plan.StorageIntentId, pair[1].Plan.StorageIntentId);
        Assert.Equal(pair[0].PlanFingerprint, pair[1].PlanFingerprint);
        var saved = await workbench.MaterializeProcessAssetAsync(pair[0], AssetMedia(), default);
        var error = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => workbench.MaterializeProcessAssetAsync(pair[1],
            AssetMedia() with { Base64Data = Convert.ToBase64String("changed content"u8.ToArray()) }, default));
        Assert.Equal("ProcessAssetIntentConflict", error.ErrorCode);
        Assert.Equal(saved.MaterializedFingerprint, (await workbench.FindProcessAssetAsync(test.Invocation.Admitted.IntentId))!.MaterializedFingerprint);
        var original = new ProjectProcessAssetProposalCodec().Read(test.Invocation.Admitted.Payload).Create!;
        var changed = new ProjectProcessAssetProposalCodec().Prepare(ProjectStructureToolPolicy.ProjectStructureAssetCreate,
            JsonSerializer.SerializeToElement(new ProjectProcessAssetCreateProposal(test.Owner.Project.ProjectId, original with { Title = "Other intent content" }),
                ProjectProcessAssetProposalCodec.Json));
        error = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => workbench.PrepareProcessAssetAsync(
            new(test.Invocation.Admitted with { Payload = changed, ApprovedDigest = changed.Digest }, test.Invocation.Execution, test.Invocation.ParentNodeKey), default));
        Assert.Equal("ProcessAssetIntentConflict", error.ErrorCode);
        Assert.Equal(0, driver.StableWrites);
    }

    [Theory]
    [InlineData(AssetFenceChange.ParentBinding)]
    [InlineData(AssetFenceChange.ExpiredClaim)]
    [InlineData(AssetFenceChange.CurrentGrant)]
    public async Task Prepared_asset_rechecks_original_parent_claim_and_current_source_before_new_bytes(AssetFenceChange change) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, false);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await workbench.PrepareProcessAssetAsync(test.Invocation, default);
        prepared = await workbench.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        switch (change) {
            case AssetFenceChange.ParentBinding:
                await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
                    var parent = await database.Set<ProjectObjectRecord>().SingleAsync(item => item.NodeKey == test.Invocation.ParentNodeKey);
                    parent.ObjectSubtype = "feature";
                    await database.SaveChangesAsync();
                }
                break;
            case AssetFenceChange.ExpiredClaim:
                clock.Now = clock.Now.AddHours(2);
                break;
            case AssetFenceChange.CurrentGrant:
                await test.Owner.RevokeAsync(Revocation.Write);
                break;
        }
        var failure = await Record.ExceptionAsync(() => workbench.CommitProcessAssetAsync(prepared, default));
        Assert.NotNull(failure);
        Assert.True(failure is ProjectStructureAgentException or ProcessExecutionAuthorityMismatchException or ProcessLaunchAuthorityRejectedException);
        Assert.Equal(0, driver.StableWrites);
        Assert.Null((await workbench.FindProcessAssetAsync(test.Invocation.Admitted.IntentId))!.Receipt);
        await AssertAssetRowsAsync(services, prepared, 0, 0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Asset_external_dispatch_has_no_held_catalog_or_SQL_lock_and_commit_rechecks_its_original_claim(bool expireAfterBytes) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, false);
        driver.BeforeWrite = async token => {
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
            bounded.CancelAfter(TimeSpan.FromSeconds(10));
            await test.Owner.Writer.UpdateCatalogAsync(catalog => catalog, bounded.Token);
            await using var context = test.Owner.Context();
            await using var transaction = await context.Database.BeginTransactionAsync(bounded.Token);
            Assert.True(await context.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({NativeRootKey(test.Invocation.Execution.RootRunId.Value)}) AS \"Value\"").SingleAsync(bounded.Token));
            var key = ProjectMutationScopeKeys.ForProject(test.Owner.Project.ProjectId);
            Assert.True(await context.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({key}, 0)) AS \"Value\"").SingleAsync(bounded.Token));
        };
        driver.AfterWrite = () => {
            if (expireAfterBytes) {
                clock.Now = clock.Now.AddHours(2);
            }
        };
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await workbench.PrepareProcessAssetAsync(test.Invocation, default);
        prepared = await workbench.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        if (expireAfterBytes) {
            await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => workbench.CommitProcessAssetAsync(prepared, default));
        } else {
            Assert.NotNull((await workbench.CommitProcessAssetAsync(prepared, default)).Observation.Receipt);
        }
        Assert.Equal(1, driver.StableWrites);
        Assert.Equal(1, driver.BeforeWriteCalls);
        Assert.Equal(StorageStablePlacementState.Completed,
            (await services.GetRequiredService<StorageStablePlacementService>().FindAsync(prepared.Plan.StorageIntentId))!.State);
        await AssertAssetRowsAsync(services, prepared, expireAfterBytes ? 0 : 1, expireAfterBytes ? 0 : 1, 0);
    }

    [Fact]
    public async Task Registered_Process_asset_producer_replays_same_dynamic_proposal_then_allows_distinct_explicit_intent() {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe { AfterWriteFailure = new IOException("Injected byte acknowledgment loss.") };
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, false);
        var context = await ToolProducerContextAsync(test.Owner, test.Run);
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        using var audit = WorkspaceExecutionAuditContext.BeginScope(test.Run);
        await using var lease = await test.Journal.AcquireRunAsync(test.Run.ToolAdmission!.Session.Reference, default);
        using var bound = lease.Bind();
        await test.Journal.BeginSegmentAsync(lease, CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var batch = Assert.Single((await test.Journal.AdmitBatchAsync(lease, test.Payload.Digest,
            CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), [new("first", test.Payload, false), new("deliberate-second", test.Payload, false)], default)).Batches);
        var tool = Assert.Single((await provider.CreateToolsAsync(context, default)).OfType<AIFunction>(),
            item => item.Name == ProjectStructureToolPolicy.ProjectStructureAssetCreate);
        var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureAssetCreate);
        Assert.Equal(AgentToolProposalRecovery.OwnerReceipt, metadata.PrepareAdmission!(JsonSerializer.Deserialize<JsonElement>(test.Payload.ArgumentsJson)).Recovery);
        var input = new ProjectProcessAssetProposalCodec().Read(test.Payload).Create!;
        var results = new List<ProjectStructureNodeSummary>();
        foreach (var call in new[] { "first", "deliberate-second" }) {
            var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, call, test.Payload, default);
            using var dispatch = claim.Bind();
            using var effects = AgentToolInvocationEffectScope.Begin();
            var result = ReadRegisteredToolResult<ProjectStructureNodeSummary>(tool, await tool.InvokeAsync(new AIFunctionArguments {
                ["projectId"] = test.Owner.Project.ProjectId, ["request"] = input
            }));
            var replay = ReadRegisteredToolResult<ProjectStructureNodeSummary>(tool, await tool.InvokeAsync(new AIFunctionArguments {
                ["projectId"] = test.Owner.Project.ProjectId, ["request"] = input
            }));
            Assert.Equal(result.ProcessAssetReceipt!.Receipt, replay.ProcessAssetReceipt!.Receipt);
            Assert.True(replay.ProcessAssetReceipt.WasReplay);
            Assert.Equal(new AgentToolCommittedEffect(ProjectProcessAssetToolAdmission.EffectSourceKind, claim.Proposal.IntentId.Value.ToString("D")), effects.CommittedEffect);
            Assert.Equal(claim.Proposal.IntentId, result.ProcessAssetReceipt.Receipt.IntentId);
            Assert.NotNull(result.ProcessAssetReceipt.StorageObservationWarning);
            results.Add(result);
            await test.Journal.CompleteInvocationAsync(claim, CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), AgentToolEffectState.Committed, default);
        }
        Assert.Equal(2, driver.StableWrites);
        Assert.NotEqual(results[0].Id, results[1].Id);
        Assert.NotEqual(results[0].ProcessAssetReceipt!.Receipt.StorageIntentId, results[1].ProcessAssetReceipt!.Receipt.StorageIntentId);
        await test.Owner.RevokeAsync(Revocation.Write);
        await using (var read = await metadata.AuthorizeResultDisclosureAsync!(new(batch.Proposals[0].IntentId, test.Payload,
            AgentToolEffectState.Committed, JsonSerializer.SerializeToElement(results[0], ProjectProcessAssetPersistence.Json)), default)) {
            Assert.NotNull(read);
        }
        await test.Owner.RevokeAsync(Revocation.Read);
        var denied = await Record.ExceptionAsync(async () => {
            await using var permission = await metadata.AuthorizeResultDisclosureAsync!(new(batch.Proposals[0].IntentId, test.Payload,
                AgentToolEffectState.Committed, JsonSerializer.SerializeToElement(results[0], ProjectProcessAssetPersistence.Json)), default);
        });
        Assert.True(denied is ProcessLaunchAuthorityRejectedException or ProcessExecutionAuthorityMismatchException or AgentToolAdmissionException);
    }

    [Fact]
    public async Task Asset_receipt_and_original_Process_claim_survive_full_host_restart_and_another_profile_is_empty() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-asset-receipt-restart");
        var original = environment.CreatePostgreSqlProfile("original");
        var other = environment.CreatePostgreSqlProfile("other");
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        var configured = AssetHarness(clock, driver);
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = original,
            ConfigureServices = configured.ConfigureServices };
        ProjectProcessAssetSnapshot prepared;
        ProjectProcessAssetReceipt receipt;
        await using (var first = await TestApplication.CreateAsync(options)) {
            await using var scope = first.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var test = await CreateAssetCaseAsync(services, clock, false);
            var owner = services.GetRequiredService<ProjectWorkbenchService>();
            prepared = await owner.PrepareProcessAssetAsync(test.Invocation, default);
            prepared = await owner.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
            receipt = (await owner.CommitProcessAssetAsync(prepared, default)).Observation.Receipt;
        }
        clock.Now = clock.Now.AddHours(2);
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var originalScope = restarted.Services.CreateAsyncScope();
        var originalOwner = originalScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var loaded = Assert.IsType<ProjectProcessAssetSnapshot>(await originalOwner.FindProcessAssetAsync(receipt.IntentId));
        Assert.Equal(prepared.Plan.Execution.Evidence, loaded.Plan.Execution.Evidence);
        Assert.Equal(prepared.Plan.ProjectAdmission, loaded.Plan.ProjectAdmission);
        Assert.Equal(prepared.PlanFingerprint, loaded.PlanFingerprint);
        Assert.Equal(receipt, (await originalOwner.CommitProcessAssetAsync(loaded, default)).Observation.Receipt);
        Assert.Equal(1, driver.StableWrites);
        await using var isolated = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = other,
            ConfigureServices = configured.ConfigureServices });
        await using var isolatedScope = isolated.Services.CreateAsyncScope();
        Assert.Null(await isolatedScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().FindProcessAssetAsync(receipt.IntentId));
        Assert.Equal(receipt, (await originalOwner.FindProcessAssetAsync(receipt.IntentId))!.Receipt);
    }

    [Fact]
    public async Task Asset_service_uses_retained_materialization_without_repeating_its_external_source_read() {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, false);
        var codec = new ProjectProcessAssetProposalCodec();
        var input = codec.Read(test.Payload).Create! with { Media = null, SourceUrl = "https://asset.example.test/original.txt" };
        var payload = codec.Prepare(ProjectStructureToolPolicy.ProjectStructureAssetCreate,
            JsonSerializer.SerializeToElement(new ProjectProcessAssetCreateProposal(test.Owner.Project.ProjectId, input), ProjectProcessAssetProposalCodec.Json));
        var invocation = new ProjectProcessAssetInvocation(test.Invocation.Admitted with { Payload = payload, ApprovedDigest = payload.Digest },
            test.Invocation.Execution, test.Invocation.ParentNodeKey);
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await owner.PrepareProcessAssetAsync(invocation, default);
        prepared = await owner.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        var forbiddenRead = new AssetForbiddenHttp();
        var service = ActivatorUtilities.CreateInstance<ProjectStructureAgentService>(services, forbiddenRead);
        var agent = new ProjectStructureAgentContext(test.Owner.Agent.Id.ToString("D"), "Original source", Environment.MachineName,
            app.RootPath, "fixture", Guid.NewGuid().ToString("D")) { ProcessAssetInvocation = invocation };
        var result = await service.CreateAssetAsync(test.Owner.Project.ProjectId, input.ToServiceRequest(), agent);
        Assert.Equal(prepared.Plan.NativeObjectId, result.ProcessAssetReceipt!.Receipt.NativeObjectId);
        Assert.Equal(0, forbiddenRead.Calls);
        Assert.Equal(1, driver.StableWrites);
    }

    [Fact]
    public async Task Asset_uncertain_byte_readback_retains_its_intents_and_recovers_without_another_write() {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe { AfterWriteFailure = new IOException("Lost byte acknowledgement."),
            ReadFailure = new IOException("Readback temporarily unavailable.") };
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, false);
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await owner.PrepareProcessAssetAsync(test.Invocation, default);
        prepared = await owner.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        var uncertain = await Assert.ThrowsAsync<ProjectProcessAssetReconciliationRequiredException>(() => owner.CommitProcessAssetAsync(prepared, default));
        Assert.Equal(prepared.Plan.Producer.IntentId, uncertain.IntentId);
        Assert.Equal(prepared.Plan.StorageIntentId, uncertain.StorageIntentId);
        Assert.Equal(StorageStablePlacementState.Uncertain, uncertain.StorageState);
        Assert.Equal(AgentToolEffectState.Unknown, uncertain.EffectState);
        Assert.IsType<StorageStablePlacementPendingException>(uncertain.InnerException);
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(services, prepared, 0, 0, 0);
        driver.ReadFailure = null;
        var recovered = await owner.CommitProcessAssetAsync(prepared, default);
        Assert.Equal(prepared.Plan.StorageIntentId, recovered.Observation.Receipt.StorageIntentId);
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(services, prepared, 1, 1, 0);
    }

    private static async Task<AssetCase> CreateAssetCaseAsync(IServiceProvider services, NativeClock clock, bool revision) {
        var receiptOwner = services.GetRequiredService<ProjectProcessAssetToolAdmission>();
        var receiptProviders = services.GetServices<IAgentToolReceiptReconciliationProvider>().ToArray();
        foreach (var toolName in new[] { ProjectStructureToolPolicy.ProjectStructureAssetCreate, ProjectProcessAssetProposalCodec.RevisionToolName }) {
            Assert.Same(receiptOwner, Assert.Single(receiptProviders, provider => provider.Supports(toolName)));
        }
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, persistExecution: false);
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var journal = new AgentToolAdmissionJournal(store, NativeProfile(services), backgroundSources: [NativeBackgroundPolicy(services, fixture, clock)]);
        run = run with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(run, "Create the admitted managed asset.") };
        await store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var parent = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(fixture.Project.ProjectId,
            new(revision ? ProjectObjectType.File : ProjectObjectType.ProjectBlock, "Asset parent", "", "", $"project:{fixture.Project.ProjectId}",
                Media: revision ? AssetMedia() : null));
        var codec = new ProjectProcessAssetProposalCodec();
        var payload = revision
            ? codec.Prepare(ProjectProcessAssetProposalCodec.RevisionToolName, JsonSerializer.SerializeToElement(
                new ProjectProcessAssetRevisionProposal(fixture.Project.ProjectId, parent.Id, new("Revision", "", "", AssetMedia())), ProjectProcessAssetProposalCodec.Json))
            : codec.Prepare(ProjectStructureToolPolicy.ProjectStructureAssetCreate, JsonSerializer.SerializeToElement(
                new ProjectProcessAssetCreateProposal(fixture.Project.ProjectId, new(ProjectObjectType.File, "Process asset",
                    Media: AssetMedia(), ParentNodeKey: parent.Id)), ProjectProcessAssetProposalCodec.Json));
        var admitted = new AgentToolAdmittedInvocation(run.ToolAdmission.Session.Reference, new(Guid.NewGuid()), new(Guid.NewGuid()),
            payload, ExecutionApprovalStatus.Approved, payload.Digest);
        var execution = Assert.IsType<ProcessExecutionDispatchAuthority>((await ((IProcessExecutionDispatchAuthorityReader)NativeReader(services, clock)).ReadAsync(run.Id)).Snapshot);
        return new(fixture, run, journal, payload, new(admitted, execution, parent.Id));
    }

    private static TestHarnessOptions AssetHarness(NativeClock clock, AssetDriverProbe driver, AssetCommitProbe? probe = null) {
        var native = ToolProducerHarness(clock);
        return new() { ConfigureServices = services => {
            native.ConfigureServices!(services);
            services.AddSingleton<IStorageDriver>(provider => new AssetDriver(
                new FileSystemStorageDriver(provider.GetRequiredService<FileSystemStoragePathPolicy>()), driver));
            if (probe is not null) {
                services.AddScoped<IDbContextFactory<WorkbenchDbContext>>(provider => new Factory<WorkbenchDbContext>(
                    NativeOptions<WorkbenchDbContext>(provider, probe), static options => new(options)));
            }
        } };
    }

    private static ProjectObjectMediaPayload AssetMedia() => new("process.txt", "text/plain", Convert.ToBase64String("retained asset content"u8.ToArray()));

    private static async Task AssertAssetRowsAsync(IServiceProvider services, ProjectProcessAssetSnapshot prepared, int natives, int receipts, int links) {
        await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Equal(natives, await database.Set<ProjectObjectRecord>().CountAsync(item => item.Id == prepared.Plan.NativeObjectId));
        Assert.Equal(receipts, await database.Set<ProjectProcessAssetContributionRecord>().CountAsync(item =>
            item.IntentId == prepared.Plan.Producer.IntentId.Value && item.ReceiptJson != ""));
        Assert.Equal(1, await database.Set<ProjectProcessAssetContributionRecord>().CountAsync(item => item.IntentId == prepared.Plan.Producer.IntentId.Value));
        var key = $"custom:{prepared.Plan.NativeObjectId:N}";
        Assert.Equal(links, await database.Set<ProjectObjectLinkRecord>().CountAsync(item => item.SourceNodeKey == key && item.LinkKind == ProjectObjectLinkKind.DerivedFrom));
    }

    private sealed record AssetCase(Fixture Owner, ExecutionRunRecord Run, AgentToolAdmissionJournal Journal,
        AgentToolPreparedPayload Payload, ProjectProcessAssetInvocation Invocation);

    public enum AssetFenceChange { ParentBinding, ExpiredClaim, CurrentGrant }

    private sealed class AssetCommitProbe : SaveChangesInterceptor, IDbTransactionInterceptor {
        public Guid IntentId { get; set; }
        public bool AfterCommit { get; init; }
        public Exception? Failure { get; set; }
        public bool Fired { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (!AfterCommit && !Fired && Failure is not null && eventData.Context is WorkbenchDbContext context &&
                    context.ChangeTracker.Entries<ProjectProcessAssetContributionRecord>().Any(entry => entry.Entity.IntentId == IntentId && entry.Entity.ReceiptJson.Length > 0)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Fired = true;
                throw Failure;
            }
            return ValueTask.FromResult(result);
        }

        public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (!AfterCommit || Fired || Failure is null || IntentId == Guid.Empty || eventData.Context is not WorkbenchDbContext context) {
                return;
            }
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM \"Workbench_ProcessAssetContributions\" WHERE \"IntentId\" = @id AND \"ReceiptJson\" <> '')";
            command.Parameters.Add(new NpgsqlParameter<Guid>("id", IntentId));
            if (await command.ExecuteScalarAsync(cancellationToken) is true) {
                Fired = true;
                throw Failure;
            }
        }
    }

    private sealed class AssetDriverProbe {
        public int StableWrites { get; set; }
        public int BeforeWriteCalls { get; set; }
        public Func<CancellationToken, Task>? BeforeWrite { get; set; }
        public Action? AfterWrite { get; set; }
        public Exception? AfterWriteFailure { get; init; }
        public Exception? ReadFailure { get; set; }
    }

    private sealed class AssetForbiddenHttp : IHttpClientFactory {
        public int Calls { get; private set; }
        public HttpClient CreateClient(string name) {
            Calls++;
            throw new InvalidOperationException("A retained asset materialization must not reread its external source.");
        }
    }

    private sealed class AssetDriver(FileSystemStorageDriver inner, AssetDriverProbe probe) : IStorageDriver, IStorageStablePlacementDriver {
        public StorageProviderKind ProviderKind => inner.ProviderKind;
        public StorageCapability SupportedCapabilities => inner.SupportedCapabilities;
        public bool CanRecoverWithoutWriteAcknowledgement => true;
        public Task<StorageConnectionTestResult> TestConnectionAsync(StorageDriverInput storage, string? secret, CancellationToken cancellationToken = default)
            => inner.TestConnectionAsync(storage, secret, cancellationToken);
        public Task<StorageWriteResult> SaveAsync(StorageDriverInput storage, StorageWriteRequest request, CancellationToken cancellationToken = default)
            => inner.SaveAsync(storage, request, cancellationToken);
        public Task<Stream> OpenReadAsync(StorageDriverInput storage, StorageObjectReference reference, CancellationToken cancellationToken = default)
            => probe.ReadFailure is { } failure ? Task.FromException<Stream>(failure) : inner.OpenReadAsync(storage, reference, cancellationToken);
        public Task DeleteAsync(StorageDriverInput storage, StorageObjectReference reference, CancellationToken cancellationToken = default)
            => inner.DeleteAsync(storage, reference, cancellationToken);
        public Task<StorageObjectReference> PrepareStableTargetAsync(StorageDriverInput storage, StoragePlacementIntentId id, StorageWriteRequest request, CancellationToken cancellationToken)
            => ((IStorageStablePlacementDriver)inner).PrepareStableTargetAsync(storage, id, request, cancellationToken);
        public async Task<StorageWriteResult> WriteStableTargetAsync(StorageDriverInput storage, StorageObjectReference target, StorageWriteRequest request, CancellationToken cancellationToken) {
            probe.StableWrites++;
            if (probe.BeforeWrite is not null) {
                await probe.BeforeWrite(cancellationToken);
                probe.BeforeWriteCalls++;
            }
            var result = await ((IStorageStablePlacementDriver)inner).WriteStableTargetAsync(storage, target, request, cancellationToken);
            probe.AfterWrite?.Invoke();
            if (probe.AfterWriteFailure is not null) {
                throw probe.AfterWriteFailure;
            }
            return result;
        }
        public Task CompleteStableTargetAsync(StorageDriverInput storage, StorageObjectReference target, CancellationToken cancellationToken)
            => ((IStorageStablePlacementDriver)inner).CompleteStableTargetAsync(storage, target, cancellationToken);
    }
}
