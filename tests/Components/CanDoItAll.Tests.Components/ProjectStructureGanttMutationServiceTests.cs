using CanDoItAll.Components.Gantt;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureGanttMutationServiceTests
{
    private static readonly DateTimeOffset Baseline = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ApplyTitleAsync_RejectsStaleTitleWithoutWriting()
    {
        await using var fixture = await MutationFixture.CreateAsync(
            CreateTask("custom:00000000000000000000000000000001", "Authoritative", 0, 1));
        var request = new GanttTaskTitleChangeRequest(
            TaskId("custom:00000000000000000000000000000001"),
            "Stale title",
            "Proposed title");

        var exception = await Assert.ThrowsAsync<ProjectStructureGanttMutationException>(() =>
            fixture.Service.ApplyTitleAsync(fixture.ProjectId, request));

        Assert.Equal(ProjectStructureGanttMutationErrorCode.StaleTask, exception.Code);
        var task = await fixture.FindTaskAsync(request.TaskId.Value);
        Assert.Equal("Authoritative", task.Title);
    }

    [Fact]
    public async Task ApplyDependencyAsync_PersistsSuccessorDependsOnPredecessor_AndPreservesMultiplePrerequisites()
    {
        var predecessorA = CreateTask("custom:00000000000000000000000000000001", "A", 0, 1);
        var successor = CreateTask("custom:00000000000000000000000000000002", "B", 1, 2);
        var predecessorC = CreateTask("custom:00000000000000000000000000000003", "C", 2, 3);
        var existingLink = DependsOn(successor.NodeKey, predecessorA.NodeKey);
        await using var fixture = await MutationFixture.CreateAsync(
            [predecessorA, successor, predecessorC],
            [existingLink]);
        var addedId = Guid.NewGuid();
        var request = new GanttDependencyMutationRequest(
            GanttDependencyMutationKind.Add,
            null,
            new GanttDependency(
                PendingDependencyId(addedId),
                TaskId(predecessorC.NodeKey),
                TaskId(successor.NodeKey)));

        var result = await fixture.Service.ApplyDependencyAsync(fixture.ProjectId, request);

        Assert.Equal(1, result.AddedDependencyCount);
        var links = await fixture.LoadLinksAsync();
        Assert.Equal(2, links.Count);
        Assert.Contains(links, link =>
            link.Id == addedId &&
            link.SourceNodeKey == successor.NodeKey &&
            link.TargetNodeKey == predecessorC.NodeKey);
        Assert.Contains(links, link => link.Id == existingLink.Id);
        var shiftedSuccessor = await fixture.FindTaskAsync(successor.NodeKey);
        Assert.Equal(At(3), shiftedSuccessor.StartUtc);
        Assert.Equal(At(4), shiftedSuccessor.EndUtc);
        Assert.Equal(3600, shiftedSuccessor.DurationSeconds);
    }

    [Fact]
    public async Task ApplyDependencyAsync_RejectsCycleWithoutWriting()
    {
        var taskA = CreateTask("custom:00000000000000000000000000000001", "A", 0, 1);
        var taskB = CreateTask("custom:00000000000000000000000000000002", "B", 1, 2);
        var taskC = CreateTask("custom:00000000000000000000000000000003", "C", 2, 3);
        var links = new[]
        {
            DependsOn(taskB.NodeKey, taskA.NodeKey),
            DependsOn(taskC.NodeKey, taskB.NodeKey)
        };
        await using var fixture = await MutationFixture.CreateAsync([taskA, taskB, taskC], links);
        var request = new GanttDependencyMutationRequest(
            GanttDependencyMutationKind.Add,
            null,
            new GanttDependency(
                PendingDependencyId(Guid.NewGuid()),
                TaskId(taskC.NodeKey),
                TaskId(taskA.NodeKey)));

        var exception = await Assert.ThrowsAsync<ProjectStructureGanttMutationException>(() =>
            fixture.Service.ApplyDependencyAsync(fixture.ProjectId, request));

        Assert.Equal(ProjectStructureGanttMutationErrorCode.CycleDetected, exception.Code);
        Assert.Equal(2, (await fixture.LoadLinksAsync()).Count);
    }

    [Fact]
    public async Task ApplyDependencyAsync_ReconnectsPersistedLinkIdentity_AndShiftsNewSuccessorConstraint()
    {
        var taskA = CreateTask("custom:00000000000000000000000000000001", "A", 0, 1);
        var taskB = CreateTask("custom:00000000000000000000000000000002", "B", 1, 2);
        var taskC = CreateTask("custom:00000000000000000000000000000003", "C", 2, 3);
        var link = DependsOn(taskB.NodeKey, taskA.NodeKey);
        await using var fixture = await MutationFixture.CreateAsync([taskA, taskB, taskC], [link]);
        var previous = Dependency(link, taskA.NodeKey, taskB.NodeKey);
        var request = new GanttDependencyMutationRequest(
            GanttDependencyMutationKind.Reconnect,
            previous,
            new GanttDependency(
                previous.Id,
                TaskId(taskC.NodeKey),
                TaskId(taskB.NodeKey)));

        await fixture.Service.ApplyDependencyAsync(fixture.ProjectId, request);

        var persistedLink = Assert.Single(await fixture.LoadLinksAsync());
        Assert.Equal(link.Id, persistedLink.Id);
        Assert.Equal(taskB.NodeKey, persistedLink.SourceNodeKey);
        Assert.Equal(taskC.NodeKey, persistedLink.TargetNodeKey);
        var persistedB = await fixture.FindTaskAsync(taskB.NodeKey);
        Assert.Equal(At(3), persistedB.StartUtc);
        Assert.Equal(At(4), persistedB.EndUtc);
    }

    [Fact]
    public async Task ApplyScheduleAsync_WhenOneAffectedTaskIsStale_WritesNoDates()
    {
        var taskA = CreateTask("custom:00000000000000000000000000000001", "A", 0, 1);
        var taskB = CreateTask("custom:00000000000000000000000000000002", "B", 1, 2);
        await using var fixture = await MutationFixture.CreateAsync(
            [taskA, taskB],
            [DependsOn(taskB.NodeKey, taskA.NodeKey)]);
        var request = new GanttTaskScheduleChangeRequest(
            TaskId(taskA.NodeKey),
            GanttScheduleGesture.ResizeEnd,
            [
                DateChange(taskA.NodeKey, 0, 1, 0, 2),
                new GanttTaskDateChange(
                    TaskId(taskB.NodeKey),
                    At(1).AddMinutes(1),
                    At(2).AddMinutes(1),
                    At(2),
                    At(3),
                    true)
            ],
            [TaskId(taskA.NodeKey), TaskId(taskB.NodeKey)]);

        var exception = await Assert.ThrowsAsync<ProjectStructureGanttMutationException>(() =>
            fixture.Service.ApplyScheduleAsync(fixture.ProjectId, request));

        Assert.Equal(ProjectStructureGanttMutationErrorCode.StaleTask, exception.Code);
        var persistedA = await fixture.FindTaskAsync(taskA.NodeKey);
        var persistedB = await fixture.FindTaskAsync(taskB.NodeKey);
        Assert.Equal(At(0), persistedA.StartUtc);
        Assert.Equal(At(1), persistedA.EndUtc);
        Assert.Equal(At(1), persistedB.StartUtc);
        Assert.Equal(At(2), persistedB.EndUtc);
    }

    [Fact]
    public async Task ApplyScheduleAsync_RejectsProjectionOnlySchedule()
    {
        var task = CreateTask("custom:00000000000000000000000000000001", "Unscheduled", 0, 1);
        task.StartUtc = null;
        task.EndUtc = null;
        task.DurationSeconds = null;
        await using var fixture = await MutationFixture.CreateAsync(task);
        var request = new GanttTaskScheduleChangeRequest(
            TaskId(task.NodeKey),
            GanttScheduleGesture.Move,
            [DateChange(task.NodeKey, 0, 1, 2, 3)],
            []);

        var exception = await Assert.ThrowsAsync<ProjectStructureGanttMutationException>(() =>
            fixture.Service.ApplyScheduleAsync(fixture.ProjectId, request));

        Assert.Equal(ProjectStructureGanttMutationErrorCode.ProjectionOnlySchedule, exception.Code);
        var persisted = await fixture.FindTaskAsync(task.NodeKey);
        Assert.Null(persisted.StartUtc);
        Assert.Null(persisted.EndUtc);
    }

    [Fact]
    public async Task ApplyTaskDetailsAsync_Persists_title_progress_estimate_and_propagated_schedule_atomically()
    {
        var taskA = CreateTask("custom:00000000000000000000000000000001", "A", 0, 1);
        taskA.ProgressPercent = 25;
        taskA.ProgressMode = "progress";
        var currentEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.ManDays,
            900m,
            "USD");
        taskA.MetadataJson = EstimateMetadata(currentEstimate);
        var taskB = CreateTask("custom:00000000000000000000000000000002", "B", 1, 2);
        await using var fixture = await MutationFixture.CreateAsync(
            [taskA, taskB],
            [DependsOn(taskB.NodeKey, taskA.NodeKey)]);
        var proposedEstimate = new ProjectTaskEstimate(
            4m,
            ProjectWorkItemEffortUnit.Hours,
            500m,
            "EUR");
        var schedule = new GanttTaskScheduleChangeRequest(
            TaskId(taskA.NodeKey),
            GanttScheduleGesture.ResizeEnd,
            [
                DateChange(taskA.NodeKey, 0, 1, 0, 2),
                DateChange(taskB.NodeKey, 1, 2, 2, 3)
            ],
            [TaskId(taskA.NodeKey), TaskId(taskB.NodeKey)]);
        var request = new ProjectStructureTaskDetailsUpdateRequest(
            TaskId(taskA.NodeKey),
            "A",
            "Updated A",
            25,
            60,
            currentEstimate,
            proposedEstimate,
            schedule,
            AssigneeChanged: false,
            ProposedAssignee: null);

        var result = await fixture.Service.ApplyTaskDetailsAsync(fixture.ProjectId, request);

        Assert.Equal(2, result.AffectedTaskIds.Count);
        var persistedA = await fixture.FindTaskAsync(taskA.NodeKey);
        var persistedB = await fixture.FindTaskAsync(taskB.NodeKey);
        Assert.Equal("Updated A", persistedA.Title);
        Assert.Equal("progress", persistedA.ProgressMode);
        Assert.Equal(60, persistedA.ProgressPercent);
        Assert.Equal(At(0), persistedA.StartUtc);
        Assert.Equal(At(2), persistedA.EndUtc);
        Assert.Equal(At(2), persistedB.StartUtc);
        Assert.Equal(At(3), persistedB.EndUtc);
        var metadata = ProjectObjectMetadataSerializer.Parse(persistedA.MetadataJson).WorkItem;
        Assert.NotNull(metadata);
        Assert.Equal(proposedEstimate.ExpectedEffortHours, metadata.ExpectedEffortHours);
        Assert.Equal(proposedEstimate.ExpectedEffortUnit, metadata.ExpectedEffortUnit);
        Assert.Equal(proposedEstimate.ExpectedCostAmount, metadata.ExpectedCostAmount);
        Assert.Equal(proposedEstimate.ExpectedCostCurrencyCode, metadata.ExpectedCostCurrencyCode);
    }

    [Fact]
    public async Task ApplyTaskDetailsAsync_When_estimate_is_stale_writes_nothing()
    {
        var task = CreateTask("custom:00000000000000000000000000000001", "Authoritative", 0, 1);
        task.ProgressPercent = 10;
        task.MetadataJson = EstimateMetadata(new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            100m,
            "USD"));
        await using var fixture = await MutationFixture.CreateAsync(task);
        var staleEstimate = new ProjectTaskEstimate(
            4m,
            ProjectWorkItemEffortUnit.Hours,
            100m,
            "USD");
        var request = new ProjectStructureTaskDetailsUpdateRequest(
            TaskId(task.NodeKey),
            "Authoritative",
            "Should not persist",
            10,
            90,
            staleEstimate,
            ProjectTaskEstimate.Empty(),
            ScheduleChange: null,
            AssigneeChanged: false,
            ProposedAssignee: null);

        var exception = await Assert.ThrowsAsync<ProjectStructureGanttMutationException>(() =>
            fixture.Service.ApplyTaskDetailsAsync(fixture.ProjectId, request));

        Assert.Equal(ProjectStructureGanttMutationErrorCode.StaleTask, exception.Code);
        var persisted = await fixture.FindTaskAsync(task.NodeKey);
        Assert.Equal("Authoritative", persisted.Title);
        Assert.Equal(10, persisted.ProgressPercent);
        Assert.Equal(At(0), persisted.StartUtc);
        Assert.Equal(At(1), persisted.EndUtc);
    }

    [Fact]
    public async Task ApplyInsertionAsync_RewiresOnlyBridge_PreservesOtherPrerequisite_AndPersistsCriticalPathDates()
    {
        var taskA = CreateTask("custom:00000000000000000000000000000001", "A", 0, 1);
        var taskB = CreateTask("custom:00000000000000000000000000000002", "B", 1, 2);
        var taskC = CreateTask("custom:00000000000000000000000000000003", "C", 2, 3);
        var taskD = CreateTask("custom:00000000000000000000000000000004", "D", 0, 1);
        var bridge = DependsOn(taskB.NodeKey, taskA.NodeKey);
        var otherPrerequisite = DependsOn(taskB.NodeKey, taskD.NodeKey);
        var downstream = DependsOn(taskC.NodeKey, taskB.NodeKey);
        await using var fixture = await MutationFixture.CreateAsync(
            [taskA, taskB, taskC, taskD],
            [bridge, otherPrerequisite, downstream]);
        var insertedId = TaskId("custom:00000000000000000000000000000005");
        var predecessorToInsertedId = Guid.NewGuid();
        var insertedToSuccessorId = Guid.NewGuid();
        var insertedTask = new GanttTask(insertedId, "Inserted", At(1), At(1.5));
        var request = new GanttTaskInsertionRequest(
            insertedTask,
            TaskId(taskA.NodeKey),
            TaskId(taskB.NodeKey),
            [
                new GanttDependencyMutationRequest(
                    GanttDependencyMutationKind.Remove,
                    Dependency(bridge, taskA.NodeKey, taskB.NodeKey),
                    null),
                new GanttDependencyMutationRequest(
                    GanttDependencyMutationKind.Add,
                    null,
                    new GanttDependency(
                        PendingDependencyId(predecessorToInsertedId),
                        TaskId(taskA.NodeKey),
                        insertedId)),
                new GanttDependencyMutationRequest(
                    GanttDependencyMutationKind.Add,
                    null,
                    new GanttDependency(
                        PendingDependencyId(insertedToSuccessorId),
                        insertedId,
                        TaskId(taskB.NodeKey)))
            ],
            [
                new GanttTaskDateChange(insertedId, At(1), At(1.5), At(1), At(1.5), true),
                DateChange(taskB.NodeKey, 1, 2, 1.5, 2.5),
                DateChange(taskC.NodeKey, 2, 3, 2.5, 3.5)
            ],
            [TaskId(taskA.NodeKey), insertedId, TaskId(taskB.NodeKey), TaskId(taskC.NodeKey)]);

        var result = await fixture.Service.ApplyInsertionAsync(fixture.ProjectId, request);

        Assert.Equal(2, result.AddedDependencyCount);
        Assert.Equal(1, result.RemovedDependencyCount);
        var links = await fixture.LoadLinksAsync();
        Assert.Equal(4, links.Count);
        Assert.DoesNotContain(links, link => link.Id == bridge.Id);
        Assert.Contains(links, link => link.Id == otherPrerequisite.Id);
        Assert.Contains(links, link => link.Id == downstream.Id);
        Assert.Contains(links, link =>
            link.Id == predecessorToInsertedId &&
            link.SourceNodeKey == insertedId.Value &&
            link.TargetNodeKey == taskA.NodeKey);
        Assert.Contains(links, link =>
            link.Id == insertedToSuccessorId &&
            link.SourceNodeKey == taskB.NodeKey &&
            link.TargetNodeKey == insertedId.Value);

        var inserted = await fixture.FindTaskAsync(insertedId.Value);
        var persistedB = await fixture.FindTaskAsync(taskB.NodeKey);
        var persistedC = await fixture.FindTaskAsync(taskC.NodeKey);
        Assert.Equal(ProjectObjectType.WorkItem, inserted.ObjectType);
        Assert.Equal("task", inserted.ObjectSubtype);
        Assert.Equal(At(1), inserted.StartUtc);
        Assert.Equal(At(1.5), inserted.EndUtc);
        Assert.Equal(1800, inserted.DurationSeconds);
        Assert.Equal(At(1.5), persistedB.StartUtc);
        Assert.Equal(At(2.5), persistedB.EndUtc);
        Assert.Equal(At(2.5), persistedC.StartUtc);
        Assert.Equal(At(3.5), persistedC.EndUtc);
    }

    private static ProjectObjectRecord CreateTask(
        string nodeKey,
        string title,
        double startHour,
        double endHour)
        => new()
        {
            Id = Guid.NewGuid(),
            NodeKey = nodeKey,
            ObjectType = ProjectObjectType.WorkItem,
            ObjectSubtype = "task",
            Title = title,
            Status = "Draft",
            MetadataJson = "{}",
            MarkersJson = "[]",
            StartUtc = At(startHour),
            EndUtc = At(endHour),
            DurationSeconds = (int)TimeSpan.FromHours(endHour - startHour).TotalSeconds,
            CreatedAtUtc = Baseline,
            UpdatedAtUtc = Baseline
        };

    private static ProjectObjectLinkRecord DependsOn(string successorId, string predecessorId)
        => new()
        {
            Id = Guid.NewGuid(),
            SourceNodeKey = successorId,
            TargetNodeKey = predecessorId,
            LinkKind = ProjectObjectLinkKind.DependsOn,
            CreatedAtUtc = Baseline
        };

    private static string EstimateMetadata(ProjectTaskEstimate estimate)
        => ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExpectedEffortHours = estimate.ExpectedEffortHours,
                ExpectedEffortUnit = estimate.ExpectedEffortUnit,
                ExpectedCostAmount = estimate.ExpectedCostAmount,
                ExpectedCostCurrencyCode = estimate.ExpectedCostCurrencyCode
            }
        });

    private static GanttDependency Dependency(
        ProjectObjectLinkRecord record,
        string predecessorId,
        string successorId)
        => new(
            PersistedDependencyId(record.Id),
            TaskId(predecessorId),
            TaskId(successorId));

    private static GanttTaskDateChange DateChange(
        string taskId,
        double previousStart,
        double previousEnd,
        double proposedStart,
        double proposedEnd)
        => new(
            TaskId(taskId),
            At(previousStart),
            At(previousEnd),
            At(proposedStart),
            At(proposedEnd),
            true);

    private static DateTimeOffset At(double hours)
        => Baseline.AddHours(hours);

    private static GanttTaskId TaskId(string value)
        => new(value);

    private static GanttDependencyId PersistedDependencyId(Guid value)
        => new($"project-link:{value:N}");

    private static GanttDependencyId PendingDependencyId(Guid value)
        => new($"gantt-dependency:{value:N}");

    private sealed class MutationFixture : IAsyncDisposable
    {
        private readonly TestDbContextFactory dbContextFactory;

        private MutationFixture(
            Guid projectId,
            TestDbContextFactory dbContextFactory,
            ProjectStructureGanttMutationService service)
        {
            ProjectId = projectId;
            this.dbContextFactory = dbContextFactory;
            Service = service;
        }

        public Guid ProjectId { get; }

        public ProjectStructureGanttMutationService Service { get; }

        public static Task<MutationFixture> CreateAsync(params ProjectObjectRecord[] tasks)
            => CreateAsync((IReadOnlyList<ProjectObjectRecord>)tasks, []);

        public static async Task<MutationFixture> CreateAsync(
            IReadOnlyList<ProjectObjectRecord> tasks,
            IReadOnlyList<ProjectObjectLinkRecord> links)
        {
            AppDbContextModelRegistry.ConfigureAssemblies(
            [
                typeof(Project).Assembly,
                typeof(ProjectObjectRecord).Assembly
            ]);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"gantt-mutations-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var factory = new TestDbContextFactory(options);
            var projectId = Guid.NewGuid();
            await using (var context = await factory.CreateDbContextAsync())
            {
                context.Set<Project>().Add(new Project
                {
                    Id = projectId,
                    Name = "Gantt mutation tests",
                    Slug = $"gantt-{projectId:N}",
                    CreatedAtUtc = Baseline,
                    UpdatedAtUtc = Baseline
                });
                foreach (var task in tasks)
                {
                    task.ProjectId = projectId;
                }

                foreach (var link in links)
                {
                    link.ProjectId = projectId;
                }

                context.Set<ProjectObjectRecord>().AddRange(tasks);
                context.Set<ProjectObjectLinkRecord>().AddRange(links);
                await context.SaveChangesAsync();
            }

            var service = new ProjectStructureGanttMutationService(
                factory,
                new FixedClock(Baseline.AddDays(1)),
                NullLogger<ProjectStructureGanttMutationService>.Instance);
            return new MutationFixture(projectId, factory, service);
        }

        public async Task<ProjectObjectRecord> FindTaskAsync(string nodeKey)
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Set<ProjectObjectRecord>().SingleAsync(task =>
                task.ProjectId == ProjectId && task.NodeKey == nodeKey);
        }

        public async Task<IReadOnlyList<ProjectObjectLinkRecord>> LoadLinksAsync()
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Set<ProjectObjectLinkRecord>()
                .Where(link => link.ProjectId == ProjectId && link.LinkKind == ProjectObjectLinkKind.DependsOn)
                .ToListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            await context.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset GetUtcNow()
            => value;
    }
}
