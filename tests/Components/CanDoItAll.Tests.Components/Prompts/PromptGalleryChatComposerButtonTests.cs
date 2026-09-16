using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptGalleryChatComposerButtonTests
{
    [Fact]
    public async Task Selection_evaluates_chat_compatibility_and_emits_trimmed_content()
    {
        const string provider = "OpenAI";
        const string model = "gpt-5.4-mini";
        var gallery = new ScriptedPromptGalleryService();
        Guid? evaluatedArtifactId = null;
        PromptGalleryConsumerContext? compatibilityContext = null;
        gallery.EvaluateCompatibility = (id, context) =>
        {
            evaluatedArtifactId = id;
            compatibilityContext = context;
            return Task.FromResult(Result<PromptCompatibilityResult>.Success(new PromptCompatibilityResult([])));
        };
        using var context = CreateContext(gallery);
        string? selectedContent = null;
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, model)
            .Add(component => component.ContentSelected, content => selectedContent = content));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();
        var selection = CreateSelection("  Keep the answer concise.  ");

        Assert.Equal(provider, picker.Instance.Provider);
        Assert.Equal(model, picker.Instance.Model);
        Assert.True(picker.Instance.ShowActualChatModelFilter);

        await cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(selection));

        Assert.Equal(selection.ArtifactId, evaluatedArtifactId);
        var evaluated = Assert.IsType<PromptGalleryConsumerContext>(compatibilityContext);
        Assert.Equal(PromptGalleryConsumer.Chat, evaluated.Consumer);
        Assert.Equal(PromptGalleryCompatibilityPurpose.Selection, evaluated.Purpose);
        Assert.Equal(provider, evaluated.Provider);
        Assert.Equal(model, evaluated.Model);
        Assert.Equal("Keep the answer concise.", selectedContent);
    }

    [Fact]
    public async Task Incompatible_selection_opens_dialog_and_cancel_does_not_emit_content()
    {
        var gallery = new ScriptedPromptGalleryService();
        gallery.EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(new PromptCompatibilityResult(
        [
            new PromptCompatibilityIssue(
                PromptCompatibilityIssueCode.ConsumerNotSupported,
                PromptCompatibilitySeverity.Error,
                "This Gallery item does not support chat.",
                IsSuppressible: false,
                IsSuppressed: false)
        ])));
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        string? selectedContent = null;
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.ContentSelected, content => selectedContent = content));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();

        var selectionTask = cut.InvokeAsync(() =>
            picker.Instance.Selected.InvokeAsync(CreateSelection("Should not be inserted.")));

        host.WaitForElement("[data-testid='prompt-gallery-chat-compatibility-dialog']");
        // A blocking error offers no insert path, only Cancel.
        var cancel = Assert.Single(
            host.FindAll("[data-testid='prompt-compatibility-warning-dialog'] button"));
        Assert.Contains("Cancel", cancel.TextContent, StringComparison.Ordinal);
        Assert.Empty(host.FindAll("[data-testid='prompt-compatibility-insert']"));
        cancel.Click();
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Null(selectedContent);
        Assert.Empty(dialogService.Dialogs);
    }

    [Fact]
    public async Task Insert_and_suppress_persists_preferences_and_a_rejected_preference_does_not_block_insertion()
    {
        var gallery = new ScriptedPromptGalleryService();
        gallery.EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(new PromptCompatibilityResult(
        [
            new PromptCompatibilityIssue(
                PromptCompatibilityIssueCode.ProviderModelNotSupported,
                PromptCompatibilitySeverity.Warning,
                "The selected provider and model are not declared as supported.",
                IsSuppressible: true,
                IsSuppressed: false)
        ])));
        gallery.SetWarningSuppression = (_, _, _, _) => Task.FromResult(Result.Failure(
            Error.Failure("Preference store unavailable.", "prompts.compatibility.store-unavailable")));
        using var context = CreateContext(gallery);
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var host = context.Render<DialogHost>();
        string? selectedContent = null;
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.Provider, "OpenAI")
            .Add(component => component.Model, "gpt-5.4-mini")
            .Add(component => component.ContentSelected, content => selectedContent = content));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();
        var selection = CreateSelection("Insert with suppression.");

        var selectionTask = cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(selection));
        host.WaitForElement("[data-testid='prompt-compatibility-insert-suppress']").Click();
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        var write = Assert.Single(gallery.SuppressionWrites);
        Assert.Equal((selection.ArtifactId, PromptGalleryConsumer.Chat, PromptCompatibilityIssueCode.ProviderModelNotSupported, true), write);
        Assert.Contains(notifications.Messages, message => message.Summary == "Warning preference was not saved");
        Assert.Equal("Insert with suppression.", selectedContent);
    }

    [Fact]
    public async Task Provider_or_model_change_while_the_warning_is_open_drops_the_stale_selection()
    {
        var gallery = new ScriptedPromptGalleryService();
        gallery.EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(new PromptCompatibilityResult(
        [
            new PromptCompatibilityIssue(
                PromptCompatibilityIssueCode.ItemKindMismatch,
                PromptCompatibilitySeverity.Warning,
                "Kind mismatch.",
                IsSuppressible: true,
                IsSuppressed: false)
        ])));
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        string? selectedContent = null;
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.Provider, "OpenAI")
            .Add(component => component.Model, "gpt-5.4-mini")
            .Add(component => component.ContentSelected, content => selectedContent = content));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();

        var selectionTask = cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(CreateSelection("Stale target.")));
        host.WaitForElement("[data-testid='prompt-compatibility-insert']");

        cut.Render(parameters => parameters
            .Add(component => component.Provider, "OpenAI")
            .Add(component => component.Model, "gpt-5.4")
            .Add(component => component.ContentSelected, content => selectedContent = content));
        host.Find("[data-testid='prompt-compatibility-insert']").Click();
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Null(selectedContent);
        Assert.Empty(dialogService.Dialogs);
        Assert.Empty(gallery.SuppressionWrites);
    }

    [Fact]
    public async Task Disposing_the_composer_closes_its_warning_dialog_and_never_inserts()
    {
        var gallery = new ScriptedPromptGalleryService();
        gallery.EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(new PromptCompatibilityResult(
        [
            new PromptCompatibilityIssue(
                PromptCompatibilityIssueCode.ItemKindMismatch,
                PromptCompatibilitySeverity.Warning,
                "Kind mismatch.",
                IsSuppressible: true,
                IsSuppressed: false)
        ])));
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var unrelated = dialogService.OpenAsync(
            "Unrelated",
            _ => builder => builder.AddMarkupContent(0, "<p>Unrelated</p>"),
            new DialogOptions { TestId = "unrelated-dialog" });
        string? selectedContent = null;
        var probe = context.Render<ConditionalRenderHost>(parameters => parameters
            .AddChildContent<PromptGalleryChatComposerButton>(button => button
                .Add(component => component.ContentSelected, EventCallback.Factory.Create<string>(this, content => selectedContent = content))));
        var composer = probe.FindComponent<PromptGalleryChatComposerButton>();
        var picker = composer.FindComponent<PromptGalleryPickerButton>();

        var selectionTask = probe.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(CreateSelection("Disposed owner.")));
        host.WaitForElement("[data-testid='prompt-compatibility-insert']");
        var warning = dialogService.Dialogs.Single(dialog => dialog.Options.TestId == "prompt-gallery-chat-compatibility-dialog");

        await probe.InvokeAsync(() => probe.Instance.Hide());
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(warning.Result.IsCanceled);
        Assert.Single(dialogService.Dialogs);
        Assert.False(unrelated.IsCompleted);
        Assert.Null(selectedContent);
    }

    private static PromptGallerySelection CreateSelection(string content)
        => new(
            Guid.NewGuid(),
            VersionId: null,
            VersionNumber: null,
            "Reusable chat prompt",
            "A prompt for chat.",
            PromptGalleryItemKind.FullPrompt,
            content,
            Tags: [],
            SupportedModels: [],
            Recommendations: new PromptModelRecommendations());

    private static BunitContext CreateContext(ScriptedPromptGalleryService gallery)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);
        return context;
    }
}
