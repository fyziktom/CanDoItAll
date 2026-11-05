using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Editing.UI;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDefinitionConcurrentEditingTests {
    [Fact]
    public async Task Accepted_save_keeps_unsubmitted_tag_input_and_the_edit_context() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var saved = await workspace.SaveAgentDefinitionAsync(new() { Name = "Unsubmitted tag original" });
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, saved.Id));
        cut.WaitForElement("[data-testid='agents-catalog-tags-input']").Input("unfinished 東京");
        await cut.InvokeAsync(() => cut.FindComponent<AgentEditorCoreSurface>().Instance.State.SectionChanged.InvokeAsync(AgentEditorSection.WorkspaceTools));
        await cut.InvokeAsync(() => cut.FindComponent<AgentEditorCoreSurface>().Instance.State.SectionChanged.InvokeAsync(AgentEditorSection.Identity));
        Assert.Equal("unfinished 東京", cut.Find("[data-testid='agents-catalog-tags-input']").GetAttribute("value"));
        cut.Find("[data-testid='agents-catalog-name']").Input("Acknowledged name");
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        await cut.Find("form").SubmitAsync();
        Assert.Equal("unfinished 東京", cut.Find("[data-testid='agents-catalog-tags-input']").GetAttribute("value"));
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        var accepted = await workspace.GetAgentEditorAsync(saved.Id);
        Assert.Equal("Acknowledged name", accepted.Name);
        Assert.DoesNotContain("unfinished 東京", accepted.Tags);
        Assert.Equal(accepted.ExpectedUpdatedAtUtc, ((AgentEditorModel)context!.Model).ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Same_value_postcommit_typing_keeps_the_original_edit_context() {
        PostCommitPeerCommands? commands = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<IAgentEditorCommands>(provider => commands = new(
                ActivatorUtilities.CreateInstance<AgentEditorCommands>(provider), provider.GetRequiredService<IAgentFrameworkWorkspaceService>())));
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var saved = await workspace.SaveAgentDefinitionAsync(new() { Name = "Before same-value input" });
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, saved.Id));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Input("Submitted name");
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        var submit = cut.Find("form").SubmitAsync();
        await commands!.Committed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        cut.Find("[data-testid='agents-catalog-name']").Input("Temporary later input");
        cut.Find("[data-testid='agents-catalog-name']").Input("Submitted name");
        commands.Release.SetResult();
        await submit;
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Submitted name", ((AgentEditorModel)context!.Model).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Peer_change_preserves_the_open_draft_and_comparison_requires_a_separate_save(bool sameField) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var saved = await workspace.SaveAgentDefinitionAsync(new() { Name = "Original comparison agent", Summary = "Original summary" });
        var dialogs = harness.Context.Render<DialogHost>();
        var first = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, saved.Id));
        var second = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, saved.Id));
        first.WaitForElement("[data-testid='agents-catalog-name']").Input("My retained name 東京");
        second.WaitForElement("[data-testid='agents-catalog-name']");
        var context = first.FindComponent<EditForm>().Instance.EditContext!;
        if (sameField) {
            second.Find("[data-testid='agents-catalog-name']").Input("Peer name");
        } else {
            second.Find("[data-testid='agents-catalog-summary']").Input("Peer summary");
        }
        await second.Find("form").SubmitAsync();
        first.WaitForElement("[data-testid='agents-editor-changed-elsewhere']");
        Assert.True(first.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        Assert.Same(context, first.FindComponent<EditForm>().Instance.EditContext);
        var compare = first.Find("[data-testid='agents-editor-compare']").ClickAsync();
        dialogs.WaitForElement("[data-testid='agents-definition-comparison']");
        if (sameField) {
            Assert.True(dialogs.Find("[data-testid='agents-comparison-apply']").HasAttribute("disabled"));
            var name = dialogs.FindComponents<MergeFieldView<string>>().Single(component => component.Instance.Label == "Name");
            await name.FindAll("button").Single(button => button.TextContent.Trim() == "Use my draft").ClickAsync();
        }
        await dialogs.Find("[data-testid='agents-comparison-apply']").ClickAsync();
        await compare;
        Assert.Same(context, first.FindComponent<EditForm>().Instance.EditContext);
        var draft = (AgentEditorModel)context.Model;
        Assert.Equal("My retained name 東京", draft.Name);
        Assert.Equal(sameField ? "Original summary" : "Peer summary", draft.Summary);
        var beforeSave = await workspace.GetAgentEditorAsync(saved.Id);
        Assert.Equal(sameField ? "Peer name" : saved.Name, beforeSave.Name);
        Assert.Equal(beforeSave.ExpectedUpdatedAtUtc, draft.ExpectedUpdatedAtUtc);
        await first.Find("form").SubmitAsync();
        Assert.Equal("My retained name 東京", (await workspace.GetAgentEditorAsync(saved.Id)).Name);
        Assert.Empty(first.FindAll("[data-testid='agents-editor-changed-elsewhere']"));
    }

    [Fact]
    public async Task A_newer_peer_commit_marks_the_open_comparison_stale_and_disables_apply() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var saved = await workspace.SaveAgentDefinitionAsync(new() { Name = "Stale comparison original", Summary = "Original summary" });
        var dialogs = harness.Context.Render<DialogHost>();
        var first = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, saved.Id));
        var second = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, saved.Id));
        first.WaitForElement("[data-testid='agents-catalog-name']").Input("Retained local name");
        second.WaitForElement("[data-testid='agents-catalog-summary']").Input("Peer before comparison");
        await second.Find("form").SubmitAsync();
        first.WaitForElement("[data-testid='agents-editor-changed-elsewhere']");
        var compare = first.Find("[data-testid='agents-editor-compare']").ClickAsync();
        dialogs.WaitForElement("[data-testid='agents-definition-comparison']");
        try {
            second.Find("[data-testid='agents-catalog-summary']").Input("Peer after comparison opened");
            await second.Find("form").SubmitAsync();
            dialogs.WaitForAssertion(() => Assert.True(dialogs.Find("[data-testid='agents-comparison-apply']").HasAttribute("disabled")));
            Assert.NotNull(dialogs.Find("[data-testid='agents-comparison-stale']"));
        } finally {
            await harness.Context.Services.GetRequiredService<DialogService>().CloseAsync(false);
            await compare;
        }
        Assert.Equal("Retained local name", first.Find("[data-testid='agents-catalog-name']").GetAttribute("value"));
    }

    [Fact]
    public async Task Postcommit_peer_read_does_not_replace_the_exact_accepted_base_or_later_typing() {
        PostCommitPeerCommands? commands = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<IAgentEditorCommands>(provider => commands = new(
                ActivatorUtilities.CreateInstance<AgentEditorCommands>(provider), provider.GetRequiredService<IAgentFrameworkWorkspaceService>())));
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var saved = await workspace.SaveAgentDefinitionAsync(new() { Name = "Before acknowledgement", Summary = "Original summary" });
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, saved.Id));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Input("Submitted name");
        var context = cut.FindComponent<EditForm>().Instance.EditContext!;
        var submit = cut.Find("form").SubmitAsync();
        await commands!.Committed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        cut.Find("[data-testid='agents-catalog-name']").Input("Typed after commit");
        commands.Release.SetResult();
        await submit;
        var current = await workspace.GetAgentEditorAsync(saved.Id);
        var draft = (AgentEditorModel)context.Model;
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Typed after commit", draft.Name);
        Assert.Equal("Original summary", draft.Summary);
        Assert.Equal(commands.Accepted!.UpdatedAtUtc, draft.ExpectedUpdatedAtUtc);
        Assert.NotEqual(current.ExpectedUpdatedAtUtc, draft.ExpectedUpdatedAtUtc);
        Assert.Equal("Later peer summary", current.Summary);
        Assert.NotNull(cut.Find("[data-testid='agents-editor-changed-elsewhere']"));
        Assert.True(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
    }

    private sealed class PostCommitPeerCommands(IAgentEditorCommands inner, IAgentFrameworkWorkspaceService workspace) : IAgentEditorCommands {
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AgentDefinition? Accepted { get; private set; }
        public async Task<AgentEditorSaveOutcome> SaveAsync(AgentEditorModel request, CancellationToken cancellationToken = default) {
            var accepted = Assert.IsType<AgentEditorSaveOutcome.Committed>(await inner.SaveAsync(request, cancellationToken));
            Accepted = Assert.IsType<AgentDefinition>(accepted.Accepted);
            var peer = await workspace.GetAgentEditorAsync(accepted.AgentId, cancellationToken);
            peer.Summary = "Later peer summary";
            await workspace.SaveAgentAsync(peer, cancellationToken);
            Committed.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return accepted;
        }
        public Task<AgentEditorCatalogRefresh> ReconcileAsync(Guid id, IReadOnlyList<ProviderProfile> providers, CancellationToken cancellationToken = default)
            => inner.ReconcileAsync(id, providers, cancellationToken);
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, cancellationToken);
        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default)
            => inner.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
    }
}
