using System.Data.Common;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructurePageActionLifetimeTests {
    public enum SummaryAction { Status, Workbook, Gantt }

    [Theory]
    [InlineData(SummaryAction.Status)]
    [InlineData(SummaryAction.Workbook)]
    [InlineData(SummaryAction.Gantt)]
    public async Task Summary_actions_reject_the_recreated_project_and_reused_node(SummaryAction action) {
        await using var harness = await CreateHarnessAsync();
        var target = await CreateTargetAsync(harness, "Original summary");
        var cut = Render(harness, target.ProjectId);
        await ContextActionAsync(cut, target.Node.Id, "summary");
        await RecreateProjectAsync(harness, target);

        if (action == SummaryAction.Status) {
            await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSupportDialogs>().Instance
                .ChangeSummaryStatus.InvokeAsync((target.Node.Id, "Done")));
        } else {
            await Button(cut, action == SummaryAction.Workbook ? "Export XLSX" : "Export Gantt").ClickAsync(new MouseEventArgs());
        }

        var replacement = await Workbench(harness).GetStructureAsync(target.ProjectId);
        Assert.Equal("Open", Assert.Single(replacement.Nodes, node => node.Id == target.Node.Id).Status);
        Assert.DoesNotContain(replacement.Nodes, node => node.ObjectType == ProjectObjectType.File);
        cut.WaitForAssertion(() => Assert.Contains(target.Admission.LifetimeId.ToString("D"), cut.Markup, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Summary_export_keeps_its_original_parent_and_leaves_a_new_dialog_open(bool gantt) {
        var gate = new MutationGate();
        var generator = new TextGeneratorGate();
        await using var harness = await CreateHarnessAsync(gate, generator: generator);
        var original = await CreateTargetAsync(harness, "First export");
        var next = await CreateTargetAsync(harness, "Second export");
        var cut = Render(harness, original.ProjectId);
        await ContextActionAsync(cut, original.Node.Id, "summary");
        if (gantt) {
            generator.Armed = true;
        } else {
            gate.Arm(original.ProjectId, ProjectObjectType.File);
        }
        var pending = Button(cut, gantt ? "Export Gantt" : "Export XLSX").ClickAsync(new MouseEventArgs());
        try {
            await (gantt ? generator.Entered.Task : gate.Entered.Task).WaitAsync(TimeSpan.FromSeconds(20));
            Navigate(harness, cut, next.ProjectId);
            await ContextActionAsync(cut, next.Node.Id, "summary");
        } finally {
            generator.Release.TrySetResult();
            gate.Release.TrySetResult();
        }
        await pending;

        var created = Assert.Single((await Workbench(harness).GetStructureAsync(original.ProjectId)).Nodes,
            node => node.ObjectType == ProjectObjectType.File);
        Assert.Equal(original.Node.Id, created.ParentId);
        Assert.Equal($"{original.Node.Title} {(gantt ? "gantt" : "progress workbook")}", created.Title);
        Assert.DoesNotContain((await Workbench(harness).GetStructureAsync(next.ProjectId)).Nodes, node => node.ObjectType == ProjectObjectType.File);
        cut.WaitForAssertion(() => Assert.Equal(next.Node.Title, cut.FindComponent<ProjectStructureSupportDialogs>().Instance.SummaryDialog!.RootTitle));
        AssertNavigation(harness, next.ProjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Canvas_capture_keeps_original_selection_and_denies_a_recreated_target(bool recreate) {
        await using var harness = await CreateHarnessAsync();
        var original = await CreateTargetAsync(harness, "Original image");
        var next = await CreateTargetAsync(harness, "New selection");
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.update", _ => true).SetResult(true);
        var cut = Render(harness, original.ProjectId);
        await SelectAsync(cut, original.Node.Id);
        var capture = harness.Context.JSInterop.Setup<string?>("CanDoItAll.canvasWorkbench.exportImageData", _ => true);
        var pending = cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSelectionPanel>().Instance.ExecuteInspectorActionAsync.InvokeAsync("export-image"));
        try {
            cut.WaitForAssertion(() => Assert.Single(capture.Invocations));
            if (recreate) {
                await RecreateProjectAsync(harness, original);
            }
            Navigate(harness, cut, next.ProjectId);
            await SelectAsync(cut, next.Node.Id);
        } finally {
            capture.SetResult("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO5+Rf0AAAAASUVORK5CYII=");
        }
        await pending;

        var images = (await Workbench(harness).GetStructureAsync(original.ProjectId)).Nodes.Where(node => node.ObjectType == ProjectObjectType.ImageAsset).ToList();
        if (recreate) {
            Assert.Empty(images);
            Assert.Contains(original.Admission.LifetimeId.ToString("D"), cut.Markup, StringComparison.Ordinal);
        } else {
            var image = Assert.Single(images);
            Assert.Equal(original.Node.Id, image.ParentId);
            Assert.Equal($"{original.Node.Title} mindmap image", image.Title);
            Assert.Equal("original-image-node-mindmap.png", image.MediaOriginalFileName);
        }
        Assert.DoesNotContain((await Workbench(harness).GetStructureAsync(next.ProjectId)).Nodes, node => node.ObjectType == ProjectObjectType.ImageAsset);
        AssertNavigation(harness, next.ProjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transcript_link_uses_original_admission_after_the_scaffold_commit(bool recreate) {
        var gate = new MutationGate();
        await using var harness = await CreateHarnessAsync(gate);
        var original = await CreateTargetAsync(harness, "Recording", ProjectObjectType.Recording);
        var next = await CreateTargetAsync(harness, "Next page");
        var cut = Render(harness, original.ProjectId);
        gate.Arm(original.ProjectId, ProjectObjectType.Transcript, afterCommit: true);
        var pending = ContextActionAsync(cut, original.Node.Id, "transcript:create");
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await using (var database = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
                var scaffold = Assert.Single(await database.Set<ProjectObjectRecord>().AsNoTracking()
                    .Where(node => node.ProjectId == original.ProjectId && node.ObjectType == ProjectObjectType.Transcript).ToListAsync());
                Assert.Equal(original.Node.Id, scaffold.ParentNodeKey);
                Assert.False(await database.Set<ProjectObjectLinkRecord>().AnyAsync(link =>
                    link.ProjectId == original.ProjectId && link.LinkKind == ProjectObjectLinkKind.DerivedFrom));
            }
            if (recreate) {
                await RecreateProjectAsync(harness, original).WaitAsync(TimeSpan.FromSeconds(20));
            }
            Navigate(harness, cut, next.ProjectId);
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));

        var first = await Workbench(harness).GetStructureAsync(original.ProjectId);
        if (recreate) {
            Assert.DoesNotContain(first.Links, link => link.Kind == ProjectObjectLinkKind.DerivedFrom);
            cut.WaitForAssertion(() => {
                Assert.Contains("was saved", cut.Markup, StringComparison.Ordinal);
                Assert.Contains(original.Admission.LifetimeId.ToString("D"), cut.Markup, StringComparison.Ordinal);
            });
        } else {
            var transcript = Assert.Single(first.Nodes, node => node.ObjectType == ProjectObjectType.Transcript);
            Assert.Equal(original.Node.Id, transcript.ParentId);
            Assert.Contains(first.Links, link => link.SourceId == original.Node.Id && link.TargetId == transcript.Id && link.Kind == ProjectObjectLinkKind.DerivedFrom);
        }
        Assert.DoesNotContain((await Workbench(harness).GetStructureAsync(next.ProjectId)).Nodes, node => node.ObjectType == ProjectObjectType.Transcript);
        AssertNavigation(harness, next.ProjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transcript_provider_result_keeps_original_action_and_lifetime(bool recreate) {
        var provider = new ProviderGate();
        await using var harness = await CreateHarnessAsync(provider: provider);
        var original = await CreateTargetAsync(harness, "Original transcript", ProjectObjectType.Transcript);
        var next = await CreateTargetAsync(harness, "Next transcript", ProjectObjectType.Transcript);
        var cut = Render(harness, original.ProjectId);
        await ContextActionAsync(cut, original.Node.Id, "transcript:summarize");
        var pending = Button(cut, "Send request").ClickAsync(new MouseEventArgs());
        try {
            await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (recreate) {
                await RecreateProjectAsync(harness, original);
            }
            Navigate(harness, cut, next.ProjectId);
            await ContextActionAsync(cut, next.Node.Id, "transcript:find-my-tasks");
        } finally {
            provider.Release.TrySetResult();
        }
        await pending;

        Assert.Equal(1, provider.Calls);
        Assert.Contains(original.Node.Title, provider.Request!.Prompt, StringComparison.Ordinal);
        var stored = Assert.Single((await Workbench(harness).GetStructureAsync(original.ProjectId)).Nodes, node => node.Id == original.Node.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(stored.MetadataJson);
        if (recreate) {
            Assert.True(string.IsNullOrWhiteSpace(metadata.Transcript?.SummaryText));
            Assert.Contains("provider request completed", cut.Markup, StringComparison.OrdinalIgnoreCase);
        } else {
            Assert.Equal("Original provider answer", metadata.Transcript?.SummaryText);
            Assert.Equal(ProjectLlmActionKind.Summarize, metadata.Transcript?.LastActionKind);
        }
        var nextStored = Assert.Single((await Workbench(harness).GetStructureAsync(next.ProjectId)).Nodes, node => node.Id == next.Node.Id);
        Assert.True(string.IsNullOrWhiteSpace(ProjectObjectMetadataSerializer.Parse(nextStored.MetadataJson).Transcript?.SummaryText));
        cut.WaitForAssertion(() => Assert.Equal(ProjectLlmActionKind.FindMyTasks,
            cut.FindComponent<ProjectStructureSupportDialogs>().Instance.PendingTranscriptAction!.ActionKind));
        AssertNavigation(harness, next.ProjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secret_creation_keeps_original_form_and_reference_after_await(bool recreate) {
        var vault = new VaultGate();
        await using var harness = await CreateHarnessAsync(vault: vault);
        var original = await CreateTargetAsync(harness, "Original secret project");
        var next = await CreateTargetAsync(harness, "Next secret project");
        var cut = Render(harness, original.ProjectId);
        await OpenSecretCreateAsync(cut, original.Node.Id);
        cut.Find("[data-testid='project-structure-secret-purpose']").Input("Original purpose");
        cut.Find("[data-testid='project-structure-secret-external-reference']").Input("Original note");
        cut.Find("[data-testid='project-structure-secret-create-name']").Change("Original UI secret");
        cut.Find("input[data-testid='project-structure-secret-create-value']").Change("synthetic-component-secret");
        vault.Armed = true;
        var pending = cut.Find("[data-testid='project-structure-secret-create-use']").ClickAsync(new MouseEventArgs());
        try {
            await vault.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (recreate) {
                await RecreateProjectAsync(harness, original);
            }
            Navigate(harness, cut, next.ProjectId);
            await OpenSecretCreateAsync(cut, next.Node.Id);
            cut.Find("[data-testid='project-structure-secret-purpose']").Input("New purpose");
            cut.Find("[data-testid='project-structure-secret-create-name']").Change("New unfinished secret");
        } finally {
            vault.Release.TrySetResult();
        }
        await pending;

        await using var security = await harness.Context.Services.GetRequiredService<IDbContextFactory<SecurityDbContext>>().CreateDbContextAsync();
        var secret = Assert.Single(await security.Set<SecretRecord>().AsNoTracking().Where(row => row.Name == "Original UI secret").ToListAsync());
        Assert.NotEqual(Guid.Empty, secret.Id);
        Assert.False(await security.Set<SecretRecord>().AnyAsync(row => row.Name == "New unfinished secret"));
        var references = (await Workbench(harness).GetStructureAsync(original.ProjectId)).Nodes.Where(node => node.ObjectType == ProjectObjectType.SecretReference).ToList();
        if (recreate) {
            Assert.Empty(references);
            Assert.Contains($"Secret {secret.Id:D} was created", cut.Markup, StringComparison.Ordinal);
        } else {
            var reference = Assert.Single(references);
            var metadata = ProjectObjectMetadataSerializer.Parse(reference.MetadataJson).SecretReference!;
            Assert.Equal(secret.Id, metadata.SecretId);
            Assert.Equal("Original purpose", metadata.Purpose);
            Assert.Equal("Original note", metadata.ExternalReference);
            Assert.Equal(original.Node.Id, reference.ParentId);
        }
        Assert.DoesNotContain((await Workbench(harness).GetStructureAsync(next.ProjectId)).Nodes, node => node.ObjectType == ProjectObjectType.SecretReference);
        cut.WaitForAssertion(() => {
            Assert.Equal("New purpose", cut.Find("[data-testid='project-structure-secret-purpose']").GetAttribute("value"));
            Assert.Equal("New unfinished secret", cut.Find("[data-testid='project-structure-secret-create-name']").GetAttribute("value"));
            Assert.False(cut.Find("[data-testid='project-structure-secret-create-use']").HasAttribute("disabled"));
        });
        AssertNavigation(harness, next.ProjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secret_reference_edit_preserves_original_node_or_denies_its_recreated_lifetime(bool recreate) {
        var gate = new MutationGate();
        await using var harness = await CreateHarnessAsync(gate);
        var secretResult = await harness.Context.Services.GetRequiredService<SecretService>().SaveAsync(new() {
            Name = "Selected fixture secret", SecretValue = "synthetic-selection-value"
        });
        Assert.True(secretResult.IsSuccess);
        var original = await CreateTargetAsync(harness, "Reference", ProjectObjectType.SecretReference);
        var next = await CreateTargetAsync(harness, "Other reference project");
        var cut = Render(harness, original.ProjectId);
        await SelectAsync(cut, original.Node.Id);
        await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSelectionPanel>().Instance.ExecuteInspectorActionAsync.InvokeAsync("edit"));
        cut.WaitForElement("[data-testid='project-structure-secret-dialog']");
        cut.Find("[data-testid='project-structure-secret-select']").Change(secretResult.Value.ToString("D"));
        cut.Find("[data-testid='project-structure-secret-purpose']").Input("Original edit purpose");
        if (recreate) {
            await RecreateProjectAsync(harness, original);
        } else {
            gate.Arm(original.ProjectId, ProjectObjectType.SecretReference);
        }
        var pending = cut.Find("[data-testid='project-structure-secret-use-selected']").ClickAsync(new MouseEventArgs());
        if (!recreate) {
            try {
                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
                Navigate(harness, cut, next.ProjectId);
                await OpenSecretCreateAsync(cut, next.Node.Id);
            } finally {
                gate.Release.TrySetResult();
            }
        }
        await pending;
        var stored = Assert.Single((await Workbench(harness).GetStructureAsync(original.ProjectId)).Nodes, node => node.Id == original.Node.Id);
        if (recreate) {
            Assert.Equal(original.Node.Title, stored.Title);
            Assert.Contains(original.Admission.LifetimeId.ToString("D"), cut.Markup, StringComparison.Ordinal);
        } else {
            Assert.Equal(secretResult.Value, ProjectObjectMetadataSerializer.Parse(stored.MetadataJson).SecretReference!.SecretId);
            Assert.Equal("Original edit purpose", stored.Notes);
            cut.WaitForElement("[data-testid='project-structure-secret-create-use']");
            AssertNavigation(harness, next.ProjectId);
        }
    }

    [Fact]
    public async Task Late_secret_picker_read_cannot_reopen_a_closed_newer_dialog() {
        var read = new SecretReadGate();
        await using var harness = await CreateHarnessAsync(read: read);
        var original = await CreateTargetAsync(harness, "Old picker");
        var next = await CreateTargetAsync(harness, "New picker");
        var cut = Render(harness, original.ProjectId);
        read.Armed = true;
        var pending = ContextActionAsync(cut, original.Node.Id, "add-secret-reference");
        try {
            await read.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Navigate(harness, cut, next.ProjectId);
            await OpenSecretCreateAsync(cut, next.Node.Id);
            var close = Assert.Single(cut.FindComponents<CanDoItAll.Components.BaseLib.Button>(),
                button => button.Instance.Text == "Close" && button.Instance.Icon == "close");
            await close.Find("button").ClickAsync(new MouseEventArgs());
            Assert.Empty(cut.FindAll("[data-testid='project-structure-secret-dialog']"));
        } finally {
            read.Release.TrySetResult();
        }
        await pending;
        Assert.Empty(cut.FindAll("[data-testid='project-structure-secret-dialog']"));
        AssertNavigation(harness, next.ProjectId);
    }

    private static Task<ComponentTestHarness> CreateHarnessAsync(MutationGate? mutation = null, TextGeneratorGate? generator = null,
        ProviderGate? provider = null, VaultGate? vault = null, SecretReadGate? read = null)
        => ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<ISecretVault>(vault is null ? new InMemorySecretVault() : vault));
            if (mutation is not null) {
                services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(serviceProvider => new PooledDbContextFactory<WorkbenchDbContext>(
                    new DbContextOptionsBuilder<WorkbenchDbContext>(serviceProvider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>())
                        .AddInterceptors(mutation).Options));
                services.Replace(ServiceDescriptor.Scoped<ProjectWorkbenchRelationService>(serviceProvider => new(
                    new GatedWorkbenchFactory(serviceProvider.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>(), mutation),
                    serviceProvider.GetRequiredService<ProjectStructureMutationScopeFactory>(),
                    serviceProvider.GetRequiredService<IClock>(),
                    serviceProvider.GetRequiredService<ProjectStructureAssemblyService>())));
            }
            if (read is not null) {
                services.AddSingleton<IDbContextFactory<SecurityDbContext>>(serviceProvider => new PooledDbContextFactory<SecurityDbContext>(
                    new DbContextOptionsBuilder<SecurityDbContext>(serviceProvider.GetRequiredService<DbContextOptions<SecurityDbContext>>())
                        .AddInterceptors(read).Options));
            }
            if (generator is not null) {
                services.Replace(ServiceDescriptor.Singleton(new ProjectAssetCreationService(new ProjectAssetContentGeneratorResolver([generator]))));
            }
            if (provider is not null) {
                services.Replace(ServiceDescriptor.Singleton<IProviderRuntimeProfileSource>(provider));
                services.Replace(ServiceDescriptor.Singleton<IProviderRuntimeProfileSnapshotSource>(provider));
                services.Replace(ServiceDescriptor.Singleton<IProviderPromptExecutionService>(provider));
            }
        });

    private static ProjectWorkbenchService Workbench(ComponentTestHarness harness)
        => harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

    private sealed record Target(Guid ProjectId, ProjectWriteAdmission Admission, ProjectStructureNode Node);

    private static async Task<Target> CreateTargetAsync(ComponentTestHarness harness, string name, ProjectObjectType type = ProjectObjectType.Note) {
        var result = await harness.Context.Services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = name });
        Assert.True(result.IsSuccess);
        Guid? secretId = null;
        if (type == ProjectObjectType.SecretReference) {
            var secret = await harness.Context.Services.GetRequiredService<SecretService>().SaveAsync(new() {
                Name = $"{name} initial secret", SecretValue = "synthetic-initial-reference-value"
            });
            Assert.True(secret.IsSuccess);
            secretId = secret.Value;
        }
        var metadata = type switch {
            ProjectObjectType.Transcript => new ProjectObjectMetadataEnvelope { Transcript = new() { TranscriptText = $"Transcript for {name}" } },
            ProjectObjectType.SecretReference => new ProjectObjectMetadataEnvelope { SecretReference = new() { SecretId = secretId, SecretNameSnapshot = $"{name} initial secret", Purpose = "Initial purpose" } },
            _ => new ProjectObjectMetadataEnvelope()
        };
        var node = await Workbench(harness).CreateObjectAsync(result.Value,
            new ProjectObjectCreateRequest(type, $"{name} node", "Fixture", "Original notes", $"project:{result.Value}",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(metadata), Status: "Open"));
        var surface = await Workbench(harness).GetStructureAsync(result.Value);
        return new(result.Value, Assert.IsType<ProjectWriteAdmission>(surface.ExpectedProjectAdmission), node);
    }

    private static async Task RecreateProjectAsync(ComponentTestHarness harness, Target original) {
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        await projects.DeleteAsync(original.ProjectId, expectedProjectAdmission: original.Admission);
        Assert.True((await projects.CreateAsync(original.ProjectId, new() { Name = "Unrelated replacement" })).IsSuccess);
        await using var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        db.Set<ProjectObjectRecord>().Add(new() {
            Id = Guid.Parse(original.Node.Id["custom:".Length..]),
            NodeKey = original.Node.Id,
            ProjectId = original.ProjectId,
            ObjectType = original.Node.ObjectType,
            Title = original.Node.Title,
            Notes = "Replacement notes",
            Status = "Open",
            MetadataJson = original.Node.MetadataJson,
            ParentNodeKey = $"project:{original.ProjectId}",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var replacement = await Workbench(harness).GetStructureAsync(original.ProjectId);
        Assert.Equal(original.ProjectId, replacement.ProjectId);
        Assert.NotEqual(original.Admission.LifetimeId, replacement.ExpectedProjectAdmission!.LifetimeId);
    }

    private static IRenderedComponent<ProjectStructurePage> Render(ComponentTestHarness harness, Guid projectId) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{projectId:D}/structure");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Contains(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{projectId}"));
        return cut;
    }

    private static void Navigate(ComponentTestHarness harness, IRenderedComponent<ProjectStructurePage> cut, Guid projectId) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{projectId:D}/structure");
        cut.Render(parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Contains(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{projectId}"));
    }

    private static void AssertNavigation(ComponentTestHarness harness, Guid projectId)
        => Assert.EndsWith($"/projects/{projectId:D}/structure", harness.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);

    private static Task ContextActionAsync(IRenderedComponent<ProjectStructurePage> cut, string nodeId, string action)
        => cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(nodeId, action, 0, 0));

    private static Task SelectAsync(IRenderedComponent<ProjectStructurePage> cut, string nodeId)
        => cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(nodeId, JsonSerializer.Serialize(new[] { nodeId })));

    private static IElement Button(IRenderedComponent<ProjectStructurePage> cut, string text)
        => cut.FindAll(".project-structure-summary-dialog button").Single(button => button.TextContent.Trim() == text);

    private static async Task OpenSecretCreateAsync(IRenderedComponent<ProjectStructurePage> cut, string parent) {
        await ContextActionAsync(cut, parent, "add-secret-reference");
        cut.WaitForElement("[data-testid='project-structure-secret-dialog']");
    }

    private sealed class MutationGate : SaveChangesInterceptor, IDbTransactionInterceptor {
        private Guid? projectId;
        private ProjectObjectType objectType;
        private bool afterCommit;
        private DbContext? matchedContext;
        private int pauseNextContext;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm(Guid project, ProjectObjectType type, bool afterCommit = false) {
            projectId = project;
            objectType = type;
            this.afterCommit = afterCommit;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (projectId is { } project && eventData.Context?.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry =>
                    entry.State is EntityState.Added or EntityState.Modified && entry.Entity.ProjectId == project && entry.Entity.ObjectType == objectType) is true) {
                projectId = null;
                matchedContext = eventData.Context;
                if (!afterCommit) {
                    Entered.TrySetResult();
                    await Release.Task.WaitAsync(cancellationToken);
                }
            }
            return result;
        }

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (afterCommit && ReferenceEquals(eventData.Context, matchedContext)) {
                matchedContext = null;
                Interlocked.Exchange(ref pauseNextContext, 1);
            }
            return Task.CompletedTask;
        }

        public async Task BeforeContextCreationAsync(CancellationToken cancellationToken) {
            if (Interlocked.Exchange(ref pauseNextContext, 0) == 1) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class GatedWorkbenchFactory(IDbContextFactory<WorkbenchDbContext> inner, MutationGate gate)
        : IDbContextFactory<WorkbenchDbContext> {
        public WorkbenchDbContext CreateDbContext() => inner.CreateDbContext();

        public async Task<WorkbenchDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            await gate.BeforeContextCreationAsync(cancellationToken);
            return await inner.CreateDbContextAsync(cancellationToken);
        }
    }

    private sealed class TextGeneratorGate : IProjectAssetContentGenerator {
        public ProjectAssetGenerationKind GenerationKind => ProjectAssetGenerationKind.Text;
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<ProjectAssetContent> GenerateAsync(ProjectAssetContentGenerationRequest request, CancellationToken cancellationToken = default) {
            if (Armed) {
                Armed = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return await new ProjectTextAssetContentGenerator().GenerateAsync(request, cancellationToken);
        }
    }

    private sealed class ProviderGate : IProviderRuntimeProfileSource, IProviderRuntimeProfileSnapshotSource, IProviderPromptExecutionService {
        private readonly ProviderProfile profile = new(Guid.NewGuid(), "Fixture provider", ProviderKind.OpenAi,
            "https://provider.invalid", string.Empty, "fixture", ProviderTransportKind.Responses, true,
            false, false, false, false, "{}", string.Empty, "Ready", null, []);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ProviderPromptExecutionRequest? Request { get; private set; }
        public int Calls { get; private set; }
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([profile]);
        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProviderProfile?>(providerId == profile.Id ? profile : null);
        public ProviderRuntimeProfileSnapshotLease? CaptureProvider(Guid providerId, SandboxWorkspaceCatalogSnapshot catalogSnapshot) {
            ArgumentNullException.ThrowIfNull(catalogSnapshot);
            return providerId == profile.Id
                ? new(profile, ProviderConfigurationFingerprintFactory.Create(profile))
                : null;
        }
        public async Task<Result<ProviderPromptExecutionResponse>> ExecuteAsync(ProviderPromptExecutionRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            Request = request;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Result<ProviderPromptExecutionResponse>.Success(new(profile.Name, "fixture", "Original provider answer", "Markdown", false));
        }
    }

    private sealed class VaultGate : ISecretVault {
        private readonly InMemorySecretVault inner = new();
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SetAsync(string key, string value, CancellationToken ct = default) {
            await inner.SetAsync(key, value, ct);
            if (Armed) {
                Armed = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(ct);
            }
        }
        public Task<string?> GetAsync(string key, CancellationToken ct = default) => inner.GetAsync(key, ct);
        public Task DeleteAsync(string key, CancellationToken ct = default) => inner.DeleteAsync(key, ct);
    }

    private sealed class SecretReadGate : DbCommandInterceptor {
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is SecurityDbContext && command.CommandText.Contains("Security_SecretRecords", StringComparison.Ordinal)) {
                Armed = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
