using System.Data.Common;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Content.UI.Analysis;
using CanDoItAll.Workbench.Insights.UI;
using CanDoItAll.Editing;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePageActionLifetimeTests {
    public enum ContentCommand { SummaryStatus, Workbook, Gantt, CanvasImage, TranscriptScaffold, TranscriptAnalysis }
    public enum ContentBoundary { Allowed, DeniedBefore, AfterNativeSql, AfterNativeCommit }
    public enum ContentStatusCommand { Metadata, Media, MetadataOnly }

    [Theory]
    [InlineData(ContentStatusCommand.Metadata)]
    [InlineData(ContentStatusCommand.Media)]
    [InlineData(ContentStatusCommand.MetadataOnly)]
    public async Task Native_content_checks_original_status_when_it_writes_status_and_preserves_independent_status_otherwise(ContentStatusCommand command) {
        await using var harness = await CreateHarnessAsync();
        var target = await CreateTargetAsync(harness, "Original content status", ProjectObjectType.Transcript);
        var workbench = Workbench(harness);
        var original = command == ContentStatusCommand.Media
            ? await workbench.CreateObjectAsync(target.ProjectId, new(ProjectObjectType.File, "Original status file", "", "Original file notes",
                target.Node.Id, ObjectSubtype: "text", Media: new("original-status.txt", "text/plain", Convert.ToBase64String(Encoding.UTF8.GetBytes("Original file bytes")))) {
                    ExpectedProjectAdmission = target.Admission
                })
            : target.Node;
        Assert.Equal(1, await workbench.UpdateObjectStatusesAsync(target.ProjectId, [original.Id], "Done",
            expectedProjectAdmission: target.Admission, expectedStates: ProjectStructureNodeStateGuard.CaptureMany([original], target.Admission)));
        var peer = Assert.Single((await workbench.GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == original.Id);
        var before = await CaptureContentRowsAsync(harness, target.ProjectId);
        var metadata = ProjectObjectMetadataSerializer.Parse(original.MetadataJson);
        if (metadata.Transcript is { } transcript) {
            transcript.SummaryText = "Owned metadata result";
        }
        var replacement = ProjectObjectMetadataSerializer.SerializePreservingUnknownProperties(original.MetadataJson, metadata);
        if (command == ContentStatusCommand.MetadataOnly) {
            var accepted = await workbench.UpdateContentMetadataAsync(target.Admission, original, replacement);
            Assert.NotNull(accepted);
            Assert.Equal((peer.Status, peer.ProgressMode, peer.ProgressPercent), (accepted.Status, accepted.ProgressMode, accepted.ProgressPercent));
            Assert.Equal("Owned metadata result", ProjectObjectMetadataSerializer.Parse(accepted.MetadataJson).Transcript?.SummaryText);
        } else {
            Func<Task> submit = command == ContentStatusCommand.Metadata
                ? () => workbench.UpdateContentMetadataAsync(target.Admission, original, replacement, "Review")
                : () => workbench.ReplaceContentMediaAsync(target.Admission, original,
                    new("replacement-status.txt", "text/plain", Convert.ToBase64String(Encoding.UTF8.GetBytes("Replacement file bytes"))), replacement, "Review");
            await Assert.ThrowsAsync<ProjectStructureEditConflictException>(submit);
            Assert.Equal(before, await CaptureContentRowsAsync(harness, target.ProjectId));
        }
    }

    [Fact]
    public async Task Immediate_native_create_retains_its_known_node_when_postcommit_read_access_is_lost() {
        var access = new EditingUiTestAccess();
        var probe = new ContentAuthorityProbe(access) { Boundary = ContentBoundary.AfterNativeCommit, Type = ProjectObjectType.Note };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var target = await CreateTargetAsync(harness, "Immediate create authority");
        var page = Render(harness, target.ProjectId);
        var shown = JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes);
        var request = new CanvasWorkbenchCreateActionRequest("add-note", target.Node.Id, 0, 0, target.Node.Id,
            "Known immediate creation", "Original instant create context", "Original instant create notes", "child", "dialog", "", null);
        probe.Project = target.ProjectId;
        probe.Armed = true;
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnCreateAction(JsonSerializer.Serialize(request)));
        Assert.True(probe.Reached);
        var created = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Title == request.Title);
        Assert.Equal(target.Node.Id, created.ParentId);
        Assert.Equal(shown, JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes));
        var outcome = page.Instance.AuthoringOutcomes.Last();
        Assert.Equal(target.Admission, outcome.Project);
        Assert.Equal(created.RecordId, outcome.Node?.RecordId);
        Assert.Equal(ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
        Assert.NotNull(outcome.Failure);
        Assert.Contains(harness.Context.Services.GetRequiredService<CanDoItAll.Components.BaseLib.NotificationService>().Messages,
            message => message.Summary == "Structure changes saved; refresh required");
        Assert.DoesNotContain(harness.Context.Services.GetRequiredService<CanDoItAll.Components.BaseLib.NotificationService>().Messages,
            message => message.Summary == "Node could not be created");
    }

    [Theory]
    [InlineData(ContentBoundary.Allowed)]
    [InlineData(ContentBoundary.DeniedBefore)]
    [InlineData(ContentBoundary.AfterNativeSql)]
    [InlineData(ContentBoundary.AfterNativeCommit)]
    public async Task Shared_native_create_keeps_original_access_at_commit_and_publication(ContentBoundary boundary) {
        var access = new EditingUiTestAccess();
        var probe = new ContentAuthorityProbe(access) { Boundary = boundary, Type = ProjectObjectType.Note };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.update", _ => true).SetResult(true);
        var target = await CreateTargetAsync(harness, "Generic create authority");
        probe.Project = target.ProjectId;
        var page = Render(harness, target.ProjectId);
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        var request = new CanvasWorkbenchCreateActionRequest("add-note", target.Node.Id, 0, 0, target.Node.Id,
            "Guarded generic creation", "Original create context", "Original generic create notes", "child", "dialog", "", null) {
                ComposerOpeningId = Guid.NewGuid(), ComposerSubmissionId = Guid.NewGuid()
            };
        await page.InvokeAsync(() => canvas.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(request.ComposerOpeningId.Value, request, null))));
        var before = await CaptureContentRowsAsync(harness, target.ProjectId);
        var shown = JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes);
        access.Allowed = boundary != ContentBoundary.DeniedBefore;
        probe.Armed = true;
        await page.InvokeAsync(() => canvas.OnCreateAction(JsonSerializer.Serialize(request)));
        if (boundary is ContentBoundary.AfterNativeSql or ContentBoundary.AfterNativeCommit) {
            Assert.True(probe.Reached);
        }
        var after = await CaptureContentRowsAsync(harness, target.ProjectId);
        var outcome = page.Instance.AuthoringOutcomes.Last();
        Assert.Equal(target.Admission, outcome.Project);
        if (boundary is ContentBoundary.DeniedBefore or ContentBoundary.AfterNativeSql) {
            Assert.Equal(before, after);
            Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, outcome.Kind);
            Assert.Null(outcome.Node);
        } else {
            var accepted = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Title == request.Title);
            Assert.Equal(target.Node.Id, accepted.ParentId);
            Assert.Equal(request.Notes, accepted.Notes);
            Assert.Equal(accepted.RecordId, outcome.Node?.RecordId);
            if (boundary == ContentBoundary.AfterNativeCommit) {
                Assert.Equal(shown, JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes));
                Assert.NotNull(outcome.Failure);
                access.Allowed = true;
                await page.InvokeAsync(() => canvas.OnCreateAction(JsonSerializer.Serialize(request with { ComposerSubmissionId = Guid.NewGuid() })));
                Assert.Equal(after, await CaptureContentRowsAsync(harness, target.ProjectId));
            }
        }
    }

    public static IEnumerable<object[]> ContentAuthorityCases()
        => from command in Enum.GetValues<ContentCommand>()
           from boundary in Enum.GetValues<ContentBoundary>()
           select new object[] { command, boundary };

    [Theory]
    [MemberData(nameof(ContentAuthorityCases))]
    public async Task Content_authoring_enforces_current_access_at_native_boundaries(ContentCommand command, ContentBoundary boundary) {
        var access = new EditingUiTestAccess();
        var probe = new ContentAuthorityProbe(access) { Boundary = boundary };
        var provider = new ProviderGate();
        provider.Release.SetResult();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
            services.Replace(ServiceDescriptor.Singleton<IProviderRuntimeProfileSource>(provider));
            services.Replace(ServiceDescriptor.Singleton<IProviderRuntimeProfileSnapshotSource>(provider));
            services.Replace(ServiceDescriptor.Singleton<IProviderPromptExecutionService>(provider));
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.update", _ => true).SetResult(true);
        harness.Context.JSInterop.Setup<string?>("CanDoItAll.canvasWorkbench.exportImageData", _ => true)
            .SetResult("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO5+Rf0AAAAASUVORK5CYII=");
        var type = command switch {
            ContentCommand.TranscriptScaffold => ProjectObjectType.Recording,
            ContentCommand.TranscriptAnalysis => ProjectObjectType.Transcript,
            _ => ProjectObjectType.Note
        };
        var target = await CreateTargetAsync(harness, "Native content authority", type);
        var cut = Render(harness, target.ProjectId);
        if (command is ContentCommand.SummaryStatus or ContentCommand.Workbook or ContentCommand.Gantt) {
            await ContextActionAsync(cut, target.Node.Id, "summary");
        } else if (command == ContentCommand.TranscriptAnalysis) {
            await ContextActionAsync(cut, target.Node.Id, "transcript:summarize");
            cut.WaitForAssertion(() => Assert.Equal(ContentConfirmationPhase.Ready, cut.FindComponent<ContentTranscriptDialog>().Instance.State.Phase));
        } else {
            await SelectAsync(cut, target.Node.Id);
        }
        probe.Project = target.ProjectId;
        probe.Type = command switch {
            ContentCommand.Workbook or ContentCommand.Gantt => ProjectObjectType.File,
            ContentCommand.CanvasImage => ProjectObjectType.ImageAsset,
            ContentCommand.TranscriptScaffold or ContentCommand.TranscriptAnalysis => ProjectObjectType.Transcript,
            _ => ProjectObjectType.Note
        };
        var before = await CaptureContentRowsAsync(harness, target.ProjectId);
        var shown = JsonSerializer.Serialize(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes);
        probe.Armed = true;
        access.Allowed = boundary != ContentBoundary.DeniedBefore;
        switch (command) {
            case ContentCommand.SummaryStatus:
                await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSupportDialogs>().Instance.ChangeSummaryStatus.InvokeAsync((target.Node.Id, "Done")));
                break;
            case ContentCommand.Workbook:
                await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSupportDialogs>().Instance.ExportSummaryWorkbook.InvokeAsync());
                break;
            case ContentCommand.Gantt:
                await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSupportDialogs>().Instance.ExportSummaryGantt.InvokeAsync());
                break;
            case ContentCommand.CanvasImage:
                await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Inspector("export-image"));
                break;
            case ContentCommand.TranscriptScaffold:
                await ContextActionAsync(cut, target.Node.Id, "transcript:create");
                break;
            case ContentCommand.TranscriptAnalysis:
                await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSupportDialogs>().Instance.ExecuteTranscriptAction.InvokeAsync());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
        if (boundary is ContentBoundary.AfterNativeSql or ContentBoundary.AfterNativeCommit) {
            Assert.True(probe.Reached);
        }
        var after = await CaptureContentRowsAsync(harness, target.ProjectId);
        var outcome = cut.Instance.AuthoringOutcomes.Last();
        Assert.Equal(target.Admission, outcome.Project);
        if (boundary is ContentBoundary.DeniedBefore or ContentBoundary.AfterNativeSql) {
            Assert.Equal(before, after);
            Assert.Equal(command == ContentCommand.TranscriptAnalysis && boundary == ContentBoundary.AfterNativeSql
                ? ProjectStructureAuthoringResultKind.PartialCommit : ProjectStructureAuthoringResultKind.Rejected, outcome.Kind);
        } else {
            Assert.NotEqual(before, after);
            Assert.Equal(command == ContentCommand.TranscriptScaffold && boundary == ContentBoundary.AfterNativeCommit
                ? ProjectStructureAuthoringResultKind.PartialCommit : ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
            if (boundary == ContentBoundary.AfterNativeCommit) {
                Assert.Equal(shown, JsonSerializer.Serialize(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes));
                Assert.NotNull(outcome.Failure);
            }
        }
        Assert.Equal(command == ContentCommand.TranscriptAnalysis && boundary != ContentBoundary.DeniedBefore ? 1 : 0, provider.Calls);
        if (command == ContentCommand.TranscriptScaffold && boundary == ContentBoundary.AfterNativeCommit) {
            Assert.NotNull(outcome.Node);
            Assert.Null(outcome.Link);
            Assert.Equal(ProjectObjectType.Transcript, Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == outcome.Node.Id).ObjectType);
        }
        if (command == ContentCommand.TranscriptAnalysis && boundary != ContentBoundary.DeniedBefore) {
            Assert.Equal(ProjectStructureExternalEffectState.Completed, outcome.ExternalEffect);
            Assert.Equal("Original provider answer", outcome.TranscriptResponse?.OutputText);
            await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSupportDialogs>().Instance.ExecuteTranscriptAction.InvokeAsync());
            Assert.Equal(1, provider.Calls);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Summary_export_retired_after_actual_SQL_rolls_back_and_keeps_the_successor_dialog(bool gantt) {
        var gate = new ContentWriteHold(afterCommit: false);
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(gate), ServiceLifetime.Singleton));
        var original = await CreateTargetAsync(harness, "Uncommitted summary export");
        var next = await CreateTargetAsync(harness, "Successor summary export");
        var before = await CaptureContentRowsAsync(harness, original.ProjectId);
        var nextBefore = await CaptureContentRowsAsync(harness, next.ProjectId);
        var cut = Render(harness, original.ProjectId);
        await ContextActionAsync(cut, original.Node.Id, "summary");
        gate.Project = original.ProjectId;
        gate.Armed = true;
        var pending = cut.InvokeAsync(() => Button(cut, gantt ? "Export Gantt" : "Export XLSX").ClickAsync(new MouseEventArgs()));
        Guid successorOpening;
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Navigate(harness, cut, next.ProjectId);
            await ContextActionAsync(cut, next.Node.Id, "summary");
            successorOpening = cut.FindComponent<ContentProgressSummaryDialog>().Instance.State.OpeningId;
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(before, await CaptureContentRowsAsync(harness, original.ProjectId));
        Assert.Equal(nextBefore, await CaptureContentRowsAsync(harness, next.ProjectId));
        Assert.Equal(successorOpening, cut.FindComponent<ContentProgressSummaryDialog>().Instance.State.OpeningId);
        Assert.Empty(cut.FindComponent<ContentProgressSummaryDialog>().Instance.State.Message);
        var outcome = Assert.Single(cut.Instance.AuthoringOutcomes);
        Assert.Equal(original.Admission, outcome.Project);
        Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, outcome.Kind);
        Assert.Null(outcome.Node);
        Assert.DoesNotContain(harness.Context.Services.GetRequiredService<CanDoItAll.Components.BaseLib.NotificationService>().Messages,
            message => message.Summary.StartsWith("Structure", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Canvas_capture_keeps_its_original_parent_after_selection_changes_within_the_same_project() {
        await using var harness = await CreateHarnessAsync();
        var original = await CreateTargetAsync(harness, "Same Project capture");
        var neighbor = await Workbench(harness).CreateObjectAsync(original.ProjectId,
            new(ProjectObjectType.Note, "Unrelated selected neighbor", "", "Original neighbor notes", $"project:{original.ProjectId:D}"));
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.update", _ => true).SetResult(true);
        var cut = Render(harness, original.ProjectId);
        await SelectAsync(cut, original.Node.Id);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = harness.Context.JSInterop.Setup<string?>("CanDoItAll.canvasWorkbench.exportImageData", _ => {
            entered.TrySetResult();
            return true;
        });
        var pending = InvokeSelectionAsync(cut, new InsightsSelectionCommand.Inspector("export-image"));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await SelectAsync(cut, neighbor.Id);
        } finally {
            capture.SetResult("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO5+Rf0AAAAASUVORK5CYII=");
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var stored = await Workbench(harness).GetStructureAsync(original.ProjectId);
        var exported = Assert.Single(stored.Nodes, node => node.ObjectType == ProjectObjectType.ImageAsset);
        Assert.Equal(original.Node.Id, exported.ParentId);
        Assert.Equal(JsonSerializer.Serialize(neighbor), JsonSerializer.Serialize(stored.Nodes.Single(node => node.Id == neighbor.Id)));
        Assert.Equal(ProjectStructureAuthoringResultKind.Committed, Assert.Single(cut.Instance.AuthoringOutcomes).Kind);
    }

    [Fact]
    public async Task Gantt_export_retired_during_local_generation_does_not_create_a_file_or_change_the_successor() {
        var generator = new TextGeneratorGate();
        await using var harness = await CreateHarnessAsync(generator: generator);
        var original = await CreateTargetAsync(harness, "Original generated summary");
        var next = await CreateTargetAsync(harness, "New generated summary");
        var before = await CaptureContentRowsAsync(harness, original.ProjectId);
        var nextBefore = await CaptureContentRowsAsync(harness, next.ProjectId);
        var cut = Render(harness, original.ProjectId);
        await ContextActionAsync(cut, original.Node.Id, "summary");
        generator.Armed = true;
        var pending = cut.InvokeAsync(() => Button(cut, "Export Gantt").ClickAsync(new MouseEventArgs()));
        Guid successorOpening;
        try {
            await generator.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Navigate(harness, cut, next.ProjectId);
            await ContextActionAsync(cut, next.Node.Id, "summary");
            successorOpening = cut.FindComponent<ContentProgressSummaryDialog>().Instance.State.OpeningId;
        } finally {
            generator.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(before, await CaptureContentRowsAsync(harness, original.ProjectId));
        Assert.Equal(nextBefore, await CaptureContentRowsAsync(harness, next.ProjectId));
        Assert.Equal(successorOpening, cut.FindComponent<ContentProgressSummaryDialog>().Instance.State.OpeningId);
        Assert.Empty(cut.FindComponent<ContentProgressSummaryDialog>().Instance.State.Message);
        Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, Assert.Single(cut.Instance.AuthoringOutcomes).Kind);
    }

    private sealed class ContentWriteHold(bool afterCommit) : SaveChangesInterceptor, IDbTransactionInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private DbContext? matchedContext;

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectObjectRecord>()
                    .Any(entry => entry.Entity.ProjectId == Project && entry.Entity.ObjectType == ProjectObjectType.File)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                matchedContext = context;
                if (!afterCommit) {
                    await HoldAsync(cancellationToken);
                }
            }
            return result;
        }

        public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (Armed && afterCommit && ReferenceEquals(eventData.Context, matchedContext)) {
                await HoldAsync(CancellationToken.None);
            }
        }

        private async Task HoldAsync(CancellationToken cancellationToken) {
            Armed = false;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private static async Task<string> CaptureContentRowsAsync(ComponentTestHarness harness, Guid project) {
        await using var context = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var nodes = await context.Set<ProjectObjectRecord>().AsNoTracking().Where(node => node.ProjectId == project && !node.IsSystemManaged)
            .OrderBy(node => node.NodeKey).ToListAsync();
        var links = await context.Set<ProjectObjectLinkRecord>().AsNoTracking().Where(link => link.ProjectId == project).OrderBy(link => link.Id).ToListAsync();
        return JsonSerializer.Serialize(new { Nodes = nodes, Links = links });
    }

    private sealed class ContentAuthorityProbe(EditingUiTestAccess access) : SaveChangesInterceptor, IDbTransactionInterceptor {
        public ContentBoundary Boundary { get; init; }
        public Guid Project { get; set; }
        public ProjectObjectType Type { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        private DbContext? pendingCommit;

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectObjectRecord>()
                    .Any(entry => entry.Entity.ProjectId == Project && entry.Entity.ObjectType == Type)) {
                if (Boundary == ContentBoundary.AfterNativeSql) {
                    Assert.NotNull(context.Database.CurrentTransaction);
                    Revoke();
                } else if (Boundary == ContentBoundary.AfterNativeCommit) {
                    pendingCommit = context;
                }
            }
            return ValueTask.FromResult(result);
        }

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (Armed && ReferenceEquals(eventData.Context, pendingCommit)) {
                Revoke();
            }
            return Task.CompletedTask;
        }

        private void Revoke() {
            Armed = false;
            Reached = true;
            access.Allowed = false;
        }
    }

    [Theory]
    [InlineData("transcript:summarize", ProjectLlmActionKind.Summarize)]
    [InlineData("transcript:find-my-tasks", ProjectLlmActionKind.FindMyTasks)]
    [InlineData("transcript:find-others-deliveries", ProjectLlmActionKind.FindOthersDeliveries)]
    public async Task Transcript_analysis_preserves_unknown_fields_references_and_native_identity(string action, ProjectLlmActionKind kind) {
        var provider = new ProviderGate();
        provider.Release.SetResult();
        await using var harness = await CreateHarnessAsync(provider: provider);
        var target = await CreateTargetAsync(harness, "Exact analysis", ProjectObjectType.Transcript);
        var json = JsonNode.Parse(target.Node.MetadataJson)!.AsObject();
        json["unowned"] = new JsonObject { ["keep"] = 73 };
        json["transcript"]!["futureField"] = "preserved";
        var originalAdmission = (await Workbench(harness).GetStructureAsync(target.ProjectId)).ExpectedProjectAdmission;
        var original = await Workbench(harness).UpdateObjectMetadataAsync(target.ProjectId, target.Node.Id, json.ToJsonString(),
            expectedState: ProjectStructureNodeStateGuard.Capture(target.Node, originalAdmission));
        Assert.NotNull(original);
        var cut = Render(harness, target.ProjectId);
        await ContextActionAsync(cut, target.Node.Id, action);
        await cut.InvokeAsync(() => Button(cut, "Send request").ClickAsync(new MouseEventArgs()));
        var stored = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == target.Node.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(stored.MetadataJson).Transcript!;
        Assert.Equal(kind, metadata.LastActionKind);
        Assert.Equal("Original provider answer", kind switch {
            ProjectLlmActionKind.Summarize => metadata.SummaryText,
            ProjectLlmActionKind.FindMyTasks => metadata.MyTasksText,
            _ => metadata.OthersDeliveriesText
        });
        Assert.Equal(original.RecordId, stored.RecordId);
        Assert.Equal(provider.Request!.ProviderProfileId, stored.NodeReferences!.TranscriptProviderProfileId);
        Assert.Equal(73, JsonNode.Parse(stored.MetadataJson)!["unowned"]!["keep"]!.GetValue<int>());
        Assert.Equal("preserved", JsonNode.Parse(stored.MetadataJson)!["transcript"]!["futureField"]!.GetValue<string>());
        Assert.Equal(1, provider.Calls);
        Assert.DoesNotContain((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.ObjectType == ProjectObjectType.WorkItem);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_transcript_content_is_not_overwritten_and_completed_provider_cannot_be_resent(bool notesOnly) {
        var provider = new ProviderGate();
        await using var harness = await CreateHarnessAsync(provider: provider);
        var target = await CreateTargetAsync(harness, "Concurrent analysis", ProjectObjectType.Transcript);
        var cut = Render(harness, target.ProjectId);
        await ContextActionAsync(cut, target.Node.Id, "transcript:summarize");
        var submit = cut.FindComponent<ProjectStructureSupportDialogs>().Instance.ExecuteTranscriptAction;
        var pending = cut.InvokeAsync(() => submit.InvokeAsync());
        await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await cut.InvokeAsync(() => submit.InvokeAsync());
        ProjectStructureNode? changed;
        try {
            var json = JsonNode.Parse(target.Node.MetadataJson)!.AsObject();
            json["transcript"]!["transcriptText"] = "Legitimate concurrent content";
            var competingOrigin = await Workbench(harness).GetStructureAsync(target.ProjectId);
            changed = await Workbench(harness).UpdateObjectMetadataAsync(target.ProjectId, target.Node.Id,
                notesOnly ? target.Node.MetadataJson : json.ToJsonString(), notes: notesOnly ? "Legitimate concurrent notes" : null,
                expectedState: ProjectStructureNodeStateGuard.Capture(target.Node, competingOrigin.ExpectedProjectAdmission));
        } finally {
            provider.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        await cut.InvokeAsync(() => submit.InvokeAsync());
        var stored = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == target.Node.Id);
        Assert.NotNull(changed);
        Assert.Equal((changed.MetadataJson, changed.Notes), (stored.MetadataJson, stored.Notes));
        Assert.Equal(1, provider.Calls);
        Assert.Equal(ContentConfirmationPhase.ObservationRequired, cut.FindComponent<ContentTranscriptDialog>().Instance.State.Phase);
        Assert.Contains(cut.Instance.AuthoringOutcomes, receipt => receipt.ExternalEffect == ProjectStructureExternalEffectState.Completed &&
            receipt.Kind == ProjectStructureAuthoringResultKind.PartialCommit && receipt.Node is null);
    }

    [Fact]
    public async Task Retired_summary_and_transcript_callbacks_cannot_use_reopened_same_target() {
        var provider = new ProviderGate();
        provider.Release.SetResult();
        await using var harness = await CreateHarnessAsync(provider: provider);
        var target = await CreateTargetAsync(harness, "Same public target", ProjectObjectType.Transcript);
        var cut = Render(harness, target.ProjectId);
        await ContextActionAsync(cut, target.Node.Id, "summary");
        var export = cut.FindComponent<ProjectStructureSupportDialogs>().Instance.ExportSummaryWorkbook;
        await ContextActionAsync(cut, target.Node.Id, "summary");
        var opening = cut.FindComponent<ContentProgressSummaryDialog>().Instance.State.OpeningId;
        await cut.InvokeAsync(() => export.InvokeAsync());
        Assert.Equal(opening, cut.FindComponent<ContentProgressSummaryDialog>().Instance.State.OpeningId);
        Assert.DoesNotContain((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.ObjectType == ProjectObjectType.File);

        await ContextActionAsync(cut, target.Node.Id, "transcript:summarize");
        var submit = cut.FindComponent<ProjectStructureSupportDialogs>().Instance.ExecuteTranscriptAction;
        await ContextActionAsync(cut, target.Node.Id, "transcript:find-my-tasks");
        await cut.InvokeAsync(() => submit.InvokeAsync());
        Assert.Equal(0, provider.Calls);
        Assert.Equal(ContentTranscriptAction.FindMyTasks, cut.FindComponent<ContentTranscriptDialog>().Instance.State.Action);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stored_summary_exports_decode_to_the_accepted_rows(bool gantt) {
        await using var harness = await CreateHarnessAsync();
        var target = await CreateTargetAsync(harness, "Accepted report");
        var child = await Workbench(harness).CreateObjectAsync(target.ProjectId,
            new(ProjectObjectType.Note, "Independent exact row", "Fixture child", "Notes are separate", target.Node.Id,
                StartUtc: new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero), EndUtc: new(2026, 10, 7, 11, 0, 0, TimeSpan.Zero), Status: "Done"));
        var cut = Render(harness, target.ProjectId);
        await ContextActionAsync(cut, target.Node.Id, "summary");
        var state = cut.FindComponent<ContentProgressSummaryDialog>().Instance.State;
        Assert.Equal(2, state.Rows.Count);
        Assert.Contains(state.Rows, row => row.NodeId == child.Id && row.Status == "Done");
        await cut.InvokeAsync(() => Button(cut, gantt ? "Export Gantt" : "Export XLSX").ClickAsync(new MouseEventArgs()));
        var created = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.ObjectType == ProjectObjectType.File);
        Assert.Equal(target.Node.Id, created.ParentId);
        await using var interaction = await harness.Context.Services.GetRequiredService<ProjectStructureKnownFileInteractionCoordinator>().OpenAsync(target.ProjectId, created.Id);
        await using var content = await interaction.Session.ContentSource.OpenReadAsync(new FileContentReadRequest(interaction.Session.File));
        using var bytes = new MemoryStream();
        await content.Stream.CopyToAsync(bytes);
        if (gantt) {
            var text = Encoding.UTF8.GetString(bytes.ToArray());
            Assert.StartsWith("gantt", text, StringComparison.Ordinal);
            Assert.Contains("Independent exact row (Done) :done,", text, StringComparison.Ordinal);
            Assert.Contains("2026-10-07 09:00:00", text, StringComparison.Ordinal);
            Assert.Contains(", 2h", text, StringComparison.Ordinal);
        } else {
            bytes.Position = 0;
            using var archive = new ZipArchive(bytes, ZipArchiveMode.Read);
            using var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
            var xml = XDocument.Load(sheet);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var rows = xml.Descendants(ns + "row").Select(row => row.Descendants(ns + "t").Select(value => value.Value).ToArray()).ToArray();
            Assert.Equal(3, rows.Length);
            Assert.Equal("Title", rows[0][1]);
            Assert.Equal(target.Node.Title, rows[1][1]);
            Assert.Equal(">Independent exact row", rows[2][1].Trim());
            Assert.Equal("Done", rows[2][3]);
            Assert.Equal("2026-10-07 09:00", rows[2][5]);
            Assert.Equal("2026-10-07 11:00", rows[2][6]);
        }
        var receipt = Assert.Single(cut.Instance.AuthoringOutcomes, outcome => outcome.Node?.Id == created.Id);
        Assert.Equal(created.StorageObjectReferenceJson, receipt.Node!.StorageObjectReferenceJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_completion_and_native_save_fault_keep_one_external_request(bool afterCommit) {
        var provider = new ProviderGate();
        provider.Release.SetResult();
        var fault = new TranscriptSaveFault(afterCommit);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IProviderRuntimeProfileSource>(provider));
            services.Replace(ServiceDescriptor.Singleton<IProviderRuntimeProfileSnapshotSource>(provider));
            services.Replace(ServiceDescriptor.Singleton<IProviderPromptExecutionService>(provider));
            services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(sp => new PooledDbContextFactory<WorkbenchDbContext>(
                new DbContextOptionsBuilder<WorkbenchDbContext>(sp.GetRequiredService<DbContextOptions<WorkbenchDbContext>>()).AddInterceptors(fault).Options));
        });
        var target = await CreateTargetAsync(harness, "Native save fault", ProjectObjectType.Transcript);
        var cut = Render(harness, target.ProjectId);
        await ContextActionAsync(cut, target.Node.Id, "transcript:summarize");
        fault.ProjectId = target.ProjectId;
        var submit = cut.FindComponent<ProjectStructureSupportDialogs>().Instance.ExecuteTranscriptAction;
        await cut.InvokeAsync(() => submit.InvokeAsync());
        await cut.InvokeAsync(() => submit.InvokeAsync());
        Assert.Equal(1, provider.Calls);
        var stored = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == target.Node.Id);
        Assert.Equal(afterCommit ? "Original provider answer" : string.Empty,
            ProjectObjectMetadataSerializer.Parse(stored.MetadataJson).Transcript!.SummaryText);
        Assert.Equal(ContentConfirmationPhase.ObservationRequired, cut.FindComponent<ContentTranscriptDialog>().Instance.State.Phase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_mermaid_retired_callbacks_cannot_close_or_edit_a_new_opening(bool edit) {
        await using var harness = await CreateHarnessAsync();
        var target = await CreateTargetAsync(harness, "Legacy owner");
        var legacy = await Workbench(harness).CreateObjectAsync(target.ProjectId,
            new(ProjectObjectType.File, "Legacy diagram", "", "flowchart LR\n A --> B", target.Node.Id, ObjectSubtype: "mermaid"));
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, legacy.Id);
        Assert.True(cut.FindComponent<ProjectStructureSelectionPanel>().Instance.State.SelectedNode!.HasMermaidViewer);
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Open(InsightsNativeView.Mermaid));
        var previous = cut.FindComponent<ProjectStructureSupportDialogs>().Instance;
        var close = previous.CloseMermaidViewer;
        var previousEdit = previous.EditMermaidNode;
        await SelectAsync(cut, legacy.Id);
        Assert.True(cut.FindComponent<ProjectStructureSelectionPanel>().Instance.State.SelectedNode!.HasMermaidViewer);
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Open(InsightsNativeView.Mermaid));
        if (edit) {
            await cut.InvokeAsync(() => previousEdit.InvokeAsync(legacy));
        } else {
            await cut.InvokeAsync(() => close.InvokeAsync());
        }
        Assert.Equal(legacy.Notes, cut.FindComponent<ContentLegacyMermaidDialog>().Instance.State.Source);
        Assert.Empty(cut.FindAll("[data-testid='structure-node-form']"));
    }

    [Fact]
    public async Task Legacy_mermaid_changed_original_content_cannot_open_an_editor_from_its_old_view() {
        await using var harness = await CreateHarnessAsync();
        var target = await CreateTargetAsync(harness, "Legacy revision");
        var legacy = await Workbench(harness).CreateObjectAsync(target.ProjectId,
            new(ProjectObjectType.File, "Legacy diagram", "", "flowchart LR\n A --> B", target.Node.Id, ObjectSubtype: "mermaid"));
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, legacy.Id);
        Assert.True(cut.FindComponent<ProjectStructureSelectionPanel>().Instance.State.SelectedNode!.HasMermaidViewer);
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Open(InsightsNativeView.Mermaid));
        var competingOrigin = await Workbench(harness).GetStructureAsync(target.ProjectId);
        await Workbench(harness).UpdateObjectMetadataAsync(target.ProjectId, legacy.Id, legacy.MetadataJson, notes: "flowchart LR\n Current --> Content",
            expectedState: ProjectStructureNodeStateGuard.Capture(legacy, competingOrigin.ExpectedProjectAdmission));
        await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSupportDialogs>().Instance.EditMermaidNode.InvokeAsync(legacy));
        Assert.NotNull(cut.FindComponent<ContentLegacyMermaidDialog>());
        Assert.Contains("original Mermaid content changed", cut.Markup, StringComparison.Ordinal);
        var stored = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == legacy.Id);
        Assert.Equal("flowchart LR\n Current --> Content", stored.Notes);
    }

    private sealed class TranscriptSaveFault(bool afterCommit) : SaveChangesInterceptor, IDbTransactionInterceptor {
        private DbContext? accepted;
        public Guid? ProjectId { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (ProjectId is { } project && eventData.Context?.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry =>
                entry.State == EntityState.Modified && entry.Entity.ProjectId == project && entry.Entity.ObjectType == ProjectObjectType.Transcript) is true) {
                ProjectId = null;
                if (!afterCommit) {
                    throw new IOException("Synthetic native refusal before transcript commit.");
                }
                accepted = eventData.Context;
            }
            return ValueTask.FromResult(result);
        }
        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (accepted is not null && ReferenceEquals(accepted, eventData.Context)) {
                accepted = null;
                throw new IOException("Synthetic lost acknowledgement after transcript commit.");
            }
            return Task.CompletedTask;
        }
    }
}
