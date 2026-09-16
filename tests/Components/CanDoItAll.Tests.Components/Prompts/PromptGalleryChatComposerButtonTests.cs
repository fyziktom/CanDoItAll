using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Compatibility;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptGalleryChatComposerButtonTests
{
    private const string Provider = "OpenAI";
    private const string Model = "gpt-5.4-mini";

    [Fact]
    public async Task Selection_evaluates_chat_compatibility_and_emits_trimmed_content()
    {
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
            .Add(component => component.Provider, Provider)
            .Add(component => component.Model, Model)
            .Add(component => component.ContentSelected, content => selectedContent = content));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();
        var selection = CreateSelection("  Keep the answer concise.  ");

        Assert.Equal(Provider, picker.Instance.Provider);
        Assert.Equal(Model, picker.Instance.Model);
        Assert.True(picker.Instance.ShowActualChatModelFilter);

        await cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(selection));

        Assert.Equal(selection.ArtifactId, evaluatedArtifactId);
        var evaluated = Assert.IsType<PromptGalleryConsumerContext>(compatibilityContext);
        Assert.Equal(PromptGalleryConsumer.Chat, evaluated.Consumer);
        Assert.Equal(PromptGalleryCompatibilityPurpose.Selection, evaluated.Purpose);
        Assert.Equal(Provider, evaluated.Provider);
        Assert.Equal(Model, evaluated.Model);
        Assert.Equal("Keep the answer concise.", selectedContent);
    }

    [Fact]
    public async Task Incompatible_selection_opens_dialog_and_cancel_does_not_emit_content()
    {
        var gallery = new ScriptedPromptGalleryService { EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(Blocking())) };
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
    public async Task Warning_dialog_cancel_inserts_nothing_and_writes_no_preference()
    {
        var gallery = new ScriptedPromptGalleryService { EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(Warning())) };
        using var context = CreateContext(gallery);
        var host = context.Render<DialogHost>();
        string? selectedContent = null;
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.ContentSelected, content => selectedContent = content));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();

        var selectionTask = cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(CreateSelection("Not consented.")));
        host.WaitForElement("[data-testid='prompt-compatibility-cancel']").Click();
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Null(selectedContent);
        Assert.Empty(gallery.SuppressionWrites);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejected_or_thrown_preference_write_is_reported_separately_and_the_consented_insertion_happens_once(bool thrown)
    {
        var gallery = new ScriptedPromptGalleryService { EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(Warning())) };
        gallery.SetWarningSuppression = (_, _, _, _) => thrown
            ? Task.FromException<Result>(new IOException("Preference store offline."))
            : Task.FromResult(Result.Failure(Error.Failure("Preference store unavailable.", "prompts.compatibility.store-unavailable")));
        using var context = CreateContext(gallery);
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.Provider, Provider)
            .Add(component => component.Model, Model)
            .Add(component => component.ContentSelected, content => inserted.Add(content)));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();
        var selection = CreateSelection("Insert with suppression.");

        var selectionTask = cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(selection));
        host.WaitForElement("[data-testid='prompt-compatibility-insert-suppress']").Click();
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        var write = Assert.Single(gallery.SuppressionWrites);
        Assert.Equal((selection.ArtifactId, PromptGalleryConsumer.Chat, PromptCompatibilityIssueCode.ProviderModelNotSupported, true), write);
        Assert.Single(notifications.Messages, message => message.Summary == "Warning preference was not saved");
        Assert.DoesNotContain(notifications.Messages, message => message.Summary == "Prompt could not be inserted");
        Assert.Equal(["Insert with suppression."], inserted);
    }

    [Fact]
    public async Task Partial_preference_outcome_across_multiple_issues_inserts_once_without_replay()
    {
        var gallery = new ScriptedPromptGalleryService
        {
            EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(new PromptCompatibilityResult(
            [
                Issue(PromptCompatibilityIssueCode.ProviderModelNotSupported),
                Issue(PromptCompatibilityIssueCode.ItemKindMismatch)
            ])))
        };
        var writes = 0;
        gallery.SetWarningSuppression = (_, _, _, _) => ++writes == 1
            ? Task.FromException<Result>(new IOException("Preference store hiccup."))
            : Task.FromResult(Result.Success());
        using var context = CreateContext(gallery);
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.ContentSelected, content => inserted.Add(content)));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();

        var selectionTask = cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(CreateSelection("Insert once.")));
        host.WaitForElement("[data-testid='prompt-compatibility-insert-suppress']").Click();
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            [PromptCompatibilityIssueCode.ProviderModelNotSupported, PromptCompatibilityIssueCode.ItemKindMismatch],
            gallery.SuppressionWrites.Select(write => write.Code));
        Assert.Single(notifications.Messages, message => message.Summary == "Warning preference was not saved");
        Assert.Equal(["Insert once."], inserted);
    }

    [Theory]
    [InlineData(PromptCompatibilityWarningDecision.InsertAnyway)]
    [InlineData(PromptCompatibilityWarningDecision.InsertAndSuppress)]
    public async Task Blocking_compatibility_ignores_a_forced_insert_decision(PromptCompatibilityWarningDecision forced)
    {
        var gallery = new ScriptedPromptGalleryService { EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(Blocking())) };
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.ContentSelected, content => inserted.Add(content)));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();

        var selectionTask = cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(CreateSelection("Must not be inserted.")));
        host.WaitForElement("[data-testid='prompt-gallery-chat-compatibility-dialog']");
        var warning = Assert.Single(dialogService.Dialogs);
        // A direct or future callback path supplies a decision the normal surface never offers for a blocking error.
        await cut.InvokeAsync(() => dialogService.CloseAsync(warning, forced));
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(inserted);
        Assert.Empty(gallery.SuppressionWrites);
        Assert.Contains(notifications.Messages, message => message.Summary == "Prompt is not compatible");
    }

    [Fact]
    public void Context_change_while_the_real_picker_is_open_closes_it_without_a_selection()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = RenderComposer(context, inserted, targetKey: 1L);
        var unrelated = dialogService.OpenAsync("Unrelated", _ => builder => builder.AddMarkupContent(0, "<p>Unrelated</p>"), new DialogOptions { TestId = "unrelated-dialog" });

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        var pickerDialog = dialogService.Dialogs.Single(dialog => dialog.ComponentType == typeof(PromptGalleryPickerDialog));

        // Same provider and model, different conversation: the picker belongs to the old target.
        RenderComposer(cut, inserted, targetKey: 2L);

        cut.WaitForAssertion(() => Assert.True(pickerDialog.Result.IsCanceled));
        Assert.Single(dialogService.Dialogs);
        Assert.False(unrelated.IsCompleted);
        Assert.Empty(inserted);
        Assert.Empty(notifications.Messages);

        // The new context can start its own picker once the obsolete interaction has been released.
        var picker = cut.FindComponent<PromptGalleryPickerButton>();
        cut.WaitForAssertion(() => Assert.False(picker.Instance.HasOpenInteraction));
        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        Assert.Equal(2, dialogService.Dialogs.Count);
    }

    [Fact]
    public async Task Context_change_while_the_selection_details_read_is_pending_drops_the_selection()
    {
        var gallery = new ScriptedPromptGalleryService();
        var pendingDetails = new TaskCompletionSource<Result<PromptGalleryItemDetails>>();
        gallery.GetItem = _ => pendingDetails.Task;
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = RenderComposer(context, inserted, targetKey: "conversation-a");

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']").Click();
        cut.WaitForAssertion(() => Assert.Single(gallery.GetItemCalls));

        RenderComposer(cut, inserted, targetKey: "conversation-b");
        cut.WaitForAssertion(() => Assert.Empty(dialogService.Dialogs));
        await cut.InvokeAsync(() => pendingDetails.SetResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(gallery.GetItemCalls[0]))));
        await cut.InvokeAsync(() => Task.CompletedTask);

        Assert.Empty(inserted);
        Assert.Empty(notifications.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Context_change_while_compatibility_is_pending_ignores_its_late_success_or_failure(bool fail)
    {
        var gallery = new ScriptedPromptGalleryService();
        var pendingCompatibility = new TaskCompletionSource<Result<PromptCompatibilityResult>>();
        gallery.EvaluateCompatibility = (_, _) => pendingCompatibility.Task;
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = RenderComposer(context, inserted, targetKey: 1L);

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']").Click();
        cut.WaitForAssertion(() => Assert.Empty(dialogService.Dialogs));

        RenderComposer(cut, inserted, provider: "Anthropic", model: "claude-sonnet-5", targetKey: 1L);
        await cut.InvokeAsync(() =>
        {
            if (fail)
            {
                pendingCompatibility.SetException(new IOException("Late compatibility failure."));
            }
            else
            {
                pendingCompatibility.SetResult(Result<PromptCompatibilityResult>.Success(Warning()));
            }
        });
        await cut.InvokeAsync(() => Task.CompletedTask);

        Assert.Empty(dialogService.Dialogs);
        Assert.Empty(inserted);
        Assert.Empty(notifications.Messages);
        Assert.Empty(gallery.SuppressionWrites);
    }

    [Fact]
    public async Task Context_A_B_A_keeps_the_original_interaction_obsolete()
    {
        var gallery = new ScriptedPromptGalleryService();
        var pendingCompatibility = new TaskCompletionSource<Result<PromptCompatibilityResult>>();
        gallery.EvaluateCompatibility = (_, _) => pendingCompatibility.Task;
        using var context = CreateContext(gallery);
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = RenderComposer(context, inserted, targetKey: "a");

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']").Click();
        cut.WaitForAssertion(() => Assert.Empty(context.Services.GetRequiredService<DialogService>().Dialogs));

        RenderComposer(cut, inserted, targetKey: "b");
        RenderComposer(cut, inserted, targetKey: "a");
        await cut.InvokeAsync(() => pendingCompatibility.SetResult(Result<PromptCompatibilityResult>.Success(new PromptCompatibilityResult([]))));
        await cut.InvokeAsync(() => Task.CompletedTask);

        Assert.Empty(inserted);
    }

    [Fact]
    public void Same_context_rerender_keeps_the_open_picker_and_the_selection_is_inserted()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = RenderComposer(context, inserted, targetKey: 7L);

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        RenderComposer(cut, inserted, targetKey: 7L);
        Assert.Single(dialogService.Dialogs);

        host.Find("[data-testid='prompt-gallery-select']").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Loaded content"], inserted));
        Assert.Empty(dialogService.Dialogs);
    }

    [Fact]
    public async Task Context_change_during_the_preference_write_inserts_nothing()
    {
        var gallery = new ScriptedPromptGalleryService { EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(Warning())) };
        var pendingWrite = new TaskCompletionSource<Result>();
        gallery.SetWarningSuppression = (_, _, _, _) => pendingWrite.Task;
        using var context = CreateContext(gallery);
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = RenderComposer(context, inserted, targetKey: 1L);
        var picker = cut.FindComponent<PromptGalleryPickerButton>();

        var selectionTask = cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(CreateSelection("Stale after write.")));
        host.WaitForElement("[data-testid='prompt-compatibility-insert-suppress']").Click();
        cut.WaitForAssertion(() => Assert.Single(gallery.SuppressionWrites));

        RenderComposer(cut, inserted, targetKey: 2L);
        await cut.InvokeAsync(() => pendingWrite.SetResult(Result.Success()));
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(inserted);
        Assert.Empty(notifications.Messages);
    }

    [Fact]
    public async Task Provider_or_model_change_while_the_warning_is_open_closes_it_and_drops_the_selection()
    {
        var gallery = new ScriptedPromptGalleryService { EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(Warning())) };
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var inserted = new List<string>();
        var cut = RenderComposer(context, inserted, targetKey: 1L);
        var picker = cut.FindComponent<PromptGalleryPickerButton>();

        var selectionTask = cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(CreateSelection("Stale target.")));
        host.WaitForElement("[data-testid='prompt-compatibility-insert']");
        var warning = Assert.Single(dialogService.Dialogs);

        RenderComposer(cut, inserted, provider: Provider, model: "gpt-5.4", targetKey: 1L);
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(warning.Result.IsCanceled);
        Assert.Empty(dialogService.Dialogs);
        Assert.Empty(inserted);
        Assert.Empty(gallery.SuppressionWrites);
    }

    [Fact]
    public async Task Disposing_the_composer_closes_its_warning_dialog_and_never_inserts()
    {
        var gallery = new ScriptedPromptGalleryService { EvaluateCompatibility = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(Warning())) };
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

    private static IRenderedComponent<PromptGalleryChatComposerButton> RenderComposer(
        BunitContext context,
        List<string> inserted,
        object? targetKey,
        string? provider = Provider,
        string? model = Model)
        => context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, model)
            .Add(component => component.TargetKey, targetKey)
            .Add(component => component.ContentSelected, content => inserted.Add(content)));

    private static void RenderComposer(
        IRenderedComponent<PromptGalleryChatComposerButton> cut,
        List<string> inserted,
        object? targetKey,
        string? provider = Provider,
        string? model = Model)
        => cut.Render(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, model)
            .Add(component => component.TargetKey, targetKey)
            .Add(component => component.ContentSelected, content => inserted.Add(content)));

    private static PromptCompatibilityResult Blocking()
        => new(
        [
            new PromptCompatibilityIssue(
                PromptCompatibilityIssueCode.ConsumerNotSupported,
                PromptCompatibilitySeverity.Error,
                "This Gallery item does not support chat.",
                IsSuppressible: false,
                IsSuppressed: false)
        ]);

    private static PromptCompatibilityResult Warning()
        => new([Issue(PromptCompatibilityIssueCode.ProviderModelNotSupported)]);

    private static PromptCompatibilityIssue Issue(PromptCompatibilityIssueCode code)
        => new(code, PromptCompatibilitySeverity.Warning, $"{code} warning.", IsSuppressible: true, IsSuppressed: false);

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
