using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class WorkflowHttpSecretAdmissionIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostgreSql_OldOrCurrentChildOriginWithRetainedParentSourceAndProfileSurvivesReleasedLeaseExpiry(bool legacy) {
        var (fixture, parent) = await CreateMappedFixtureAsync(legacy);
        await using var lifetime = fixture;
        await using (var database = parent.Context()) {
            var claim = await database.DispatchClaims.SingleAsync(item => item.ClaimToken == parent.Claim.Value);
            Assert.Equal(DispatchClaimStatus.Released, claim.Status);
            Assert.True(claim.ExpiresAtUtc > claim.CreatedAtUtc);
        }
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(6);
        await fixture.InvokeAsync();
        Assert.Equal(1, fixture.Vault.ReadCount);
        Assert.Contains("Bearer admission-secret", await fixture.Server.Request, StringComparison.Ordinal);
        await using var read = parent.Context();
        Assert.Equal(DispatchClaimStatus.Released, (await read.DispatchClaims.SingleAsync()).Status);
        Assert.Null((await read.RuntimeSteps.SingleAsync()).ActiveClaimToken);
        await fixture.AssertPendingUnchangedAsync();
        if (legacy) {
            await using var workflow = await fixture.Factory.CreateDbContextAsync();
            Assert.IsType<WorkflowLaunchOrigin.ProcessAssignment>((await workflow.Set<WorkflowRunRecordEntity>()
                .AsNoTracking().SingleAsync(item => item.RunId == fixture.Run.RunId.Value)).ToSnapshot().Origin);
        }
    }

    [Theory]
    [InlineData(DeferredChange.Rework)]
    [InlineData(DeferredChange.ReworkThenDeferred)]
    [InlineData(DeferredChange.Assignment)]
    [InlineData(DeferredChange.ProjectReplacement)]
    [InlineData(DeferredChange.MissingEvents)]
    [InlineData(DeferredChange.PendingReceipt)]
    [InlineData(DeferredChange.MissingReceipt)]
    [InlineData(DeferredChange.ReceiptInput)]
    [InlineData(DeferredChange.ReceiptChild)]
    [InlineData(DeferredChange.AmbiguousChild)]
    public async Task PostgreSql_DeferredMappedApprovalRejectsChangedOriginalOwnerEvidence(DeferredChange change) {
        var (fixture, parent) = await CreateMappedFixtureAsync(legacy: false);
        await using var lifetime = fixture;
        switch (change) {
            case DeferredChange.Rework:
                await parent.ReworkAsync(deferAgain: false);
                break;
            case DeferredChange.ReworkThenDeferred:
                await parent.ReworkAsync(deferAgain: true);
                break;
            case DeferredChange.ProjectReplacement:
                await parent.ReplaceProjectAsync();
                break;
            case DeferredChange.Assignment:
                await using (var database = parent.Context()) {
                    var row = await database.RuntimeStepAssignments.SingleAsync();
                    row.WorkflowId = Guid.NewGuid();
                    await database.SaveChangesAsync();
                }
                break;
            case DeferredChange.AmbiguousChild:
                await using (var database = await fixture.Factory.CreateDbContextAsync()) {
                    database.Add(WorkflowRunRecordEntity.FromSnapshot(fixture.Run with { RunId = WorkflowRunId.New() }));
                    await database.SaveChangesAsync();
                }
                break;
            case DeferredChange.MissingEvents:
                await using (var database = parent.Context()) {
                    database.RuntimeEvents.Remove(await database.RuntimeEvents.SingleAsync(item =>
                        item.EventType == ProcessRuntimeEventTypes.StepWaiting.Value));
                    await database.SaveChangesAsync();
                }
                break;
            default:
                await using (var database = await fixture.Factory.CreateDbContextAsync()) {
                    var row = await database.Set<WorkflowLaunchIdempotencyRecordEntity>().SingleAsync();
                    if (change == DeferredChange.PendingReceipt) {
                        row.State = WorkflowLaunchIdempotencyClaimState.Pending;
                    } else if (change == DeferredChange.MissingReceipt) {
                        database.Remove(row);
                    } else if (change == DeferredChange.ReceiptInput) {
                        row.CanonicalInputHash = new string('0', 64);
                    } else if (change == DeferredChange.ReceiptChild) {
                        var completion = JsonSerializer.Deserialize<WorkflowLaunchIdempotencyCompletion>(row.CompletionJson, Fixture.JsonOptions)!;
                        row.CompletionJson = JsonSerializer.Serialize(completion with { Run = completion.Run with { RunId = WorkflowRunId.New() } }, Fixture.JsonOptions);
                    } else {
                        throw new ArgumentOutOfRangeException(nameof(change));
                    }
                    await database.SaveChangesAsync();
                }
                break;
        }
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());
        Assert.Equal(0, fixture.Vault.ReadCount);
        Assert.False(fixture.Server.Request.IsCompleted);
        Assert.False(fixture.Headers.SecretHeaderWasApplied);
        await fixture.AssertPendingUnchangedAsync();
        await using var retained = await fixture.Factory.CreateDbContextAsync();
        Assert.Equal(fixture.Run.RunId, (await retained.Set<WorkflowRunRecordEntity>().SingleAsync(row => row.RunId == fixture.Run.RunId.Value)).ToSnapshot().RunId);
    }

    [Fact]
    public async Task PostgreSql_OldChildOriginWithRetainedParentSourceRejectsReworkWithoutBackfill() {
        var (fixture, parent) = await CreateMappedFixtureAsync(legacy: true);
        await using var lifetime = fixture;
        await parent.ReworkAsync(deferAgain: true);
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());
        Assert.Equal(0, fixture.Vault.ReadCount);
        Assert.False(fixture.Server.Request.IsCompleted);
        await using var read = await fixture.Factory.CreateDbContextAsync();
        Assert.IsType<WorkflowLaunchOrigin.ProcessAssignment>((await read.Set<WorkflowRunRecordEntity>().SingleAsync()).ToSnapshot().Origin);
    }

    [Fact]
    public async Task PostgreSql_DeferredChildCannotReuseInitialClaimAdmissionForANewLaunch() {
        var (fixture, parent) = await CreateMappedFixtureAsync(legacy: false);
        await using var lifetime = fixture;
        var origin = Assert.IsType<WorkflowLaunchOrigin.ProcessDispatchAssignment>(fixture.Run.Origin);
        await Assert.ThrowsAnyAsync<Exception>(() => parent.Source.AcquireForMutationAsync(origin));
        await fixture.InvokeAsync();
        Assert.Equal(1, fixture.Vault.ReadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostgreSql_DeferredSourceIsRecheckedInsideCredentialTransactionAfterObservation(bool rework) {
        var (fixture, parent) = await CreateMappedFixtureAsync(legacy: false);
        await using var lifetime = fixture;
        var observation = new ContinuationChangeProbe(parent.Source, () => rework ? parent.ReworkAsync(deferAgain: true) : parent.CancelAsync());
        fixture.Headers.Inner = fixture.CreateOwner(mappedProcessSource: observation);
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());
        Assert.Equal(1, observation.Observed);
        Assert.Equal(0, fixture.Vault.ReadCount);
        Assert.False(fixture.Server.Request.IsCompleted);
        await fixture.AssertPendingUnchangedAsync();
    }

    [Fact]
    public async Task PostgreSql_PreCutoverParentWithoutOriginalProfileCannotMintContinuationAuthority() {
        var (fixture, parent) = await CreateMappedFixtureAsync(legacy: true);
        await using var lifetime = fixture;
        await using (var database = parent.Context()) {
            var state = await database.RuntimeStates.SingleAsync();
            state.LaunchAdmissionId = null;
            state.ProjectAdmissionDatabaseProfileId = null;
            state.ProjectAdmissionProjectId = null;
            state.ProjectAdmissionLifetimeId = null;
            await database.SaveChangesAsync();
        }
        var unavailable = await Assert.ThrowsAnyAsync<Exception>(() => parent.Source.AcquireForContinuationAsync(fixture.Run));
        Assert.Contains("retained original profile", unavailable.Message, StringComparison.Ordinal);
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());
        Assert.Equal(0, fixture.Vault.ReadCount);
        Assert.False(fixture.Server.Request.IsCompleted);
        await fixture.AssertPendingUnchangedAsync();
        await using var read = await fixture.Factory.CreateDbContextAsync();
        Assert.IsType<WorkflowLaunchOrigin.ProcessAssignment>((await read.Set<WorkflowRunRecordEntity>().SingleAsync()).ToSnapshot().Origin);
    }

    public enum DeferredChange { Rework, ReworkThenDeferred, Assignment, ProjectReplacement, MissingEvents, PendingReceipt, MissingReceipt, ReceiptInput, ReceiptChild, AmbiguousChild }

    private static async Task<(Fixture Http, MappedParent Parent)> CreateMappedFixtureAsync(bool legacy, bool captureDisclosure = false, bool projectBound = true) {
        MappedParent? parent = null;
        var http = await Fixture.CreateAsync(originBuilder: async (services, definition) => {
            parent = await MappedParent.CreateAsync(services, definition, projectBound);
            var origin = await parent.Source.CaptureAsync(new(parent.Assignment.RunId.Value), new(parent.Assignment.StepInstanceId.Value),
                parent.Claim.Value, parent.Contract.ContractHash);
            WorkflowLaunchOrigin savedOrigin = legacy ? new WorkflowLaunchOrigin.ProcessAssignment(origin.Dispatch.ProcessRun, origin.Dispatch.Assignment, origin.CorrelationId) {
                AuthorizationScope = origin.AuthorizationScope, AuthorizationPolicyFingerprint = origin.AuthorizationPolicyFingerprint
            } : origin;
            return (savedOrigin, parent.InputJson);
        }, captureDisclosure: captureDisclosure);
        try {
            Assert.NotNull(parent);
            await parent.SaveReceiptAsync(http);
            var forbiddenLaunch = new NoReplacementWorkflowLaunch();
            var producer = new WorkflowProcessStepExecutor(forbiddenLaunch, http.Scope.ServiceProvider.GetRequiredService<IWorkflowRuntimeManager>(),
                http.Scope.ServiceProvider.GetRequiredService<ProcessExecutionResultConverter>(), parent.Source,
                new PersistentWorkflowProcessAssignmentRunQuery(http.Factory, http.Scope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>()));
            await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() => producer.ExecuteAsync(parent.Assignment, parent.Contract,
                dispatchClaimIdentity: new(parent.Claim.Value)).AsTask());
            Assert.Equal(0, forbiddenLaunch.Calls);
            await parent.DeferAsync();
            http.Headers.Inner = http.CreateOwner(mappedProcessSource: parent.Source);
            return (http, parent);
        } catch {
            await http.DisposeAsync();
            throw;
        }
    }

    private sealed class MappedParent(IServiceProvider services, Clock clock, DbContextOptions<ProcessPersistenceDbContext> options,
        ProcessRuntimeStepAssignment assignment, ProcessStepExecutionContract contract, DispatchWorkItem work, DispatchClaimToken claim,
        ProcessProjectAdmission? project) {
        public ProcessRuntimeStepAssignment Assignment { get; } = assignment;
        public ProcessStepExecutionContract Contract { get; } = contract;
        public DispatchClaimToken Claim { get; private set; } = claim;
        public string InputJson => JsonSerializer.Serialize(new WorkflowProcessAssignmentInputEnvelope(WorkflowProcessAssignmentInputEnvelope.CurrentSchemaVersion,
            new(Assignment.RunId.Value), new(Assignment.StepInstanceId.Value), Assignment.StepKey, Assignment.RoleKey, Assignment.Prompt,
            Contract.ContractHash, Assignment.LaunchVariables), AgentOutputJson.SerializerOptions);
        public ProjectStructureWorkflowAuthorityService Source => services.GetRequiredService<ProjectStructureWorkflowAuthorityService>();
        public ProcessPersistenceDbContext Context() => new(options);
        private static DispatcherOwnerId Owner => new("deferred-workflow-test");
        private CoordinatedDatabaseTransaction Coordinator => services.GetRequiredService<CoordinatedDatabaseTransaction>();
        private EfProcessRuntimeUnitOfWork Unit(ProcessPersistenceDbContext context) => new(context, clock, Coordinator,
            new ProjectProcessAdmissionPolicy(services.GetRequiredService<ProjectWriteAdmissionService>()),
            services.GetRequiredService<IProcessLaunchAuthorityPolicy>());
        private RuntimeCommandContext Command() => new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("deferred-workflow-test")),
            new($"deferred-{Guid.NewGuid():N}"), clock.Now);

        public static async Task<MappedParent> CreateAsync(IServiceProvider services, WorkflowDefinition definition, bool projectBound = true) {
            var clock = Assert.IsType<Clock>(services.GetRequiredService<TimeProvider>());
            var canonical = services.GetRequiredService<ICanonicalRuntimeDatabase>();
            Guid? projectId = null;
            ProcessProjectAdmission? project = null;
            if (projectBound) {
                projectId = Guid.NewGuid();
                Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId.Value, new() { Name = "Deferred Workflow source" })).IsSuccess);
                var admission = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId.Value));
                project = new ProcessProjectAdmission(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId);
            }
            var configured = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
            AppDbContextOptionsConfigurator.Configure(configured, canonical.Profile);
            var options = configured.Options;
            var authority = await services.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
                .CaptureLocalAsync(projectId, ProcessLaunchOperatorSurface.UserInterface);
            Assert.Equal(canonical.Profile.Profile.Id, authority.DatabaseProfileId);
            Assert.Equal(project, authority.ProjectAdmission);
            Assert.Equal(ProcessLaunchOperatorSurface.UserInterface,
                Assert.IsType<ProcessLaunchPrincipal.LocalOperator>(authority.Principal).Surface);
            var preparation = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()));
            var initial = preparation.InitialCommit;
            var assignment = Assert.Single(initial.InitialAssignments!) with {
                ExecutorKind = ProcessLaunchExecutorKinds.Workflow, ExecutorId = "workflow-display",
                WorkflowBinding = new(new(definition.Id.Value), new(definition.VersionId.Value))
            };
            preparation = preparation with { InitialCommit = initial with { InitialAssignments = [assignment] } };
            var store = new EfProcessPreparedLaunchStore(new ProcessFactory(options), options,
                coordinatedTransaction: services.GetRequiredService<CoordinatedDatabaseTransaction>());
            var saved = await store.PrepareAsync(preparation);
            var state = initial.Mutation.State;
            var contract = ProcessRuntimeArtifactContracts.BuildStepContract(state, Assert.Single(state.Steps), initial.InitialPlan!.Branches);
            var work = new DispatchWorkItem(state.RunId, assignment.StepInstanceId, Assert.Single(state.Steps).StepDefinitionId,
                Assert.Single(initial.InitialPlan.Steps).ExecutionStrategyBinding!, 1, contract);
            var result = new MappedParent(services, clock, options, assignment, contract, work, new(Guid.NewGuid()), project);
            await using (var context = result.Context()) {
                Assert.True((await result.Unit(context).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
            }
            var persisted = await store.GetAsync(saved.Preparation.AdmissionId);
            Assert.NotNull(persisted);
            Assert.Equal(authority, persisted.Preparation.Authority);
            await using (var context = result.Context()) {
                var row = await context.RuntimeStates.SingleAsync(item => item.RunId == state.RunId.Value);
                row.Status = ProcessRuntimeStatus.Active;
                (await context.RuntimeSteps.SingleAsync(item => item.RunId == state.RunId.Value)).Status = ProcessRuntimeStepStatus.Ready;
                await context.SaveChangesAsync();
            }
            await result.ClaimAsync();
            return result;
        }

        private async Task ClaimAsync() {
            await using var context = Context();
            var unit = Unit(context);
            var state = (await unit.LoadAsync(Assignment.RunId))!;
            var engine = new ProcessRuntimeEngine(unit);
            var claimed = await engine.CreateClaimAsync(state, Command(), new(work with { AttemptNumber = Assert.Single(state.Steps).AttemptNumber + 1 },
                Owner, Claim, clock.Now.AddMinutes(5)));
            Assert.True(claimed.Succeeded);
            Assert.True((await engine.MarkClaimRunningAsync(claimed.State, Command(), Assignment.StepInstanceId, Claim)).Succeeded);
        }

        public async Task DeferAsync() {
            await using var context = Context();
            var unit = Unit(context);
            var result = await new ProcessRuntimeEngine(unit).DeferClaimAsync((await unit.LoadAsync(Assignment.RunId))!, Command(),
                new(Assignment.StepInstanceId, Owner, Claim, null));
            Assert.True(result.Succeeded);
            Assert.Equal(ProcessRuntimeStepStatus.Waiting, Assert.Single(result.State.Steps).Status);
        }

        public async Task ReworkAsync(bool deferAgain) {
            await using (var context = Context()) {
                var unit = Unit(context);
                Assert.True((await new ProcessRuntimeEngine(unit).RequestStepReworkAsync((await unit.LoadAsync(Assignment.RunId))!, Command(),
                    new(Assignment.StepInstanceId, "Operator changed this exact occurrence"))).Succeeded);
            }
            if (deferAgain) {
                Claim = new(Guid.NewGuid());
                await ClaimAsync();
                await DeferAsync();
            }
        }

        public async Task CancelAsync() {
            await using var context = Context();
            var unit = Unit(context);
            Assert.True((await new ProcessRuntimeEngine(unit).RequestCancellationAsync((await unit.LoadAsync(Assignment.RunId))!, Command())).Succeeded);
        }

        public async Task ReplaceProjectAsync() {
            Assert.NotNull(project);
            var projects = services.GetRequiredService<ProjectsService>();
            await projects.DeleteAsync(project.ProjectId);
            Assert.True((await projects.CreateAsync(project.ProjectId, new() { Name = "Replacement source" })).IsSuccess);
            var current = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(project.ProjectId));
            Assert.NotEqual(project.LifetimeId, current.LifetimeId);
        }

        public async Task SaveReceiptAsync(Fixture http) {
            var input = InputJson;
            var key = new WorkflowLaunchIdempotencyKey($"process-assignment:{Assignment.RunId.Value:N}:{Assignment.StepInstanceId.Value:N}");
            var intent = new WorkflowLaunchIntent(new WorkflowDefinitionSelection.ExactSavedVersion(http.Definition.Id, http.Definition.VersionId),
                WorkflowLaunchMode.Production, http.Run.Origin!, input, WorkflowLaunchCompletionPolicy.WaitForStopped,
                new WorkflowLaunchIdempotency.CallerSupplied(key));
            var scope = WorkflowLaunchIdempotencyRequestFactory.CreateScope(intent, key);
            var store = new PersistentWorkflowLaunchIdempotencyStore(http.Factory);
            var token = new WorkflowLaunchIdempotencyClaimToken(Guid.NewGuid());
            var claimed = await store.TryClaimAsync(scope, WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(intent, input),
                token, http.Run.RunId, clock.Now, clock.Now.AddMinutes(5));
            Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Acquired, claimed.Outcome);
            var backend = new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess]).GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
            Assert.True(await store.TryCompleteClaimAsync(scope, token, new(http.Run,
                new(http.Definition, input, backend, WorkflowPreviewSimulationPlan.Empty, intent.Mode, intent.Origin,
                    intent.CompletionPolicy, intent.Idempotency, clock.Now), clock.Now)));
        }
    }

    private sealed class ProcessFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class NoReplacementWorkflowLaunch : IWorkflowLaunchService {
        public int Calls { get; private set; }
        public Task<WorkflowLaunchResult> LaunchAsync(WorkflowLaunchIntent intent, CancellationToken cancellationToken = default) {
            Calls++;
            throw new InvalidOperationException("An existing deferred child must not be replaced.");
        }
    }

    private sealed class ContinuationChangeProbe(IWorkflowMappedProcessSourceAuthority inner, Func<Task> change) : IWorkflowMappedProcessSourceAuthority {
        public int Observed { get; private set; }
        public Task<WorkflowLaunchOrigin.ProcessDispatchAssignment> CaptureAsync(WorkflowProcessRunId runId,
            WorkflowProcessAssignmentId assignmentId, Guid claimToken, string contractHash, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A continuation cannot acquire replacement launch authority.");
        public Task<IWorkflowStructureSourceAuthorityLease> AcquireForMutationAsync(WorkflowLaunchOrigin.ProcessDispatchAssignment origin,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A continuation cannot reuse initial launch admission.");
        public async Task<IWorkflowMappedProcessContinuationLease> AcquireForContinuationAsync(WorkflowRunSnapshot child,
            CancellationToken cancellationToken = default) {
            var lease = await inner.AcquireForContinuationAsync(child, cancellationToken);
            try {
                Observed++;
                await change();
                return lease;
            } catch {
                await lease.DisposeAsync();
                throw;
            }
        }
    }
}
