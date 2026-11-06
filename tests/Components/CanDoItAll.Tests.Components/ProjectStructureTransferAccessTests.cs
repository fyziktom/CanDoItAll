using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Structure.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureTransferAccessTests {
    public enum Boundary { Allowed, DeniedBefore, AfterPrivateSql, AfterCreationSql, AfterCreationCommit, AfterMoveSql, AfterMoveCommit, ProjectCreationDenied, RouteAfterMoveSql, RouteAfterMoveCommit }

    [Theory]
    [InlineData(Boundary.Allowed)]
    [InlineData(Boundary.DeniedBefore)]
    [InlineData(Boundary.AfterPrivateSql)]
    [InlineData(Boundary.AfterCreationSql)]
    [InlineData(Boundary.AfterCreationCommit)]
    [InlineData(Boundary.AfterMoveSql)]
    [InlineData(Boundary.AfterMoveCommit)]
    [InlineData(Boundary.ProjectCreationDenied)]
    public async Task Actual_transfer_validates_each_native_authority_boundary_and_keeps_known_receipts(Boundary boundary) {
        var access = new TransferAccess();
        var state = new TransferProbe(access) { Boundary = boundary };
        var sql = new TransferSql(state);
        var committed = new TransferCommit(state);
        var reads = new TransferRead(state);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(sql, committed, reads), ServiceLifetime.Singleton);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(sql, committed), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        state.Source = (await projects.SaveAsync(new() { Name = "Native transfer source" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(state.Source)).ExpectedProjectAdmission!;
        var anchor = await CreateAsync(workbench, admission, "Transfer anchor", $"project:{state.Source:D}");
        var child = await CreateAsync(workbench, admission, "Transfer child", anchor.Id);
        var neighbor = await CreateAsync(workbench, admission, "Transfer neighbor", $"project:{state.Source:D}");
        state.Child = child.Id;
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{state.Source:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, state.Source));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == child.Id));
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(anchor.Id, "move-descendants-to-subproject", anchor.X, anchor.Y));
        page.WaitForElement("[data-testid='project-structure-subproject-transfer-name']");
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-subproject-transfer-name']").Input("Owned native transfer target"));
        var snapshots = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        var before = await CaptureAsync(snapshots, state.Source);
        var shown = JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes);
        state.Armed = true;
        access.Allowed = boundary != Boundary.DeniedBefore;
        access.ProjectWritesAllowed = boundary != Boundary.ProjectCreationDenied;
        await page.InvokeAsync(() => page.FindComponent<StructureStructuralDialogs>().Instance.ExecuteSubprojectTransfer.InvokeAsync());
        if (boundary is not Boundary.Allowed and not Boundary.DeniedBefore and not Boundary.ProjectCreationDenied) {
            Assert.True(state.Reached);
        }
        var outcome = page.Instance.AuthoringOutcomes.First();
        Assert.Equal(state.Source, outcome.Project.ProjectId);
        var after = await CaptureAsync(snapshots, state.Source);
        if (boundary is Boundary.Allowed or Boundary.AfterMoveCommit) {
            Assert.NotEqual(before, after);
            Assert.NotNull(outcome.Creation);
            var target = outcome.TargetProjectId!.Value;
            var accepted = await workbench.GetStructureAsync(target);
            var moved = Assert.Single(accepted.Nodes, node => node.Id == child.Id);
            Assert.Equal($"project:{target:D}", moved.ParentId);
            Assert.Equal((child.RecordId, child.Title, child.Notes, child.MetadataJson), (moved.RecordId, moved.Title, moved.Notes, moved.MetadataJson));
            var source = await workbench.GetStructureAsync(state.Source);
            Assert.DoesNotContain(source.Nodes, node => node.Id == child.Id);
            Assert.Equal(JsonSerializer.Serialize(neighbor), JsonSerializer.Serialize(source.Nodes.Single(node => node.Id == neighbor.Id)));
            if (boundary == Boundary.AfterMoveCommit) {
                Assert.Equal(ProjectStructureAuthoringResultKind.PartialCommit, outcome.Kind);
                var recovery = Assert.IsType<ProjectStructureTransferPartialCommitException>(outcome.Failure).Recovery;
                await using var context = await snapshots.CreateDbContextAsync();
                var receipt = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync(record => record.Id == recovery.DurableMutationId);
                Assert.Equal(ProjectCrossModuleMutationStatus.WorkbenchCommitted, receipt.Status);
                Assert.Equal(0, receipt.AttemptCount);
                Assert.Equal(shown, JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes));
                Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message => message.Summary == "Structure changes saved; refresh required");
            } else {
                Assert.Equal(ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
            }
            return;
        }
        Assert.Equal(before, after);
        Assert.DoesNotContain(await projects.ListAsync(), project => project.Id != state.Source);
        await using var original = await snapshots.CreateDbContextAsync();
        Assert.False(await original.Set<ProjectCrossModuleMutationRecord>().AnyAsync(record =>
            record.MutationKind == ProjectCrossModuleMutationKind.MoveDescendants || record.MutationKind == ProjectCrossModuleMutationKind.MoveSelectedNodes));
        if (boundary is Boundary.AfterCreationCommit or Boundary.AfterMoveSql) {
            Assert.Equal(ProjectStructureAuthoringResultKind.Compensated, outcome.Kind);
            Assert.NotNull(outcome.Creation);
            Assert.Equal(state.Target, outcome.Creation.Project.ProjectId);
            var compensation = await original.Set<ProjectCrossModuleMutationRecord>().SingleAsync(record =>
                record.ProjectId == state.Target && record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject);
            Assert.Equal(ProjectCrossModuleMutationStatus.Completed, compensation.Status);
        } else {
            Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, outcome.Kind);
            Assert.Null(outcome.Creation);
            Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message => message.Summary == "Structure changes not saved");
        }
    }

    [Theory]
    [InlineData(Boundary.RouteAfterMoveSql)]
    [InlineData(Boundary.RouteAfterMoveCommit)]
    public async Task Route_retirement_keeps_the_original_native_transfer_and_does_not_publish_into_the_successor(Boundary boundary) {
        var state = new TransferProbe(new TransferAccess()) { Boundary = boundary };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(new TransferSql(state)), ServiceLifetime.Singleton);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(new TransferSql(state), new TransferCommit(state)), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        state.Source = (await projects.SaveAsync(new() { Name = "Original transfer route" })).Value;
        var successor = (await projects.SaveAsync(new() { Name = "Unrelated successor route" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(state.Source)).ExpectedProjectAdmission!;
        var anchor = await CreateAsync(workbench, admission, "Original transfer anchor", $"project:{state.Source:D}");
        var child = await CreateAsync(workbench, admission, "Original transfer child", anchor.Id);
        state.Child = child.Id;
        var snapshots = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        var before = await CaptureAsync(snapshots, state.Source);
        var successorBefore = await CaptureAsync(snapshots, successor);
        var navigation = services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/projects/{state.Source:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, state.Source));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == child.Id));
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(anchor.Id, "move-descendants-to-subproject", anchor.X, anchor.Y));
        page.WaitForElement("[data-testid='project-structure-subproject-transfer-name']");
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-subproject-transfer-name']").Input("Original route target"));
        state.Armed = true;
        var pending = page.InvokeAsync(() => page.FindComponent<StructureStructuralDialogs>().Instance.ExecuteSubprojectTransfer.InvokeAsync());
        try {
            await state.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            navigation.NavigateTo($"/projects/{successor:D}/structure");
            await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, successor)));
            page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{successor:D}"));
        } finally {
            state.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(state.Reached);
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(state.Source, outcome.Project.ProjectId);
        Assert.NotNull(outcome.Creation);
        Assert.Equal(state.Target, outcome.Creation.Project.ProjectId);
        Assert.Equal(successorBefore, await CaptureAsync(snapshots, successor));
        Assert.DoesNotContain(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == anchor.Id || node.Id == child.Id);
        Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary.StartsWith("Structure", StringComparison.Ordinal));
        if (boundary == Boundary.RouteAfterMoveSql) {
            Assert.Equal(ProjectStructureAuthoringResultKind.Compensated, outcome.Kind);
            Assert.Equal(before, await CaptureAsync(snapshots, state.Source));
            Assert.DoesNotContain(await projects.ListAsync(), project => project.Id == state.Target);
        } else {
            Assert.Equal(ProjectStructureAuthoringResultKind.PartialCommit, outcome.Kind);
            var recovery = Assert.IsType<ProjectStructureTransferPartialCommitException>(outcome.Failure).Recovery;
            await using var context = await snapshots.CreateDbContextAsync();
            var receipt = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync(record => record.Id == recovery.DurableMutationId);
            Assert.Equal(ProjectCrossModuleMutationStatus.WorkbenchCommitted, receipt.Status);
            Assert.Equal(0, receipt.AttemptCount);
            var moved = Assert.Single((await workbench.GetStructureAsync(state.Target)).Nodes, node => node.Id == child.Id);
            Assert.Equal((child.RecordId, child.Title, child.Notes, child.MetadataJson), (moved.RecordId, moved.Title, moved.Notes, moved.MetadataJson));
            Assert.Equal($"project:{state.Target:D}", moved.ParentId);
            Assert.DoesNotContain((await workbench.GetStructureAsync(state.Source)).Nodes, node => node.Id == child.Id);
        }
    }

    private static Task<ProjectStructureNode> CreateAsync(ProjectWorkbenchService workbench, ProjectWriteAdmission admission, string title, string parent)
        => workbench.CreateObjectAsync(admission.ProjectId, new(ProjectObjectType.Note, title, "", "Original transfer content", parent, 100, 100) {
            ExpectedProjectAdmission = admission
        });

    private static async Task<string> CaptureAsync(IDbContextFactory<WorkbenchDbContext> snapshots, Guid project) {
        await using var context = await snapshots.CreateDbContextAsync();
        var nodes = await context.Set<ProjectObjectRecord>().AsNoTracking().Where(node => node.ProjectId == project && !node.IsSystemManaged)
            .OrderBy(node => node.NodeKey).ToListAsync();
        var links = await context.Set<ProjectObjectLinkRecord>().AsNoTracking().Where(link => link.ProjectId == project).OrderBy(link => link.Id).ToListAsync();
        return JsonSerializer.Serialize(new { Nodes = nodes, Links = links });
    }

    private sealed class TransferAccess : IEditingUiAccess {
        public bool Allowed { get; set; } = true;
        public bool ProjectWritesAllowed { get; set; } = true;

        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Allowed) {
                throw new UnauthorizedAccessException("Transfer access denied.");
            }
            return Task.FromResult(new EditingUiGrant(topic, "native-transfer-actor", "Native transfer actor"));
        }

        public ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Allowed || grant.Lifetime.IsCancellationRequested || write && !ProjectWritesAllowed && grant.Topic.Owner == EditingOwner.Project) {
                grant.Revoke();
                throw new UnauthorizedAccessException("Original transfer access retired.");
            }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TransferProbe(TransferAccess access) {
        public Boundary Boundary { get; init; }
        public Guid Source { get; set; }
        public string Child { get; set; } = string.Empty;
        public Guid Target { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task HoldAsync() {
            Armed = false;
            Reached = true;
            Entered.TrySetResult();
            await Release.Task;
        }

        public void Revoke() {
            Armed = false;
            Reached = true;
            access.Allowed = false;
        }
    }

    private sealed class TransferSql(TransferProbe state) : SaveChangesInterceptor {
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (state.Armed && result > 0 && eventData.Context is ProjectsDbContext projects &&
                    projects.ChangeTracker.Entries<Project>().FirstOrDefault(entry => entry.Entity.Id != state.Source) is { } created) {
                state.Target = created.Entity.Id;
                if (state.Boundary == Boundary.AfterCreationSql) {
                    Assert.NotNull(projects.Database.CurrentTransaction);
                    state.Revoke();
                }
            }
            if (state.Armed && result > 0 && state.Boundary is Boundary.AfterMoveSql or Boundary.RouteAfterMoveSql && eventData.Context is WorkbenchDbContext workbench &&
                    workbench.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry => entry.Entity.NodeKey == state.Child && entry.Entity.ProjectId != state.Source)) {
                Assert.NotNull(workbench.Database.CurrentTransaction);
                if (state.Boundary == Boundary.RouteAfterMoveSql) {
                    await state.HoldAsync();
                } else {
                    state.Revoke();
                }
            }
            return result;
        }
    }

    private sealed class TransferCommit(TransferProbe state) : DbTransactionInterceptor {
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (state.Armed && state.Boundary == Boundary.AfterCreationCommit && eventData.Context is ProjectsDbContext projects &&
                    projects.ChangeTracker.Entries<Project>().FirstOrDefault(entry => entry.Entity.Id != state.Source) is { } created) {
                state.Target = created.Entity.Id;
                state.Revoke();
            }
            if (state.Armed && state.Boundary is Boundary.AfterMoveCommit or Boundary.RouteAfterMoveCommit && eventData.Context is WorkbenchDbContext workbench &&
                    workbench.ChangeTracker.Entries<ProjectCrossModuleMutationRecord>().Any(entry => entry.Entity.MutationKind == ProjectCrossModuleMutationKind.MoveDescendants &&
                        entry.Entity.Status == ProjectCrossModuleMutationStatus.WorkbenchCommitted)) {
                if (state.Boundary == Boundary.RouteAfterMoveCommit) {
                    await state.HoldAsync();
                } else {
                    state.Revoke();
                }
            }
        }
    }

    private sealed class TransferRead(TransferProbe state) : DbCommandInterceptor {
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default) {
            if (state.Armed && state.Boundary == Boundary.AfterPrivateSql && eventData.Context is ProjectsDbContext &&
                    command.CommandText.Contains("Projects_Projects", StringComparison.Ordinal) && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)) {
                state.Revoke();
            }
            return ValueTask.FromResult(result);
        }
    }
}
