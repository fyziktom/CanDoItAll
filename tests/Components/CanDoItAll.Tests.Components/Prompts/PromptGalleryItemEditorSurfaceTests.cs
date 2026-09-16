using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Prompts.UI.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptGalleryItemEditorSurfaceTests
{
    [Fact]
    public void Existing_item_renders_supported_model_and_version_metadata()
    {
        var itemId = Guid.NewGuid();
        var details = ScriptedPromptGalleryService.Details(itemId, currentVersionNumber: 1);
        using var context = CreateContext();

        var cut = context.Render<PromptGalleryItemEditorSurface>(parameters => parameters
            .Add(component => component.Presentation, Ready(itemId, details)));

        Assert.NotNull(cut.Find("[data-testid='prompt-gallery-item-editor']"));
        Assert.Contains("gpt-5.4-mini", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Ready for reuse", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Saved Gallery item", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Loaded prompt", cut.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value"));
        Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-archive']"));
        Assert.Contains("Compatibility warning preferences", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Rerender_with_the_same_source_preserves_edits_and_a_new_source_resets_the_form()
    {
        using var context = CreateContext();
        var presentation = PromptGalleryEditorPresentation.CreateNew(1);
        var cut = context.Render<PromptGalleryItemEditorSurface>(parameters => parameters
            .Add(component => component.Presentation, presentation));

        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("In progress");
        cut.Find("[data-testid='prompt-gallery-editor-provider-to-add']").Change("OpenAI");
        cut.Find("[data-testid='prompt-gallery-editor-model-to-add']").Change("gpt-5.4-mini");
        cut.Find("[data-testid='prompt-gallery-editor-add-model']").Click();
        var form = cut.Instance.CurrentForm;

        // Same source reference, new presentation record (for example a busy flag) keeps the draft and the edit context.
        cut.Render(parameters => parameters
            .Add(component => component.Presentation, presentation with { IsBusy = true }));
        Assert.Same(form, cut.Instance.CurrentForm);
        Assert.Equal("In progress", cut.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value"));
        Assert.Contains("gpt-5.4-mini", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("true", cut.Find("fieldset").GetAttribute("aria-busy"));

        cut.Render(parameters => parameters
            .Add(component => component.Presentation, PromptGalleryEditorPresentation.CreateNew(2)));
        Assert.NotSame(form, cut.Instance.CurrentForm);
        Assert.Equal("", cut.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value") ?? "");
        Assert.DoesNotContain("gpt-5.4-mini", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_requires_name_and_content_and_emits_an_independent_snapshot()
    {
        var intents = new List<PromptGalleryEditorIntent>();
        using var context = CreateContext();
        var cut = context.Render<PromptGalleryItemEditorSurface>(parameters => parameters
            .Add(component => component.Presentation, PromptGalleryEditorPresentation.CreateNew(7))
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGalleryEditorIntent>(this, intents.Add)));

        cut.Find("form").Submit();
        Assert.Empty(intents);
        Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-validation']"));

        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("Reusable prompt");
        cut.Find("[data-testid='prompt-gallery-editor-content']").Change("Instruction body");
        cut.Find("form").Submit();

        var save = Assert.IsType<PromptGalleryEditorIntent.SaveDraft>(Assert.Single(intents));
        Assert.Equal(7, save.Generation);
        Assert.Equal("Reusable prompt", save.Submission.Title);
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-editor-validation']"));

        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("Edited later");
        Assert.Equal("Reusable prompt", save.Submission.Title);

        cut.Find("[data-testid='prompt-gallery-editor-finalize']").Click();
        var finalize = Assert.IsType<PromptGalleryEditorIntent.CreateVersion>(intents[1]);
        Assert.Equal("Edited later", finalize.Submission.Title);
    }

    [Fact]
    public void Failed_phase_shows_retry_and_close_instead_of_a_form()
    {
        var itemId = Guid.NewGuid();
        var intents = new List<PromptGalleryEditorIntent>();
        using var context = CreateContext();
        var cut = context.Render<PromptGalleryItemEditorSurface>(parameters => parameters
            .Add(component => component.Presentation, PromptGalleryEditorPresentation.CreateLoading(3, itemId) with
            {
                Phase = PromptGalleryEditorPhase.Failed,
                FailureMessage = "Prompt Gallery item was not found."
            })
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGalleryEditorIntent>(this, intents.Add)));

        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-item-editor']"));
        Assert.Contains("was not found", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='prompt-gallery-editor-retry']").Click();
        cut.Find("[data-testid='prompt-gallery-editor-close']").Click();

        Assert.Equal(3, Assert.IsType<PromptGalleryEditorIntent.Retry>(intents[0]).Generation);
        Assert.IsType<PromptGalleryEditorIntent.Cancel>(intents[1]);
    }

    [Fact]
    public void Busy_presentation_blocks_duplicate_commands()
    {
        var itemId = Guid.NewGuid();
        var intents = new List<PromptGalleryEditorIntent>();
        using var context = CreateContext();
        var cut = context.Render<PromptGalleryItemEditorSurface>(parameters => parameters
            .Add(component => component.Presentation, Ready(itemId, ScriptedPromptGalleryService.Details(itemId)) with { IsBusy = true })
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGalleryEditorIntent>(this, intents.Add)));

        cut.Find("form").Submit();
        cut.Find("[data-testid='prompt-gallery-editor-finalize']").Click();
        cut.Find("[data-testid='prompt-gallery-editor-archive']").Click();

        Assert.Empty(intents);
        Assert.True(cut.Find("fieldset").HasAttribute("disabled"));
    }

    [Fact]
    public void Persisted_items_expose_warning_preferences_and_archive_while_new_drafts_do_not()
    {
        var itemId = Guid.NewGuid();
        var intents = new List<PromptGalleryEditorIntent>();
        using var context = CreateContext();
        var cut = context.Render<PromptGalleryItemEditorSurface>(parameters => parameters
            .Add(component => component.Presentation, PromptGalleryEditorPresentation.CreateNew(1))
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGalleryEditorIntent>(this, intents.Add)));

        Assert.DoesNotContain("Compatibility warning preferences", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-editor-archive']"));
        Assert.Contains("New Gallery item", cut.Markup, StringComparison.Ordinal);

        cut.Render(parameters => parameters
            .Add(component => component.Presentation, Ready(itemId, ScriptedPromptGalleryService.Details(itemId)))
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGalleryEditorIntent>(this, intents.Add)));

        var suppressionCheckbox = cut.Find("input[aria-label='Suppress item-kind warnings for Chat']");
        suppressionCheckbox.Change(true);
        var suppression = Assert.IsType<PromptGalleryEditorIntent.SetWarningSuppression>(Assert.Single(intents));
        Assert.Equal(new PromptWarningSuppression(PromptGalleryConsumer.Chat, PromptCompatibilityIssueCode.ItemKindMismatch), suppression.Preference);
        Assert.True(suppression.Suppressed);

        cut.Find("[data-testid='prompt-gallery-editor-archive']").Click();
        Assert.IsType<PromptGalleryEditorIntent.ToggleArchive>(intents[1]);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static PromptGalleryEditorPresentation Ready(Guid itemId, PromptGalleryItemDetails details)
        => new(
            Generation: 1,
            itemId,
            PromptGalleryEditorPhase.Ready,
            PromptGalleryEditorSource.FromDetails(details),
            details.IsArchived,
            details.Versions,
            details.WarningSuppressions,
            IsBusy: false,
            FailureMessage: null,
            Warning: null);
}
