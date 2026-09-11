using System.Data;
using System.Data.Common;
using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTransferOwnerInspectionTests {
    [Fact]
    public async Task All_twelve_areas_read_the_explicit_target_and_final_reads_share_its_locked_transaction() {
        await using var other = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        await using (var unrelated = other.Factory.CreateDbContext()) {
            unrelated.Add(ProjectDocument());
            await unrelated.SaveChangesAsync();
        }

        var probe = new InspectionProbe();
        await using var owner = target.Factory.WithInterceptor(probe).CreateDbContext();
        await owner.Database.OpenConnectionAsync();
        var runner = new ProjectTransferTargetInspectionRunner();
        var participants = CreateParticipants(runner);
        Assert.Equal(12, participants.Length);
        var operations = DatabaseTransferTestSupport.ForProfile(target.Profile, owner, runner);
        var guard = new ProjectTransferTargetStateGuard(participants, operations);
        Assert.Empty(await operations.RunIndependentAsync(target.Profile, guard.FindPreflightResiduesAsync));
        AssertOwnerModels(probe);
        Assert.All(probe.Commands, command => {
            Assert.Equal(owner.Database.GetDbConnection().Database, command.Database);
            Assert.Null(command.Transaction);
        });

        await Assert.ThrowsAsync<InspectionRollback>(() => guard.RunLockedImportAsync<bool>(target.Profile, async (session, token) => {
            await using var search = await operations.CreateOwnerAsync<SearchDbContext>(session, static options => new SearchDbContext(options), token);
            search.Add(ProjectDocument());
            await search.SaveChangesAsync(token);
            var transaction = search.Database.CurrentTransaction!;
            probe.Commands.Clear();
            var residues = await guard.FindLockedResiduesAsync(session, token);
            Assert.Contains(residues, residue => residue.Area == ProjectTransferTargetStateArea.Infrastructure && residue.Description == "project search documents");
            AssertOwnerModels(probe);
            Assert.All(probe.Commands, command => {
                Assert.Same(owner.Database.GetDbConnection(), command.Connection);
                Assert.Same(transaction.GetDbTransaction(), command.Transaction);
                Assert.Equal(IsolationLevel.Serializable, command.Isolation);
            });
            throw new InspectionRollback();
        }));

        Assert.Empty(await operations.RunIndependentAsync(target.Profile, guard.FindPreflightResiduesAsync));
        await using var unchanged = other.Factory.CreateDbContext();
        Assert.Single(await unchanged.Set<SearchDocument>().ToArrayAsync());
    }

    [Theory]
    [InlineData(InvalidRequest.Identity)]
    [InlineData(InvalidRequest.Profile)]
    [InlineData(InvalidRequest.Mode)]
    [InlineData(InvalidRequest.Disposed)]
    public async Task Invalid_or_retired_inspection_requests_cannot_run_an_owner_query(InvalidRequest change) {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var owner = database.Factory.CreateDbContext();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        var runner = new ProjectTransferTargetInspectionRunner();
        using var scope = runner.Begin(database.Profile, owner, ProjectTransferTargetInspectionMode.Locked, out var request);
        var invalid = change switch {
            InvalidRequest.Identity => request with { Id = Guid.NewGuid() },
            InvalidRequest.Profile => request with { TargetProfileId = Guid.NewGuid() },
            InvalidRequest.Mode => request with { Mode = ProjectTransferTargetInspectionMode.Independent },
            InvalidRequest.Disposed => request,
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        if (change == InvalidRequest.Disposed) {
            scope.Dispose();
        }
        var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ReadOwnerAsync<SearchDbContext, bool>(invalid,
            static options => new(options), (db, token) => {
                called = true;
                return db.Set<SearchDocument>().AnyAsync(token);
            }));
        Assert.False(called);
    }

    [Fact]
    public async Task Missing_transaction_and_wrong_physical_target_are_explicit_failures() {
        await using var first = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var second = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var owner = first.Factory.CreateDbContext();
        var runner = new ProjectTransferTargetInspectionRunner();
        Assert.Throws<InvalidOperationException>(() => runner.Begin(first.Profile, owner, ProjectTransferTargetInspectionMode.Locked, out _));
        Assert.Throws<InvalidOperationException>(() => runner.Begin(second.Profile, owner, ProjectTransferTargetInspectionMode.Independent, out _));
        await using var transaction = await owner.Database.BeginTransactionAsync();
        Assert.Throws<InvalidOperationException>(() => runner.Begin(second.Profile, owner, ProjectTransferTargetInspectionMode.Locked, out _));
        var request = new ProjectTransferTargetInspection(Guid.NewGuid(), first.Profile.Profile.Id, ProjectTransferTargetInspectionMode.Locked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ReadOwnerAsync<SearchDbContext, bool>(request,
            static options => new(options), static (db, token) => db.Set<SearchDocument>().AnyAsync(token)));
    }

    [Theory]
    [InlineData(TransactionRetirement.Commit)]
    [InlineData(TransactionRetirement.ReplaceEfWrapper)]
    public async Task Final_inspection_cannot_reuse_a_retired_or_replaced_transaction(TransactionRetirement retirement) {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var owner = database.Factory.CreateDbContext();
        await owner.Database.OpenConnectionAsync();
        await using var transaction = await owner.Database.GetDbConnection().BeginTransactionAsync();
        var originalWrapper = await owner.Database.UseTransactionAsync(transaction);
        var runner = new ProjectTransferTargetInspectionRunner();
        using var scope = runner.Begin(database.Profile, owner, ProjectTransferTargetInspectionMode.Locked, out var request);
        if (retirement == TransactionRetirement.Commit) {
            await originalWrapper!.CommitAsync();
        } else {
            await owner.Database.UseTransactionAsync(null);
            var replacement = await owner.Database.UseTransactionAsync(transaction);
            Assert.NotSame(originalWrapper, replacement);
            Assert.Same(transaction, replacement!.GetDbTransaction());
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ReadOwnerAsync<SearchDbContext, bool>(request,
            static options => new(options), static (db, token) => db.Set<SearchDocument>().AnyAsync(token)));
    }

    [Fact]
    public async Task Every_retained_evidence_state_blocks_target_adoption_without_parsing_or_discarding_its_payload() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var owner = database.Factory.CreateDbContext();
        var runner = new ProjectTransferTargetInspectionRunner();
        var participants = CreateParticipants(runner).ToDictionary(participant => participant.Area);
        foreach (var evidence in RetainedEvidence()) {
            owner.Add(evidence.Record);
            await owner.SaveChangesAsync();
            using (runner.Begin(database.Profile, owner, ProjectTransferTargetInspectionMode.Independent, out var request)) {
                var residues = await participants[evidence.Area].FindResiduesAsync(request, default);
                Assert.Contains(residues, residue => residue.Description == evidence.Description);
            }
            owner.Remove(evidence.Record);
            await owner.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task An_unrelated_catalog_cursor_is_preserved_and_does_not_change_project_inspection_scope() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        var probe = new InspectionProbe();
        await using var owner = database.Factory.WithInterceptor(probe).CreateDbContext();
        var runner = new ProjectTransferTargetInspectionRunner();
        var participants = CreateParticipants(runner);
        var operations = DatabaseTransferTestSupport.ForProfile(database.Profile, owner, runner);
        var guard = new ProjectTransferTargetStateGuard(participants, operations);
        Assert.DoesNotContain(typeof(AiTechnicalProjectionCursor), Assert.Single(participants, item => item.Area == ProjectTransferTargetStateArea.CrmHr).EntityTypesToLock);
        Assert.DoesNotContain("\"CrmHr_AiTechnicalProjectionCursors\"", DatabaseTransferOperationRunner.ResolveTableNames(owner, participants.SelectMany(item => item.EntityTypesToLock).ToArray()));
        Assert.Empty(await owner.Set<AiResourceBinding>().ToArrayAsync());
        Assert.Empty(await operations.RunIndependentAsync(database.Profile, guard.FindPreflightResiduesAsync));
        var cursor = new AiTechnicalProjectionCursor { DatabaseProfileId = database.Profile.Profile.Id,
            SourceScopeKind = WorkspaceScopeKind.Organization, SourceScopeKey = database.Profile.Profile.Id.ToString("N"),
            CatalogRevision = 2, ProjectionSha256 = new string('A', 64), UpdatedAtUtc = DateTimeOffset.UtcNow };
        await Assert.ThrowsAsync<InspectionRollback>(() => guard.RunLockedImportAsync<bool>(database.Profile, async (session, token) => {
            await using var crm = await operations.CreateOwnerAsync<CrmHrDbContext>(session, static options => new CrmHrDbContext(options), token);
            crm.Add(cursor);
            await crm.SaveChangesAsync(token);
            var transaction = crm.Database.CurrentTransaction!;
            probe.Commands.Clear();
            var residues = await guard.FindLockedResiduesAsync(session, token);
            Assert.Empty(residues);
            var crmCommands = probe.Commands.Where(command => command.ContextType == typeof(CrmHrDbContext)).ToArray();
            Assert.NotEmpty(crmCommands);
            Assert.All(crmCommands, command => {
                Assert.Equal(30, command.EntityCount);
                Assert.Same(owner.Database.GetDbConnection(), command.Connection);
                Assert.Same(transaction.GetDbTransaction(), command.Transaction);
            });
            throw new InspectionRollback();
        }));
        owner.ChangeTracker.Clear();
        Assert.Empty(await operations.RunIndependentAsync(database.Profile, guard.FindPreflightResiduesAsync));
        owner.Add(cursor);
        await owner.SaveChangesAsync();
        Assert.Empty(await owner.Set<AiResourceBinding>().ToArrayAsync());
        await using var restarted = database.Factory.CreateDbContext();
        Assert.Empty(await operations.RunIndependentAsync(database.Profile, guard.FindPreflightResiduesAsync));
        Assert.Equal(2, (await restarted.Set<AiTechnicalProjectionCursor>().SingleAsync()).CatalogRevision);
    }

    [Fact]
    public async Task Every_project_launch_state_is_inspected_from_owner_evidence_on_the_actual_locked_transaction() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseNpgsql(database.Profile.ConnectionString).Options;
        var store = new EfProcessPreparedLaunchStore(new ProcessFactory(options), options);
        var project = new ProcessProjectAdmission(database.Profile.Profile.Id, Guid.NewGuid(), Guid.NewGuid());
        var prepared = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(database.Profile.Profile.Id, project),
            new(Guid.NewGuid()), new(project.ProjectId, "retained-node", "binding"));
        var saved = await store.PrepareAsync(prepared);
        var probe = new InspectionProbe();
        await using var owner = database.Factory.WithInterceptor(probe).CreateDbContext();
        var row = await owner.Set<ProcessPreparedLaunchEntity>().SingleAsync(item => item.Id == saved.Preparation.AdmissionId.Value);
        var payload = row.PayloadJson;
        var runner = new ProjectTransferTargetInspectionRunner();
        var participant = Assert.Single(CreateParticipants(runner), item => item.Area == ProjectTransferTargetStateArea.Processes);
        foreach (var state in Enum.GetValues<ProcessLaunchContinuationState>()) {
            foreach (var delivery in Enum.GetValues<ProcessLaunchLinkDeliveryState>()) {
                await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                row.State = state;
                row.LinkDeliveryState = delivery;
                await owner.SaveChangesAsync();
                probe.Commands.Clear();
                using (runner.Begin(database.Profile, owner, ProjectTransferTargetInspectionMode.Locked, out var request)) {
                    Assert.Contains(await participant.FindResiduesAsync(request, default),
                        residue => residue.Description == "retained process prepared launches linked to projects");
                }
                Assert.NotEmpty(probe.Commands);
                Assert.All(probe.Commands, command => {
                    Assert.Equal(typeof(ProcessPersistenceDbContext), command.ContextType);
                    Assert.Equal(19, command.EntityCount);
                    Assert.Same(owner.Database.GetDbConnection(), command.Connection);
                    Assert.Same(transaction.GetDbTransaction(), command.Transaction);
                });
                await transaction.RollbackAsync();
                await owner.Entry(row).ReloadAsync();
                Assert.Equal(payload, row.PayloadJson);
            }
        }
        row.PayloadJson += " ";
        await owner.SaveChangesAsync();
        using (runner.Begin(database.Profile, owner, ProjectTransferTargetInspectionMode.Independent, out var request)) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => participant.FindResiduesAsync(request, default));
        }
    }

    private sealed class ProcessFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private static IProjectTransferTargetStateParticipant[] CreateParticipants(ProjectTransferTargetInspectionRunner runner)
        => ModuleAssemblies.All.Append(typeof(IProjectTransferTargetStateParticipant).Assembly).Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IProjectTransferTargetStateParticipant).IsAssignableFrom(type))
            .Select(type => (IProjectTransferTargetStateParticipant)Activator.CreateInstance(type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, args: [runner], culture: null)!)
            .OrderBy(participant => participant.Area).ToArray();

    private static SearchDocument ProjectDocument() => new() {
        Id = Guid.NewGuid(), SourceType = SearchDocument.ProjectSourceType, SourceKey = Guid.NewGuid().ToString("D"),
        Category = "project", Title = "Explicit target inspection", Route = "/projects/inspection", UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static void AssertOwnerModels(InspectionProbe probe) {
        Dictionary<Type, int> expected = new() {
            [typeof(SearchDbContext)] = 1, [typeof(StorageDbContext)] = 3,
            [typeof(WorkflowDbContext)] = 17, [typeof(AgentProjectAccessDbContext)] = 1, [typeof(AgentHistoryDbContext)] = 1,
            [typeof(CollaborationDbContext)] = 4, [typeof(CrmHrDbContext)] = 30, [typeof(ProcessPersistenceDbContext)] = 19,
            [typeof(ProjectsDbContext)] = 6, [typeof(PromptsDbContext)] = 10, [typeof(ResourcesDbContext)] = 1,
            [typeof(SchedulerPlannerDbContext)] = 3, [typeof(TestLabDbContext)] = 4, [typeof(WorkbenchDbContext)] = 15,
            [typeof(WorkspaceConnectorCommandDbContext)] = 2
        };
        var observed = probe.Commands.Select(command => (command.ContextType, command.EntityCount)).Distinct()
            .ToDictionary(value => value.ContextType, value => value.EntityCount);
        Assert.Equal(expected.OrderBy(pair => pair.Key.FullName), observed.OrderBy(pair => pair.Key.FullName));
    }

    private static IEnumerable<RetainedRecord> RetainedEvidence() {
        const string payload = "{\"origin\":\"source-profile-evidence\",\"grant\":false}";
        for (var state = 0; state < 3; state++) {
            yield return new(ProjectTransferTargetStateArea.Workbench, "retained Process asset contributions", new ProjectProcessAssetContributionRecord {
                IntentId = Guid.NewGuid(), DatabaseProfileId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), ProjectLifetimeId = Guid.NewGuid(),
                SourceExecutionRunId = Guid.NewGuid(), NativeObjectId = Guid.NewGuid(), StorageIntentId = Guid.NewGuid(),
                PlanJson = payload, PlanFingerprint = new string('A', 64), PreparedAtUtc = DateTimeOffset.UtcNow,
                MaterializedRequestJson = state > 0 ? payload : string.Empty, MaterializedFingerprint = state > 0 ? new string('B', 64) : string.Empty,
                NodeJson = state > 1 ? payload : string.Empty, ReceiptJson = state > 1 ? payload : string.Empty
            });
        }
        foreach (var state in Enum.GetValues<StorageStablePlacementState>()) {
            yield return new(ProjectTransferTargetStateArea.Infrastructure, "retained project storage placement intents", new StoragePlacementIntentRecord {
                Id = Guid.NewGuid(), StorageId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), State = state,
                RequestFingerprint = new string('A', 64), PlanJson = payload, ReceiptJson = payload
            });
        }
        foreach (var complete in new[] { false, true }) {
            yield return new(ProjectTransferTargetStateArea.AgentFramework, "retained workflow structure output manifests", new WorkflowStructureOutputRecord {
                RunId = Guid.NewGuid(), OccurrencePath = "source", Slot = 1, PlanJson = payload,
                ReceiptJson = complete ? payload : "", IsComplete = complete, AssetDispatchStarted = true
            });
            yield return new(ProjectTransferTargetStateArea.Workbench, "retained workflow contribution receipts", new ProjectWorkflowContributionRecord {
                RunId = Guid.NewGuid(), OccurrencePath = "source", Slot = 1, ProjectId = Guid.NewGuid(), NativeObjectId = Guid.NewGuid(),
                PlanJson = payload, RequestJson = payload, ReceiptJson = complete ? payload : "", NodeJson = complete ? payload : "",
                PreparedAtUtc = DateTimeOffset.UtcNow
            });
        }
        foreach (var state in Enum.GetValues<ProjectWorkflowDeliveryState>()) {
            yield return new(ProjectTransferTargetStateArea.Workbench, "retained workflow delivery admissions", new ProjectWorkflowAdmissionRecord {
                IntentId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), NodeId = "source", NativeNodeId = Guid.NewGuid(), RunId = Guid.NewGuid(),
                Sequence = 1, AdmissionJson = payload, Delivery = state, StatusJson = payload
            });
        }
        foreach (var state in Enum.GetValues<SchedulerFireAdmissionState>()) {
            yield return new(ProjectTransferTargetStateArea.SchedulerPlanner, "retained scheduler fire admissions", new SchedulerFireAdmissionRecord {
                Id = Guid.NewGuid(), PlanId = Guid.NewGuid(), DedupeKey = Guid.NewGuid().ToString("N"), SnapshotJson = payload,
                SnapshotFingerprint = new string('B', 64), PreparedWorkflowRunId = Guid.NewGuid(), State = state, Generation = 1
            });
        }
        foreach (var state in Enum.GetValues<ProjectCreationReservationState>()) {
            yield return new(ProjectTransferTargetStateArea.Projects, "project core records", new ProjectCreationReservationRecord {
                Id = Guid.NewGuid(), DatabaseProfileId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), LifetimeId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(), State = state, CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        foreach (var state in new[] { ProcessRuntimeStatus.Completed, ProcessRuntimeStatus.Failed, ProcessRuntimeStatus.Cancelled }) {
            yield return new(ProjectTransferTargetStateArea.Processes, "retained process project admissions", new ProcessRuntimeStateEntity {
                RunId = Guid.NewGuid(), RootRunId = Guid.NewGuid(), PlanId = Guid.NewGuid(), Status = state,
                ProjectAdmissionDatabaseProfileId = Guid.NewGuid(), ProjectAdmissionProjectId = Guid.NewGuid(), ProjectAdmissionLifetimeId = Guid.NewGuid()
            });
        }

    }

    private sealed record RetainedRecord(ProjectTransferTargetStateArea Area, string Description, object Record);
    private sealed record ObservedCommand(Type ContextType, int EntityCount, DbConnection Connection, string Database,
        DbTransaction? Transaction, IsolationLevel? Isolation);

    private sealed class InspectionProbe : DbCommandInterceptor {
        public List<ObservedCommand> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(new(eventData.Context!.GetType(), eventData.Context.Model.GetEntityTypes().Count(), command.Connection!,
                command.Connection!.Database, command.Transaction, command.Transaction?.IsolationLevel));
            return ValueTask.FromResult(result);
        }
    }

    public enum InvalidRequest { Identity, Profile, Mode, Disposed }
    public enum TransactionRetirement { Commit, ReplaceEfWrapper }
    private sealed class InspectionRollback : Exception;

}
