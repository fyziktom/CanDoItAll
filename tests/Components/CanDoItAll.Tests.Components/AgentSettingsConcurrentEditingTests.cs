using Bunit;
using CanDoItAll.AgentFramework.UI.Settings;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using CanDoItAll.Editing.UI;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentSettingsConcurrentEditingTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Navigation_keeps_a_dirty_draft_and_requires_explicit_discard_without_leaving_during_pending_or_unknown_work(bool busy, bool unknown) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.SetRendererInfo(new("Server", isInteractive: true));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var cut = context.Render<DraftNavigationGuard>(parameters => parameters
            .Add(component => component.MustRetain, true).Add(component => component.IsBusy, busy)
            .Add(component => component.OutcomeUnknown, unknown));
        navigation.NavigateTo("/after-settings");
        cut.WaitForElement("[data-testid='draft-navigation-guard']");
        Assert.DoesNotContain("after-settings", navigation.Uri, StringComparison.Ordinal);
        var discard = cut.FindAll("button").Single(button => button.TextContent.Trim() == "Discard draft and continue");
        Assert.Equal(busy || unknown, discard.HasAttribute("disabled"));
        if (!busy && !unknown) {
            discard.Click();
            cut.WaitForAssertion(() => Assert.EndsWith("/after-settings", navigation.Uri, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void A_review_combines_independent_fields_and_requires_an_explicit_choice_for_a_shared_field() {
        var editor = VoiceSettingsFields.CreateEditor();
        var original = new VoiceSettingsValues { SampleText = "Original", Language = "en", UpdatedAtUtc = DateTimeOffset.UtcNow };
        editor.Open(original);
        editor.Input(original with { SampleText = "Local", Language = "cs" });
        var current = original with { SampleText = "Peer", VoiceId = "nova", UpdatedAtUtc = original.UpdatedAtUtc!.Value.AddSeconds(1) };
        Assert.True(editor.Compare(current, editor.InputRevision));
        Assert.False(editor.CanApply);
        Assert.False(editor.Apply(false));
        var conflict = Assert.Single(editor.Comparison!.Fields, entry => !entry.IsResolved);
        Assert.Equal("Sample text", conflict.Label);
        conflict.UseLocal();
        Assert.True(editor.Apply(false));
        Assert.Equal("Local", editor.Draft.SampleText);
        Assert.Equal("cs", editor.Draft.Language);
        Assert.Equal("nova", editor.Draft.VoiceId);
        Assert.Same(current, editor.Original);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void Undo_restores_raw_local_values_over_the_reviewed_current_original_and_blocks_save_until_review() {
        var editor = FloatingChatSettingsInput.CreateEditor();
        var original = FloatingChatSettingsInput.From(new(UpdatedAtUtc: DateTimeOffset.UtcNow));
        var local = original with { ActiveLimit = "-", PreparedLimit = "003" };
        var current = original with { Retention = "25", UpdatedAtUtc = original.UpdatedAtUtc!.Value.AddSeconds(1) };
        editor.Open(original);
        editor.Input(local);
        editor.Compare(current, editor.InputRevision);
        Assert.True(editor.Apply(false));
        Assert.Equal("-", editor.Draft.ActiveLimit);
        Assert.Equal("25", editor.Draft.Retention);
        editor.Undo();
        Assert.Equal(local, editor.Draft);
        Assert.Same(current, editor.Original);
        Assert.True(editor.NeedsReview);
    }

    [Fact]
    public void Same_value_input_during_await_keeps_the_entire_raw_draft_and_adopts_only_the_exact_accepted_original() {
        var editor = FloatingChatSettingsInput.CreateEditor();
        var source = FloatingChatSettingsInput.From(new());
        editor.Open(source);
        editor.Input(source with { ActiveLimit = "003" });
        var submitted = editor.Draft;
        var revision = editor.InputRevision;
        editor.Input(editor.Draft);
        var accepted = source with { ActiveLimit = "3", Retention = "22", UpdatedAtUtc = DateTimeOffset.UtcNow };
        editor.Accept(submitted, accepted, revision);
        Assert.Same(submitted, editor.Draft);
        Assert.Same(accepted, editor.Original);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void An_unchanged_submission_adopts_the_exact_accepted_content_without_a_read_back() {
        var editor = VoiceSettingsFields.CreateEditor();
        editor.Open(new());
        editor.Input(editor.Draft with { Language = " cs " });
        var accepted = editor.Draft with { Language = "cs", UpdatedAtUtc = DateTimeOffset.UtcNow };
        editor.Accept(editor.Draft, accepted, editor.InputRevision);
        Assert.Equal("cs", editor.Draft.Language);
        Assert.False(editor.IsDirty);
        Assert.Same(accepted, editor.Original);
    }

    [Fact]
    public void A_review_or_read_started_before_later_input_cannot_replace_the_later_draft() {
        var editor = VoiceSettingsFields.CreateEditor();
        editor.Open(new());
        var revision = editor.InputRevision;
        editor.Input(editor.Draft with { Prompt = "Typed during read" });
        Assert.False(editor.Compare(new(), revision));
        editor.Compare(new(), editor.InputRevision);
        editor.Input(editor.Draft with { SampleText = "Typed during review" });
        Assert.False(editor.CanApply);
        Assert.False(editor.Apply(false));
        Assert.Equal("Typed during review", editor.Draft.SampleText);
    }

    [Fact]
    public void Unknown_requires_an_explicit_review_and_acknowledgement_and_never_clears_on_a_hint() {
        var editor = VoiceSettingsFields.CreateEditor();
        editor.Open(new());
        editor.Unknown();
        editor.Hint();
        Assert.True(editor.OutcomeUnknown);
        editor.Compare(new(), editor.InputRevision);
        Assert.False(editor.Apply(false));
        Assert.True(editor.Apply(true));
        Assert.False(editor.OutcomeUnknown);
        Assert.False(editor.UpdatesAvailable);
    }

    [Fact]
    public void Invalid_numeric_text_is_retained_across_status_and_comparison_and_cannot_submit_a_last_valid_number() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var presentation = new FloatingChatSettingsPresentation(Source: new(), Limits: new(100, 50, 100), Status: FloatingChatSettingsStatus.Ready);
        var intents = new List<FloatingChatSettingsIntent>();
        var cut = context.Render<FloatingChatSettingsSurface>(parameters => parameters.Add(component => component.Presentation, presentation)
            .Add(component => component.Intent, intent => intents.Add(intent)));
        cut.Find("[data-testid='floating-agent-chat-maximum-active']").Input("-");
        cut.Find("[data-testid='floating-agent-chat-settings-save']").Click();
        Assert.Empty(intents);
        cut.Render(parameters => parameters.Add(component => component.Presentation, presentation with { Status = FloatingChatSettingsStatus.Conflict }));
        Assert.Equal("-", cut.Find("[data-testid='floating-agent-chat-maximum-active']").GetAttribute("value"));
        presentation.Editor.Compare(presentation.Editor.Original!, presentation.Editor.InputRevision);
        Assert.True(presentation.Editor.Apply(false));
        Assert.Equal("-", presentation.Editor.Draft.ActiveLimit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_settings_authority_rejects_actor_or_dataset_changes_after_a_pending_read(bool dataset) {
        var scope = new EditingScope(EditingScopeKind.Dataset, Guid.NewGuid(), 1);
        var access = new EditingUiTestAccess();
        var feed = new EditingChangeFeed();
        var configuration = new AgentConfigurationChanges(feed, () => scope, access, new(), NullLogger<EditingObservation>.Instance);
        var writes = new WorkflowChanges(feed, scope, new(), NullLogger<WorkflowChanges>.Instance);
        await using var opening = new AgentSettingsOpening(configuration, writes, EditingOwner.FloatingChatSettings, () => true);
        await opening.StartAsync(() => Task.CompletedTask, () => Task.CompletedTask);
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = opening.ReadAsync(_ => pending.Task, default);
        if (dataset) {
            scope = scope with { ActivationGeneration = scope.ActivationGeneration + 1 };
        } else {
            access.Principal = "Other current actor";
        }
        pending.SetResult("Protected settings result");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => read);
        var dispatched = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => opening.WriteAsync(_ => {
            dispatched = true;
            return Task.FromResult(true);
        }, default));
        Assert.False(dispatched);
    }
}
