using System.Text;
using System.Text.Json;
using System.Data.Common;
using System.Security.Claims;
using System.Security.Cryptography;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureTextAssetLifetimeTests {
    public enum NodeChange { Kind, Parent, Record }

    [Fact]
    public async Task Independent_pages_create_exact_files_with_metadata_frozen_before_upload_await() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var first = (await projects.SaveAsync(new() { Name = "Independent first" })).Value;
        var second = (await projects.SaveAsync(new() { Name = "Independent second" })).Value;
        var firstPage = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, first));
        var secondPage = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, second));
        firstPage.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        secondPage.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var dialogs = harness.Context.Render<DialogHost>();
        var firstOpening = OpenAsync(firstPage, first, "Frozen first");
        dialogs.WaitForElement("[data-testid='project-structure-text-asset-source-upload']");
        var firstDialog = dialogs.FindComponent<ProjectStructureTextAssetCreateDialog>();
        await firstDialog.InvokeAsync(() => firstDialog.Find("[data-testid='project-structure-text-asset-source-upload']").ClickAsync(new MouseEventArgs()));
        var upload = new HeldUpload();
        await firstDialog.InvokeAsync(() => firstDialog.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([upload])));
        var firstPending = firstDialog.InvokeAsync(() => firstDialog.Find("[data-testid='project-structure-text-asset-submit']").ClickAsync(new MouseEventArgs()));
        try {
            await upload.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await firstDialog.InvokeAsync(() => {
                firstDialog.Find("[data-testid='project-structure-text-asset-title']").Input("Later title");
                firstDialog.Find("[data-testid='project-structure-text-asset-subtitle']").Input("Later context");
                firstDialog.Find("[data-testid='project-structure-text-asset-notes']").Input("Later notes");
            });
            var secondOpening = OpenAsync(secondPage, second, "Independent second asset");
            dialogs.WaitForAssertion(() => Assert.Equal(2, dialogs.FindComponents<ProjectStructureTextAssetCreateDialog>().Count));
            var secondDialog = dialogs.FindComponents<ProjectStructureTextAssetCreateDialog>().Single(component => component.Instance.CreateRequest.Title == "Independent second asset");
            await secondDialog.InvokeAsync(() => secondDialog.Find("[data-testid='project-structure-text-asset-content']").Input("Second exact body"));
            await secondDialog.InvokeAsync(() => secondDialog.Find("[data-testid='project-structure-text-asset-submit']").ClickAsync(new MouseEventArgs()));
            await secondOpening.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Single(harness.Context.Services.GetRequiredService<DialogService>().Dialogs);
        } finally {
            upload.Release.TrySetResult();
        }
        await firstPending.WaitAsync(TimeSpan.FromSeconds(20));
        await firstOpening.WaitAsync(TimeSpan.FromSeconds(20));
        var firstNode = Assert.Single((await workbench.GetStructureAsync(first)).Nodes, node => node.ObjectType == ProjectObjectType.File);
        Assert.Equal(("Frozen first", "Original context", "Original notes"), (firstNode.Title, firstNode.Subtitle, firstNode.Notes));
        var secondNode = Assert.Single((await workbench.GetStructureAsync(second)).Nodes, node => node.ObjectType == ProjectObjectType.File);
        Assert.Equal("Independent second asset", secondNode.Title);
        Assert.NotEqual(firstNode.RecordId, secondNode.RecordId);
        Assert.NotEqual(firstNode.StorageObjectReferenceJson, secondNode.StorageObjectReferenceJson);
        Assert.Equal(first, Assert.Single(firstPage.Instance.AuthoringOutcomes).Project.ProjectId);
        Assert.Equal(second, Assert.Single(secondPage.Instance.AuthoringOutcomes).Project.ProjectId);
        await AssertBytesAsync(harness, first, firstNode, upload.Content);
        await AssertBytesAsync(harness, second, secondNode, Encoding.UTF8.GetBytes("Second exact body"));
    }

    [Fact]
    public async Task Actual_native_create_then_followup_failure_retains_saved_identity_without_retry() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Known native partial commit" })).Value;
        var admission = (await projects.GetAsync(project)).ExpectedProjectAdmission;
        var coordinator = harness.Context.Services.GetRequiredService<ProjectStructureTextAssetCreationCoordinator>();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition("add-file-text", out var definition));
        ProjectStructureNode? accepted = null;
        var creation = new ProjectStructureTextAssetCreationContext(project, async (_, request, media, cancellationToken) => {
            accepted = await workbench.CreateObjectAsync(project, new(ProjectObjectType.File, request.Title, request.Subtitle,
                request.Notes, $"project:{project:D}", ObjectSubtype: "text", Media: media) { ExpectedProjectAdmission = admission }, cancellationToken);
            throw new ProjectStructureNodeCreatedWithFollowUpFailureException(accepted, new IOException("Synthetic observation failure after actual native acceptance."));
        });
        var dialogs = harness.Context.Render<DialogHost>();
        var opening = dialogs.InvokeAsync(() => coordinator.CreateAsync(creation, definition,
            new(definition.ActionId, $"project:{project:D}", 0, 0, $"project:{project:D}", "Saved partial", "", "Descriptive notes", "child", "dialog", "text", null)));
        dialogs.WaitForElement("[data-testid='project-structure-text-asset-content']");
        await dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-content']").Input("Exact saved partial bytes"));
        await dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-submit']").ClickAsync(new MouseEventArgs()));
        await opening.WaitAsync(TimeSpan.FromSeconds(20));
        var stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.ObjectType == ProjectObjectType.File);
        Assert.NotNull(accepted);
        Assert.Equal((accepted.Id, accepted.RecordId, accepted.StorageObjectReferenceJson), (stored.Id, stored.RecordId, stored.StorageObjectReferenceJson));
        Assert.Empty(harness.Context.Services.GetRequiredService<DialogService>().Dialogs);
        await AssertBytesAsync(harness, project, stored, Encoding.UTF8.GetBytes("Exact saved partial bytes"));
    }

    [Theory]
    [InlineData(NodeChange.Kind)]
    [InlineData(NodeChange.Parent)]
    [InlineData(NodeChange.Record)]
    public async Task Original_parent_occurrence_must_still_match_at_native_dispatch(NodeChange change) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Original parent owner" })).Value;
        var parent = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original parent", "", "Original", $"project:{project:D}"));
        var neighbor = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Neighbor", "", "Untouched", $"project:{project:D}"));
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var dialogs = harness.Context.Render<DialogHost>();
        var opening = OpenAsync(page, project, "Stale child", parent.Id);
        dialogs.WaitForElement("[data-testid='project-structure-text-asset-content']");
        await dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-content']").Input("Must not be saved"));
        if (change == NodeChange.Kind) {
            await workbench.ReclassifyObjectAsync(project, parent.Id, new(ProjectObjectType.ProjectBlock, "decision", "Changed kind", "", "Original"));
        } else if (change == NodeChange.Parent) {
            await workbench.ReparentObjectAsync(project, parent.Id, neighbor.Id);
        } else {
            await using var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            var record = await db.Set<ProjectObjectRecord>().SingleAsync(node => node.ProjectId == project && node.NodeKey == parent.Id);
            db.Remove(record);
            await db.SaveChangesAsync();
            db.Add(new ProjectObjectRecord { Id = Guid.NewGuid(), ProjectId = project, NodeKey = parent.Id,
                ParentNodeKey = parent.ParentId, ObjectType = parent.ObjectType, ObjectSubtype = parent.ObjectSubtype,
                Title = parent.Title, Notes = parent.Notes, Status = parent.Status, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        await dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-submit']").ClickAsync(new MouseEventArgs()));
        var actual = await workbench.GetStructureAsync(project);
        Assert.DoesNotContain(actual.Nodes, node => node.Title == "Stale child");
        Assert.Equal(neighbor.Notes, actual.Nodes.Single(node => node.Id == neighbor.Id).Notes);
        Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, Assert.Single(page.Instance.AuthoringOutcomes).Kind);
        await CloseAsync(harness, dialogs);
        await opening.WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_actor_or_A_B_A_route_cannot_reactivate_the_original_upload(bool changeActor) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var first = (await projects.SaveAsync(new() { Name = "A" })).Value;
        var second = (await projects.SaveAsync(new() { Name = "B" })).Value;
        var actor = Actor("First");
        var host = harness.Context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(component => component.Value, actor).AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, first)));
        var page = host.FindComponent<ProjectStructurePage>();
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var dialogs = harness.Context.Render<DialogHost>();
        var opening = OpenAsync(page, first, "Retired upload");
        dialogs.WaitForElement("[data-testid='project-structure-text-asset-source-upload']");
        await dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-source-upload']").ClickAsync(new MouseEventArgs()));
        var upload = new HeldUpload();
        await dialogs.InvokeAsync(() => dialogs.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([upload])));
        var pending = dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-submit']").ClickAsync(new MouseEventArgs()));
        try {
            await upload.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await host.InvokeAsync(() => host.Render(parameters => parameters.Add(component => component.Value, changeActor ? Actor("Second") : actor)
                .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, changeActor ? first : second))));
            if (!changeActor) {
                page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{second:D}"));
                await host.InvokeAsync(() => host.Render(parameters => parameters.Add(component => component.Value, actor)
                    .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, first))));
            }
        } finally {
            upload.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.DoesNotContain((await workbench.GetStructureAsync(first)).Nodes, node => node.ObjectType == ProjectObjectType.File);
        Assert.DoesNotContain((await workbench.GetStructureAsync(second)).Nodes, node => node.ObjectType == ProjectObjectType.File);
        await CloseAsync(harness, dialogs);
        await opening.WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_native_file_survives_retirement_without_closing_successor_or_replaying(bool loseReply) {
        var gate = new TextCommitGate(loseReply);
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => new PooledDbContextFactory<WorkbenchDbContext>(
                new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>()).AddInterceptors(gate).Options)));
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var first = (await projects.SaveAsync(new() { Name = "Accepted file owner" })).Value;
        var second = (await projects.SaveAsync(new() { Name = "Successor file owner" })).Value;
        var neighbor = await workbench.CreateObjectAsync(first, new(ProjectObjectType.Note, "Neighbor", "", "Unchanged", $"project:{first:D}"));
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, first));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var dialogs = harness.Context.Render<DialogHost>();
        var original = OpenAsync(page, first, "Accepted original");
        dialogs.WaitForElement("[data-testid='project-structure-text-asset-content']");
        const string body = "Exact native bytes \u03c0\nsecond line";
        await dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-content']").Input(body));
        gate.Project = first;
        var pending = dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-submit']").ClickAsync(new MouseEventArgs()));
        Task? successor = null;
        ProjectStructureNode committed;
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            committed = Assert.Single((await workbench.GetStructureAsync(first)).Nodes, node => node.ObjectType == ProjectObjectType.File);
            await CloseAsync(harness, dialogs);
            await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, second)));
            page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{second:D}"));
            successor = OpenAsync(page, second, "Successor draft");
            dialogs.WaitForElement("[data-testid='project-structure-text-asset-title']");
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        await original.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal("Successor draft", dialogs.Find("[data-testid='project-structure-text-asset-title']").GetAttribute("value"));
        var stored = Assert.Single((await workbench.GetStructureAsync(first)).Nodes, node => node.ObjectType == ProjectObjectType.File);
        Assert.Equal((committed.Id, committed.RecordId, committed.StorageObjectReferenceJson), (stored.Id, stored.RecordId, stored.StorageObjectReferenceJson));
        Assert.Equal(neighbor.Notes, (await workbench.GetStructureAsync(first)).Nodes.Single(node => node.Id == neighbor.Id).Notes);
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(loseReply ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
        Assert.Equal(first, outcome.Project.ProjectId);
        if (!loseReply) {
            Assert.Equal(committed.Id, outcome.Node!.Id);
        }
        var coordinator = harness.Context.Services.GetRequiredService<ProjectStructureKnownFileInteractionCoordinator>();
        await using var interaction = await coordinator.OpenAsync(first, stored.Id);
        await using var content = await interaction.Session.ContentSource.OpenReadAsync(new FileContentReadRequest(interaction.Session.File));
        using var bytes = new MemoryStream();
        await content.Stream.CopyToAsync(bytes);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(body)), SHA256.HashData(bytes.ToArray()));
        Assert.DoesNotContain((await workbench.GetStructureAsync(second)).Nodes, node => node.ObjectType == ProjectObjectType.File);
        Assert.DoesNotContain(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == committed.Id);
        await CloseAsync(harness, dialogs);
        await successor!.WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task Upload_opened_for_original_project_cannot_write_into_same_public_id_replacement() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Original text owner" })).Value;
        var originalAdmission = (await projects.GetAsync(project)).ExpectedProjectAdmission;
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var dialogs = harness.Context.Render<DialogHost>();
        var request = new CanvasWorkbenchCreateActionRequest("add-file-text", $"project:{project:D}", 0, 0,
            $"project:{project:D}", "Original upload", "Original context", "Original notes", "child", "dialog", "text", null);
        var opening = page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnCreateAction(JsonSerializer.Serialize(request)));
        dialogs.WaitForElement("[data-testid='project-structure-text-asset-source-upload']");
        await dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-source-upload']").ClickAsync(new MouseEventArgs()));
        var upload = new HeldUpload();
        await dialogs.InvokeAsync(() => dialogs.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([upload])));
        var pending = dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-text-asset-submit']").ClickAsync(new MouseEventArgs()));
        try {
            await upload.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await projects.DeleteAsync(project, expectedProjectAdmission: originalAdmission);
            Assert.True((await projects.CreateAsync(project, new() { Name = "Replacement text owner" })).IsSuccess);
            var replacement = (await projects.GetAsync(project)).ExpectedProjectAdmission;
            Assert.NotEqual(originalAdmission, replacement);
            await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, project)));
            page.WaitForAssertion(() => Assert.Equal("Replacement text owner", page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes
                .Single(node => node.Id == $"project:{project:D}").Title));
        } finally {
            upload.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var actual = await workbench.GetStructureAsync(project);
        Assert.DoesNotContain(actual.Nodes, node => node.Title == "Original upload");
        Assert.Equal("Replacement text owner", actual.Nodes.Single(node => node.Id == $"project:{project:D}").Title);
        foreach (var dialog in harness.Context.Services.GetRequiredService<DialogService>().Dialogs.ToArray()) {
            await dialogs.InvokeAsync(() => dialog.CloseAsync());
        }
        await opening.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static Task<AuthenticationState> Actor(string name)
        => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Fixture"))));

    private static async Task AssertBytesAsync(ComponentTestHarness harness, Guid project, ProjectStructureNode node, byte[] expected) {
        await using var interaction = await harness.Context.Services.GetRequiredService<ProjectStructureKnownFileInteractionCoordinator>().OpenAsync(project, node.Id);
        await using var content = await interaction.Session.ContentSource.OpenReadAsync(new FileContentReadRequest(interaction.Session.File));
        using var bytes = new MemoryStream();
        await content.Stream.CopyToAsync(bytes);
        Assert.Equal(SHA256.HashData(expected), SHA256.HashData(bytes.ToArray()));
    }

    private static Task OpenAsync(IRenderedComponent<ProjectStructurePage> page, Guid project, string title, string? parent = null) {
        var request = new CanvasWorkbenchCreateActionRequest("add-file-text", parent ?? $"project:{project:D}", 0, 0,
            parent ?? $"project:{project:D}", title, "Original context", "Original notes", "child", "dialog", "text", null);
        return page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnCreateAction(JsonSerializer.Serialize(request)));
    }

    private static async Task CloseAsync(ComponentTestHarness harness, IRenderedComponent<DialogHost> dialogs) {
        foreach (var dialog in harness.Context.Services.GetRequiredService<DialogService>().Dialogs.ToArray()) {
            await dialogs.InvokeAsync(() => dialog.CloseAsync());
        }
    }

    private sealed class TextCommitGate(bool loseReply) : SaveChangesInterceptor, IDbTransactionInterceptor {
        private DbContext? context;
        public Guid? Project { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Project is { } project && eventData.Context?.ChangeTracker.Entries<ProjectObjectRecord>()
                .Any(entry => entry.State == EntityState.Added && entry.Entity.ProjectId == project && entry.Entity.ObjectType == ProjectObjectType.File) is true) {
                context = eventData.Context;
                Project = null;
            }
            return ValueTask.FromResult(result);
        }

        public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (context is not null && ReferenceEquals(context, eventData.Context)) {
                context = null;
                Entered.TrySetResult();
                await Release.Task;
                if (loseReply) {
                    throw new IOException("Synthetic lost acknowledgement after native text commit.");
                }
            }
        }
    }

    private sealed class HeldUpload : IBrowserFile {
        private readonly byte[] content = Encoding.UTF8.GetBytes("Original upload bytes must never reach the replacement lifetime.");
        public byte[] Content => content;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Name => "original.txt";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => content.Length;
        public string ContentType => "text/plain";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) {
            Assert.True(content.Length <= maxAllowedSize);
            return new HeldUploadStream(content, Entered, Release);
        }
    }

    private sealed class HeldUploadStream(byte[] content, TaskCompletionSource entered, TaskCompletionSource release) : MemoryStream(content, writable: false) {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            await base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }
}
