using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectProjectionRepairAdmissionIntegrationTests {
    [Fact]
    public async Task Delayed_repair_rejects_same_id_recreation_and_preserves_all_replacement_rows() {
        await using var application = await TestApplication.CreateAsync();
        await using var originalScope = application.Services.CreateAsyncScope();
        await using var replacementScope = application.Services.CreateAsyncScope();
        var originalServices = originalScope.ServiceProvider;
        var replacementServices = replacementScope.ServiceProvider;
        var projectId = Guid.NewGuid();
        var projects = replacementServices.GetRequiredService<ProjectsService>();
        Assert.True((await projects.CreateAsync(projectId, new() { Name = "Original projection lifetime" })).IsSuccess);
        var admissions = replacementServices.GetRequiredService<ProjectWriteAdmissionService>();
        var originalAdmission = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(projectId));
        var ownerFactory = replacementServices.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        var pausedFactory = new PausingFactory(originalServices.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>());
        var maintenance = ActivatorUtilities.CreateInstance<ProjectStructureProjectionMaintenanceService>(originalServices, pausedFactory);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var repair = maintenance.RepairAsync(projectId, timeout.Token);
        SeededRows? replacementRows = null;
        ProjectWriteAdmission? replacementAdmission = null;
        Exception? failure = null;
        try {
            await pausedFactory.Paused.Task.WaitAsync(timeout.Token);
            await projects.DeleteAsync(projectId, timeout.Token, originalAdmission);
            Assert.Null(await admissions.CaptureAsync(projectId, timeout.Token));
            Assert.True((await projects.CreateAsync(projectId, new() { Name = "Replacement projection lifetime" }, timeout.Token)).IsSuccess);
            replacementAdmission = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(projectId, timeout.Token));
            Assert.NotEqual(originalAdmission.LifetimeId, replacementAdmission.LifetimeId);
            replacementRows = await SeedRowsAsync(ownerFactory, projectId, timeout.Token);
        } finally {
            pausedFactory.Resume.TrySetResult();
            failure = await Record.ExceptionAsync(() => repair.WaitAsync(timeout.Token));
        }

        Assert.Equal(originalAdmission, Assert.IsType<ProjectWriteAdmissionRejectedException>(failure).Admission);
        Assert.Equal(replacementAdmission, await admissions.CaptureAsync(projectId, timeout.Token));
        await AssertSeededRowsAsync(ownerFactory, Assert.IsType<SeededRows>(replacementRows), repaired: false, timeout.Token);
    }

    [Fact]
    public async Task Delayed_contributor_holds_the_project_gate_and_preserves_a_queued_human_layout_edit() {
        await using var application = await TestApplication.CreateAsync();
        await using var repairScope = application.Services.CreateAsyncScope();
        await using var editorScope = application.Services.CreateAsyncScope();
        var services = repairScope.ServiceProvider;
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>()
            .CreateAsync(projectId, new() { Name = "Projection repair with a queued edit" })).IsSuccess);
        var admission = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
        var factory = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var rows = await SeedRowsAsync(factory, projectId, timeout.Token);
        var gate = new PausingContributor();
        var contributors = services.GetServices<IProjectStructureProjectionContributor>().Append(gate).ToArray();
        var maintenance = ActivatorUtilities.CreateInstance<ProjectStructureProjectionMaintenanceService>(services, (object)contributors);
        var repair = maintenance.RepairAsync(projectId, timeout.Token);
        Task<IReadOnlyList<string>>? edit = null;
        ProjectStructureProjectionRepairResult? repairResult = null;
        IReadOnlyList<string>? editedNodes = null;
        Exception? repairFailure = null;
        Exception? editFailure = null;
        try {
            await gate.Paused.Task.WaitAsync(timeout.Token);
            await using (var observer = await factory.CreateDbContextAsync(timeout.Token)) {
                Assert.True(observer.Database.IsNpgsql());
                await using var observation = await observer.Database.BeginTransactionAsync(timeout.Token);
                var projectKey = ProjectMutationScopeKeys.ForProject(projectId);
                var acquired = await observer.Database.SqlQuery<bool>($"""
                    SELECT pg_try_advisory_xact_lock(hashtextextended({projectKey}, 0)) AS "Value"
                    """).SingleAsync(timeout.Token);
                Assert.False(acquired);
            }
            edit = editorScope.ServiceProvider.GetRequiredService<ProjectWorkbenchRelationService>().MoveObjectsAsync(
                projectId, [new(rows.HumanNode.NodeKey, 1111, 1222), new(rows.ValidLayout.NodeKey, 1333, 1444)],
                timeout.Token, expectedProjectAdmission: admission);
        } finally {
            gate.Resume.TrySetResult();
            repairFailure = await Record.ExceptionAsync(async () => {
                repairResult = await repair.WaitAsync(timeout.Token);
            });
            if (edit is not null) {
                editFailure = await Record.ExceptionAsync(async () => {
                    editedNodes = await edit.WaitAsync(timeout.Token);
                });
            }
        }

        Assert.Null(repairFailure);
        Assert.Null(editFailure);
        Assert.Equal(new ProjectStructureProjectionRepairResult(1, 1, 1), repairResult);
        Assert.Equal(new[] { rows.HumanNode.NodeKey, rows.ValidLayout.NodeKey }, editedNodes);
        await AssertSeededRowsAsync(factory, rows, repaired: true, timeout.Token, edited: true);
        Assert.Equal(admission, await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId, timeout.Token));
        Assert.Equal(new ProjectStructureProjectionRepairResult(0, 0, 0), await maintenance.RepairAsync(projectId, timeout.Token));
    }

    [Fact]
    public async Task A_failure_after_the_delete_flush_rolls_back_all_repair_rows() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>()
            .CreateAsync(projectId, new() { Name = "Projection repair rollback" })).IsSuccess);
        var factory = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var rows = await SeedRowsAsync(factory, projectId, timeout.Token);
        var expectedFailure = new InvalidOperationException("Injected projection repair flush failure.");
        var interceptor = new FailAfterFlush(expectedFailure);
        var options = new DbContextOptionsBuilder<WorkbenchDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        options.AddInterceptors(interceptor);
        var maintenance = ActivatorUtilities.CreateInstance<ProjectStructureProjectionMaintenanceService>(services, new OwnerFactory(options.Options));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.RepairAsync(projectId, timeout.Token));

        Assert.Same(expectedFailure, failure);
        Assert.Equal(3, interceptor.FlushedRowCount);
        Assert.True(interceptor.HadTransaction);
        await AssertSeededRowsAsync(factory, rows, repaired: false, timeout.Token);
        Assert.Equal(new ProjectStructureProjectionRepairResult(1, 1, 1),
            await services.GetRequiredService<ProjectStructureProjectionMaintenanceService>().RepairAsync(projectId, timeout.Token));
        await AssertSeededRowsAsync(factory, rows, repaired: true, timeout.Token);
    }

    private static async Task<SeededRows> SeedRowsAsync(IDbContextFactory<WorkbenchDbContext> factory, Guid projectId,
        CancellationToken cancellationToken) {
        var createdAt = new DateTimeOffset(2026, 4, 5, 13, 45, 0, TimeSpan.Zero);
        var rootKey = $"project:{projectId}";
        var human = new ProjectObjectRecord {
            ProjectId = projectId, NodeKey = Guid.NewGuid().ToString("D"), ObjectType = ProjectObjectType.Connector,
            Title = "Human title", Notes = "Human notes remain intact.", MetadataJson = "{\"human\":true}",
            ParentNodeKey = rootKey, PositionX = 120, PositionY = 220, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt
        };
        var stale = new ProjectObjectRecord {
            ProjectId = projectId, NodeKey = Guid.NewGuid().ToString("D"), ObjectType = ProjectObjectType.Connector,
            Title = "Legacy projection", ParentNodeKey = rootKey, IsSystemManaged = true,
            CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt
        };
        var humanLink = new ProjectObjectLinkRecord {
            ProjectId = projectId, SourceNodeKey = rootKey, TargetNodeKey = human.NodeKey,
            LinkKind = ProjectObjectLinkKind.Contains, CreatedAtUtc = createdAt
        };
        var staleLink = new ProjectObjectLinkRecord {
            ProjectId = projectId, SourceNodeKey = rootKey, TargetNodeKey = stale.NodeKey,
            LinkKind = ProjectObjectLinkKind.Contains, IsSystemManaged = true, CreatedAtUtc = createdAt
        };
        var validLayout = new ProjectStructureProjectionLayoutRecord {
            ProjectId = projectId, NodeKey = rootKey, PositionX = 320, PositionY = 420,
            IsHidden = true, UpdatedAtUtc = createdAt
        };
        var orphan = new ProjectStructureProjectionLayoutRecord {
            ProjectId = projectId, NodeKey = "projection:orphan-repair", PositionX = 520, PositionY = 620, UpdatedAtUtc = createdAt
        };
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        Assert.True(context.Database.IsNpgsql());
        context.AddRange(human, stale, humanLink, staleLink, validLayout, orphan);
        await context.SaveChangesAsync(cancellationToken);
        return new(human, stale, humanLink, staleLink, validLayout, orphan);
    }

    private static async Task AssertSeededRowsAsync(IDbContextFactory<WorkbenchDbContext> factory, SeededRows rows,
        bool repaired, CancellationToken cancellationToken, bool edited = false) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var human = await context.Set<ProjectObjectRecord>().AsNoTracking().SingleAsync(row => row.Id == rows.HumanNode.Id, cancellationToken);
        Assert.False(human.IsSystemManaged);
        Assert.Equal(rows.HumanNode.Title, human.Title);
        Assert.Equal(rows.HumanNode.Notes, human.Notes);
        Assert.Equal(rows.HumanNode.MetadataJson, human.MetadataJson);
        Assert.Equal(rows.HumanNode.ParentNodeKey, human.ParentNodeKey);
        Assert.Equal(edited ? 1111 : rows.HumanNode.PositionX, human.PositionX);
        Assert.Equal(edited ? 1222 : rows.HumanNode.PositionY, human.PositionY);
        var link = await context.Set<ProjectObjectLinkRecord>().AsNoTracking().SingleAsync(row => row.Id == rows.HumanLink.Id, cancellationToken);
        Assert.False(link.IsSystemManaged);
        Assert.Equal(rows.HumanLink.SourceNodeKey, link.SourceNodeKey);
        Assert.Equal(rows.HumanLink.TargetNodeKey, link.TargetNodeKey);
        Assert.Equal(rows.HumanLink.LinkKind, link.LinkKind);
        Assert.Equal(rows.HumanLink.CreatedAtUtc, link.CreatedAtUtc);
        var layout = await context.Set<ProjectStructureProjectionLayoutRecord>().AsNoTracking().SingleAsync(row => row.Id == rows.ValidLayout.Id, cancellationToken);
        Assert.Equal(rows.ValidLayout.IsHidden, layout.IsHidden);
        Assert.Equal(edited ? 1333 : rows.ValidLayout.PositionX, layout.PositionX);
        Assert.Equal(edited ? 1444 : rows.ValidLayout.PositionY, layout.PositionY);
        if (!edited) {
            Assert.Equal(rows.HumanNode.UpdatedAtUtc, human.UpdatedAtUtc);
            Assert.Equal(rows.ValidLayout.UpdatedAtUtc, layout.UpdatedAtUtc);
        }
        Assert.Equal(!repaired, await context.Set<ProjectObjectRecord>().AnyAsync(row => row.Id == rows.StaleNode.Id, cancellationToken));
        Assert.Equal(!repaired, await context.Set<ProjectObjectLinkRecord>().AnyAsync(row => row.Id == rows.StaleLink.Id, cancellationToken));
        Assert.Equal(!repaired, await context.Set<ProjectStructureProjectionLayoutRecord>().AnyAsync(row => row.Id == rows.OrphanLayout.Id, cancellationToken));
    }

    private sealed record SeededRows(ProjectObjectRecord HumanNode, ProjectObjectRecord StaleNode,
        ProjectObjectLinkRecord HumanLink, ProjectObjectLinkRecord StaleLink,
        ProjectStructureProjectionLayoutRecord ValidLayout, ProjectStructureProjectionLayoutRecord OrphanLayout);

    private sealed class PausingFactory(IDbContextFactory<WorkbenchDbContext> inner) : IDbContextFactory<WorkbenchDbContext> {
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WorkbenchDbContext CreateDbContext() => throw new InvalidOperationException("This fixture requires asynchronous context creation.");

        public async Task<WorkbenchDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            Paused.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
            return await inner.CreateDbContextAsync(cancellationToken);
        }
    }

    private sealed class PausingContributor : IProjectStructureProjectionContributor {
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken) {
            Paused.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class OwnerFactory(DbContextOptions<WorkbenchDbContext> options) : IDbContextFactory<WorkbenchDbContext> {
        public WorkbenchDbContext CreateDbContext() => new(options);

        public Task<WorkbenchDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class FailAfterFlush(Exception failure) : SaveChangesInterceptor {
        public int FlushedRowCount { get; private set; }
        public bool HadTransaction { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            FlushedRowCount = result;
            HadTransaction = eventData.Context?.Database.CurrentTransaction is not null;
            throw failure;
        }
    }
}
