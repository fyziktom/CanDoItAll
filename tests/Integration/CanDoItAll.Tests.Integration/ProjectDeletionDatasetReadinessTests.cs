using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectDeletionDatasetReadinessTests(Xunit.Abstractions.ITestOutputHelper output) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Initial_selection_stays_repairable_while_legacy_work_refuses_readiness(bool attach, bool interrupted) {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        DatasetActivationBinding? existing = attach ? await fixture.Coordinator.EnsureReadyAsync(fixture.Profile) : null;
        await using (var context = fixture.Projects()) {
            context.Add(new AgentProjectStructureAccessRevocationRecord {
                Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), DatabaseProfileId = fixture.Profile.Profile.Id,
                ProjectLifetimeId = Guid.NewGuid(), Status = AgentProjectStructureAccessRevocationStatus.Pending,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }
        string before = await NativeStateAsync(fixture);
        await using var selectionHost = CanDoItAllTestEnvironment.Create("dataset-activation-coordinator");
        int publications = 0;
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (interrupted && stage == DurableFileWriteStage.BeforeCommit && ++publications == 2) {
                throw new IOException("Synthetic initial Active publication interruption.");
            }
        });
        var coordinator = fixture.CoordinatorFor(selectionHost, writer);
        var failure = await Record.ExceptionAsync(() => attach ? coordinator.AttachAsync(fixture.Profile) : coordinator.EnsureReadyAsync(fixture.Profile));
        var retained = await fixture.ReadFileAsync(selectionHost);
        if (interrupted) {
            Assert.IsType<IOException>(failure);
            Assert.Equal(DatasetActivationFileState.Prepared, retained.State);
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.CoordinatorFor(selectionHost).ResumeAsync(fixture.Profile));
        } else {
            Assert.IsType<InvalidDataException>(failure);
        }
        var active = await fixture.ReadFileAsync(selectionHost);
        Assert.Equal(DatasetActivationFileState.Active, active.State);
        Assert.Equal(retained.Plan, active.Plan);
        Assert.Equal(existing ?? active.Plan.Binding, (await fixture.Lifecycle.ReadAsync())!.Binding);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.CoordinatorFor(selectionHost).EnsureReadyAsync(fixture.Profile));
        Assert.Equal(active, await fixture.ReadFileAsync(selectionHost));
        Assert.Equal(before, await NativeStateAsync(fixture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Readiness_accepts_bound_pending_pages_but_rotation_waits_for_release_and_native_completion(bool released) {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        await SeedAsync(fixture, binding, ProjectDeletionPendingPage.MaximumCount + 3, released);
        string before = await NativeStateAsync(fixture);
        var selection = await fixture.ReadFileAsync();
        Assert.Equal(binding, await fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.ReactivateAsync(fixture.Profile, binding.Namespace, Guid.NewGuid()));
        Assert.Equal(selection, await fixture.ReadFileAsync());
        Assert.Equal(before, await NativeStateAsync(fixture));
        await CompleteNativeAsync(fixture);
        var next = await fixture.Coordinator.ReactivateAsync(fixture.Profile, binding.Namespace, Guid.NewGuid());
        Assert.NotEqual(binding.Namespace, next.Namespace);
        Assert.Equal(next, await fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
    }

    [Theory]
    [InlineData(InvalidBinding.MissingCompletion)]
    [InlineData(InvalidBinding.MissingRetirement)]
    [InlineData(InvalidBinding.ForeignNamespace)]
    [InlineData(InvalidBinding.ForeignProfile)]
    [InlineData(InvalidBinding.WrongObligation)]
    [InlineData(InvalidBinding.UnboundAgent)]
    [InlineData(InvalidBinding.PartialAgent)]
    [InlineData(InvalidBinding.UnboundWorkbench)]
    [InlineData(InvalidBinding.InvalidAgentStatus)]
    public async Task Readiness_refuses_inconsistent_native_work_without_changing_it(InvalidBinding fault) {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        await SeedAsync(fixture, binding);
        await using (var context = fixture.Projects()) {
            var agent = await context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync();
            var completion = await context.Set<ProjectDeletionCompletionRecord>().SingleAsync();
            switch (fault) {
                case InvalidBinding.MissingCompletion:
                    context.Remove(completion);
                    break;
                case InvalidBinding.MissingRetirement:
                    context.Remove(await context.Set<ProjectRetirementRecord>().SingleAsync());
                    break;
                case InvalidBinding.ForeignNamespace:
                    agent.CompletionNamespaceId = Guid.NewGuid();
                    break;
                case InvalidBinding.ForeignProfile:
                    agent.DatabaseProfileId = Guid.NewGuid();
                    break;
                case InvalidBinding.WrongObligation:
                    var original = AgentProjectDeletionBinding.Read(agent, fixture.Profile.Profile.Id)!;
                    completion.ReferencePayload = ProjectDeletionCompletionCodec.Write(new(original, []));
                    break;
                case InvalidBinding.UnboundAgent:
                    agent.CompletionId = null;
                    agent.CompletionNamespaceId = null;
                    break;
                case InvalidBinding.PartialAgent:
                    await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"AgentFramework_ProjectAccessRevocations\" DROP CONSTRAINT \"CK_AF_ProjectAccessRevocations_Completion\"");
                    agent.CompletionNamespaceId = null;
                    break;
                case InvalidBinding.UnboundWorkbench:
                    var work = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync();
                    work.PayloadJson = JsonSerializer.Serialize(ProjectWorkbenchDeletionPayload.Read(work.PayloadJson) with { CompletionContext = null }, Json);
                    break;
                case InvalidBinding.InvalidAgentStatus:
                    agent.Status = (AgentProjectStructureAccessRevocationStatus)999;
                    break;
            }
            await context.SaveChangesAsync();
        }
        string before = await NativeStateAsync(fixture);
        var selection = await fixture.ReadFileAsync();
        var failure = await Record.ExceptionAsync(() => fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
        Assert.True(failure is InvalidDataException or ProjectDeletionParticipantCleanupException, failure?.ToString());
        Assert.Equal(selection, await fixture.ReadFileAsync());
        Assert.Equal(before, await NativeStateAsync(fixture));
    }

    [Fact]
    public async Task Workbench_byte_limited_short_page_does_not_skip_later_invalid_work() {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        await SeedAsync(fixture, binding, 7);
        await using (var context = fixture.Projects()) {
            var rows = await context.Set<ProjectCrossModuleMutationRecord>().OrderBy(row => row.Id).ToListAsync();
            foreach (var row in rows) {
                row.PayloadJson = Pad(row.PayloadJson, 1536 * 1024);
            }
            await context.SaveChangesAsync();
        }
        await InOwnerAsync(fixture, async (transactions, inventory) => {
            var first = await inventory.ReadPageAsync(fixture.Profile.Profile.Id, null);
            Assert.Equal(5, first.Items.Length);
            var second = await inventory.ReadPageAsync(fixture.Profile.Profile.Id, first.Items[^1].Obligation.RecoveryId);
            Assert.Equal(2, second.Items.Length);
            Assert.Empty((await inventory.ReadPageAsync(fixture.Profile.Profile.Id, second.Items[^1].Obligation.RecoveryId)).Items);
        });
        Assert.Equal(binding, await fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
        await using (var context = fixture.Projects()) {
            var last = await context.Set<ProjectCrossModuleMutationRecord>().OrderByDescending(row => row.Id).FirstAsync();
            last.PayloadJson = Pad(last.PayloadJson, ProjectWorkbenchDeletionRecoveryInspector.MaximumPayloadBytes + 1);
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workbench_first_invalid_or_oversized_payload_never_looks_like_an_empty_inventory(bool oversized) {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        await SeedAsync(fixture, binding);
        await using (var context = fixture.Projects()) {
            var row = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync();
            row.PayloadJson = oversized ? Pad(row.PayloadJson, ProjectWorkbenchDeletionPendingInventory.MaximumPagePayloadBytes + 1) : "";
            await context.SaveChangesAsync();
        }
        await InOwnerAsync(fixture, async (_, inventory) => {
            Assert.True(await inventory.HasPendingAsync());
            await Assert.ThrowsAsync<InvalidDataException>(() => inventory.ReadPageAsync(fixture.Profile.Profile.Id, null));
        });
    }

    [Fact]
    public async Task Deletion_committed_after_preflight_is_refused_under_SQL_exclusion_and_previous_selection_can_be_restored() {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var proceed = new ManualResetEventSlim();
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (stage == DurableFileWriteStage.Committed) {
                published.TrySetResult();
                if (!proceed.Wait(TimeSpan.FromSeconds(30))) {
                    throw new TimeoutException("The deletion race did not release its publication barrier.");
                }
            }
        });
        var activation = Task.Run(() => fixture.CoordinatorFor(fixture.Environment, writer)
            .ReactivateAsync(fixture.Profile, binding.Namespace, Guid.NewGuid()));
        var seedFailure = await Record.ExceptionAsync(async () => {
            await published.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await SeedAsync(fixture, binding);
        });
        proceed.Set();
        var activationFailure = await Record.ExceptionAsync(() => activation.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Null(seedFailure);
        Assert.IsType<InvalidOperationException>(activationFailure);
        var prepared = await fixture.ReadFileAsync();
        Assert.Equal(DatasetActivationFileState.Prepared, prepared.State);
        Assert.Equal(binding, (await fixture.Lifecycle.ReadAsync())!.Binding);
        var lifecycle = new DbContextOptionsBuilder<DatasetLifecycleDbContext>().UseNpgsql(fixture.Profile.ConnectionString).Options;
        var transactions = CoordinatedDatabaseTransaction.ForProfile(fixture.Profile);
        var restore = new DatasetActivationRestorationCoordinator(fixture.Profile.Profile.Id, fixture.Files(), lifecycle,
            fixture.EpochsFor(transactions), transactions, [fixture.Pool], _ => Task.CompletedTask,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DatasetActivationRestorationCoordinator>.Instance);
        var outcome = await restore.RestorePreviousAsync(prepared.Plan.PlanId, binding.Namespace, DatasetRestorationPrecondition.AllWriterHostsStopped);
        Assert.IsType<DatasetRestorationOutcome.Restored>(outcome);
        Assert.Equal(binding, await fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
    }

    [Fact]
    public async Task Rotation_resume_after_SQL_commit_validates_the_new_binding() {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        int publications = 0;
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (stage == DurableFileWriteStage.BeforeCommit && ++publications == 2) {
                throw new IOException("Synthetic Active publication interruption.");
            }
        });
        await Assert.ThrowsAsync<IOException>(() => fixture.CoordinatorFor(fixture.Environment, writer)
            .ReactivateAsync(fixture.Profile, binding.Namespace, Guid.NewGuid()));
        var prepared = await fixture.ReadFileAsync();
        Assert.Equal(DatasetActivationFileState.Prepared, prepared.State);
        Assert.Equal(prepared.Plan.Binding, (await fixture.Lifecycle.ReadAsync())!.Binding);
        await SeedAsync(fixture, prepared.Plan.Binding);
        Assert.Equal(prepared.Plan.Binding, await fixture.Coordinator.ResumeAsync(fixture.Profile));
        Assert.Equal(DatasetActivationFileState.Active, (await fixture.ReadFileAsync()).State);
    }

    [Fact]
    public async Task Awaiting_completion_without_pending_native_rows_still_blocks_rotation() {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        await SeedAsync(fixture, binding);
        await CompleteNativeAsync(fixture);
        await using (var context = fixture.Projects()) {
            await context.Set<ProjectDeletionCompletionRecord>().ExecuteUpdateAsync(set => set
                .SetProperty(row => row.ReleaseState, ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease)
                .SetProperty(row => row.ReleasedAtUtc, (DateTimeOffset?)null));
        }
        Assert.Equal(binding, await fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
        var selection = await fixture.ReadFileAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.ReactivateAsync(fixture.Profile, binding.Namespace, Guid.NewGuid()));
        Assert.Equal(selection, await fixture.ReadFileAsync());
    }

    [Fact]
    public async Task Unfinished_existence_and_header_reads_use_partial_indexes_with_large_terminal_history() {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        await SeedAsync(fixture, binding);
        await CompleteNativeAsync(fixture);
        await using var context = fixture.Projects();
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "AgentFramework_ProjectAccessRevocations"
                ("Id", "ProjectId", "Status", "CreatedAtUtc", "UpdatedAtUtc", "AttemptCount", "CompletedAtUtc")
            SELECT gen_random_uuid(), gen_random_uuid(), 4, now(), now(), 0, now() FROM generate_series(1, 10000);
            INSERT INTO "Workbench_ProjectCrossModuleMutations"
                ("Id", "ProjectId", "ScopeNodeKey", "MutationKind", "Status", "ApprovalState", "PayloadJson", "ErrorMessage", "AttemptCount", "CreatedAtUtc", "UpdatedAtUtc", "CompletedAtUtc")
            SELECT gen_random_uuid(), gen_random_uuid(), 'terminal', 4, 2, 0, repeat('x', 1024), '', 0, now(), now(), now() FROM generate_series(1, 10000);
            INSERT INTO "Projects_DeletionCompletions"
                ("NamespaceId", "CompletionId", "DatabaseProfileId", "ProjectId", "LifetimeId", "SchemaVersion", "OriginKind", "CreatedAtUtc", "ReleaseState", "ReleasedAtUtc", "ReferencePayload", "RecoveryPayload", "RecoveryDigest")
            SELECT gen_random_uuid(), gen_random_uuid(), c."DatabaseProfileId", gen_random_uuid(), gen_random_uuid(), c."SchemaVersion", c."OriginKind", now(), 2, now(), c."ReferencePayload", c."RecoveryPayload", c."RecoveryDigest"
            FROM (SELECT * FROM "Projects_DeletionCompletions" LIMIT 1) AS c CROSS JOIN generate_series(1, 10000);
            """);
        await SeedAsync(fixture, binding, ProjectDeletionPendingPage.MaximumCount + 3);
        await context.Database.ExecuteSqlRawAsync("""
            ANALYZE "AgentFramework_ProjectAccessRevocations";
            ANALYZE "Workbench_ProjectCrossModuleMutations";
            ANALYZE "Projects_DeletionCompletions";
            """);
        await context.Database.OpenConnectionAsync();
        var captured = new PendingCommands();
        var transactions = CoordinatedDatabaseTransaction.ForProfile(fixture.Profile);
        await using (var ownerTransaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)) {
            using (transactions.Enter(context)) {
                IProjectDeletionPendingInventory[] inventories = [
                    new AgentProjectDeletionPendingInventory(new DbContextOptionsBuilder<AgentProjectAccessDbContext>()
                        .UseNpgsql(fixture.Profile.ConnectionString).AddInterceptors(captured).Options, transactions),
                    new ProjectWorkbenchDeletionPendingInventory(new DbContextOptionsBuilder<WorkbenchDbContext>()
                        .UseNpgsql(fixture.Profile.ConnectionString).AddInterceptors(captured).Options, transactions)
                ];
                foreach (var inventory in inventories) {
                    Assert.True(await inventory.HasPendingAsync());
                    var first = await inventory.ReadPageAsync(fixture.Profile.Profile.Id, null);
                    Assert.Equal(ProjectDeletionPendingPage.MaximumCount, first.Items.Length);
                    var next = await inventory.ReadPageAsync(fixture.Profile.Profile.Id, first.Items[^1].Obligation.RecoveryId);
                    Assert.Equal(3, next.Items.Length);
                }
                var guard = new ProjectDeletionDatasetGuard(fixture.Profile.Profile.Id,
                    new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(fixture.Profile.ConnectionString)
                        .AddInterceptors(captured).Options, transactions, inventories);
                await Assert.ThrowsAsync<InvalidOperationException>(() => guard.RequireRotationAllowedEnlistedAsync(binding));
            }
            Assert.Equal(7, captured.Queries.Count);
            foreach (var query in captured.Queries) {
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.Transaction = ownerTransaction.GetDbTransaction();
                command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + query.Text;
                command.CommandTimeout = 5;
                command.Parameters.AddRange(query.Parameters);
                string plan = Assert.IsType<string>(await command.ExecuteScalarAsync());
                output.WriteLine(plan);
                string index = query.Text.Contains("AgentFramework_ProjectAccessRevocations", StringComparison.Ordinal)
                    ? "IX_AF_ProjectAccessRevocations_Unfinished"
                    : query.Text.Contains("Workbench_ProjectCrossModuleMutations", StringComparison.Ordinal)
                        ? "IX_Workbench_ProjectCrossModuleMutations_UnfinishedDeletion" : "IX_Projects_DeletionCompletions_Awaiting";
                Assert.Contains(index, plan, StringComparison.Ordinal);
                using var parsed = JsonDocument.Parse(plan);
                var nodes = PlanNodes(parsed.RootElement[0].GetProperty("Plan")).ToArray();
                Assert.DoesNotContain(nodes, node => node.GetProperty("Node Type").GetString() == "Seq Scan" &&
                    node.TryGetProperty("Relation Name", out var table) &&
                    table.GetString() is "AgentFramework_ProjectAccessRevocations" or "Workbench_ProjectCrossModuleMutations" or "Projects_DeletionCompletions");
                if (query.Parameters.Any(parameter => parameter.Value is Guid)) {
                    Assert.Contains(nodes, node => node.TryGetProperty("Index Cond", out var condition) &&
                        condition.GetString()!.Contains('>'));
                }
            }
        }
        Assert.Equal(binding, await fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
    }

    private static IEnumerable<JsonElement> PlanNodes(JsonElement node) {
        yield return node;
        if (node.TryGetProperty("Plans", out var children)) {
            foreach (var child in children.EnumerateArray().SelectMany(PlanNodes)) {
                yield return child;
            }
        }
    }

    [Fact]
    public void Native_participant_catalog_requires_the_two_explicit_inventory_owners() {
        var participants = CanDoItAll.Composition.ModuleAssemblies.All.SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract && typeof(IProjectDeletionParticipant).IsAssignableFrom(type)).Distinct().OrderBy(type => type.FullName).ToArray();
        Assert.Equal(new[] { typeof(AgentProjectStructureAccessDeletionParticipant), typeof(ProjectWorkbenchDeletionParticipant) }.OrderBy(type => type.FullName), participants);
        var inventories = CanDoItAll.Composition.ModuleAssemblies.All.SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract && typeof(IProjectDeletionPendingInventory).IsAssignableFrom(type)).Distinct().OrderBy(type => type.FullName).ToArray();
        Assert.Equal(new[] { typeof(AgentProjectDeletionPendingInventory), typeof(ProjectWorkbenchDeletionPendingInventory) }.OrderBy(type => type.FullName), inventories);
    }

    private static async Task InOwnerAsync(DatasetActivationTestFixture fixture,
        Func<CoordinatedDatabaseTransaction, IProjectDeletionPendingInventory, Task> action) {
        var transactions = CoordinatedDatabaseTransaction.ForProfile(fixture.Profile);
        await using var owner = fixture.Projects();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        using (transactions.Enter(owner)) {
            await action(transactions, new ProjectWorkbenchDeletionPendingInventory(
                new DbContextOptionsBuilder<WorkbenchDbContext>().UseNpgsql(fixture.Profile.ConnectionString).Options, transactions));
        }
    }

    private static async Task SeedAsync(DatasetActivationTestFixture fixture, DatasetActivationBinding binding, int count = 1, bool released = false) {
        await using var context = fixture.Projects();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding);
        for (int index = 0; index < count; index++) {
            var original = new ProjectDeletionPreparationContext(new(fixture.Profile.Profile.Id, binding.Namespace, new(Guid.NewGuid())),
                new(fixture.Profile.Profile.Id, Guid.NewGuid(), Guid.NewGuid()));
            var agent = new AgentProjectStructureAccessRevocationRecord {
                Id = Guid.NewGuid(), ProjectId = original.Project.ProjectId, DatabaseProfileId = original.Project.DatabaseProfileId,
                ProjectLifetimeId = original.Project.LifetimeId, CompletionNamespaceId = binding.Namespace.Value, CompletionId = original.Completion.Id.Value,
                Status = AgentProjectStructureAccessRevocationStatus.Pending, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            var work = new ProjectCrossModuleMutationRecord {
                Id = Guid.NewGuid(), ProjectId = original.Project.ProjectId, ScopeNodeKey = ProjectWorkbenchDeletionParticipant.ProjectDeletionScopeNodeKey,
                MutationKind = ProjectCrossModuleMutationKind.DeleteProject, Status = ProjectCrossModuleMutationStatus.WorkbenchCommitted,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
                PayloadJson = JsonSerializer.Serialize(new DeleteProjectMutationPayload([], [], SourceReference: ProjectAssignmentReference.From(original.Project), CompletionContext: original), Json)
            };
            var obligations = new ProjectDeletionObligation[] {
                new(new(AgentProjectStructureAccessDeletionParticipant.ParticipantIdValue), agent.Id),
                new(new(ProjectWorkbenchDeletionParticipant.ParticipantIdValue), work.Id)
            };
            var recovery = ProjectDeletionRecoveryCodec.Write(new(original,
                new ProjectDeletionOriginEvidence.Compensation(original.Project, new string('a', 64), null),
                new ProjectDeletionSourceRecovery.NoExternalSourceLease(), [original.Project]));
            context.AddRange(agent, work, new ProjectRetirementRecord {
                ProjectId = original.Project.ProjectId, LifetimeId = original.Project.LifetimeId, RetiredAtUtc = DateTimeOffset.UtcNow
            }, new ProjectDeletionCompletionRecord {
                NamespaceId = binding.Namespace.Value, CompletionId = original.Completion.Id.Value, DatabaseProfileId = original.Project.DatabaseProfileId,
                ProjectId = original.Project.ProjectId, LifetimeId = original.Project.LifetimeId, SchemaVersion = ProjectDeletionCompletionProtocol.SchemaVersion,
                OriginKind = ProjectDeletionCompletionOrigin.CreationCompensation, CreatedAtUtc = DateTimeOffset.UtcNow,
                ReleaseState = released ? ProjectDeletionCompletionReleaseState.Released : ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease,
                ReleasedAtUtc = released ? DateTimeOffset.UtcNow : null, ReferencePayload = ProjectDeletionCompletionCodec.Write(new(original, obligations)),
                RecoveryPayload = recovery, RecoveryDigest = SHA256.HashData(recovery)
            });
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static async Task CompleteNativeAsync(DatasetActivationTestFixture fixture) {
        await using var context = fixture.Projects();
        var now = DateTimeOffset.UtcNow;
        await context.Set<AgentProjectStructureAccessRevocationRecord>().ExecuteUpdateAsync(set => set
            .SetProperty(row => row.Status, AgentProjectStructureAccessRevocationStatus.Completed).SetProperty(row => row.CompletedAtUtc, now));
        await context.Set<ProjectCrossModuleMutationRecord>().ExecuteUpdateAsync(set => set
            .SetProperty(row => row.Status, ProjectCrossModuleMutationStatus.Completed).SetProperty(row => row.CompletedAtUtc, now));
        await context.Set<ProjectDeletionCompletionRecord>().ExecuteUpdateAsync(set => set
            .SetProperty(row => row.ReleaseState, ProjectDeletionCompletionReleaseState.Released).SetProperty(row => row.ReleasedAtUtc, now));
    }

    private static async Task<string> NativeStateAsync(DatasetActivationTestFixture fixture) {
        await using var context = fixture.Projects();
        return JsonSerializer.Serialize(new {
            Agents = await context.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            Workbench = await context.Set<ProjectCrossModuleMutationRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            Completions = await context.Set<ProjectDeletionCompletionRecord>().AsNoTracking().OrderBy(row => row.CompletionId).ToArrayAsync()
        }, Json);
    }

    private static string Pad(string json, int bytes) => json.Insert(1, "\"retainedPadding\":\"" + new string('a', bytes - Encoding.UTF8.GetByteCount(json) - 21) + "\",");

    public enum InvalidBinding { MissingCompletion, MissingRetirement, ForeignNamespace, ForeignProfile, WrongObligation, UnboundAgent, PartialAgent, UnboundWorkbench, InvalidAgentStatus }

    private sealed class PendingCommands : DbCommandInterceptor {
        internal List<PendingQuery> Queries { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Queries.Add(new(command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
                .Select(parameter => (NpgsqlParameter)((ICloneable)parameter).Clone()).ToArray()));
            return ValueTask.FromResult(result);
        }
    }

    private sealed record PendingQuery(string Text, NpgsqlParameter[] Parameters);

}
