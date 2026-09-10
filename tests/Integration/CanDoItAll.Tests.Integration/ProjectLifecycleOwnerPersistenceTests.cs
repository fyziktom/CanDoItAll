using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectLifecycleOwnerPersistenceTests {
    [Fact]
    public async Task Agent_runtime_model_preserves_the_complete_mapping_and_deletion_contract_exposes_no_context() {
        await using var application = await TestApplication.CreateAsync();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>()
            .CreateDbContextAsync();
        await using var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        var entity = Assert.Single(owner.GetService<IDesignTimeModel>().Model.GetEntityTypes());
        Assert.Equal(typeof(AgentProjectStructureAccessRevocationRecord), entity.ClrType);
        var complete = Assert.IsAssignableFrom<IEntityType>(schema.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(AgentProjectStructureAccessRevocationRecord)));
        Assert.Equal(complete.ToDebugString(MetadataDebugStringOptions.LongDefault),
            entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
        Assert.DoesNotContain(typeof(IProjectDeletionParticipant).GetMethods().SelectMany(method => method.GetParameters()),
            parameter => typeof(DbContext).IsAssignableFrom(parameter.ParameterType));
    }

    [Fact]
    public async Task Legacy_pending_and_completed_agent_recoveries_survive_restart_and_remain_profile_isolated() {
        await using var environment = CanDoItAllTestEnvironment.Create("project-lifecycle-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var pending = Revocation(Guid.NewGuid(), AgentProjectStructureAccessRevocationStatus.Pending, timestamp);
        pending.AttemptCount = 2;
        pending.LastAttemptAtUtc = timestamp.AddMinutes(1);
        pending.LastFailureCode = "legacy.retry-required";
        var completed = Revocation(Guid.NewGuid(), AgentProjectStructureAccessRevocationStatus.Completed, timestamp);
        completed.AttemptCount = 3;
        completed.LastAttemptAtUtc = timestamp.AddMinutes(2);
        completed.CompletedAtUtc = timestamp.AddMinutes(3);
        await using (var original = await TestApplication.CreateAsync(options)) {
            await using var schema = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            schema.AddRange(pending, completed);
            await schema.SaveChangesAsync();
        }
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var scope = restarted.Services.CreateAsyncScope();
        var participant = AgentParticipant(scope.ServiceProvider);
        Assert.Equal(pending.Id, Assert.Single(await participant.ListPendingRecoveriesAsync()).RecoveryId);
        Assert.Equal(completed.Id, Assert.Single(await participant.ListCompletionNoticesAsync()).RecoveryId);
        await using var owner = await restarted.Services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
        var records = await owner.Set<AgentProjectStructureAccessRevocationRecord>().ToDictionaryAsync(item => item.Id);
        Assert.Equal(2, records.Count);
        AssertRevocation(pending, records[pending.Id]);
        AssertRevocation(completed, records[completed.Id]);
        var coordinator = restarted.Services.GetRequiredService<CoordinatedDatabaseTransaction>();
        await using (var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable)) {
            using (coordinator.Enter(owner)) {
                Assert.Null(await participant.PrepareAsync(completed.ProjectId));
                Assert.Equal(new ProjectDeletionParticipantPreparation(pending.ProjectId, pending.Id),
                    await participant.PrepareAsync(pending.ProjectId));
            }
            await transaction.CommitAsync();
        }
        owner.ChangeTracker.Clear();
        AssertRevocation(completed, await owner.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync(item => item.Id == completed.Id));
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherScope = other.Services.CreateAsyncScope();
        Assert.Empty(await AgentParticipant(otherScope.ServiceProvider).ListPendingRecoveriesAsync());
        Assert.Empty(await AgentParticipant(otherScope.ServiceProvider).ListCompletionNoticesAsync());
        Assert.Equal(pending.Id, Assert.Single(await participant.ListPendingRecoveriesAsync()).RecoveryId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_project_delete_stages_all_owners_on_one_serializable_transaction_and_rolls_back_together(bool failFinalSave) {
        var saves = new OwnerSaveProbe { FailFinalSave = failFinalSave };
        await using var application = await TestApplication.CreateAsync(Harness(saves));
        var projectId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var unrelatedProjectId = Guid.NewGuid();
        await using (var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            schema.AddRange(new Project { Id = projectId, Name = "Atomic owner deletion" },
                ProjectObject(projectId, objectId), new ProjectNodeBindingRecord { ProjectObjectId = objectId, Route = "/retained/note" },
                new SearchDocument { ProjectId = projectId, SourceType = "lifecycle-note", SourceKey = objectId.ToString(), Title = "Owned search" },
                new SearchDocument { SourceType = SearchDocument.ProjectSourceType, SourceKey = projectId.ToString(), Title = "Legacy project search without ProjectId" },
                new SearchDocument { ProjectId = unrelatedProjectId, SourceType = "lifecycle-note", SourceKey = unrelatedProjectId.ToString(), Title = "Unrelated search" },
                new StorageRoutingRule { ProjectId = projectId, Name = "Project routing" },
                new StorageRoutingRule { ProjectId = unrelatedProjectId, Name = "Unrelated routing" });
            await schema.SaveChangesAsync();
        }
        saves.Saved.Clear();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        if (failFinalSave) {
            await Assert.ThrowsAsync<InjectedProjectSaveFailure>(() => projects.DeleteAsync(projectId));
        } else {
            Assert.Equal(projectId, (await projects.DeleteAsync(projectId)).ProjectId);
        }
        var ownerSave = Assert.IsType<SaveObservation>(saves.FinalSave);
        Assert.Equal(IsolationLevel.Serializable, ownerSave.IsolationLevel);
        Type[] expectedParticipants = [typeof(AgentProjectAccessDbContext), typeof(WorkbenchDbContext), typeof(SearchDbContext), typeof(StorageDbContext)];
        var staged = saves.Saved.Where(item => ReferenceEquals(item.Transaction, ownerSave.Transaction)).ToArray();
        foreach (var participantType in expectedParticipants) {
            var participantSave = Assert.Single(staged, item => item.ContextType == participantType);
            Assert.Same(ownerSave.Connection, participantSave.Connection);
            Assert.Same(ownerSave.Transaction, participantSave.Transaction);
            Assert.True(participantSave.SavedCount > 0);
        }
        await using var readback = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.Equal(failFinalSave, await readback.Set<Project>().AnyAsync(item => item.Id == projectId));
        Assert.Equal(!failFinalSave, await readback.Set<ProjectRetirementRecord>().AnyAsync(item => item.ProjectId == projectId));
        Assert.Equal(failFinalSave, await readback.Set<ProjectObjectRecord>().AnyAsync(item => item.Id == objectId));
        Assert.Equal(failFinalSave, await readback.Set<ProjectNodeBindingRecord>().AnyAsync(item => item.ProjectObjectId == objectId));
        Assert.Equal(failFinalSave ? 2 : 0, await readback.Set<SearchDocument>().CountAsync(item => item.ProjectId == projectId ||
            item.SourceType == SearchDocument.ProjectSourceType && item.SourceKey == projectId.ToString()));
        Assert.Equal(failFinalSave, await readback.Set<StorageRoutingRule>().AnyAsync(item => item.ProjectId == projectId));
        Assert.True(await readback.Set<SearchDocument>().AnyAsync(item => item.ProjectId == unrelatedProjectId));
        Assert.True(await readback.Set<StorageRoutingRule>().AnyAsync(item => item.ProjectId == unrelatedProjectId));
        if (failFinalSave) {
            Assert.False(await readback.Set<AgentProjectStructureAccessRevocationRecord>().AnyAsync(item => item.ProjectId == projectId));
            Assert.False(await readback.Set<ProjectCrossModuleMutationRecord>().AnyAsync(item => item.ProjectId == projectId));
        } else {
            Assert.Equal(AgentProjectStructureAccessRevocationStatus.Completed,
                (await readback.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync(item => item.ProjectId == projectId)).Status);
            Assert.Equal(ProjectCrossModuleMutationStatus.Completed,
                (await readback.Set<ProjectCrossModuleMutationRecord>().SingleAsync(item => item.ProjectId == projectId)).Status);
        }
    }

    [Fact]
    public async Task Completed_participants_still_save_residual_view_cleanup_when_the_project_is_already_missing() {
        await using var application = await TestApplication.CreateAsync();
        var projectId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var mutation = CompletedMutation(projectId, timestamp);
        var revocation = Revocation(projectId, AgentProjectStructureAccessRevocationStatus.Completed, timestamp);
        await using (var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            schema.AddRange(mutation, revocation, new ProjectWorkbenchViewStateRecord {
                ProjectId = projectId, SurfaceKind = "diagram", StateJson = "{\"retained\":true}", UpdatedAtUtc = timestamp
            });
            await schema.SaveChangesAsync();
        }
        await using var scope = application.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProjectsService>().DeleteAsync(projectId);
        await using var readback = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.False(await readback.Set<ProjectWorkbenchViewStateRecord>().AnyAsync(item => item.ProjectId == projectId));
        Assert.Equal(mutation.Id, (await readback.Set<ProjectCrossModuleMutationRecord>().SingleAsync(item => item.ProjectId == projectId)).Id);
        Assert.Equal(revocation.Id, (await readback.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync(item => item.ProjectId == projectId)).Id);
    }

    [Fact]
    public async Task Residual_recovery_waits_for_the_managed_binding_gate_before_removing_new_bindings() {
        var reads = new RecoveryReadProbe();
        await using var application = await TestApplication.CreateAsync(Harness(reads: reads));
        var projectId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var mutation = CompletedMutation(projectId, DateTimeOffset.UtcNow);
        var factory = application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var schema = await factory.CreateDbContextAsync()) {
            schema.AddRange(mutation, ProjectObject(projectId, objectId), new ProjectNodeBindingRecord {
                ProjectObjectId = objectId, Route = "/residual/note"
            });
            await schema.SaveChangesAsync();
        }
        await using var scope = application.Services.CreateAsyncScope();
        var participant = Assert.Single(scope.ServiceProvider.GetServices<IProjectDeletionParticipant>()
            .OfType<ProjectWorkbenchDeletionParticipant>());
        Task<ProjectDeletionParticipantCompletion> completion;
        await using (var lockContext = await factory.CreateDbContextAsync())
        await using (var gate = await SerializableMutationScope.BeginAsync(lockContext,
            ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey, CancellationToken.None)) {
            reads.Armed = true;
            completion = participant.CompleteAsync(new(projectId, mutation.Id));
            await reads.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var observation = Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.Same(observation, await Task.WhenAny(completion, observation));
            await using var readback = await factory.CreateDbContextAsync();
            Assert.True(await readback.Set<ProjectNodeBindingRecord>().AnyAsync(item => item.ProjectObjectId == objectId));
            await gate.CommitAsync(CancellationToken.None);
        }
        var result = await completion.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.NotEqual(mutation.Id, result.RecoveryId);
        await using var verification = await factory.CreateDbContextAsync();
        Assert.False(await verification.Set<ProjectObjectRecord>().AnyAsync(item => item.Id == objectId));
        Assert.False(await verification.Set<ProjectNodeBindingRecord>().AnyAsync(item => item.ProjectObjectId == objectId));
        var retained = await verification.Set<ProjectCrossModuleMutationRecord>().Where(item => item.ProjectId == projectId).ToArrayAsync();
        Assert.Equal(2, retained.Length);
        Assert.Contains(retained, item => item.Id == mutation.Id);
        Assert.Contains(retained, item => item.Id == result.RecoveryId);
        Assert.All(retained, item => Assert.Equal(ProjectCrossModuleMutationStatus.Completed, item.Status));
    }

    private static ProjectObjectRecord ProjectObject(Guid projectId, Guid objectId) => new() {
        Id = objectId, ProjectId = projectId, NodeKey = $"note:{objectId:D}", ObjectType = ProjectObjectType.Note,
        Title = "Lifecycle note", CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static ProjectCrossModuleMutationRecord CompletedMutation(Guid projectId, DateTimeOffset timestamp) => new() {
        ProjectId = projectId, ScopeNodeKey = "project", MutationKind = ProjectCrossModuleMutationKind.DeleteProject,
        Status = ProjectCrossModuleMutationStatus.Completed,
        PayloadJson = JsonSerializer.Serialize(new DeleteProjectMutationPayload([], [], [], [], []), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp, CompletedAtUtc = timestamp
    };

    private static AgentProjectStructureAccessRevocationRecord Revocation(Guid projectId,
        AgentProjectStructureAccessRevocationStatus status, DateTimeOffset timestamp) => new() {
        Id = Guid.NewGuid(), ProjectId = projectId, Status = status, CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
    };

    private static IProjectDeletionParticipant AgentParticipant(IServiceProvider services) =>
        Assert.Single(services.GetServices<IProjectDeletionParticipant>().OfType<AgentProjectStructureAccessDeletionParticipant>());

    private static void AssertRevocation(AgentProjectStructureAccessRevocationRecord expected, AgentProjectStructureAccessRevocationRecord actual) {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.ProjectId, actual.ProjectId);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
        Assert.Equal(expected.AttemptCount, actual.AttemptCount);
        Assert.Equal(expected.LastAttemptAtUtc, actual.LastAttemptAtUtc);
        Assert.Equal(expected.CompletedAtUtc, actual.CompletedAtUtc);
        Assert.Equal(expected.LastFailureCode, actual.LastFailureCode);
    }

    private static TestHarnessOptions Harness(OwnerSaveProbe? saves = null, RecoveryReadProbe? reads = null) => new() {
        ConfigureServices = services => {
            AddOptions<ProjectsDbContext>(services, saves, null);
            AddOptions<AgentProjectAccessDbContext>(services, saves, null);
            AddOptions<WorkbenchDbContext>(services, saves, reads);
            AddOptions<SearchDbContext>(services, saves, null);
            AddOptions<StorageDbContext>(services, saves, null);
        }
    };

    private static void AddOptions<TContext>(IServiceCollection services, OwnerSaveProbe? saves, RecoveryReadProbe? reads) where TContext : DbContext {
        services.AddSingleton<DbContextOptions<TContext>>(provider => {
            var options = new DbContextOptionsBuilder<TContext>();
            AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
            if (saves is not null) {
                options.AddInterceptors(saves);
            }
            if (reads is not null) {
                options.AddInterceptors(reads);
            }
            return options.Options;
        });
    }

    private sealed record SaveObservation(Type ContextType, DbConnection Connection, DbTransaction Transaction,
        IsolationLevel IsolationLevel, int SavedCount);

    private sealed class InjectedProjectSaveFailure() : Exception("Injected failure after all project-deletion participants saved and before the Projects save.") {
    }

    private sealed class OwnerSaveProbe : SaveChangesInterceptor {
        public bool FailFinalSave { get; init; }
        public SaveObservation? FinalSave { get; private set; }
        public ConcurrentQueue<SaveObservation> Saved { get; } = new();

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context is ProjectsDbContext context && context.ChangeTracker.Entries<Project>()
                .Any(entry => entry.State == EntityState.Deleted)) {
                var transaction = context.Database.CurrentTransaction?.GetDbTransaction()
                    ?? throw new InvalidOperationException("The final Projects save lost its transaction.");
                FinalSave = new(context.GetType(), context.Database.GetDbConnection(), transaction, transaction.IsolationLevel, 0);
                if (FailFinalSave) {
                    throw new InjectedProjectSaveFailure();
                }
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (eventData.Context is { } context && context.Database.CurrentTransaction?.GetDbTransaction() is { } transaction) {
                Saved.Enqueue(new(context.GetType(), context.Database.GetDbConnection(), transaction, transaction.IsolationLevel, result));
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecoveryReadProbe : DbCommandInterceptor {
        public bool Armed { get; set; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("Workbench_ProjectCrossModuleMutations", StringComparison.Ordinal)) {
                Started.TrySetResult(true);
            }
            return ValueTask.FromResult(result);
        }
    }
}
