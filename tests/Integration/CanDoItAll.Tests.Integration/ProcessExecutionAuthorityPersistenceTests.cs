using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessExecutionAuthorityPersistenceTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Restart_reads_exact_saved_source_and_lifetime_without_impersonating_executor(int channel) {
        await using var environment = CanDoItAllTestEnvironment.Create("process-execution-authority");
        var profile = environment.CreatePostgreSqlProfile("authority");
        var harness = Harness(environment, profile);
        Fixture fixture;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var scope = app.Services.CreateAsyncScope();
            fixture = await SeedAsync(scope.ServiceProvider, channel);
            var actual = Assert.IsType<ProcessExecutionProjectAuthoritySnapshot>((await Reader(scope.ServiceProvider).ReadAsync(fixture.Run.Id)).Snapshot);
            AssertSnapshot(fixture, actual);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = restarted.Services.CreateAsyncScope();
        var probe = new ReadProbe();
        var reader = Reader(reopened.ServiceProvider, probe);
        var result = await reader.ReadAsync(fixture.Run.Id);
        Assert.Equal(ProcessExecutionAuthorityDisposition.Bound, result.Disposition);
        AssertSnapshot(fixture, Assert.IsType<ProcessExecutionProjectAuthoritySnapshot>(result.Snapshot));
        Assert.Equal(2, probe.Selects);
        Assert.Equal(0, probe.Writes);
        var storedRun = await reopened.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id);
        Assert.Equal(fixture.Run.Revision, storedRun!.Revision);
        Assert.Null(storedRun.ToolAdmission);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Exact_old_source_remains_observable_after_dispatch_expiry_replacement_cancellation_or_completion(int change) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await SeedAsync(services);
        await using (var context = new ProcessPersistenceDbContext(Options(services))) {
            var state = await context.RuntimeStates.SingleAsync(item => item.RunId == fixture.RunId);
            var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == fixture.RunId);
            var claim = await context.DispatchClaims.SingleAsync(item => item.RunId == fixture.RunId);
            switch (change) {
                case 0:
                    claim.ExpiresAtUtc = ProcessProjectAdmissionFixture.Now.AddSeconds(3);
                    break;
                case 1:
                    step.ActiveClaimToken = Guid.NewGuid();
                    break;
                case 2:
                    state.Status = ProcessRuntimeStatus.CancelRequested;
                    break;
                case 3:
                    step.Status = ProcessRuntimeStepStatus.Completed;
                    claim.Status = DispatchClaimStatus.Completed;
                    break;
            }
            await context.SaveChangesAsync();
        }
        var result = await Reader(services).ReadAsync(fixture.Run.Id);
        var snapshot = Assert.IsType<ProcessExecutionProjectAuthoritySnapshot>(result.Snapshot);
        Assert.Equal(ProcessExecutionAuthorityDisposition.Bound, result.Disposition);
        Assert.False(snapshot.ObservedCurrentDispatch);
        Assert.Equal(fixture.Prepared.Preparation.AdmissionId, snapshot.Reference.AdmissionId);
        Assert.Equal(fixture.Prepared.Preparation.Authority!.ProjectAdmission, snapshot.SourceAuthority.ProjectAdmission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Assignment_repair_or_a_claim_from_another_occurrence_cannot_bind_the_old_execution(bool changeClaim) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await SeedAsync(services);
        if (changeClaim) {
            var changed = fixture.Run with { MetadataJson = Metadata(Guid.NewGuid()) };
            await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().SaveExecutionRunDetailAsync(new(changed, null, [], []));
        } else {
            await using var context = new ProcessPersistenceDbContext(Options(services));
            var assignment = await context.RuntimeStepAssignments.SingleAsync(item => item.RunId == fixture.RunId);
            assignment.ExecutorId = Guid.NewGuid().ToString("D");
            assignment.ReadinessHash = "sha256:repaired-executor";
            await context.SaveChangesAsync();
        }
        var result = await Reader(services).ReadAsync(fixture.Run.Id);
        Assert.Equal(ProcessExecutionAuthorityDisposition.DispatchBindingChanged, result.Disposition);
        Assert.Null(result.Snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_missing_admission_or_claim_requires_reconciliation_without_rewriting_history(bool missingClaim) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await SeedAsync(services);
        if (missingClaim) {
            await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().SaveExecutionRunDetailAsync(new(fixture.Run with {
                MetadataJson = "{}"
            }, null, [], []));
        } else {
            await using var context = new ProcessPersistenceDbContext(Options(services));
            var state = await context.RuntimeStates.SingleAsync(item => item.RunId == fixture.RunId);
            state.LaunchAdmissionId = null;
            await context.SaveChangesAsync();
        }
        var result = await Reader(services).ReadAsync(fixture.Run.Id);
        Assert.Equal(ProcessExecutionAuthorityDisposition.AdmissionReconciliationRequired, result.Disposition);
        Assert.Null(result.Snapshot);
        Assert.NotNull(await services.GetRequiredService<IProcessPreparedLaunchStore>().GetAsync(fixture.Prepared.Preparation.AdmissionId));
        Assert.NotNull(await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(new(fixture.RunId)));
    }

    [Fact]
    public async Task Runtime_lifetime_mismatch_fails_instead_of_recapturing_the_current_project() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await SeedAsync(services);
        await using (var context = new ProcessPersistenceDbContext(Options(services))) {
            var state = await context.RuntimeStates.SingleAsync(item => item.RunId == fixture.RunId);
            state.ProjectAdmissionLifetimeId = Guid.NewGuid();
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => Reader(services).ReadAsync(fixture.Run.Id));
    }

    [Fact]
    public async Task Another_profile_cannot_join_copied_execution_metadata_to_canonical_process_state() {
        await using var first = await TestApplication.CreateAsync(Harness());
        await using var second = await TestApplication.CreateAsync(Harness());
        await using var firstScope = first.Services.CreateAsyncScope();
        await using var secondScope = second.Services.CreateAsyncScope();
        var fixture = await SeedAsync(firstScope.ServiceProvider);
        var services = secondScope.ServiceProvider;
        var source = await firstScope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>().LoadCatalogAsync();
        var copiedAgent = Assert.Single(source.Agents, agent => agent.Id == fixture.Run.AgentId);
        await services.GetRequiredService<ISandboxWorkspaceStore>().UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Where(agent => agent.Id != copiedAgent.Id).Append(copiedAgent).ToArray()
        });
        await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().SaveExecutionRunDetailAsync(new(fixture.Run, null, [], []));
        var result = await Reader(services).ReadAsync(fixture.Run.Id);
        Assert.Equal(ProcessExecutionAuthorityDisposition.DispatchBindingChanged, result.Disposition);
        Assert.Null(result.Snapshot);
        Assert.Null(await services.GetRequiredService<IProcessPreparedLaunchStore>().GetAsync(fixture.Prepared.Preparation.AdmissionId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Display_metadata_and_other_source_kinds_do_not_become_Process_authority(int kind) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await SeedAsync(services);
        var changed = kind switch {
            0 => fixture.Run with { SourceKind = "hr" },
            1 => fixture.Run with { RequestedByKind = "user" },
            _ => fixture.Run with { RequestedBy = "display-agent" }
        };
        await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().SaveExecutionRunDetailAsync(new(changed, null, [], []));
        var result = await Reader(services).ReadAsync(fixture.Run.Id);
        Assert.Equal(ProcessExecutionAuthorityDisposition.UnsupportedSource, result.Disposition);
        Assert.Null(result.Snapshot);
    }

    private static async Task<Fixture> SeedAsync(IServiceProvider services, int channel = 0) {
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Saved Process source" })).IsSuccess);
        var project = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
        var source = channel switch {
            0 => (ProcessLaunchPrincipal)new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface),
            1 => new ProcessLaunchPrincipal.AuthenticatedOperator("original-subject", ProcessProjectAdmissionFixture.Now.AddDays(1)),
            _ => new ProcessLaunchPrincipal.AgentExecution(new(Guid.NewGuid(), Guid.NewGuid(), 1,
                ProcessLaunchSourceScopeKind.Project, projectId.ToString("D"), true, true, "fixture-policy", "fixture-ceiling",
                ["project_structure.node.create"], ["fixture-capability"], ["product"], ["reference"], ["artifact:retained"]),
                ProcessLaunchAgentOperation.StructureStart)
        };
        var authority = new ProcessLaunchAuthority(source, project.DatabaseProfileId,
            new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId), true, true, "fixture-source");
        var prepared = await services.GetRequiredService<IProcessPreparedLaunchStore>()
            .PrepareAsync(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())));
        await using (var context = new ProcessPersistenceDbContext(Options(services))) {
            var commit = new EfProcessRuntimeUnitOfWork(context,
                coordinatedTransaction: services.GetRequiredService<CoordinatedDatabaseTransaction>(),
                projectAdmissionPolicy: services.GetRequiredService<IProcessProjectAdmissionPolicy>(),
                launchAuthorityPolicy: new FixtureAdmissionPolicy());
            Assert.True((await commit.CommitAsync(ProcessPreparedLaunchFixture.Commit(prepared))).Succeeded);
        }
        prepared = (await services.GetRequiredService<IProcessPreparedLaunchStore>().GetAsync(prepared.Preparation.AdmissionId))!;
        var assignment = Assert.Single(prepared.Preparation.InitialCommit.InitialAssignments!);
        var catalog = await services.GetRequiredService<ISandboxWorkspaceStore>().UpdateCatalogAsync(current => {
            var executor = current.Agents.First() with {
                Id = Guid.Parse(assignment.ExecutorId), Name = "Authority fixture executor", TemplateKey = string.Empty, Tags = [],
                IsTemplate = false, Status = AgentLifecycleStatus.Active, ConfigurationJson = "{}", Capabilities = []
            };
            return current with { Agents = [.. current.Agents, executor] };
        });
        Assert.Contains(catalog.Agents, agent => agent.Id == Guid.Parse(assignment.ExecutorId));
        var claim = Guid.NewGuid();
        await using (var context = new ProcessPersistenceDbContext(Options(services))) {
            var state = await context.RuntimeStates.SingleAsync(item => item.RunId == assignment.RunId.Value);
            var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == assignment.RunId.Value);
            state.Status = ProcessRuntimeStatus.Active;
            step.Status = ProcessRuntimeStepStatus.Running;
            step.AttemptNumber = 1;
            step.ActiveClaimToken = claim;
            context.DispatchClaims.Add(new() { RunId = assignment.RunId.Value, StepInstanceId = assignment.StepInstanceId.Value,
                ClaimToken = claim, OwnerId = "fixture-dispatch", Status = DispatchClaimStatus.Claimed, AttemptNumber = 1,
                CreatedAtUtc = ProcessProjectAdmissionFixture.Now, ExpiresAtUtc = ProcessProjectAdmissionFixture.Now.AddHours(1) });
            await context.SaveChangesAsync();
        }
        var created = ProcessProjectAdmissionFixture.Now.AddSeconds(1);
        var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.Parse(assignment.ExecutorId), null, "Saved Process execution",
            "process-step", assignment.StepKey, assignment.RunId.ToString(), assignment.StepInstanceId.ToString(),
            "process-runtime", "system", Metadata(claim), "Execute assigned step.", string.Empty, "fixture", "fixture-model",
            ExecutionState.Running, null, created, created, created, null, string.Empty, null, [],
            ProcessRunId: assignment.RunId.ToString(), ProcessStepId: assignment.StepInstanceId.ToString());
        run = (await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().SaveExecutionRunDetailAsync(new(run, null, [], []))).Run;
        return new(prepared, run, claim);
    }

    private static string Metadata(Guid claim) => JsonSerializer.Serialize(new Dictionary<string, string> {
        ["agentProcessDispatchClaimIdentity"] = claim.ToString("D")
    });

    private static void AssertSnapshot(Fixture fixture, ProcessExecutionProjectAuthoritySnapshot actual) {
        Assert.Equal(fixture.Run.Id, actual.ExecutionRunId);
        Assert.Equal(fixture.Run.AgentId, actual.ExecutorAgentId);
        Assert.Equal(fixture.Claim, actual.DispatchClaimToken);
        Assert.Equal(fixture.Prepared.Preparation.AdmissionId, actual.Reference.AdmissionId);
        Assert.Equal(fixture.Prepared.PreparationFingerprint, actual.Reference.PreparationFingerprint);
        Assert.Equal(fixture.Prepared.Preparation.Authority!.ProjectAdmission, actual.SourceAuthority.ProjectAdmission);
        Assert.Equal(fixture.Prepared.Preparation.Authority.Principal.GetType(), actual.SourceAuthority.Principal.GetType());
        Assert.Equal(JsonSerializer.Serialize(fixture.Prepared.Preparation.Authority), JsonSerializer.Serialize(actual.SourceAuthority));
        Assert.True(actual.ObservedCurrentDispatch);
        if (actual.SourceAuthority.Principal is ProcessLaunchPrincipal.AgentExecution agent) {
            Assert.NotEqual(actual.ExecutorAgentId, agent.Ceiling.AgentId);
            Assert.NotEmpty(agent.Ceiling.AllowedOperations);
            Assert.NotEmpty(agent.Ceiling.AllowedCapabilityKeys);
            Assert.NotEmpty(agent.Ceiling.AllowedManagedArtifactReadRefs);
        }
    }

    private static ProcessExecutionProjectAuthorityReader Reader(IServiceProvider services, params IInterceptor[] interceptors)
        => new(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>(), new(new Factory(Options(services, interceptors)), new Clock()),
            services.GetRequiredService<ICanonicalRuntimeDatabase>());

    private static DbContextOptions<ProcessPersistenceDbContext> Options(IServiceProvider services, params IInterceptor[] interceptors) {
        var builder = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
        AppDbContextOptionsConfigurator.Configure(builder, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        return builder.AddInterceptors(interceptors).Options;
    }

    private static TestHarnessOptions Harness(CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null)
        => new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
            foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                services.Remove(descriptor);
            }
        } };

    private sealed record Fixture(ProcessPreparedLaunchSnapshot Prepared, ExecutionRunRecord Run, Guid Claim) {
        public Guid RunId => Prepared.Preparation.InitialCommit.Mutation.State.RunId.Value;
    }

    private sealed class Factory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class Clock : TimeProvider {
        public override DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now.AddSeconds(5);
    }

    private sealed class FixtureAdmissionPolicy : IProcessLaunchAuthorityPolicy {
        public Task<IProcessLaunchAuthorityLease> AcquireAsync(ProcessLaunchAuthority saved, ProcessLaunchAuthority currentCaller,
            CancellationToken cancellationToken = default) => Task.FromResult<IProcessLaunchAuthorityLease>(new Lease());
        public Task RequireCurrentAsync(ProcessLaunchAuthority authority, CancellationToken cancellationToken = default) => Task.CompletedTask;
        private sealed class Lease : IProcessLaunchAuthorityLease {
            public Task RequireForMutationAsync(ProcessLaunchLinkTarget? linkTarget, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class ReadProbe : DbCommandInterceptor {
        public int Selects { get; private set; }
        public int Writes { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.Ordinal)) {
                Selects++;
            } else {
                Writes++;
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Writes++;
            return ValueTask.FromResult(result);
        }
    }
}
