using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Source_Agent_authority_is_held_through_actual_Process_commit_and_is_distinct_from_assigned_executor() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var saved = await fixture.PrepareAsync();
        Assert.NotEqual(fixture.Agent.Id.ToString("D"), saved.Preparation.InitialCommit.InitialAssignments!.Single().ExecutorId);
        var probe = new CatalogCommitProbe(fixture.Writer);
        await using var context = fixture.Context(probe);
        var accepted = await fixture.UnitOfWork(context).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved));
        Assert.True(accepted.Succeeded);
        Assert.True(probe.BeforeCommit);
        Assert.True(probe.AfterCommit);
        await fixture.RevokeAsync(Revocation.Tools);
        Assert.NotNull((await fixture.Store.GetAsync(saved.Preparation.AdmissionId))!.AcceptedAtUtc);
        Assert.Equal(fixture.Agent.Id,
            Assert.IsType<ProcessLaunchPrincipal.AgentExecution>((await fixture.Store.GetAsync(saved.Preparation.AdmissionId))!.Preparation.Authority!.Principal).Ceiling.AgentId);
    }

    public enum Revocation { Tools, Read, Write, ExactLifetime }

    [Theory]
    [InlineData(Revocation.Tools)]
    [InlineData(Revocation.Read)]
    [InlineData(Revocation.Write)]
    [InlineData(Revocation.ExactLifetime)]
    public async Task Current_catalog_revocation_denies_new_admission_without_any_initial_Process_writes(Revocation revocation) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var saved = await fixture.PrepareAsync();
        await fixture.RevokeAsync(revocation);
        if (revocation == Revocation.Read) {
            await using var revoked = await fixture.Writer.AcquireAgentReadLeaseAsync(fixture.Agent.Id);
            Assert.False(AgentProjectStructureAccessMetadata.Read(revoked.Agent!.ConfigurationJson).CanRead);
        }
        await using var context = fixture.Context();
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => fixture.UnitOfWork(context)
            .CommitAsync(ProcessPreparedLaunchFixture.Commit(saved)));
        Assert.Null((await fixture.Store.GetAsync(saved.Preparation.AdmissionId))!.AcceptedAtUtc);
        await AssertNoInitialRowsAsync(context, saved);
        await using var held = await fixture.Writer.AcquireAgentReadLeaseAsync(fixture.Agent.Id);
        Assert.Equal(fixture.Agent.Id, held.Agent!.Id);
    }

    [Fact]
    public async Task Actual_flush_failure_rolls_back_the_admission_and_releases_catalog_lock_without_replacing_exception() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var saved = await fixture.PrepareAsync();
        var failure = new ArgumentException("Injected Process failure after SQL flush with a held catalog read lease.");
        var fault = new FlushFault(failure);
        await using (var context = fixture.Context(fault)) {
            Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => fixture.UnitOfWork(context)
                .CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))));
        }
        Assert.True(fault.Flushed);
        await fixture.RevokeAsync(Revocation.Tools);
        await using var read = fixture.Context();
        await AssertNoInitialRowsAsync(read, saved);
        Assert.Null((await fixture.Store.GetAsync(saved.Preparation.AdmissionId))!.AcceptedAtUtc);
    }

    [Fact]
    public async Task Lost_commit_ack_then_revocation_preserves_exact_accepted_replay_and_denies_a_new_intent() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var saved = await fixture.PrepareAsync();
        var failure = new ArgumentException("Injected acknowledgement loss after authority-fenced Process admission.");
        await using (var context = fixture.Context(new CommitFault(failure))) {
            Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => fixture.UnitOfWork(context)
                .CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))));
        }
        await fixture.RevokeAsync(Revocation.Tools);
        var accepted = Assert.IsType<ProcessPreparedLaunchSnapshot>(await fixture.Store.GetAsync(saved.Preparation.AdmissionId));
        Assert.NotNull(accepted.AcceptedAtUtc);
        await using var retry = fixture.Context();
        var replay = await fixture.UnitOfWork(retry).CommitAsync(ProcessPreparedLaunchFixture.Commit(accepted));
        Assert.Equal(saved.Preparation.InitialCommit.Mutation.Outcome, replay.Outcome);
        Assert.Equal(saved.Preparation.InitialCommit.Mutation.State.RunId, replay.State.RunId);
        Assert.Equal(saved.Preparation.AdmissionId, replay.State.LaunchAdmissionId);
        var another = await fixture.PrepareAsync();
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => fixture.UnitOfWork(retry)
            .CommitAsync(ProcessPreparedLaunchFixture.Commit(another)));
        await AssertNoInitialRowsAsync(retry, another);
    }

    [Fact]
    public async Task Project_deletion_releases_SQL_before_catalog_cleanup_so_a_held_source_lease_cannot_deadlock_admission() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var saved = await fixture.PrepareAsync();
        await using var held = await fixture.Authority.AcquireAsync(fixture.SavedAuthority, fixture.SavedAuthority);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var deletion = scope.ServiceProvider.GetRequiredService<ProjectsService>().DeleteAsync(fixture.Project.ProjectId, timeout.Token);
        await using (var projects = new ProjectsDbContext(fixture.ProjectOptions)) {
            while (!await projects.Set<ProjectRetirementRecord>().AsNoTracking()
                .AnyAsync(item => item.ProjectId == fixture.Project.ProjectId && item.LifetimeId == fixture.Project.LifetimeId, timeout.Token)) {
                await Task.Delay(20, timeout.Token);
            }
        }
        Assert.False(deletion.IsCompleted);
        await using var context = fixture.Context();
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => fixture.UnitOfWork(context, new HeldPolicy(held))
            .CommitAsync(ProcessPreparedLaunchFixture.Commit(saved), timeout.Token));
        var deleted = await deletion.WaitAsync(timeout.Token);
        Assert.Equal(fixture.Project.ProjectId, deleted.ProjectId);
        Assert.Empty(deleted.Warnings);
        await AssertNoInitialRowsAsync(context, saved);
        Assert.Null((await fixture.Store.GetAsync(saved.Preparation.AdmissionId))!.AcceptedAtUtc);
        await using var after = await fixture.Writer.AcquireAgentReadLeaseAsync(fixture.Agent.Id, timeout.Token);
        Assert.DoesNotContain(AgentProjectStructureAccessMetadata.Read(after.Agent!.ConfigurationJson).AllowedProjectLifetimes,
            lifetime => lifetime.ProjectId == fixture.Project.ProjectId && lifetime.LifetimeId == fixture.Project.LifetimeId);
    }

    public enum AuthorityChange { Operation, Generation, MissingOperation }

    [Theory]
    [InlineData(AuthorityChange.Operation)]
    [InlineData(AuthorityChange.Generation)]
    [InlineData(AuthorityChange.MissingOperation)]
    public async Task Process_operation_and_current_generation_are_fenced_without_inventing_a_different_source(AuthorityChange change) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var principal = Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(fixture.SavedAuthority.Principal);
        var restricted = fixture.SavedAuthority with { Principal = principal with {
            Ceiling = principal.Ceiling with { AllowedOperations = [AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart] }
        }, CanCreateTasks = false, CanCreateAssets = false };
        var changed = restricted with { Principal = change != AuthorityChange.Generation ?
            principal with { Operation = change == AuthorityChange.MissingOperation
                    ? ProcessLaunchAgentOperation.Unspecified : ProcessLaunchAgentOperation.SubprocessLaunch,
                Ceiling = principal.Ceiling with { AllowedOperations = [AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart] } } :
            principal with { Ceiling = principal.Ceiling with { DatabaseProfileGeneration = principal.Ceiling.DatabaseProfileGeneration + 1,
                AllowedOperations = [AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart] } }
        };
        await using (var allowed = await fixture.Authority.AcquireAsync(restricted, restricted)) {
            Assert.NotNull(allowed);
        }
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => fixture.Authority.AcquireAsync(changed, changed));
        var request = ProcessPreparedLaunchFixture.Create(restricted, new(Guid.NewGuid())).Request;
        if (change != AuthorityChange.Generation) {
            Assert.NotEqual(ProcessLaunchIntentFingerprint.Compute(request),
                ProcessLaunchIntentFingerprint.Compute(request with { Authority = changed }));
        }
    }

    [Fact]
    public async Task Canonical_catalog_source_uses_its_immutable_profile_and_does_not_accept_another_profile_catalog() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-authority-profiles");
        var firstProfile = environment.CreatePostgreSqlProfile("first");
        var secondProfile = environment.CreatePostgreSqlProfile("second");
        await using var first = await TestApplication.CreateAsync(Harness(environment, firstProfile));
        await using var second = await TestApplication.CreateAsync(Harness(environment, secondProfile));
        await using var firstScope = first.Services.CreateAsyncScope();
        await using var secondScope = second.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(firstScope.ServiceProvider);
        var other = await Fixture.CreateAsync(secondScope.ServiceProvider);
        await using var firstHeld = await fixture.Source.AcquireAgentReadLeaseAsync(fixture.Agent.Id);
        await using var secondHeld = await other.Source.AcquireAgentReadLeaseAsync(fixture.Agent.Id);
        Assert.Equal(fixture.Agent.Id, firstHeld.Agent!.Id);
        Assert.Null(secondHeld.Agent);
        Assert.NotEqual(firstHeld.Scope, secondHeld.Scope);
        await firstHeld.DisposeAsync();
        await secondHeld.DisposeAsync();
        var wrongSourcePolicy = fixture.CreatePolicy(other.Source);
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => wrongSourcePolicy.AcquireAsync(fixture.SavedAuthority, fixture.SavedAuthority));
        await using var available = await fixture.Source.AcquireAgentReadLeaseAsync(fixture.Agent.Id);
        Assert.Equal(fixture.Agent.Id, available.Agent!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Operator_channels_recheck_current_API_policy_without_treating_local_identity_as_authenticated(bool authenticated) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<ApiAccessOptions>>().CurrentValue;
        options.Enabled = true;
        options.Authorization.Enabled = authenticated;
        var admitted = authenticated
            ? await fixture.Authority.CaptureAuthenticatedAsync(fixture.Project.ProjectId, "fixture-operator", DateTimeOffset.UtcNow.AddMinutes(5))
            : await fixture.Authority.CaptureLocalAsync(fixture.Project.ProjectId, ProcessLaunchOperatorSurface.Api);
        await fixture.Authority.RequireCurrentAsync(admitted);
        if (authenticated) {
            await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => fixture.Authority.CaptureLocalAsync(
                fixture.Project.ProjectId, ProcessLaunchOperatorSurface.Api));
            await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => fixture.Authority.CaptureAuthenticatedAsync(
                fixture.Project.ProjectId, "fixture-operator", DateTimeOffset.UtcNow.AddMinutes(-1)));
        }
        options.Enabled = false;
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => fixture.Authority.RequireCurrentAsync(admitted));
        var local = await fixture.Authority.CaptureLocalAsync(fixture.Project.ProjectId, ProcessLaunchOperatorSurface.UserInterface);
        Assert.IsType<ProcessLaunchPrincipal.LocalOperator>(local.Principal);
        Assert.NotEqual(ProcessLaunchIntentFingerprint.CallerFingerprint(admitted), ProcessLaunchIntentFingerprint.CallerFingerprint(local));
    }

    private static TestHarnessOptions Harness(CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null)
        => new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
            foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                services.Remove(descriptor);
            }
        } };

    private static async Task AssertNoInitialRowsAsync(ProcessPersistenceDbContext context, ProcessPreparedLaunchSnapshot saved) {
        var request = saved.Preparation.InitialCommit;
        var run = request.Mutation.State.RunId.Value;
        var eventId = request.Mutation.Events.Single().EventId.Value;
        Assert.False(await context.RuntimeStates.AnyAsync(item => item.RunId == run));
        Assert.False(await context.InstancePlans.AnyAsync(item => item.PlanId == request.Mutation.State.PlanId.Value));
        Assert.False(await context.RuntimeStepAssignments.AnyAsync(item => item.RunId == run));
        Assert.False(await context.RuntimeEvents.AnyAsync(item => item.RunId == run));
        Assert.False(await context.OutboxMessages.AnyAsync(item => item.EventId == eventId));
        Assert.False(await context.ArtifactLedgerEvents.AnyAsync(item => item.EventId == eventId));
        Assert.False(await context.IdempotencyKeys.AnyAsync(item => item.RunId == run));
    }

    private sealed class Fixture(IServiceProvider services) {
        public CoordinatedDatabaseTransaction Coordinator { get; } = CoordinatedDatabaseTransaction.ForProfile(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        public ProcessProjectAdmission Project { get; private set; } = null!;
        public AgentDefinition Agent { get; private set; } = null!;
        public FileSandboxWorkspaceStore Writer { get; private set; } = null!;
        public CanonicalAgentCatalogLeaseSource Source { get; private set; } = null!;
        public ProjectProcessLaunchAuthorityService Authority { get; private set; } = null!;
        public ProcessLaunchAuthority SavedAuthority { get; private set; } = null!;
        public DbContextOptions<ProjectsDbContext> ProjectOptions => Options<ProjectsDbContext>();
        public EfProcessPreparedLaunchStore Store => new(new Factory<ProcessPersistenceDbContext>(Options<ProcessPersistenceDbContext>(), static options => new(options)),
            Options<ProcessPersistenceDbContext>(), coordinatedTransaction: Coordinator);

        public static async Task<Fixture> CreateAsync(IServiceProvider services) {
            var result = new Fixture(services);
            var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>();
            var projectId = Guid.NewGuid();
            Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Process authority fixture" })).IsSuccess);
            var projects = result.Admissions();
            var project = Assert.IsType<ProjectWriteAdmission>(await projects.CaptureAsync(projectId));
            result.Project = new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId);
            result.Source = new(profile, services.GetRequiredService<IOptions<StorageOptions>>(), services.GetRequiredService<IHostEnvironment>(), projects);
            result.Writer = new(profile.Profile.Profile.Storage.WorkspaceRoot,
                WorkspaceScopeDescriptor.Organization(profile.Profile.Profile.Id.ToString("N")),
                new AgentProjectAccessCatalogPolicy(projects, profile.Profile.Profile.Id));
            var catalog = await result.Writer.LoadCatalogAsync();
            result.Agent = catalog.Agents.First() with {
                Id = Guid.NewGuid(), Name = "Process authority source", IsTemplate = false, TemplateKey = string.Empty, Tags = [],
                Status = AgentLifecycleStatus.Active, Permissions = AgentPermissionsPolicy.Default,
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write("{}", new() {
                    CanRead = true, CanWrite = true, AllowedProjectIds = [projectId],
                    AllowedProjectLifetimes = [new(project.DatabaseProfileId, projectId, project.LifetimeId)]
                })
            };
            await result.Writer.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, result.Agent] });
            result.Authority = result.CreatePolicy(result.Source);
            var governance = new AgentExecutionGovernanceSnapshot(new(Guid.NewGuid()), result.Agent.Id, profile.Profile.Profile.Id,
                services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration(),
                WorkspaceScopeDescriptor.Organization(profile.Profile.Profile.Id.ToString("N")), true, true, "test-policy", "test-policy-fingerprint");
            result.SavedAuthority = await result.Authority.CaptureAgentAsync(result.Project, governance, ProcessLaunchAgentOperation.StructureStart);
            return result;
        }

        public ProjectProcessLaunchAuthorityService CreatePolicy(IAgentCatalogReadLeaseStore source)
            => new(services.GetRequiredService<ICanonicalRuntimeDatabase>(), source,
                services.GetRequiredService<IAgentExecutionProfileGenerationSource>(), Admissions(),
                new(new Factory<WorkbenchDbContext>(Options<WorkbenchDbContext>(), static options => new(options)),
                    Options<WorkbenchDbContext>(), Coordinator, services.GetRequiredService<ProjectWorkbenchService>()),
                services.GetRequiredService<IOptionsMonitor<ApiAccessOptions>>(), TimeProvider.System);

        public Task<ProcessPreparedLaunchSnapshot> PrepareAsync()
            => Store.PrepareAsync(ProcessPreparedLaunchFixture.Create(SavedAuthority, new(Guid.NewGuid())));

        public ProcessPersistenceDbContext Context(params IInterceptor[] interceptors) => new(Options<ProcessPersistenceDbContext>(interceptors));

        public EfProcessRuntimeUnitOfWork UnitOfWork(ProcessPersistenceDbContext context, IProcessLaunchAuthorityPolicy? policy = null)
            => new(context, coordinatedTransaction: Coordinator, projectAdmissionPolicy: new ProjectProcessAdmissionPolicy(Admissions()),
                launchAuthorityPolicy: policy ?? Authority);

        public Task<SandboxWorkspaceCatalog> RevokeAsync(Revocation kind)
            => Writer.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(agent => {
                if (agent.Id != Agent.Id) {
                    return agent;
                }
                if (kind == Revocation.Tools) {
                    return agent with { Permissions = agent.Permissions with { CanUseTools = false } };
                }
                if (kind == Revocation.ExactLifetime) {
                    return agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.RevokeProjectLifetime(agent.ConfigurationJson,
                        AgentProjectStructureRevocationTarget.ForLifetime(new(Project.DatabaseProfileId, Project.ProjectId, Project.LifetimeId))).ConfigurationJson };
                }
                var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
                if (kind == Revocation.Read) {
                    return agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, new()) };
                }
                access.CanWrite = false;
                access.CanWriteNonTaskStructure = false;
                access.CanWriteTasks = false;
                return agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access) };
            }).ToArray() });

        private ProjectWriteAdmissionService Admissions()
            => new(new Factory<ProjectsDbContext>(ProjectOptions, static options => new(options)), ProjectOptions, Coordinator,
                services.GetRequiredService<ICanonicalRuntimeDatabase>());

        private DbContextOptions<T> Options<T>(params IInterceptor[] interceptors) where T : DbContext {
            var builder = new DbContextOptionsBuilder<T>();
            AppDbContextOptionsConfigurator.Configure(builder, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
            return builder.AddInterceptors(interceptors).Options;
        }
    }

    private sealed class Factory<T>(DbContextOptions<T> options, Func<DbContextOptions<T>, T> create) : IDbContextFactory<T> where T : DbContext {
        public T CreateDbContext() => create(options);
    }

    private sealed class CatalogCommitProbe(FileSandboxWorkspaceStore writer) : DbTransactionInterceptor {
        public bool BeforeCommit { get; private set; }
        public bool AfterCommit { get; private set; }
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            await RequireCatalogHeldAsync();
            BeforeCommit = true;
            return result;
        }
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            await RequireCatalogHeldAsync();
            AfterCommit = true;
        }
        private async Task RequireCatalogHeldAsync() {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.UpdateCatalogAsync(catalog => catalog, timeout.Token));
        }
    }

    private sealed class FlushFault(Exception failure) : SaveChangesInterceptor {
        public bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw failure;
        }
    }
    private sealed class CommitFault(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) => throw failure;
    }
    private sealed class HeldPolicy(IProcessLaunchAuthorityLease held) : IProcessLaunchAuthorityPolicy {
        public Task<IProcessLaunchAuthorityLease> AcquireAsync(ProcessLaunchAuthority saved, ProcessLaunchAuthority currentCaller, CancellationToken cancellationToken = default)
            => Task.FromResult(held);
        public Task RequireCurrentAsync(ProcessLaunchAuthority authority, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This fixture only transfers its already acquired source lease into one admission.");
    }
}
