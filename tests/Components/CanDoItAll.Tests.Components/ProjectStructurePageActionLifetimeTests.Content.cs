using System.Data.Common;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Content.UI.Analysis;
using CanDoItAll.Workbench.Insights.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePageActionLifetimeTests {
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
        var original = await Workbench(harness).UpdateObjectMetadataAsync(target.ProjectId, target.Node.Id, json.ToJsonString());
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
            changed = await Workbench(harness).UpdateObjectMetadataAsync(target.ProjectId, target.Node.Id,
                notesOnly ? target.Node.MetadataJson : json.ToJsonString(), notes: notesOnly ? "Legitimate concurrent notes" : null);
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
        await Workbench(harness).UpdateObjectMetadataAsync(target.ProjectId, legacy.Id, legacy.MetadataJson, notes: "flowchart LR\n Current --> Content");
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
