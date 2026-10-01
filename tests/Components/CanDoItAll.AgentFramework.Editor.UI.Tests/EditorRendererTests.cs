using Bunit;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Editor.UiSandbox;
using CanDoItAll.AgentFramework.Editor.UiSandbox.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentEditorUi;

public sealed class EditorRendererTests {
    [Fact]
    public async Task All_ten_sections_share_one_form_and_draft_with_explicit_deferred_slots() {
        using var context = Context();
        var cut = context.Render<ScenarioEditor>();
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        Assert.Equal(10, cut.FindAll("[role='tab']").Count);
        foreach (var section in AgentEditorSections.All) {
            await cut.FindAll("[role='tab']")[AgentEditorSections.IndexOf(section.Section)].ClickAsync();
            Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
            Assert.Single(cut.FindAll("form"));
            if (section.Section is not (AgentEditorSection.Identity or AgentEditorSection.Runtime or AgentEditorSection.Images or AgentEditorSection.Voice)) {
                Assert.Contains("retained production section", cut.Markup);
            }
        }
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Unicode_input_before_blur_and_core_fields_keep_the_same_canonical_draft() {
        using var context = Context();
        var cut = context.Render<ScenarioEditor>();
        var draft = Draft(cut);
        cut.Find("[data-testid='agents-catalog-name']").Input("Žluťoučký 東京 🧭");
        cut.Find("[data-testid='agents-catalog-role']").Input("Role 東京");
        cut.Find("[data-testid='agents-catalog-summary']").Input("Summary Ž");
        cut.Find("[data-testid='agents-catalog-instructions']").Input("Instructions 東京");
        Assert.Equal("Žluťoučký 東京 🧭", draft.Name);
        Assert.Equal("Role 東京", draft.RoleTitle);
        Assert.Equal("Summary Ž", draft.Summary);
        Assert.Equal("Instructions 東京", draft.Instructions);
        Assert.Contains("Favorite", draft.Tags);
        await Tab(cut, AgentEditorSection.Images);
        cut.Find("[data-testid='agents-catalog-image-generation-enabled']").Change(true);
        cut.Find("[data-testid='agents-catalog-image-generation-project-assets']").Change(true);
        Assert.True(draft.ImageGenerationAccess.CanGenerateImages);
        Assert.True(draft.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        await Tab(cut, AgentEditorSection.Voice);
        cut.Find("[data-testid='agents-catalog-voice-enabled']").Change(true);
        cut.Find("[data-testid='agents-catalog-voice-override']").Change("alloy");
        Assert.Equal("alloy", draft.VoiceAccess.PreferredVoiceId);
        cut.Find("[data-testid='agents-catalog-voice-override']").Change(string.Empty);
        Assert.Empty(draft.VoiceAccess.PreferredVoiceId);
        Assert.Same(draft, Draft(cut));
    }

    [Fact]
    public async Task Presented_support_updates_on_the_first_render_without_replacing_tab_or_form() {
        using var context = Context();
        using var owner = new AgentEditorScenarioSession(this, AgentEditorScenario.Representative, "Agent");
        var state = owner.State with { Section = AgentEditorSection.Runtime };
        var cut = context.Render<AgentEditorCoreSurface>(p => p.Add(x => x.State, state));
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        var tabs = cut.FindComponent<Tabs>().Instance;
        state = state with { ThinkingEffort = state.ThinkingEffort with { SupportMessage = "Unknown model; saved High is unavailable." } };
        cut.Render(p => p.Add(x => x.State, state));
        Assert.Contains("Unknown model; saved High is unavailable.", cut.Find("[data-testid='agents-catalog-thinking-effort-support']").TextContent);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Same(tabs, cut.FindComponent<Tabs>().Instance);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Retired_core_callback_cannot_edit_a_reopened_editor() {
        using var context = Context();
        var cut = context.Render<ScenarioEditor>();
        var original = Draft(cut);
        var delayed = cut.FindComponent<ConversationIdentityFields>().Instance.NameChanged;
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Reopen editor").ClickAsync();
        var replacement = Draft(cut);
        await cut.InvokeAsync(() => delayed.InvokeAsync("Late old input"));
        Assert.NotSame(original, replacement);
        Assert.Equal("Fixture agent", replacement.Name);
        Assert.Equal("Fixture agent", original.Name);
    }

    [Fact]
    public async Task Default_and_explicit_None_are_distinct_and_unknown_saved_effort_is_retained() {
        using var context = Context();
        var cut = context.Render<ScenarioEditor>();
        await Tab(cut, AgentEditorSection.Runtime);
        Select(cut, "agents-catalog-thinking-effort", "None");
        Assert.Equal(AgentReasoningEffortLevel.None, Draft(cut).ThinkingEffortOverride);
        Select(cut, "agents-catalog-thinking-effort", "Provider default");
        Assert.Null(Draft(cut).ThinkingEffortOverride);
        var unknown = context.Render<ScenarioEditor>(p => p.Add(x => x.Scenario, AgentEditorScenario.UnknownEffort));
        await Tab(unknown, AgentEditorSection.Runtime);
        Assert.Equal(AgentReasoningEffortLevel.High, Draft(unknown).ThinkingEffortOverride);
        Assert.True(unknown.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        Select(unknown, "agents-catalog-thinking-effort", "Provider default");
        Assert.False(unknown.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Held_save_captures_before_await_and_rejects_duplicate_submission() {
        using var context = Context();
        using var owner = new AgentEditorScenarioSession(this, AgentEditorScenario.Representative, "Captured before await");
        var cut = context.Render<AgentEditorCoreSurface>(p => p.Add(x => x.State, owner.State));
        owner.Hold();
        var saving = cut.Find("form").SubmitAsync();
        Assert.Equal(1, owner.Writes);
        await cut.Find("form").SubmitAsync();
        owner.Draft.Name = "Newer draft";
        Assert.Contains("Captured before await", owner.SubmittedJson);
        owner.Release();
        await saving;
        Assert.Equal(1, owner.Writes);
        Assert.Equal("Newer draft", owner.Draft.Name);
    }

    [Theory]
    [InlineData(AgentEditorScenario.SaveRefusal, "Known save refusal", false)]
    [InlineData(AgentEditorScenario.CommitWarning, "Synthetic commit confirmed", false)]
    [InlineData(AgentEditorScenario.RefreshFailure, "read-back failed", true)]
    [InlineData(AgentEditorScenario.UnknownResult, "could not be confirmed", true)]
    public async Task Mutation_outcomes_retain_draft_and_only_read_retry_unlocks_a_confirmed_write(
        AgentEditorScenario scenario, string message, bool blocked) {
        using var context = Context();
        var cut = context.Render<ScenarioEditor>(p => p.Add(x => x.Scenario, scenario));
        cut.Find("[data-testid='agents-catalog-name']").Input("Retained 東京");
        await cut.Find("form").SubmitAsync();
        Assert.Contains(message, cut.Markup);
        Assert.Equal("Retained 東京", Draft(cut).Name);
        Assert.Equal(blocked, cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        if (scenario == AgentEditorScenario.RefreshFailure) {
            await cut.Find("[data-testid='agents-editor-retry-refresh']").ClickAsync();
            Assert.Contains("writes: 1; reads: 1", cut.Markup);
            Assert.False(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        }
    }

    [Theory]
    [InlineData(AgentEditorScenario.Loading)]
    [InlineData(AgentEditorScenario.LoadFailure)]
    public async Task No_form_is_admitted_until_loading_succeeds(AgentEditorScenario scenario) {
        using var context = Context();
        var cut = context.Render<ScenarioEditor>(p => p.Add(x => x.Scenario, scenario));
        Assert.Empty(cut.FindAll("form"));
        if (scenario == AgentEditorScenario.LoadFailure) {
            await cut.Find("[data-testid='agents-details-retry-load']").ClickAsync();
            Assert.Single(cut.FindAll("form"));
        }
    }

    [Fact]
    public async Task Two_editors_do_not_share_draft_or_lifetime() {
        using var context = Context();
        var first = context.Render<ScenarioEditor>(p => p.Add(x => x.Name, "First"));
        var second = context.Render<ScenarioEditor>(p => p.Add(x => x.Name, "Second"));
        first.Find("[data-testid='agents-catalog-name']").Input("First changed");
        await first.FindAll("button").Single(button => button.TextContent.Trim() == "Close editor").ClickAsync();
        Assert.Empty(first.FindAll("form"));
        Assert.Equal("Second", Draft(second).Name);
        Assert.Single(second.FindAll("form"));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
    private static AgentEditorModel Draft(IRenderedComponent<ScenarioEditor> cut)
        => Assert.IsType<AgentEditorModel>(cut.FindComponent<EditForm>().Instance.EditContext!.Model);
    private static Task Tab(IRenderedComponent<ScenarioEditor> cut, AgentEditorSection section)
        => cut.FindAll("[role='tab']")[AgentEditorSections.IndexOf(section)].ClickAsync();
    private static void Select(IRenderedComponent<ScenarioEditor> cut, string id, string label) {
        var select = cut.Find($"[data-testid='{id}']");
        var option = select.QuerySelectorAll("option").Single(item => item.TextContent.Trim() == label);
        select.Change(option.GetAttribute("value"));
    }
}
