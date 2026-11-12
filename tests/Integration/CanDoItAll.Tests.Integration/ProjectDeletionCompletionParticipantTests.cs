using System.Reflection;
using System.Data;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectDeletionCompletionParticipantTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public Task Typed_preparation_preserves_one_original_in_both_owner_records_and_rolls_back_with_the_owner() =>
        WithFixtureAsync(async fixture => {
            var services = fixture.Services;
            var project = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
                Name = "Completion participant staging", Objective = "Original owner binding", CurrentPhase = "Validation"
            });
            Assert.True(project.IsSuccess);
            var admitted = (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(project.Value))!;
            var lifecycle = await services.GetRequiredService<DatasetActivationService>()
                .ObserveActiveBindingAsync(admitted.DatabaseProfileId);
            var original = new ProjectDeletionPreparationContext(
                new(admitted.DatabaseProfileId, lifecycle.Record.Plan.Binding.Namespace, new(Guid.NewGuid())), admitted);
            var workbench = fixture.WorkbenchParticipant();
            var agent = fixture.AgentParticipant();
            foreach (var commit in new[] { false, true }) {
                await fixture.InOwnerAsync(async () => {
                    var work = Assert.IsType<ProjectDeletionParticipantPreparation>(await workbench.PrepareAsync(original));
                    var access = Assert.IsType<ProjectDeletionParticipantPreparation>(await agent.PrepareAsync(original));
                    Assert.Equal(work, await workbench.PrepareAsync(original));
                    Assert.Equal(access, await agent.PrepareAsync(original));
                    var different = new ProjectDeletionPreparationContext(
                        new(original.Completion.DatabaseProfileId, original.Completion.Namespace, new(Guid.NewGuid())), admitted);
                    await Assert.ThrowsAsync<InvalidOperationException>(() => workbench.PrepareAsync(different));
                    await Assert.ThrowsAsync<InvalidOperationException>(() => agent.PrepareAsync(different));
                }, commit);
                var works = await fixture.ReadAsync<WorkbenchDbContext, ProjectCrossModuleMutationRecord[]>(context =>
                    context.Set<ProjectCrossModuleMutationRecord>().Where(row => row.ProjectId == admitted.ProjectId).ToArrayAsync());
                var accesses = await fixture.ReadAsync<AgentProjectAccessDbContext, AgentProjectStructureAccessRevocationRecord[]>(context =>
                    context.Set<AgentProjectStructureAccessRevocationRecord>().Where(row => row.ProjectId == admitted.ProjectId).ToArrayAsync());
                if (!commit) {
                    Assert.Empty(works);
                    Assert.Empty(accesses);
                    continue;
                }
                var payload = JsonSerializer.Deserialize<DeleteProjectMutationPayload>(Assert.Single(works).PayloadJson, Json)!;
                Assert.Equal(original, payload.CompletionContext);
                Assert.Equal(ProjectAssignmentReference.From(admitted), payload.SourceReference);
                var record = Assert.Single(accesses);
                Assert.Equal(original.Completion.Namespace.Value, record.CompletionNamespaceId);
                Assert.Equal(original.Completion.Id.Value, record.CompletionId);
                Assert.Equal(admitted.DatabaseProfileId, record.DatabaseProfileId);
                Assert.Equal(admitted.LifetimeId, record.ProjectLifetimeId);
                Assert.IsType<ProjectDeletionParticipantObservation.Pending>(await workbench.ObserveCompletionAsync(original, Assert.Single(works).Id));
                Assert.IsType<ProjectDeletionParticipantObservation.Pending>(await agent.ObserveCompletionAsync(original, record.Id));
                var inspectorEvidence = new Dictionary<ProjectDeletionParticipantId, ProjectDeletionRecoveryParticipantEvidence>();
                foreach (var mode in new[] { ProjectDeletionRecoveryReadMode.Observe, ProjectDeletionRecoveryReadMode.Exclusive }) {
                    await fixture.InOwnerAsync(async () => {
                        foreach (var owner in new[] { RecoveryParticipant.Workbench, RecoveryParticipant.Agent }) {
                            var inspector = fixture.Inspector(owner);
                            var recoveryId = owner == RecoveryParticipant.Workbench ? Assert.Single(works).Id : record.Id;
                            var observed = await inspector.InspectAsync(original, recoveryId, mode);
                            Assert.False(observed.Completed);
                            Assert.Equal(new(inspector.Id, recoveryId), observed.Obligation);
                            if (inspectorEvidence.TryGetValue(inspector.Id, out var first)) {
                                Assert.Equal(first, observed);
                            } else {
                                inspectorEvidence.Add(inspector.Id, observed);
                            }
                        }
                    }, commit: false);
                }
                Assert.Equal(JsonSerializer.Serialize(works, Json), JsonSerializer.Serialize(
                    await fixture.ReadAsync<WorkbenchDbContext, ProjectCrossModuleMutationRecord[]>(context =>
                        context.Set<ProjectCrossModuleMutationRecord>().Where(row => row.ProjectId == admitted.ProjectId).ToArrayAsync()), Json));
                Assert.Equal(JsonSerializer.Serialize(accesses, Json), JsonSerializer.Serialize(
                    await fixture.ReadAsync<AgentProjectAccessDbContext, AgentProjectStructureAccessRevocationRecord[]>(context =>
                        context.Set<AgentProjectStructureAccessRevocationRecord>().Where(row => row.ProjectId == admitted.ProjectId).ToArrayAsync()), Json));
                Assert.Equal(0, fixture.PartyProbe.Calls);
                Assert.Equal(0, fixture.WorkspaceProbe.Calls);
            }
            Assert.Empty(fixture.Query.Observed);
            await Assert.ThrowsAsync<NotSupportedException>(() => workbench.PrepareAsync(admitted.ProjectId));
            await Assert.ThrowsAsync<NotSupportedException>(() => agent.PrepareAsync(admitted.ProjectId));
        });

    [Theory]
    [InlineData(RecoveryParticipant.Workbench)]
    [InlineData(RecoveryParticipant.Agent)]
    public Task Recovery_inspectors_refuse_missing_foreign_and_unbound_records_without_dispatch(RecoveryParticipant owner) =>
        WithFixtureAsync(async fixture => {
            var original = fixture.Original();
            var recoveryId = await SeedInspectionAsync(fixture, original, owner);
            var inspector = fixture.Inspector(owner);
            await Assert.ThrowsAsync<InvalidOperationException>(() => inspector.InspectAsync(original, recoveryId, ProjectDeletionRecoveryReadMode.Observe));
            await fixture.InOwnerAsync(async () => {
                var before = await inspector.InspectAsync(original, recoveryId, ProjectDeletionRecoveryReadMode.Observe);
                Assert.False(before.Completed);
                await Assert.ThrowsAsync<InvalidDataException>(() => inspector.InspectAsync(original, Guid.NewGuid(), ProjectDeletionRecoveryReadMode.Observe));
                var foreign = new ProjectDeletionPreparationContext(original.Completion,
                    new(original.Project.DatabaseProfileId, original.Project.ProjectId, Guid.NewGuid()));
                await Assert.ThrowsAsync<InvalidDataException>(() => inspector.InspectAsync(foreign, recoveryId, ProjectDeletionRecoveryReadMode.Observe));
                Assert.Equal(before, await inspector.InspectAsync(original, recoveryId, ProjectDeletionRecoveryReadMode.Exclusive));
            }, commit: false);
            if (owner == RecoveryParticipant.Workbench) {
                await fixture.UseAsync<WorkbenchDbContext>(async context => {
                    var record = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync(row => row.Id == recoveryId);
                    var payload = JsonSerializer.Deserialize<DeleteProjectMutationPayload>(record.PayloadJson, Json)!;
                    record.PayloadJson = JsonSerializer.Serialize(payload with { CompletionContext = null }, Json);
                    await context.SaveChangesAsync();
                });
            } else {
                await fixture.UseAsync<AgentProjectAccessDbContext>(async context => {
                    var record = await context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync(row => row.Id == recoveryId);
                    record.CompletionId = null;
                    record.CompletionNamespaceId = null;
                    await context.SaveChangesAsync();
                });
            }
            await fixture.InOwnerAsync(async () => {
                await Assert.ThrowsAsync<InvalidDataException>(() => inspector.InspectAsync(original, recoveryId, ProjectDeletionRecoveryReadMode.Observe));
            }, commit: false);
            Assert.Equal(0, fixture.PartyProbe.Calls);
            Assert.Equal(0, fixture.WorkspaceProbe.Calls);
            Assert.Empty(fixture.Query.Observed);
        });

    [Fact]
    public Task Recovery_workbench_inspector_refuses_an_oversized_otherwise_valid_payload() => WithFixtureAsync(async fixture => {
        var original = fixture.Original();
        var recoveryId = await SeedInspectionAsync(fixture, original, RecoveryParticipant.Workbench);
        await fixture.UseAsync<WorkbenchDbContext>(async context => {
            var record = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync(row => row.Id == recoveryId);
            record.PayloadJson = record.PayloadJson.Insert(1,
                "\"retainedPadding\":\"" + new string('a', ProjectWorkbenchDeletionRecoveryInspector.MaximumPayloadBytes + 1) + "\",");
            await context.SaveChangesAsync();
        });
        await fixture.InOwnerAsync(async () => {
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Inspector(RecoveryParticipant.Workbench)
                .InspectAsync(original, recoveryId, ProjectDeletionRecoveryReadMode.Exclusive));
        }, commit: false);
        var retained = await fixture.ReadAsync<WorkbenchDbContext, ProjectCrossModuleMutationRecord>(context =>
            context.Set<ProjectCrossModuleMutationRecord>().SingleAsync(row => row.Id == recoveryId));
        Assert.True(retained.PayloadJson.Length > ProjectWorkbenchDeletionRecoveryInspector.MaximumPayloadBytes);
        Assert.Equal(0, retained.AttemptCount);
        Assert.Equal(0, fixture.PartyProbe.Calls);
    });

    [Theory]
    [InlineData(RecoveryParticipant.Workbench)]
    [InlineData(RecoveryParticipant.Agent)]
    public Task Recovery_exclusive_inspection_uses_nowait_against_the_actual_participant_row(RecoveryParticipant owner) =>
        WithFixtureAsync(async fixture => {
            var original = fixture.Original();
            var recoveryId = await SeedInspectionAsync(fixture, original, owner);
            await fixture.UseAsync<ProjectsDbContext>(async holding => {
                await using var transaction = await holding.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                var query = owner == RecoveryParticipant.Workbench
                    ? "SELECT \"Id\" FROM \"Workbench_ProjectCrossModuleMutations\" WHERE \"Id\" = {0} FOR UPDATE"
                    : "SELECT \"Id\" FROM \"AgentFramework_ProjectAccessRevocations\" WHERE \"Id\" = {0} FOR UPDATE";
                await holding.Database.ExecuteSqlRawAsync(query, recoveryId);
                await fixture.InOwnerAsync(async () => {
                    var wrapped = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Inspector(owner)
                        .InspectAsync(original, recoveryId, ProjectDeletionRecoveryReadMode.Exclusive));
                    var busy = Assert.IsType<PostgresException>(wrapped.InnerException);
                    Assert.Equal(PostgresErrorCodes.LockNotAvailable, busy.SqlState);
                }, commit: false);
            });
            await fixture.InOwnerAsync(async () => {
                Assert.False((await fixture.Inspector(owner).InspectAsync(original, recoveryId, ProjectDeletionRecoveryReadMode.Exclusive)).Completed);
            }, commit: false);
            Assert.Equal(0, fixture.PartyProbe.Calls);
            Assert.Equal(0, fixture.WorkspaceProbe.Calls);
        });

    private static async Task<Guid> SeedInspectionAsync(GateFixture fixture, ProjectDeletionPreparationContext original, RecoveryParticipant owner) {
        var recoveryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        if (owner == RecoveryParticipant.Workbench) {
            var record = Mutation(original, ProjectCrossModuleMutationKind.DeleteProject, JsonSerializer.Serialize(
                new DeleteProjectMutationPayload([], [], SourceReference: ProjectAssignmentReference.From(original.Project), CompletionContext: original), Json));
            recoveryId = record.Id;
            await fixture.UseAsync<WorkbenchDbContext>(async context => {
                context.Add(record);
                await context.SaveChangesAsync();
            });
        } else {
            await fixture.UseAsync<AgentProjectAccessDbContext>(async context => {
                context.Add(new AgentProjectStructureAccessRevocationRecord {
                    Id = recoveryId, ProjectId = original.Project.ProjectId, DatabaseProfileId = original.Project.DatabaseProfileId,
                    ProjectLifetimeId = original.Project.LifetimeId, CompletionNamespaceId = original.Completion.Namespace.Value,
                    CompletionId = original.Completion.Id.Value, Status = AgentProjectStructureAccessRevocationStatus.Pending,
                    CreatedAtUtc = now, UpdatedAtUtc = now
                });
                await context.SaveChangesAsync();
            });
        }
        return recoveryId;
    }

    public enum RecoveryParticipant { Agent, Workbench }

    [Fact]
    public Task Workbench_stored_original_gates_dependencies_direct_dispatch_and_fresh_recovery_before_any_claim() =>
        WithFixtureAsync(async fixture => {
            var original = fixture.Original();
            var dependency = Mutation(original, ProjectCrossModuleMutationKind.DeleteSubtree,
                JsonSerializer.Serialize(new DeleteSubtreeMutationPayload("root", [], 0,
                    SourceReference: ProjectAssignmentReference.From(original.Project)), Json));
            var payload = new DeleteProjectMutationPayload([], [], OutstandingMutationIds: [dependency.Id],
                SourceReference: ProjectAssignmentReference.From(original.Project), CompletionContext: original);
            var mutation = Mutation(original, ProjectCrossModuleMutationKind.DeleteProject, JsonSerializer.Serialize(payload, Json));
            await fixture.UseAsync<WorkbenchDbContext>(async context => {
                context.AddRange(dependency, mutation);
                await context.SaveChangesAsync();
            });
            var preparation = new ProjectDeletionParticipantPreparation(original.Project.ProjectId, mutation.Id);
            foreach (var observation in Refusals()) {
                fixture.Query.Result = observation;
                await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => fixture.WorkbenchParticipant().CompleteAsync(preparation));
                await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => fixture.Processor().ProcessAsync(mutation.Id));
                var pending = Assert.Single(await fixture.WorkbenchParticipant().ListPendingRecoveriesAsync());
                Assert.False(pending.CanRetryNow);
                Assert.Equal(mutation.Id, pending.RecoveryId);
                var rows = await fixture.ReadAsync<WorkbenchDbContext, ProjectCrossModuleMutationRecord[]>(context =>
                    context.Set<ProjectCrossModuleMutationRecord>().ToArrayAsync());
                Assert.All(rows, row => Assert.Equal(0, row.AttemptCount));
                Assert.Equal(0, fixture.PartyProbe.Calls);
            }
            Assert.All(fixture.Query.Observed, observed => {
                Assert.Equal(original, observed.Original);
                Assert.Equal(new ProjectDeletionObligation(new(ProjectWorkbenchDeletionParticipant.ParticipantIdValue), mutation.Id), observed.Obligation);
            });
            fixture.Query.Result = new ProjectDeletionCompletionObservation.Released(DateTimeOffset.UtcNow);
            await fixture.UseAsync<WorkbenchDbContext>(async edit => {
                var legacy = await edit.Set<ProjectCrossModuleMutationRecord>().SingleAsync(row => row.Id == mutation.Id);
                legacy.PayloadJson = JsonSerializer.Serialize(payload with { CompletionContext = null }, Json);
                await edit.SaveChangesAsync();
            });
            var beforeLegacy = fixture.Query.Observed.Count;
            await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => fixture.WorkbenchParticipant().CompleteAsync(preparation));
            await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => fixture.Processor().ProcessAsync(mutation.Id));
            Assert.False(Assert.Single(await fixture.WorkbenchParticipant().ListPendingRecoveriesAsync()).CanRetryNow);
            Assert.Equal(beforeLegacy, fixture.Query.Observed.Count);
            Assert.Equal(0, fixture.PartyProbe.Calls);
            await fixture.UseAsync<WorkbenchDbContext>(async edit => {
                var restored = await edit.Set<ProjectCrossModuleMutationRecord>().SingleAsync(row => row.Id == mutation.Id);
                restored.PayloadJson = JsonSerializer.Serialize(payload, Json);
                await edit.SaveChangesAsync();
            });
            var completed = await fixture.WorkbenchParticipant().CompleteAsync(preparation);
            Assert.Equal(mutation.Id, completed.RecoveryId);
            Assert.Equal(2, fixture.PartyProbe.Calls);
            await fixture.UseAsync<WorkbenchDbContext>(async read => {
                Assert.All(await read.Set<ProjectCrossModuleMutationRecord>().ToArrayAsync(), row => {
                    Assert.Equal(ProjectCrossModuleMutationStatus.Completed, row.Status);
                    Assert.Equal(1, row.AttemptCount);
                });
            });
            await fixture.WorkbenchParticipant().CompleteAsync(preparation);
            Assert.Equal(2, fixture.PartyProbe.Calls);
        });

    [Fact]
    public Task Agent_stored_original_blocks_claim_and_file_dispatch_until_release_and_never_adopts_legacy_rows() =>
        WithFixtureAsync(async fixture => {
            var original = fixture.Original();
            var record = new AgentProjectStructureAccessRevocationRecord {
                Id = Guid.NewGuid(), ProjectId = original.Project.ProjectId,
                DatabaseProfileId = original.Project.DatabaseProfileId, ProjectLifetimeId = original.Project.LifetimeId,
                CompletionNamespaceId = original.Completion.Namespace.Value, CompletionId = original.Completion.Id.Value,
                Status = AgentProjectStructureAccessRevocationStatus.Pending,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await fixture.UseAsync<AgentProjectAccessDbContext>(async seed => {
                seed.Add(record);
                await seed.SaveChangesAsync();
            });
            var preparation = new ProjectDeletionParticipantPreparation(record.ProjectId, record.Id);
            foreach (var observation in Refusals()) {
                fixture.Query.Result = observation;
                await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => fixture.AgentParticipant().CompleteAsync(preparation));
                Assert.False(Assert.Single(await fixture.AgentParticipant().ListPendingRecoveriesAsync()).CanRetryNow);
                var current = await fixture.ReadAsync<AgentProjectAccessDbContext, AgentProjectStructureAccessRevocationRecord>(context =>
                    context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync());
                Assert.Equal(0, current.AttemptCount);
                Assert.Equal(AgentProjectStructureAccessRevocationStatus.Pending, current.Status);
                Assert.Equal(0, fixture.WorkspaceProbe.Calls);
            }
            Assert.All(fixture.Query.Observed, observed => {
                Assert.Equal(original, observed.Original);
                Assert.Equal(new ProjectDeletionObligation(new(AgentProjectStructureAccessDeletionParticipant.ParticipantIdValue), record.Id), observed.Obligation);
            });
            fixture.Query.Result = new ProjectDeletionCompletionObservation.Released(DateTimeOffset.UtcNow);
            await fixture.UseAsync<AgentProjectAccessDbContext>(async edit => {
                var legacy = await edit.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync();
                legacy.CompletionNamespaceId = null;
                legacy.CompletionId = null;
                await edit.SaveChangesAsync();
            });
            var before = fixture.Query.Observed.Count;
            await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => fixture.AgentParticipant().CompleteAsync(preparation));
            Assert.Equal(before, fixture.Query.Observed.Count);
            Assert.Equal(0, fixture.WorkspaceProbe.Calls);
            await fixture.UseAsync<AgentProjectAccessDbContext>(async edit => {
                var restored = await edit.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync();
                restored.CompletionNamespaceId = original.Completion.Namespace.Value;
                restored.CompletionId = original.Completion.Id.Value;
                await edit.SaveChangesAsync();
            });
            fixture.Query.Next.Enqueue(new ProjectDeletionCompletionObservation.Released(DateTimeOffset.UtcNow));
            fixture.Query.Next.Enqueue(new ProjectDeletionCompletionObservation.Unavailable(ProjectDeletionCompletionUnavailableReason.OriginalProfileChanged));
            await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => fixture.AgentParticipant().CompleteAsync(preparation));
            Assert.Equal(0, fixture.WorkspaceProbe.Calls);
            var interrupted = await fixture.ReadAsync<AgentProjectAccessDbContext, AgentProjectStructureAccessRevocationRecord>(context =>
                context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync());
            Assert.Equal(1, interrupted.AttemptCount);
            Assert.Equal(AgentProjectStructureAccessRevocationStatus.Failed, interrupted.Status);
            Assert.Empty(fixture.Query.Next);
            await fixture.AgentParticipant().CompleteAsync(preparation);
            Assert.Equal(1, fixture.WorkspaceProbe.Calls);
            var target = Assert.IsType<AgentProjectStructureRevocationTarget>(fixture.WorkspaceProbe.Arguments![0]);
            Assert.Equal(AgentProjectStructureRevocationTarget.ForLifetime(new(original.Project.DatabaseProfileId,
                original.Project.ProjectId, original.Project.LifetimeId)), target);
            await fixture.AgentParticipant().CompleteAsync(preparation);
            Assert.Equal(1, fixture.WorkspaceProbe.Calls);
        });

    private static IEnumerable<ProjectDeletionCompletionObservation> Refusals() {
        yield return new ProjectDeletionCompletionObservation.AwaitingOwnerRelease();
        foreach (var reason in Enum.GetValues<ProjectDeletionCompletionUnavailableReason>()) {
            yield return new ProjectDeletionCompletionObservation.Unavailable(reason);
        }
    }

    private static ProjectCrossModuleMutationRecord Mutation(ProjectDeletionPreparationContext original,
        ProjectCrossModuleMutationKind kind, string payload) => new() {
            ProjectId = original.Project.ProjectId, ScopeNodeKey = "project", MutationKind = kind,
            Status = ProjectCrossModuleMutationStatus.WorkbenchCommitted, PayloadJson = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };

    private static async Task WithFixtureAsync(Func<GateFixture, Task> action) {
        TestApplication? application = null;
        IAsyncDisposable? scopeOwner = null;
        Exception? primary = null;
        var query = new CompletionQueryProbe();
        try {
            application = await TestApplication.CreateAsync(new TestHarnessOptions {
                ConfigureServices = services => services.AddSingleton<IProjectDeletionCompletionQuery>(query)
            });
            var scope = application.Services.CreateAsyncScope();
            scopeOwner = scope;
            await action(new(scope.ServiceProvider, query));
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            if (primary is null) {
                await TestFixtureCleanup.DisposeAsync(scopeOwner, application);
            } else {
                await TestFixtureCleanup.DisposeAfterFailureAsync(primary, scopeOwner, application);
            }
        }
    }

    private sealed class GateFixture(IServiceProvider services, CompletionQueryProbe query) {
        public IServiceProvider Services => services;
        public CompletionQueryProbe Query => query;
        private readonly IProjectPartyIntegrationBridge party = DispatchProxy.Create<IProjectPartyIntegrationBridge, DispatchProbe>();
        private readonly IAgentFrameworkWorkspaceService workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DispatchProbe>();
        public DispatchProbe PartyProbe => (DispatchProbe)(object)party;
        public DispatchProbe WorkspaceProbe => (DispatchProbe)(object)workspace;
        public ProjectCrossModuleMutationProcessor Processor() => ActivatorUtilities.CreateInstance<ProjectCrossModuleMutationProcessor>(services, party, query);
        public ProjectWorkbenchDeletionParticipant WorkbenchParticipant() =>
            ActivatorUtilities.CreateInstance<ProjectWorkbenchDeletionParticipant>(services, Processor());
        public AgentProjectStructureAccessDeletionParticipant AgentParticipant() =>
            ActivatorUtilities.CreateInstance<AgentProjectStructureAccessDeletionParticipant>(services, workspace, query);
        public IProjectDeletionRecoveryParticipant Inspector(RecoveryParticipant owner) => owner switch {
            RecoveryParticipant.Agent => new AgentProjectDeletionRecoveryInspector(services.GetRequiredService<DbContextOptions<AgentProjectAccessDbContext>>(),
                services.GetRequiredService<CoordinatedDatabaseTransaction>()),
            RecoveryParticipant.Workbench => new ProjectWorkbenchDeletionRecoveryInspector(services.GetRequiredService<DbContextOptions<WorkbenchDbContext>>(),
                services.GetRequiredService<CoordinatedDatabaseTransaction>()),
            _ => throw new ArgumentOutOfRangeException(nameof(owner))
        };
        public ProjectDeletionPreparationContext Original() {
            var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id;
            return new(new(profile, new(Guid.NewGuid()), new(Guid.NewGuid())), new(profile, Guid.NewGuid(), Guid.NewGuid()));
        }
        public Task UseAsync<TContext>(Func<TContext, Task> action) where TContext : DbContext
            => ReadAsync<TContext, bool>(async context => {
                await action(context);
                return true;
            });
        public async Task<T> ReadAsync<TContext, T>(Func<TContext, Task<T>> action) where TContext : DbContext {
            TContext? context = null;
            Exception? primary = null;
            try {
                context = await services.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContextAsync();
                return await action(context);
            } catch (Exception failure) {
                primary = failure;
                throw;
            } finally {
                if (primary is null) {
                    await TestFixtureCleanup.DisposeAsync(context);
                } else {
                    await TestFixtureCleanup.DisposeAfterFailureAsync(primary, context);
                }
            }
        }
        public async Task InOwnerAsync(Func<Task> action, bool commit) {
            ProjectsDbContext? context = null;
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            Exception? primary = null;
            try {
                context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
                transaction = await context.Database.BeginTransactionAsync();
                using (services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(context)) {
                    await action();
                }
                if (commit) {
                    await transaction.CommitAsync();
                } else {
                    await transaction.RollbackAsync();
                }
            } catch (Exception failure) {
                primary = failure;
                throw;
            } finally {
                if (primary is null) {
                    await TestFixtureCleanup.DisposeAsync(transaction, context);
                } else {
                    await TestFixtureCleanup.DisposeAfterFailureAsync(primary, transaction, context);
                }
            }
        }
    }

    private sealed class CompletionQueryProbe : IProjectDeletionCompletionQuery {
        public ProjectDeletionCompletionObservation Result { get; set; } = new ProjectDeletionCompletionObservation.AwaitingOwnerRelease();
        public List<(ProjectDeletionPreparationContext Original, ProjectDeletionObligation Obligation)> Observed { get; } = [];
        public Queue<ProjectDeletionCompletionObservation> Next { get; } = new();
        public Task<ProjectDeletionCompletionObservation> ObserveAsync(ProjectDeletionPreparationContext original,
            ProjectDeletionObligation obligation, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Observed.Add((original, obligation));
            return Task.FromResult(Next.Count == 0 ? Result : Next.Dequeue());
        }
    }

    public class DispatchProbe : DispatchProxy {
        public int Calls { get; private set; }
        public object?[]? Arguments { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            Calls++;
            Arguments = args;
            return targetMethod?.Name switch {
                nameof(IProjectPartyIntegrationBridge.DeleteAssignmentsForProjectAsync) => Task.CompletedTask,
                nameof(IProjectPartyIntegrationBridge.DeleteAssignmentsForNodesAsync) => Task.CompletedTask,
                nameof(IAgentFrameworkWorkspaceService.RevokeProjectStructureLifetimeAccessFromAllAgentsAsync) => Task.FromResult(1),
                _ => throw new InvalidOperationException("The participant dispatched an unexpected external effect.")
            };
        }
    }
}
