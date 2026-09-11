using CanDoItAll.AgentFramework.Persistence;
using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    public enum ProcessNativeChange { Create, Edit, Reclassify, Metadata, Status, Progress, Marker, Priority }

    [Theory]
    [InlineData(ProcessNativeChange.Create)]
    [InlineData(ProcessNativeChange.Edit)]
    [InlineData(ProcessNativeChange.Reclassify)]
    [InlineData(ProcessNativeChange.Metadata)]
    [InlineData(ProcessNativeChange.Status)]
    [InlineData(ProcessNativeChange.Progress)]
    [InlineData(ProcessNativeChange.Marker)]
    [InlineData(ProcessNativeChange.Priority)]
    public async Task Every_native_path_rechecks_the_exact_saved_Process_claim_before_writing(ProcessNativeChange change) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var admission = Assert.IsType<ProjectProcessMutationAdmission>(await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id));
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = fixture.Project.ProjectId;
        var node = await workbench.CreateObjectAsync(projectId, new(ProjectObjectType.ProjectBlock, "Original", "", "", $"project:{projectId}"));
        await using (var context = fixture.Context()) {
            var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == admission.Dispatch.Evidence.RunId.Value);
            step.ActiveClaimToken = Guid.NewGuid();
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => NativeChangeAsync(workbench, admission, node.Id, change));
        await using var read = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var stored = Assert.Single(await read.Set<ProjectObjectRecord>().AsNoTracking().Where(item => item.ProjectId == projectId).ToArrayAsync());
        Assert.Equal("Original", stored.Title);
        Assert.NotEqual("Published", stored.Status);
        Assert.DoesNotContain("late", stored.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Actual_native_commit_rechecks_claim_expiry_after_the_owner_flush_and_rolls_back() {
        var clock = new NativeClock();
        var fault = new NativeCommitProbe();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock, fault));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var admission = Assert.IsType<ProjectProcessMutationAdmission>(await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id));
        fault.AfterFlush = () => clock.Now = clock.Now.AddHours(2);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => NativeChangeAsync(services.GetRequiredService<ProjectWorkbenchService>(), admission, "", ProcessNativeChange.Create));
        Assert.True(fault.NativeFlushes > 0);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 0);
    }

    [Fact]
    public async Task Native_transaction_waiter_cannot_use_the_snapshot_from_before_another_Process_commit() {
        var clock = new NativeClock();
        var probe = new NativeGuardProbe();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock, processProbe: probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var admission = Assert.IsType<ProjectProcessMutationAdmission>(await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id));
        await using var blocker = fixture.Context();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({NativeRootKey(admission.Dispatch.RootRunId.Value)})");
        probe.WaitForRoot = true;
        var mutation = NativeChangeAsync(services.GetRequiredService<ProjectWorkbenchService>(), admission, "", ProcessNativeChange.Create);
        var backendId = await probe.RootRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var observer = fixture.Context()) {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!await observer.Database.SqlQuery<bool>($"""
                       SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = {backendId} AND wait_event_type = 'Lock') AS "Value"
                       """).SingleAsync(timeout.Token)) {
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
            }
        }
        var state = await blocker.RuntimeStates.SingleAsync(item => item.RunId == admission.Dispatch.Evidence.RunId.Value);
        state.Status = ProcessRuntimeStatus.CancelRequested;
        await blocker.SaveChangesAsync();
        await transaction.CommitAsync();
        var failure = await Record.ExceptionAsync(() => mutation);
        Assert.NotNull(failure);
        Assert.True(failure is ProcessExecutionAuthorityMismatchException || SerializableMutationScope.IsConflict(failure));
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_source_policy_preserves_read_after_write_revocation_and_denies_read_after_read_revocation(bool revokeRead) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var policy = NativeBackgroundPolicy(services, fixture, clock);
        var profile = NativeProfile(services);
        var original = await policy.ObserveAsync(run, profile);
        Assert.True(original.ReadAllowed);
        Assert.True(original.DispatchAllowed);
        await fixture.RevokeAsync(revokeRead ? Revocation.Read : Revocation.Write);
        var current = await policy.ObserveAsync(run, profile);
        Assert.Equal(original.OwnerFingerprint, current.OwnerFingerprint);
        Assert.Equal(!revokeRead, current.ReadAllowed);
        Assert.False(current.DispatchAllowed);
    }

    [Fact]
    public async Task Legacy_assignment_keeps_its_real_read_contract_without_fabricating_project_mutation_authority() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        await using (var context = fixture.Context()) {
            var state = await context.RuntimeStates.SingleAsync(item => item.RunId == Guid.Parse(run.ProcessRunId!));
            state.LaunchAdmissionId = null;
            await context.SaveChangesAsync();
        }
        var reader = (IProcessExecutionDispatchAuthorityReader)NativeReader(services, clock);
        var dispatch = Assert.IsType<ProcessExecutionDispatchAuthority>((await reader.ReadAsync(run.Id)).Snapshot);
        Assert.Equal(fixture.Project.ProjectId, dispatch.ProjectId);
        Assert.Null(dispatch.SourceAuthority);
        Assert.Null(dispatch.ProjectReference);
        Assert.Contains(ProcessOperationContractNames.ReadProjectStructure, dispatch.AllowedOperations);
        var observation = await NativeBackgroundPolicy(services, fixture, clock).ObserveAsync(run, NativeProfile(services));
        Assert.True(observation.ReadAllowed);
        Assert.True(observation.DispatchAllowed);
        Assert.Null(await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Final_native_transaction_propagates_original_fault_and_retains_only_committed_state(bool afterCommit) {
        var clock = new NativeClock();
        var original = new IOException("fixture-native-commit-boundary");
        var probe = new NativeCommitProbe();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock, probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var admission = Assert.IsType<ProjectProcessMutationAdmission>(await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id));
        probe.Failure = original;
        probe.FailAfterCommit = afterCommit;
        var observed = await Record.ExceptionAsync(() => NativeChangeAsync(services.GetRequiredService<ProjectWorkbenchService>(), admission, "", ProcessNativeChange.Create));
        Assert.Same(original, observed);
        Assert.True(probe.NativeFlushes > 0);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, afterCommit ? 1 : 0);
        await fixture.RevokeAsync(Revocation.Write).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Ordinary_factory_reads_remain_independent_while_Process_and_Workbench_use_the_actual_outer_transaction() {
        var clock = new NativeClock();
        var probe = new NativeGuardProbe();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock, processProbe: probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var admission = Assert.IsType<ProjectProcessMutationAdmission>(await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id));
        await NativeChangeAsync(services.GetRequiredService<ProjectWorkbenchService>(), admission, "", ProcessNativeChange.Create);
        Assert.True(probe.Transactions.Count >= 2);
        Assert.All(probe.Transactions, transaction => Assert.Same(probe.Transactions[0], transaction));
        await using var independent = fixture.Context();
        Assert.Null(independent.Database.CurrentTransaction);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 1);
    }

    [Fact]
    public async Task Original_source_catalog_is_held_through_native_commit_and_released_before_followup_work() {
        var clock = new NativeClock();
        var probe = new NativeCommitProbe();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock, probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var admission = Assert.IsType<ProjectProcessMutationAdmission>(await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id));
        var held = new CatalogCommitProbe(fixture.Writer);
        probe.Observer = held;
        await NativeChangeAsync(services.GetRequiredService<ProjectWorkbenchService>(), admission, "", ProcessNativeChange.Create);
        Assert.True(held.BeforeCommit);
        Assert.True(held.AfterCommit);
        await fixture.RevokeAsync(Revocation.Write).WaitAsync(TimeSpan.FromSeconds(10));
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 1);
    }

    [Fact]
    public async Task Explicit_Process_request_fails_before_writes_when_native_scope_policy_is_missing() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock, missingScopePolicy: true));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var admission = Assert.IsType<ProjectProcessMutationAdmission>(await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => NativeChangeAsync(services.GetRequiredService<ProjectWorkbenchService>(), admission, "", ProcessNativeChange.Create));
        Assert.Contains("authority is not installed", error.Message, StringComparison.Ordinal);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Background_source_rejects_another_profile_fingerprint_or_generation(bool changeGeneration) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var original = NativeProfile(services);
        var changed = new AgentToolProfileBinding(original.ProfileId, changeGeneration ? original.Fingerprint : "different-profile-fingerprint",
            changeGeneration ? new(original.Generation.Value + 1) : original.Generation);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(async () => {
            await NativeBackgroundPolicy(services, fixture, clock).ObserveAsync(run, changed);
        });
        Assert.Null((await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(run.Id))!.ToolAdmission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Process_asset_placement_occurs_outside_source_and_SQL_locks_and_native_commit_rechecks_afterward(bool expireDuringPlacement) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var admission = Assert.IsType<ProjectProcessMutationAdmission>(await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id));
        var placement = new NativePlacementProbe(services.GetRequiredService<IStoragePlacementService>(), async token => {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await fixture.Writer.UpdateCatalogAsync(catalog => catalog, timeout.Token);
            await using var context = fixture.Context();
            await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
            Assert.True(await context.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({NativeRootKey(admission.Dispatch.RootRunId.Value)}) AS \"Value\"")
                .SingleAsync(timeout.Token));
            var projectKey = ProjectMutationScopeKeys.ForProject(fixture.Project.ProjectId);
            Assert.True(await context.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({projectKey}, 0)) AS \"Value\"")
                .SingleAsync(timeout.Token));
        }, () => {
            if (expireDuringPlacement) {
                clock.Now = clock.Now.AddHours(2);
            }
        });
        var workbench = new ProjectWorkbenchService(services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>(),
            services.GetRequiredService<ProjectStructureMutationScopeFactory>(), services.GetRequiredService<ProjectRecordQueryService>(),
            services.GetRequiredService<IClock>(), placement, services.GetRequiredService<ProjectManagedStoragePhysicalIdentityPolicy>(),
            services.GetRequiredService<ProjectStructureAssemblyService>(), services.GetRequiredService<ProjectWorkbenchRelationService>(),
            services.GetRequiredService<ProjectWorkbenchLifecycleService>(), services.GetRequiredService<ProjectWorkbenchCommandService>(),
            services.GetRequiredService<ProjectWorkbenchCrossModuleMutationService>(), services.GetRequiredService<ProjectStructureRuntimeNodeMetadataBoundary>());
        var create = workbench.CreateObjectAsync(fixture.Project.ProjectId, new(ProjectObjectType.File, "Process file", "", "", $"project:{fixture.Project.ProjectId}",
            Media: new("process.txt", "text/plain", Convert.ToBase64String("retained bytes"u8.ToArray()))) {
            ExpectedProjectAdmission = admission.ProjectAdmission, ProcessMutationAdmission = admission
        });
        if (expireDuringPlacement) {
            await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => create);
        } else {
            Assert.NotNull(await create);
        }
        Assert.Equal(1, placement.Calls);
        Assert.NotNull(placement.Placed);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, expireDuringPlacement ? 0 : 1);
    }

    private static async Task NativeChangeAsync(ProjectWorkbenchService workbench, ProjectProcessMutationAdmission admission,
        string nodeId, ProcessNativeChange change) {
        var projectId = admission.ProjectAdmission.ProjectId;
        switch (change) {
            case ProcessNativeChange.Create:
                await workbench.CreateObjectAsync(projectId, new(ProjectObjectType.ProjectBlock, "Native Process output", "", "", $"project:{projectId}") {
                    ExpectedProjectAdmission = admission.ProjectAdmission, ProcessMutationAdmission = admission
                });
                break;
            case ProcessNativeChange.Edit:
                await workbench.UpdateObjectAsync(projectId, nodeId, new("Late edit", "", "", null, null, "{}") {
                    ExpectedProjectAdmission = admission.ProjectAdmission, ProcessMutationAdmission = admission
                });
                break;
            case ProcessNativeChange.Reclassify:
                await workbench.ReclassifyObjectAsync(projectId, nodeId, new(ProjectObjectType.ProjectBlock, "feature", "Late type", "", "") {
                    ExpectedProjectAdmission = admission.ProjectAdmission, ProcessMutationAdmission = admission
                });
                break;
            case ProcessNativeChange.Metadata:
                await workbench.UpdateObjectMetadataAsync(projectId, nodeId, "{\"late\":true}", expectedProjectAdmission: admission.ProjectAdmission, processMutationAdmission: admission);
                break;
            case ProcessNativeChange.Status:
                await workbench.UpdateObjectStatusesAsync(projectId, [nodeId], "Published", expectedProjectAdmission: admission.ProjectAdmission, processMutationAdmission: admission);
                break;
            case ProcessNativeChange.Progress:
                await workbench.UpdateObjectProgressAsync(projectId, [nodeId], "manual", 72, expectedProjectAdmission: admission.ProjectAdmission, processMutationAdmission: admission);
                break;
            case ProcessNativeChange.Marker:
                await workbench.UpdateObjectMarkerAsync(projectId, [nodeId], "flag", "danger", "Late", expectedProjectAdmission: admission.ProjectAdmission, processMutationAdmission: admission);
                break;
            case ProcessNativeChange.Priority:
                await workbench.UpdateObjectPriorityAsync(projectId, [nodeId], 9, expectedProjectAdmission: admission.ProjectAdmission, processMutationAdmission: admission);
                break;
        }
    }

    private static async Task<ExecutionRunRecord> CreateClaimedExecutionAsync(IServiceProvider services, Fixture fixture, NativeClock clock,
        bool persistExecution = true, ProcessLaunchAuthority? authority = null) {
        var preparation = ProcessPreparedLaunchFixture.Create(authority ?? fixture.SavedAuthority, new(Guid.NewGuid()));
        var initial = preparation.InitialCommit;
        var assignment = Assert.Single(initial.InitialAssignments!) with {
            AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction],
            LaunchVariables = new Dictionary<string, string> { [ProcessRuntimeLaunchVariables.ProjectId] = fixture.Project.ProjectId.ToString("D") }
        };
        var executionStore = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var executor = fixture.Agent with {
            Id = Guid.Parse(assignment.ExecutorId), Name = "Assigned background executor", ConfigurationJson = "{}"
        };
        var catalog = await executionStore.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, executor] });
        Assert.Contains(catalog.Agents, agent => agent.Id == executor.Id);
        preparation = preparation with { InitialCommit = initial with { InitialAssignments = [assignment] } };
        var saved = await fixture.Store.PrepareAsync(preparation);
        await using (var context = fixture.Context()) {
            Assert.True((await fixture.UnitOfWork(context).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
        }
        var claim = Guid.NewGuid();
        await using (var context = fixture.Context()) {
            var state = await context.RuntimeStates.SingleAsync(item => item.RunId == assignment.RunId.Value);
            var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == assignment.RunId.Value);
            state.Status = ProcessRuntimeStatus.Active;
            step.Status = ProcessRuntimeStepStatus.Running;
            step.AttemptNumber = 1;
            step.ActiveClaimToken = claim;
            context.DispatchClaims.Add(new() { RunId = assignment.RunId.Value, StepInstanceId = assignment.StepInstanceId.Value,
                ClaimToken = claim, OwnerId = "native-fixture", Status = DispatchClaimStatus.Claimed, AttemptNumber = 1,
                CreatedAtUtc = clock.Now.AddSeconds(-1), ExpiresAtUtc = clock.Now.AddHours(1) });
            await context.SaveChangesAsync();
        }
        var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.Parse(assignment.ExecutorId), null, "Native Process executor", "process-step",
            assignment.StepKey, assignment.RunId.ToString(), assignment.StepInstanceId.ToString(), "process-runtime", "system",
            JsonSerializer.Serialize(new Dictionary<string, string> { ["agentProcessDispatchClaimIdentity"] = claim.ToString("D") }),
            "Execute.", "", "fixture", "fixture", ExecutionState.Running, null, clock.Now, clock.Now, clock.Now, null, "", null, [],
            ProcessRunId: assignment.RunId.ToString(), ProcessStepId: assignment.StepInstanceId.ToString());
        return persistExecution
            ? (await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().SaveExecutionRunDetailAsync(new(run, null, [], []))).Run
            : run with { State = ExecutionState.Preparing };
    }

    private static TestHarnessOptions NativeHarness(NativeClock clock, NativeCommitProbe? nativeProbe = null, NativeGuardProbe? processProbe = null,
        bool missingScopePolicy = false)
        => new() { ConfigureServices = services => {
            foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                services.Remove(descriptor);
            }
            if (nativeProbe is not null) {
                services.AddScoped<IDbContextFactory<WorkbenchDbContext>>(provider => new Factory<WorkbenchDbContext>(NativeOptions<WorkbenchDbContext>(provider, nativeProbe), static options => new(options)));
            }
            services.AddScoped<ProjectProcessExecutionMutationService>(provider => {
                var coordinator = provider.GetRequiredService<CoordinatedDatabaseTransaction>();
                return new((IProcessExecutionDispatchAuthorityReader)NativeReader(provider, clock),
                    new EfProcessExecutionMutationGuard(NativeOptions<ProcessPersistenceDbContext>(provider, processProbe), coordinator, clock),
                    NativeAuthority(provider, clock), coordinator);
            });
            if (missingScopePolicy) {
                services.AddScoped(provider => new ProjectStructureMutationScopeFactory(provider.GetRequiredService<ProjectRecordQueryService>(),
                    provider.GetRequiredService<ProjectWriteAdmissionService>(), provider.GetRequiredService<CoordinatedDatabaseTransaction>()));
            } else {
                services.AddScoped<ProjectStructureMutationScopeFactory>();
            }
        } };

    private static ProjectProcessLaunchAuthorityService NativeAuthority(IServiceProvider services, NativeClock clock) {
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var coordinator = services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        return new(profile, new CanonicalAgentCatalogLeaseSource(profile, services.GetRequiredService<IOptions<StorageOptions>>(),
            services.GetRequiredService<IHostEnvironment>(), admissions), services.GetRequiredService<IAgentExecutionProfileGenerationSource>(), admissions,
            new(new Factory<WorkbenchDbContext>(NativeOptions<WorkbenchDbContext>(services), static options => new(options)), NativeOptions<WorkbenchDbContext>(services),
                coordinator, services.GetRequiredService<ProjectStructureAssemblyService>()), services.GetRequiredService<IOptionsMonitor<ApiAccessOptions>>(), clock);
    }

    private static ProcessExecutionProjectAuthorityReader NativeReader(IServiceProvider services, TimeProvider clock)
        => new(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>(),
            new(new Factory<ProcessPersistenceDbContext>(NativeOptions<ProcessPersistenceDbContext>(services), static options => new(options)), clock),
            services.GetRequiredService<ICanonicalRuntimeDatabase>());

    private static ProcessToolBackgroundSourcePolicy NativeBackgroundPolicy(IServiceProvider services, Fixture fixture, TimeProvider clock)
        => new(new(new Factory<ProcessPersistenceDbContext>(NativeOptions<ProcessPersistenceDbContext>(services), static options => new(options)), clock),
            services.GetRequiredService<ICanonicalRuntimeDatabase>(), services.GetRequiredService<IAgentExecutionProfileGenerationSource>(), fixture.Authority);

    private static AgentToolProfileBinding NativeProfile(IServiceProvider services)
        => new(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id,
            services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Runtime.Fingerprint,
            services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration());

    private static DbContextOptions<T> NativeOptions<T>(IServiceProvider services, IInterceptor? interceptor = null) where T : DbContext {
        var builder = new DbContextOptionsBuilder<T>();
        AppDbContextOptionsConfigurator.Configure(builder, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        if (interceptor is not null) {
            builder.AddInterceptors(interceptor);
        }
        return builder.Options;
    }

    private static async Task AssertNativeCountAsync(IServiceProvider services, Guid projectId, int expected) {
        await using var context = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Equal(expected, await context.Set<ProjectObjectRecord>().CountAsync(item => item.ProjectId == projectId));
    }

    private static long NativeRootKey(Guid root) {
        var bytes = root.ToByteArray();
        return BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(0, 8)) ^ BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(8, 8));
    }

    private sealed class NativeClock : TimeProvider {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class NativePlacementProbe(IStoragePlacementService inner, Func<CancellationToken, Task> before, Action after) : IStoragePlacementService {
        public int Calls { get; private set; }
        public StoragePlacementResult? Placed { get; private set; }
        public async Task<StoragePlacementResult> PlaceAsync(StoragePlacementRequest request, CancellationToken cancellationToken = default) {
            await before(cancellationToken);
            Calls++;
            Placed = await inner.PlaceAsync(request, cancellationToken);
            after();
            return Placed;
        }
    }

    private sealed class NativeCommitProbe : SaveChangesInterceptor, IDbTransactionInterceptor {
        public int NativeFlushes { get; private set; }
        public Action? AfterFlush { get; set; }
        public IOException? Failure { get; set; }
        public bool FailAfterCommit { get; set; }
        public IDbTransactionInterceptor? Observer { get; set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (eventData.Context is WorkbenchDbContext && result > 0) {
                NativeFlushes++;
                AfterFlush?.Invoke();
            }
            return ValueTask.FromResult(result);
        }

        public async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (Observer is not null) {
                result = await Observer.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
            }
            if (Failure is not null && !FailAfterCommit) {
                throw Failure;
            }
            return result;
        }

        public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (Observer is not null) {
                await Observer.TransactionCommittedAsync(transaction, eventData, cancellationToken);
            }
            if (Failure is not null && FailAfterCommit) {
                throw Failure;
            }
        }
    }

    private sealed class NativeGuardProbe : DbCommandInterceptor {
        public List<DbTransaction> Transactions { get; } = [];
        public bool WaitForRoot { get; set; }
        public TaskCompletionSource<int> RootRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
                Assert.NotNull(command.Transaction);
                Transactions.Add(command.Transaction);
                if (WaitForRoot) {
                    RootRequested.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
                }
            }
            return ValueTask.FromResult(result);
        }
    }
}
