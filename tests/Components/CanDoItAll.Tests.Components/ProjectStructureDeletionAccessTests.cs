using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureDeletionAccessTests {
    public enum Command { Leaf, Branch, MissingBranchReplay, CleanupRetry }
    public enum AccessCondition { Allowed, DeniedBefore, DeniedAfterSql, DeniedAfterReplayCommit }

    public static IEnumerable<object[]> Commands => Enum.GetValues<Command>()
        .SelectMany(command => Enum.GetValues<AccessCondition>()
            .Where(condition => condition != AccessCondition.DeniedAfterReplayCommit || command == Command.MissingBranchReplay)
            .Select(condition => new object[] { command, condition }));

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task Native_deletion_and_exact_cleanup_require_current_access(Command command, AccessCondition condition) {
        var access = new EditingUiTestAccess();
        var deletionSql = new RevokeAfterDeletionSql(access);
        var replaySql = new RevokeAfterReplayRead(access);
        var replayCommit = new RevokeAfterReplayCommit(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(deletionSql, replaySql, replayCommit), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Native deletion authority" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var source = await CreateNoteAsync(workbench, admission, "Delete source", $"project:{project:D}");
        var neighbor = await CreateNoteAsync(workbench, admission, "Untouched neighbor", $"project:{project:D}");
        ProjectStructureNode? child = command == Command.Branch
            ? await CreateNoteAsync(workbench, admission, "Delete child", source.Id) : null;
        var snapshots = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        Guid? mutationId = command == Command.CleanupRetry
            ? await SeedPendingCleanupAsync(services, admission, source.Id) : null;
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == neighbor.Id));
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        if (command == Command.MissingBranchReplay) {
            mutationId = await SeedPendingCleanupAsync(services, admission, source.Id);
        }
        if (command == Command.Branch) {
            await page.InvokeAsync(() => canvas.OnContextAction(source.Id, "delete", source.X, source.Y));
            page.WaitForElement("[data-testid='content-delete-files']");
        }
        if (command == Command.CleanupRetry) {
            await page.InvokeAsync(() => page.Find("[data-testid='project-structure-inspector-toggle']").ClickAsync(new MouseEventArgs()));
            page.WaitForElement("[data-testid='project-structure-deletion-retry']");
        }
        var before = await CaptureAsync(snapshots, project);
        deletionSql.Project = project;
        deletionSql.Armed = condition == AccessCondition.DeniedAfterSql && command is Command.Leaf or Command.Branch;
        replaySql.Mutation = mutationId;
        replaySql.Armed = condition == AccessCondition.DeniedAfterSql && mutationId.HasValue;
        replayCommit.Armed = condition == AccessCondition.DeniedAfterReplayCommit;
        access.Allowed = condition != AccessCondition.DeniedBefore;
        await page.InvokeAsync(() => command switch {
            Command.Branch => page.Find("[data-testid='content-delete-files']").ClickAsync(new MouseEventArgs()),
            Command.CleanupRetry => page.Find("[data-testid='project-structure-deletion-retry']").ClickAsync(new MouseEventArgs()),
            _ => canvas.OnContextAction(source.Id, "delete", source.X, source.Y)
        });
        var after = await CaptureAsync(snapshots, project);
        if (condition is not AccessCondition.Allowed and not AccessCondition.DeniedAfterReplayCommit) {
            Assert.Equal(condition == AccessCondition.DeniedAfterSql, deletionSql.Reached || replaySql.Reached);
            Assert.Equal(before, after);
            Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message =>
                message.Summary == "Structure changes not saved" && message.Severity == NotificationSeverity.Warning);
            return;
        }
        Assert.NotEqual(before, after);
        var accepted = await workbench.GetStructureAsync(project);
        Assert.DoesNotContain(accepted.Nodes, node => node.Id == source.Id || node.Id == child?.Id);
        Assert.Equal(JsonSerializer.Serialize(neighbor), JsonSerializer.Serialize(accepted.Nodes.Single(node => node.Id == neighbor.Id)));
        await using var context = await snapshots.CreateDbContextAsync();
        var receipt = await context.Set<ProjectCrossModuleMutationRecord>().AsNoTracking()
            .SingleAsync(record => record.ProjectId == project && record.ScopeNodeKey == source.Id);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, receipt.Status);
        if (mutationId.HasValue) {
            Assert.Equal(mutationId.Value, receipt.Id);
        }
        var payload = JsonSerializer.Deserialize<DeleteSubtreeMutationPayload>(receipt.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Contains(source.Id, payload.DeletedNodeKeys);
        Assert.Equal(ProjectAssignmentReference.From(admission), payload.SourceReference);
        if (condition == AccessCondition.DeniedAfterReplayCommit) {
            Assert.True(replayCommit.Reached);
            Assert.Equal(1, receipt.AttemptCount);
            Assert.Contains(page.Instance.DeletionOutcomes, outcome => outcome.Recovery?.CompletedNodeCount == 1 || outcome.Result?.DeletedNodeCount == 1);
            Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message => message.Summary == "Structure changes saved; refresh required");
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Actual_SQL_and_physical_commit_keep_deletion_receipts_bound_to_the_original_view(bool afterCommit, bool retireView) {
        var access = new EditingUiTestAccess();
        var sql = new RevokeAfterDeletionSql(access) { Hold = true };
        var commit = new HoldDeletionCommit();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(sql, commit), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var project = (await projects.SaveAsync(new() { Name = "Original deletion lifetime" })).Value;
        var next = (await projects.SaveAsync(new() { Name = "Successor deletion lifetime" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var source = await CreateNoteAsync(workbench, admission, "Original held deletion", $"project:{project:D}");
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == source.Id));
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        var retained = JsonSerializer.Serialize(canvas.Surface.Nodes);
        var snapshots = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        var before = await CaptureAsync(snapshots, project);
        sql.Project = commit.Project = project;
        sql.Armed = !afterCommit;
        commit.Armed = afterCommit;
        var pending = page.InvokeAsync(() => canvas.OnContextAction(source.Id, "delete", source.X, source.Y));
        try {
            await (afterCommit ? commit.Entered.Task : sql.Entered.Task).WaitAsync(TimeSpan.FromSeconds(20));
            await using var context = await snapshots.CreateDbContextAsync();
            Assert.Equal(!afterCommit, await context.Set<ProjectObjectRecord>().AnyAsync(node => node.ProjectId == project && node.NodeKey == source.Id));
            if (retireView) {
                services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{next:D}/structure");
                await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, next)));
                page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{next:D}"));
            } else {
                access.Allowed = false;
            }
        } finally {
            sql.Release.TrySetResult();
            commit.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        if (afterCommit) {
            await using var context = await snapshots.CreateDbContextAsync();
            Assert.False(await context.Set<ProjectObjectRecord>().AnyAsync(node => node.ProjectId == project && node.NodeKey == source.Id));
            var receipt = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync(record => record.ProjectId == project);
            Assert.Equal(ProjectCrossModuleMutationStatus.WorkbenchCommitted, receipt.Status);
            Assert.Equal(0, receipt.AttemptCount);
            Assert.Contains(page.Instance.DeletionOutcomes, outcome => outcome.Recovery?.Recoveries.Any(recovery =>
                recovery.DurableMutationId == receipt.Id && recovery.ProjectId == project) == true);
        } else {
            Assert.Equal(before, await CaptureAsync(snapshots, project));
            Assert.Contains(page.Instance.DeletionOutcomes, outcome => outcome.Failure is UnauthorizedAccessException);
        }
        if (retireView) {
            Assert.DoesNotContain((await workbench.GetStructureAsync(next)).Nodes, node => !node.IsSystemManaged);
            Assert.DoesNotContain(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == source.Id);
            Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages, message => message.Summary.StartsWith("Structure", StringComparison.Ordinal));
            Assert.DoesNotContain("original deletion", page.Markup, StringComparison.OrdinalIgnoreCase);
        } else {
            Assert.Equal(retained, JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes));
            Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message => message.Severity == NotificationSeverity.Warning &&
                message.Summary == (afterCommit ? "Structure cleanup pending" : "Structure changes not saved"));
        }
    }

    [Fact]
    public async Task Grant_loss_during_assignment_cleanup_retains_real_managed_bytes_and_exact_retry_finishes_once() {
        var access = new EditingUiTestAccess();
        var cleanup = new RevokeDuringAssignmentCleanup(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(cleanup), ServiceLifetime.Singleton);
            services.ConfigureDbContext<CrmHrDbContext>(options => options.AddInterceptors(cleanup), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Managed deletion authority" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var asset = await workbench.CreateObjectAsync(project, new(ProjectObjectType.ImageAsset, "Managed deletion image", "", "Owned deletion bytes",
            $"project:{project:D}", Media: new ProjectObjectMediaPayload("pixel.png", "image/png",
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=")) {
            ExpectedProjectAdmission = admission
        });
        var path = Path.Combine(services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var bytes = await File.ReadAllBytesAsync(path);
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == asset.Id));
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(asset.Id, "delete", asset.X, asset.Y));
        page.WaitForElement("[data-testid='content-delete-files']");
        cleanup.Armed = true;
        await page.InvokeAsync(() => page.Find("[data-testid='content-delete-files']").ClickAsync(new MouseEventArgs()));
        Assert.True(cleanup.Claimed);
        Assert.True(cleanup.Reached);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        var snapshots = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        Guid durableId;
        await using (var context = await snapshots.CreateDbContextAsync()) {
            Assert.False(await context.Set<ProjectObjectRecord>().AnyAsync(node => node.ProjectId == project && node.NodeKey == asset.Id));
            var receipt = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync(record => record.ProjectId == project);
            durableId = receipt.Id;
            Assert.Equal(ProjectCrossModuleMutationStatus.Failed, receipt.Status);
            Assert.Equal(1, receipt.AttemptCount);
            var payload = JsonSerializer.Deserialize<DeleteSubtreeMutationPayload>(receipt.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Single(payload.ManagedStorageCandidates!);
            Assert.Contains(page.Instance.DeletionOutcomes, outcome => outcome.Recovery?.Recoveries.Any(recovery => recovery.DurableMutationId == durableId) == true);
        }
        access.Allowed = true;
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-inspector-toggle']").ClickAsync(new MouseEventArgs()));
        page.WaitForElement("[data-testid='project-structure-deletion-retry']");
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-deletion-retry']").ClickAsync(new MouseEventArgs()));
        Assert.False(File.Exists(path));
        await using var completed = await snapshots.CreateDbContextAsync();
        var terminal = await completed.Set<ProjectCrossModuleMutationRecord>().SingleAsync(record => record.ProjectId == project);
        Assert.Equal(durableId, terminal.Id);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, terminal.Status);
        Assert.Equal(2, terminal.AttemptCount);
        Assert.DoesNotContain(page.FindAll("button"), button => button.TextContent.Contains("Retry cleanup", StringComparison.Ordinal));
    }

    private static Task<ProjectStructureNode> CreateNoteAsync(ProjectWorkbenchService workbench, ProjectWriteAdmission admission,
        string title, string parent) => workbench.CreateObjectAsync(admission.ProjectId,
            new(ProjectObjectType.Note, title, "", "Retained native deletion payload", parent, 100, 100) {
                ExpectedProjectAdmission = admission
            });

    private static async Task<Guid> SeedPendingCleanupAsync(IServiceProvider services, ProjectWriteAdmission admission, string nodeKey) {
        await using var context = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        await using var scope = await services.GetRequiredService<ProjectStructureMutationScopeFactory>().BeginBindingWriteAsync(
            context, ProjectStructureSerializableMutationScope.ForProject(admission.ProjectId), CancellationToken.None, [admission]);
        var source = await context.Set<ProjectObjectRecord>().SingleAsync(node => node.ProjectId == admission.ProjectId && node.NodeKey == nodeKey);
        var coordinator = services.GetRequiredService<ProjectCrossModuleMutationCoordinator>();
        var mutation = coordinator.Begin(admission.ProjectId, nodeKey, ProjectCrossModuleMutationKind.DeleteSubtree,
            JsonSerializer.Serialize(new DeleteSubtreeMutationPayload(nodeKey, [nodeKey], 0,
                SourceReference: ProjectAssignmentReference.From(admission))));
        coordinator.MarkWorkbenchCommitted(mutation);
        context.Remove(source);
        context.Add(mutation);
        await context.SaveChangesAsync();
        await scope.CommitAsync(CancellationToken.None);
        return mutation.Id;
    }

    private static async Task<string> CaptureAsync(IDbContextFactory<WorkbenchDbContext> snapshots, Guid project) {
        await using var context = await snapshots.CreateDbContextAsync();
        var nodes = await context.Set<ProjectObjectRecord>().AsNoTracking()
            .Where(node => node.ProjectId == project && !node.IsSystemManaged).OrderBy(node => node.NodeKey).ToListAsync();
        var links = await context.Set<ProjectObjectLinkRecord>().AsNoTracking()
            .Where(link => link.ProjectId == project).OrderBy(link => link.Id).ToListAsync();
        var receipts = await context.Set<ProjectCrossModuleMutationRecord>().AsNoTracking()
            .Where(record => record.ProjectId == project).OrderBy(record => record.Id).ToListAsync();
        return JsonSerializer.Serialize(new { Nodes = nodes, Links = links, Receipts = receipts });
    }

    private sealed class RevokeAfterDeletionSql(EditingUiTestAccess access) : SaveChangesInterceptor {
        private readonly HashSet<DbContext> deleting = [];
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public bool Hold { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectObjectRecord>()
                    .Any(entry => entry.Entity.ProjectId == Project && entry.State == EntityState.Deleted)) {
                deleting.Add(context);
            }
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context && deleting.Remove(context)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Armed = false;
                Reached = true;
                if (Hold) {
                    Entered.TrySetResult();
                    await Release.Task.WaitAsync(cancellationToken);
                } else {
                    access.Allowed = false;
                }
            }
            return result;
        }
    }

    private sealed class HoldDeletionCommit : DbTransactionInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectCrossModuleMutationRecord>()
                    .Any(entry => entry.Entity.ProjectId == Project && entry.Entity.Status == ProjectCrossModuleMutationStatus.WorkbenchCommitted)) {
                Armed = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class RevokeAfterReplayRead(EditingUiTestAccess access) : DbCommandInterceptor {
        public Guid? Mutation { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is WorkbenchDbContext context && context.Database.CurrentTransaction is not null &&
                    command.CommandText.Contains("Workbench_ProjectCrossModuleMutations", StringComparison.Ordinal) &&
                    command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)) {
                Armed = false;
                Reached = true;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RevokeAfterReplayCommit(EditingUiTestAccess access) : DbCommandInterceptor {
        public bool Armed { get; set; }
        public bool Reached { get; private set; }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result == 1 && eventData.Context is WorkbenchDbContext context &&
                    command.CommandText.Contains("Workbench_ProjectCrossModuleMutations", StringComparison.Ordinal) &&
                    command.CommandText.Contains($"\"{nameof(ProjectCrossModuleMutationRecord.CompletedAtUtc)}\" =", StringComparison.Ordinal)) {
                Assert.Null(context.Database.CurrentTransaction);
                Armed = false;
                Reached = true;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RevokeDuringAssignmentCleanup(EditingUiTestAccess access) : DbCommandInterceptor {
        public bool Armed { get; set; }
        public bool Claimed { get; private set; }
        public bool Reached { get; private set; }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result == 1 && eventData.Context is WorkbenchDbContext &&
                    command.CommandText.Contains("Workbench_ProjectCrossModuleMutations", StringComparison.Ordinal) &&
                    command.CommandText.Contains($"\"{nameof(ProjectCrossModuleMutationRecord.AttemptCount)}\" =", StringComparison.Ordinal)) {
                Claimed = true;
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (Armed && Claimed && eventData.Context is CrmHrDbContext &&
                    command.CommandText.Contains("CrmHr_ProjectPartyAssignments", StringComparison.Ordinal)) {
                Armed = false;
                Reached = true;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }
}
