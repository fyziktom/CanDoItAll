using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkbenchAssignmentOwnerPersistenceTests {
    [Fact]
    public async Task Work_owner_add_and_remove_stage_final_tracked_assignments_on_the_same_canonical_task_after_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("workbench-assignment-owner");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        Seed seed;
        await using (var original = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile })) {
            seed = await SeedAsync(original);
        }
        var commands = new WorkbenchCommandProbe();
        var saves = new WorkSaveProbe();
        await using var application = await TestApplication.CreateAsync(Harness(environment, profile, commands, saves));
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var result = await service.SaveAssignmentAsync(Request(seed));
        Assert.True(result.IsSuccess);
        AssertEnlisted(commands, saves);
        await AssertTaskAsync(application, seed, expectedRevision: 1, expectedDisplayName: "Chosen person", expectedCost: null);
        await using (var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            var assignment = await canonical.Set<ProjectWorkAssignmentRecord>().SingleAsync(item => item.Id == result.Value);
            Assert.Equal(seed.ProjectId, assignment.ProjectId);
            Assert.Equal(seed.TaskNodeKey, assignment.NodeKey);
            Assert.Equal(seed.PartyId, assignment.PartyId);
        }
        commands.Commands.Clear();
        await service.DeleteAssignmentAsync(result.Value, expectedReference: ProjectAssignmentReference.From(seed.Admission));
        AssertEnlisted(commands, saves);
        await AssertTaskAsync(application, seed, expectedRevision: 2, expectedDisplayName: string.Empty, expectedCost: null);
        await using (var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            Assert.False(await canonical.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == result.Value));
        }
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherWorkbench = await other.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.False(await otherWorkbench.Set<ProjectObjectRecord>().AnyAsync(item => item.Id == seed.TaskId));
        await AssertTaskAsync(application, seed, expectedRevision: 2, expectedDisplayName: string.Empty, expectedCost: null);
    }

    [Fact]
    public async Task Failure_in_the_final_work_assignment_save_rolls_back_successfully_staged_task_changes() {
        var commands = new WorkbenchCommandProbe();
        var saves = new WorkSaveProbe();
        await using var application = await TestApplication.CreateAsync(Harness(commands: commands, saves: saves));
        var seed = await SeedAsync(application);
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        commands.Commands.Clear();
        saves.FailAssignmentSave = true;
        await Assert.ThrowsAsync<InjectedWorkSaveFailure>(() => service.SaveAssignmentAsync(Request(seed)));
        saves.FailAssignmentSave = false;
        AssertEnlisted(commands, saves);
        Assert.Contains(commands.Commands, command => command.Text.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        await AssertTaskAsync(application, seed, expectedRevision: 0, expectedDisplayName: string.Empty, expectedCost: 100m);
        await using var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.False(await canonical.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.ProjectId == seed.ProjectId));
        Assert.True(await canonical.Set<ProjectNodeReferenceRecord>().AnyAsync(item => item.Id == seed.ReferenceRowId));
    }

    [Fact]
    public async Task Later_revision_conflict_can_roll_back_an_earlier_successful_task_stage() {
        await using var application = await TestApplication.CreateAsync();
        var first = await SeedAsync(application);
        var second = await SeedAsync(application);
        await using var scope = application.Services.CreateAsyncScope();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectWorkItemAssignmentMutationBridge>();
        var admissions = scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>();
        var firstAdmission = await admissions.CaptureAsync(first.ProjectId);
        var secondAdmission = await admissions.CaptureAsync(second.ProjectId);
        var coordinator = application.Services.GetRequiredService<CoordinatedDatabaseTransaction>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.StageMutationAsync(first.ProjectId,
            new(first.TaskNodeKey), [new(ProjectPartyType.Person, first.PartyId, true, "Chosen person")], expectedProjectAdmission: firstAdmission));
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        using (coordinator.Enter(owner)) {
            var applied = await bridge.StageMutationAsync(first.ProjectId, new(first.TaskNodeKey),
                [new(ProjectPartyType.Person, first.PartyId, true, "Chosen person")], new(0), expectedProjectAdmission: firstAdmission);
            Assert.Equal(ProjectWorkItemDirectAssignmentMutationStatus.Applied, applied.Status);
            var conflict = await bridge.StageMutationAsync(second.ProjectId, new(second.TaskNodeKey),
                [new(ProjectPartyType.Person, second.PartyId, true, "Another person")], new(7), expectedProjectAdmission: secondAdmission);
            Assert.Equal(ProjectWorkItemDirectAssignmentMutationStatus.RevisionConflict, conflict.Status);
            Assert.Equal(0L, conflict.Revision!.Value.Value);
            var foreignProject = await bridge.StageMutationAsync(second.ProjectId, new(first.TaskNodeKey), [], expectedProjectAdmission: secondAdmission);
            Assert.Equal(ProjectWorkItemDirectAssignmentMutationStatus.WorkItemNotFound, foreignProject.Status);
        }
        await transaction.RollbackAsync();
        await AssertTaskAsync(application, first, expectedRevision: 0, expectedDisplayName: string.Empty, expectedCost: 100m);
        await AssertTaskAsync(application, second, expectedRevision: 0, expectedDisplayName: string.Empty, expectedCost: 100m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.StageMutationAsync(first.ProjectId, new(first.TaskNodeKey), [], expectedProjectAdmission: firstAdmission));
    }

    private static async Task<Seed> SeedAsync(TestApplication application) {
        var projectId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var bindingId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var nodeKey = $"task:{taskId:N}";
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var metadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
            WorkItem = new ProjectWorkItemMetadata {
                WorkItemKind = ProjectWorkItemKind.Task, ExpectedEffortHours = 2m, ExpectedEffortUnit = ProjectWorkItemEffortUnit.Hours,
                ExpectedCostAmount = 100m, ExpectedCostCurrencyCode = "USD", ExecutionState = ProjectTaskExecutionState.NotStarted
            }
        });
        metadata = metadata[..^1] + ",\"legacyRetained\":{\"key\":\"value\"}}";
        await using var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        canonical.AddRange(
            new Project { Id = projectId, Name = $"Assignment {projectId:N}", Slug = $"assignment-{projectId:N}", CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp },
            new Party { Id = partyId, PartyType = PartyType.Person, LifecycleStatus = PartyLifecycleStatus.Active, DisplayName = "Chosen person", CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp },
            new ProjectObjectRecord {
                Id = taskId, ProjectId = projectId, NodeKey = nodeKey, ObjectType = ProjectObjectType.WorkItem,
                ObjectSubtype = "task", Title = "Retained canonical task", MetadataJson = metadata, CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
            },
            new ProjectNodeBindingRecord {
                Id = bindingId, ProjectObjectId = taskId, Route = "/retained/task", ExternalArtifactKind = "WorkItem",
                ExternalArtifactId = taskId, CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
            },
            new ProjectNodeReferenceRecord {
                Id = referenceId, ProjectObjectId = taskId, ReferenceKind = "work-item.repository-resource",
                ReferenceId = resourceId.ToString(), OrderIndex = 3, CreatedAtUtc = timestamp
            });
        await canonical.SaveChangesAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var admission = Assert.IsType<ProjectWriteAdmission>(await scope.ServiceProvider
            .GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
        return new(projectId, partyId, taskId, nodeKey, bindingId, referenceId, resourceId, admission);
    }

    private static ProjectPartyAssignmentUpsertRequest Request(Seed seed) => new() {
        ProjectId = seed.ProjectId, ExpectedProjectAdmission = seed.Admission, PartyId = seed.PartyId, Role = ProjectPartyAssignmentRole.WorkItemAssignee,
        NodeKey = seed.TaskNodeKey, IsPrimary = true, Source = "owner-persistence-proof"
    };

    private static async Task AssertTaskAsync(TestApplication application, Seed seed, long expectedRevision, string expectedDisplayName, decimal? expectedCost) {
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var task = await owner.Set<ProjectObjectRecord>().SingleAsync(item => item.ProjectId == seed.ProjectId && item.NodeKey == seed.TaskNodeKey);
        Assert.Equal(seed.TaskId, task.Id);
        Assert.Equal(ProjectObjectType.WorkItem, task.ObjectType);
        Assert.Equal("task", task.ObjectSubtype);
        var workItem = Assert.IsType<ProjectWorkItemMetadata>(ProjectObjectMetadataSerializer.Parse(task.MetadataJson).WorkItem);
        Assert.Equal(expectedRevision, workItem.DirectAssignmentRevision);
        Assert.Equal(expectedDisplayName, workItem.AssigneePartyDisplayName);
        Assert.Equal(expectedCost, workItem.ExpectedCostAmount);
        Assert.Equal(expectedCost.HasValue ? "USD" : string.Empty, workItem.ExpectedCostCurrencyCode);
        using var json = JsonDocument.Parse(task.MetadataJson);
        Assert.Equal("value", json.RootElement.GetProperty("legacyRetained").GetProperty("key").GetString());
        var binding = await owner.Set<ProjectNodeBindingRecord>().SingleAsync(item => item.ProjectObjectId == seed.TaskId);
        Assert.Equal(seed.BindingId, binding.Id);
        Assert.Equal("/retained/task", binding.Route);
        Assert.Equal(seed.TaskId, binding.ExternalArtifactId);
        var reference = await owner.Set<ProjectNodeReferenceRecord>().SingleAsync(item => item.ProjectObjectId == seed.TaskId);
        Assert.Equal("work-item.repository-resource", reference.ReferenceKind);
        Assert.Equal(seed.ResourceId.ToString(), reference.ReferenceId);
        Assert.Equal(3, reference.OrderIndex);
    }

    private static TestHarnessOptions Harness(CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null,
        WorkbenchCommandProbe? commands = null, WorkSaveProbe? saves = null) => new() {
        TestEnvironment = environment, ActiveProfile = profile,
        ConfigureServices = services => {
            services.AddSingleton<DbContextOptions<WorkbenchDbContext>>(provider => {
                var options = new DbContextOptionsBuilder<WorkbenchDbContext>();
                AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
                if (commands is not null) {
                    options.AddInterceptors(commands);
                }
                if (saves is not null) {
                    options.AddInterceptors(saves);
                }
                return options.Options;
            });
            services.AddSingleton<DbContextOptions<AppDbContext>>(provider => {
                var options = new DbContextOptionsBuilder<AppDbContext>();
                AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
                if (saves is not null) {
                    options.AddInterceptors(saves);
                }
                return options.Options;
            });
        }
    };

    private static void AssertEnlisted(WorkbenchCommandProbe commands, WorkSaveProbe saves) {
        Assert.NotNull(saves.OwnerConnection);
        Assert.NotNull(saves.OwnerTransaction);
        var staged = commands.Commands.Where(command => command.Transaction is not null).ToArray();
        Assert.NotEmpty(staged);
        Assert.All(staged, command => {
            Assert.Same(saves.OwnerConnection, command.Connection);
            Assert.Same(saves.OwnerTransaction, command.Transaction);
        });
    }

    private sealed record Seed(Guid ProjectId, Guid PartyId, Guid TaskId, string TaskNodeKey, Guid BindingId, Guid ReferenceRowId,
        Guid ResourceId, ProjectWriteAdmission Admission);

    private sealed class InjectedWorkSaveFailure() : Exception("Injected failure after native task staging and before the final Work assignment save.") {
    }

    private sealed class WorkSaveProbe : SaveChangesInterceptor {
        public bool FailAssignmentSave { get; set; }
        public DbConnection? OwnerConnection { get; private set; }
        public DbTransaction? OwnerTransaction { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectWorkAssignmentRecord>()
                .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) {
                OwnerConnection = context.Database.GetDbConnection();
                OwnerTransaction = context.Database.CurrentTransaction?.GetDbTransaction();
                if (FailAssignmentSave) {
                    throw new InjectedWorkSaveFailure();
                }
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class WorkbenchCommandProbe : DbCommandInterceptor {
        public List<(string Text, DbConnection? Connection, DbTransaction? Transaction)> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.CommandText, command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.CommandText, command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }
    }
}
