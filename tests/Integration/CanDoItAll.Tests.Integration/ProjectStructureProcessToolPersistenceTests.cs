using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProjectStructureProcessToolPersistenceTests {
    [Fact]
    public async Task First_admitted_node_start_prepares_the_real_definition_and_binds_the_exact_source_before_commit() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider, useRealDefinition: true);
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var batch = await fixture.AdmitAsync(journal, lease, "start");
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "start", fixture.Payload, default);
        using var dispatch = claim.Bind();
        var invocation = await fixture.CaptureAsync();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>().FindByIntentAsync(invocation.IntentId));
        var result = await fixture.StartAsync(invocation);
        Assert.NotNull(result.RunId);
        Assert.Equal(fixture.Input.Request.ProcessDefinitionId, result.ProcessDefinitionId);
        var saved = Assert.IsType<ProcessPreparedLaunchSnapshot>(await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
            .FindByIntentAsync(invocation.IntentId));
        Assert.Equal(fixture.Payload.Digest.Value, saved.Preparation.Request.ProducerInputFingerprint);
        Assert.Equal(fixture.Input.NodeId, saved.Preparation.LinkTarget!.SourceNodeKey);
        Assert.Equal(invocation.Authority.ProjectAdmission, saved.Preparation.InitialCommit.Mutation.State.ProjectAdmission);
        Assert.Equal(result.RunId, saved.Preparation.InitialCommit.Mutation.State.RunId.Value);
        Assert.NotNull(saved.AcceptedAtUtc);
    }

    [Fact]
    public async Task Registered_native_tool_uses_its_owned_journal_payload_and_dispatches_the_saved_Process_admission() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var provider = Assert.Single(scope.ServiceProvider.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var batch = await fixture.AdmitAsync(journal, lease, "start");
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "start", fixture.Payload, default);
        using var dispatch = claim.Bind();
        var invocation = await fixture.RequireAndSeedAsync();
        var tools = await provider.CreateToolsAsync(fixture.Context, default);
        var metadata = Assert.Single(provider.GetToolMetadata(fixture.Context),
            item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureNodeProcessStart);
        Assert.Equal(fixture.Payload, metadata.PrepareAdmission!(JsonSerializer.SerializeToElement(fixture.Input,
            ProjectStructureProcessProposalCodec.SerializerOptions)));
        var tool = Assert.Single(tools.OfType<AIFunction>(), item => item.Name == ProjectStructureToolPolicy.ProjectStructureNodeProcessStart);
        using var effect = AgentToolInvocationEffectScope.Begin();
        var transport = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments {
            ["projectId"] = fixture.Input.ProjectId,
            ["nodeId"] = fixture.Input.NodeId,
            ["request"] = fixture.Input.Request
        }));
        var result = Assert.IsType<ProjectStructureProcessNodeStartResult>(transport.Deserialize<ProjectStructureProcessNodeStartResult>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }));
        Assert.NotNull(result.RunId);
        var saved = Assert.IsType<ProcessPreparedLaunchSnapshot>(await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
            .FindByIntentAsync(invocation.IntentId));
        Assert.Equal(saved.Preparation.AdmissionId, result.Observation!.AdmissionId);
        Assert.Equal(saved.Preparation.InitialCommit.Mutation.State.RunId.Value, result.RunId);
        Assert.Equal(new AgentToolCommittedEffect(ProjectStructureProcessToolAdmission.EffectSourceKind, saved.Preparation.AdmissionId.Value.ToString("D")),
            effect.CommittedEffect);
    }

    [Fact]
    public async Task Identical_proposals_in_one_serial_batch_keep_distinct_Process_intents_and_original_run_ids() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var batch = await fixture.AdmitAsync(journal, lease, "first", "second");
        var runs = new List<Guid>();
        foreach (var call in new[] { "first", "second" }) {
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, call, fixture.Payload, default);
            using var dispatch = claim.Bind();
            var invocation = await fixture.RequireAndSeedAsync();
            var result = await fixture.StartAsync(invocation);
            runs.Add(Assert.IsType<Guid>(result.RunId));
            Assert.Equal(invocation.IntentId.Value, claim.Proposal.IntentId.Value);
            await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope(), AgentToolEffectState.Committed, default);
        }
        Assert.NotEqual(runs[0], runs[1]);
        await using var context = new ProcessPersistenceDbContext(Options(scope.ServiceProvider));
        Assert.Equal(2, await context.PreparedLaunches.CountAsync(row => runs.Contains(row.RunId)));
        Assert.Equal(2, await context.RuntimeStates.CountAsync(row => runs.Contains(row.RunId)));
        Assert.NotEqual(batch.Proposals[0].IntentId, batch.Proposals[1].IntentId);
    }

    [Fact]
    public async Task Actual_Process_commit_ack_loss_then_journal_restart_and_revocation_replays_original_identity() {
        var failure = new ArgumentException("Injected native Process admission acknowledgement loss.");
        var fault = new AckFault(failure);
        await using var app = await TestApplication.CreateAsync(Harness(fault));
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        AgentToolBatchRecord batch;
        ProjectStructureProcessNodeStartResult first;
        var journal = fixture.Journal.NewJournal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default)) {
            using var current = lease.Bind();
            batch = await fixture.AdmitAsync(journal, lease, "start");
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "start", fixture.Payload, default);
            using var dispatch = claim.Bind();
            var invocation = await fixture.RequireAndSeedAsync();
            fault.ExpectedRunId = Assert.IsType<ProcessPreparedLaunchSnapshot>(await scope.ServiceProvider
                .GetRequiredService<IProcessPreparedLaunchStore>().FindByIntentAsync(invocation.IntentId))
                .Preparation.InitialCommit.Mutation.State.RunId.Value;
            fault.Armed = true;
            first = await fixture.StartAsync(invocation);
            Assert.NotNull(first.RunId);
            Assert.Same(failure, first.Observation!.ObservationException);
        }
        Assert.Equal(1, fault.Calls);
        await fixture.RevokeAsync();
        var restarted = fixture.Journal.NewJournal(fixture.Journal.NewStore());
        await using var retryLease = await restarted.AcquireRunAsync(fixture.Journal.Session, default);
        using var retryCurrent = retryLease.Bind();
        var replayClaim = await restarted.ClaimInvocationAsync(retryLease, batch.Id, "start", fixture.Payload, default);
        using var retryDispatch = replayClaim.Bind();
        var replayInvocation = await fixture.RequireAndSeedAsync();
        var replay = await fixture.StartAsync(replayInvocation);
        Assert.Equal(first.RunId, replay.RunId);
        Assert.Equal(first.LaunchPlanId, replay.LaunchPlanId);
        Assert.Equal(first.Observation.AdmissionId, replay.Observation!.AdmissionId);
        Assert.Equal(batch.Proposals[0].IntentId.Value, replayInvocation.IntentId.Value);
        await using var context = new ProcessPersistenceDbContext(Options(scope.ServiceProvider));
        Assert.Equal(1, await context.RuntimeStates.CountAsync(row => row.RunId == first.RunId));
        Assert.Equal(1, await context.PreparedLaunches.CountAsync(row => row.CallerIntentId == replayInvocation.IntentId.Value));
    }

    [Fact]
    public async Task Changed_tool_input_is_rejected_by_the_live_journal_before_any_Process_preparation() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var batch = await fixture.AdmitAsync(journal, lease, "start");
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "start", fixture.Payload, default);
        using var dispatch = claim.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Adapter.RequireInvocationAsync(fixture.Context,
            fixture.Input with { NodeId = "another-source" }, default));
        await using var context = new ProcessPersistenceDbContext(Options(scope.ServiceProvider));
        Assert.False(await context.PreparedLaunches.AnyAsync(row => row.CallerIntentId == claim.Proposal.IntentId.Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restart_replay_preserves_human_unlink_or_project_deletion_without_reading_a_new_source_surface(bool deleteProject) {
        await using var environment = CanDoItAllTestEnvironment.Create("structure-tool-replay");
        var profile = environment.CreatePostgreSqlProfile("structure-tool-replay");
        ProjectStructureProcessLaunchInvocation invocation;
        ProjectStructureProcessStartProposal input;
        ProjectStructureProcessNodeStartResult first;
        await using (var app = await TestApplication.CreateAsync(Harness(environment: environment, profile: profile))) {
            await using var scope = app.Services.CreateAsyncScope();
            await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
            var journal = fixture.Journal.NewJournal();
            await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
            using var current = lease.Bind();
            var batch = await fixture.AdmitAsync(journal, lease, "start");
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "start", fixture.Payload, default);
            using var dispatch = claim.Bind();
            invocation = await fixture.RequireAndSeedAsync();
            first = await fixture.StartAsync(invocation);
            input = fixture.Input;
            Assert.Equal(ProcessLaunchLinkDeliveryState.Delivered, first.Observation!.LinkDeliveryState);
            if (deleteProject) {
                var deletion = await scope.ServiceProvider.GetRequiredService<ProjectsService>().DeleteAsync(input.ProjectId);
                Assert.Equal(input.ProjectId, deletion.ProjectId);
                Assert.Empty(deletion.Warnings);
            } else {
                Assert.True(await scope.ServiceProvider.GetRequiredService<ProjectWorkbenchRelationService>().UnlinkObjectsAsync(input.ProjectId,
                    input.NodeId, ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(first.RunId!.Value), ProjectObjectLinkKind.Uses));
                await using var context = new WorkbenchDbContext(OwnerOptions<WorkbenchDbContext>(scope.ServiceProvider));
                var original = await context.Set<ProjectObjectRecord>().SingleAsync(row => row.NodeKey == input.NodeId);
                original.Title = "Human edited after Process acceptance";
                await context.SaveChangesAsync();
            }
        }
        await using var restarted = await TestApplication.CreateAsync(Harness(environment: environment, profile: profile));
        await using var currentScope = restarted.Services.CreateAsyncScope();
        var result = await currentScope.ServiceProvider.GetRequiredService<ProjectStructureProcessNodeService>().StartAsync(input.ProjectId,
            input.NodeId, input.Request, Agent(invocation));
        Assert.Equal(first.RunId, result.RunId);
        Assert.Equal(first.LaunchPlanId, result.LaunchPlanId);
        Assert.Equal(first.Observation!.AdmissionId, result.Observation!.AdmissionId);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Removed, result.Observation.LinkDeliveryState);
        await using var workbench = new WorkbenchDbContext(OwnerOptions<WorkbenchDbContext>(currentScope.ServiceProvider));
        Assert.False(await workbench.Set<ProjectObjectLinkRecord>().AnyAsync(row => row.ProjectId == input.ProjectId &&
            row.SourceNodeKey == input.NodeId && row.TargetNodeKey == ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(first.RunId!.Value)));
        if (!deleteProject) {
            Assert.Equal("Human edited after Process acceptance", (await workbench.Set<ProjectObjectRecord>()
                .SingleAsync(row => row.NodeKey == input.NodeId)).Title);
        }
    }

    [Fact]
    public async Task Cancelled_journal_reconciliation_reads_retained_identity_after_revocation_without_changing_Process_rows() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var journal = fixture.Journal.NewJournal();
        ProjectStructureProcessNodeStartResult accepted;
        await using (var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default)) {
            using var current = lease.Bind();
            var batch = await fixture.AdmitAsync(journal, lease, "start");
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "start", fixture.Payload, default);
            using var dispatch = claim.Bind();
            accepted = await fixture.StartAsync(await fixture.RequireAndSeedAsync());
        }
        await fixture.RevokeAsync();
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Journal.Store).UpdateExecutionRunDetailAsync(fixture.Journal.Session.ExecutionRunId,
            detail => detail with { Run = detail.Run with { State = ExecutionState.Completed, Outcome = RunOutcome.Cancelled,
                CompletedAtUtc = DateTimeOffset.UtcNow, Revision = detail.Run.Revision + 1 } });
        await using var before = new ProcessPersistenceDbContext(Options(scope.ServiceProvider));
        var beforeRow = await before.PreparedLaunches.AsNoTracking().SingleAsync(row => row.Id == accepted.Observation!.AdmissionId.Value);
        var eventCount = await before.RuntimeEvents.CountAsync(row => row.RunId == accepted.RunId);
        await using var readLease = await journal.AcquireCancelledReconciliationAsync(fixture.Journal.Session, default);
        using var bound = readLease.Bind();
        var reconciled = await journal.ReconcileCancelledAsync(readLease, [fixture.Adapter], currentReadAllowed: true, default);
        var outcome = Assert.Single(reconciled.Outcomes);
        Assert.Equal(AgentToolEffectState.Committed, outcome.EffectState);
        Assert.Equal(AgentToolCancellationDisposition.ReceiptCommitted, outcome.Cancellation!.Disposition);
        await using var after = new ProcessPersistenceDbContext(Options(scope.ServiceProvider));
        var afterRow = await after.PreparedLaunches.AsNoTracking().SingleAsync(row => row.Id == beforeRow.Id);
        Assert.Equal(beforeRow.PreparationFingerprint, afterRow.PreparationFingerprint);
        Assert.Equal(beforeRow.State, afterRow.State);
        Assert.Equal(beforeRow.ContinuationGeneration, afterRow.ContinuationGeneration);
        Assert.Equal(eventCount, await after.RuntimeEvents.CountAsync(row => row.RunId == accepted.RunId));
    }

    private static ProjectStructureAgentContext Agent(ProjectStructureProcessLaunchInvocation invocation)
        => new("display-source", "Original launch display", "fixture", string.Empty, string.Empty, "same-diagnostic-session") {
            ProcessLaunchInvocation = invocation
        };

    private static TestHarnessOptions Harness(AckFault? fault = null, CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null)
        => new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
            services.TryAddSingleton<ProjectStructureProcessProposalCodec>();
            services.TryAddScoped<ProjectStructureProcessToolAdmission>();
            foreach (var item in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                services.Remove(item);
            }
            services.Replace(ServiceDescriptor.Singleton<IProcessLaunchArtifactInitializer>(new NoArtifacts()));
            if (fault is not null) {
                services.AddScoped(provider => new ProcessPersistenceDbContext(Options(provider, fault)));
            }
        } };

    private static DbContextOptions<ProcessPersistenceDbContext> Options(IServiceProvider services, params IInterceptor[] interceptors)
        => OwnerOptions<ProcessPersistenceDbContext>(services, interceptors);

    private static DbContextOptions<T> OwnerOptions<T>(IServiceProvider services, params IInterceptor[] interceptors) where T : DbContext {
        var builder = new DbContextOptionsBuilder<T>();
        AppDbContextOptionsConfigurator.Configure(builder, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        return builder.AddInterceptors(interceptors).Options;
    }

    private sealed class Fixture(IServiceProvider services, AgentToolAdmissionJournalFixture journal,
        ProjectWriteAdmission project, ProjectStructureProcessStartProposal input, AgentDefinition agent,
        FileSandboxWorkspaceStore catalog) : IAsyncDisposable {
        public AgentToolAdmissionJournalFixture Journal { get; } = journal;
        public ProjectStructureProcessStartProposal Input { get; } = input;
        public AgentToolPreparedPayload Payload { get; } = new ProjectStructureProcessProposalCodec().Prepare(input);
        public ProjectStructureProcessToolAdmission Adapter { get; } = new(new AgentToolAdmissionVerifier(),
            services.GetRequiredService<ICanonicalRuntimeDatabase>(), services.GetRequiredService<IProcessPreparedLaunchStore>(),
            services.GetRequiredService<ProjectProcessLaunchAuthorityService>(), new());
        public AgentRuntimeToolProviderContext Context { get; } = new(agent, journal.Provider, [], false,
            AgentRuntimeToolProviderPurpose.InteractiveChat, "diagnostic-session", AgentRuntimeContextIntent.Empty with {
                SourceKind = "project-structure", SourceId = project.ProjectId.ToString("D"), Purpose = AgentRuntimeContextPurpose.InteractiveChat
            }, new Dictionary<string, string>()) {
            AdmittedToolSession = journal.Session,
            ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(journal.Detail.Run.MetadataJson)
        };

        public static async Task<Fixture> CreateAsync(IServiceProvider services, bool useRealDefinition = false) {
            var projectId = Guid.NewGuid();
            Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Journal Process owner" })).IsSuccess);
            var project = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
            var node = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(projectId,
                new(ProjectObjectType.ProjectBlock, "Original source", "", "Original review context", $"project:{projectId}", 420, 260));
            var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>();
            var generation = services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration();
            var journal = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: new(profile.Profile.Profile.Id, "process-tool-test", generation),
                transientContext: new("Original project launch authority", WorkspaceScopeDescriptor.Project(projectId.ToString("D"))),
                configureAgent: source => source with {
                    Id = Guid.NewGuid(), Name = "Process tool journal source", TemplateKey = string.Empty, Tags = []
                });
            var catalog = new FileSandboxWorkspaceStore(profile.Profile.Profile.Storage.WorkspaceRoot,
                WorkspaceScopeDescriptor.Organization(profile.Profile.Profile.Id.ToString("N")),
                new AgentProjectAccessCatalogPolicy(services.GetRequiredService<ProjectWriteAdmissionService>(), profile.Profile.Profile.Id));
            var actor = journal.Agent with { IsTemplate = false, Status = AgentLifecycleStatus.Active,
                Permissions = AgentPermissionsPolicy.Default, ConfigurationJson = AgentProjectStructureAccessMetadata.Write("{}", new() {
                    CanRead = true, CanWrite = true, AllowedProjectIds = [projectId],
                    AllowedProjectLifetimes = [new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)]
                }) };
            var saved = await catalog.UpdateCatalogAsync(current => current with { Agents = current.Agents.Where(item => item.Id != actor.Id).Append(actor).ToArray() });
            actor = Assert.Single(saved.Agents, item => item.Id == actor.Id);
            var savedAccess = AgentProjectStructureAccessMetadata.Read(actor.ConfigurationJson);
            Assert.True(savedAccess.CanWrite);
            Assert.Contains(new AgentProjectStructureLifetime(project.DatabaseProfileId, project.ProjectId, project.LifetimeId), savedAccess.AllowedProjectLifetimes);
            var definition = useRealDefinition ? ProcessDefinitionCatalogProjectionService.CreateDefinitionId(new("software-delivery")).Value : (Guid?)null;
            return new(services, journal, project, new(projectId, node.Id, new(ProcessDefinitionId: definition,
                RunHrMatch: false, Execute: false, IncludeLaunchPlan: true)), actor, catalog);
        }

        public async Task<AgentToolBatchRecord> AdmitAsync(AgentToolAdmissionJournal owner, AgentToolRunLease lease, params string[] calls) {
            await owner.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var saved = await owner.AdmitBatchAsync(lease, Payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                calls.Select(call => new AgentToolPreparedCall(call, Payload, false)).ToArray(), default);
            return Assert.Single(saved.Batches);
        }

        public async Task<ProjectStructureProcessLaunchInvocation> RequireAndSeedAsync() {
            var claim = await Adapter.RequireInvocationAsync(Context, Input, default);
            if (await Adapter.FindReplayAsync(claim, default) is { } retained) {
                return retained;
            }
            var invocation = await Adapter.CaptureAsync(claim, project, default);
            var target = await services.GetRequiredService<ProjectProcessLaunchTargetQuery>().CaptureAsync(Input.ProjectId, Input.NodeId);
            var preparation = ProcessPreparedLaunchFixture.Create(invocation.Authority, invocation.IntentId, target);
            var request = preparation.Request with { ProducerInputFingerprint = invocation.InputFingerprint };
            await services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(preparation with {
                Request = request, RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(request)
            });
            return invocation;
        }

        public async Task<ProjectStructureProcessLaunchInvocation> CaptureAsync()
            => await Adapter.CaptureAsync(await Adapter.RequireInvocationAsync(Context, Input, default), project, default);

        public Task<ProjectStructureProcessNodeStartResult> StartAsync(ProjectStructureProcessLaunchInvocation invocation)
            => services.GetRequiredService<ProjectStructureProcessNodeService>().StartAsync(Input.ProjectId, Input.NodeId, Input.Request, Agent(invocation));

        public Task<SandboxWorkspaceCatalog> RevokeAsync() => catalog.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Select(item => item.Id == agent.Id ? item with {
                Permissions = item.Permissions with { CanUseTools = false }
            } : item).ToArray()
        });

        public Task<SandboxWorkspaceCatalog> ChangeSourceAsync(Func<AgentDefinition, AgentDefinition> change,
            CancellationToken cancellationToken = default) => catalog.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Select(item => item.Id == agent.Id ? change(item) : item).ToArray()
            }, cancellationToken);

        public ValueTask DisposeAsync() => Journal.DisposeAsync();
    }

    private sealed class AckFault(Exception failure) : DbTransactionInterceptor {
        public bool Armed { get; set; }
        public Guid ExpectedRunId { get; set; }
        public int Calls { get; private set; }
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (!Armed || eventData.Context is not ProcessPersistenceDbContext context) {
                return;
            }
            Assert.NotEqual(Guid.Empty, ExpectedRunId);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM process_runtime_states WHERE \"RunId\" = @run_id);";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "run_id";
            parameter.Value = ExpectedRunId;
            command.Parameters.Add(parameter);
            Assert.True((bool)(await command.ExecuteScalarAsync(cancellationToken))!);
            Armed = false;
            Calls++;
            throw failure;
        }
    }

    private sealed class NoArtifacts : IProcessLaunchArtifactInitializer {
        public Task InitializeAsync(ProcessLaunchArtifactInitializationRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
