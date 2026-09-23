using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

// The picker button's context policy through the real button, the real picker dialog inside DialogHost and the real
// search host: a provider or model settling for a named target keeps the open picker, a genuine change of an already
// known provider or model retires it, and a retired interaction never reports a selection to the owner.
public sealed class PromptGalleryPickerButtonTests
{
    private const string Provider = "OpenAI";
    private const string FirstModel = "gpt-5.4-mini";
    private const string SecondModel = "gpt-5.4";
    private const string Target = "conversation-c";
    private static readonly Guid ItemId = Guid.Parse("7a000000-0000-0000-0000-000000000001");

    [Fact]
    public void Mature_model_change_for_the_same_target_retires_the_open_picker_and_the_next_picker_carries_the_new_model()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var selections = new List<PromptGallerySelection>();
        var cut = RenderButton(context, selections, Provider, FirstModel, Target);

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        var opened = Assert.Single(dialogService.Dialogs);
        Assert.Equal(FirstModel, (string?)opened.Parameters[nameof(PromptGalleryPickerDialog.Model)]);
        Assert.Equal(FirstModel, Assert.Single(host.FindComponents<PromptGallerySearchHost>()).Instance.Model);

        // The target is unchanged but its active model is no longer the one the picker was opened for.
        RenderButton(cut, selections, Provider, SecondModel, Target);

        Assert.False(cut.Instance.HasOpenInteraction);
        host.WaitForAssertion(() => Assert.Empty(dialogService.Dialogs));
        Assert.True(opened.Result.IsCanceled);
        host.WaitForAssertion(() => Assert.Empty(host.FindComponents<PromptGallerySearchHost>()));

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        var reopened = Assert.Single(dialogService.Dialogs);
        Assert.NotSame(opened, reopened);
        Assert.Equal(SecondModel, (string?)reopened.Parameters[nameof(PromptGalleryPickerDialog.Model)]);
        Assert.Equal(SecondModel, Assert.Single(host.FindComponents<PromptGallerySearchHost>()).Instance.Model);
        Assert.Empty(selections);
    }

    [Fact]
    public void Details_read_pending_across_a_model_change_never_reports_the_retired_selection()
    {
        var gallery = new ScriptedPromptGalleryService();
        var item = ScriptedPromptGalleryService.Item(ItemId, "Shared item");
        gallery.Search = (query, _) => Task.FromResult(ScriptedPromptGalleryService.Page(query, [item]));
        var firstRead = new TaskCompletionSource<Result<PromptGalleryItemDetails>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var versionReads = new List<Guid>();
        // The first details read stays pending until the test releases it; later reads answer at once. Both resolve
        // to a finalized item, so a read that is still treated as current would go on to fetch the version snapshot.
        gallery.GetItem = id => gallery.GetItemCalls.Count == 1
            ? firstRead.Task
            : Task.FromResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(id, currentVersionNumber: 1)));
        gallery.GetVersionSnapshotByNumber = (id, number) =>
        {
            versionReads.Add(id);
            return Task.FromResult(Result<PromptVersionSnapshot>.Success(ScriptedPromptGalleryService.Snapshot(id, number)));
        };
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var selections = new List<PromptGallerySelection>();
        var cut = RenderButton(context, selections, Provider, FirstModel, Target);

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        var opened = Assert.Single(dialogService.Dialogs);
        host.Find("[data-testid='prompt-gallery-select']").Click();
        cut.WaitForAssertion(() => Assert.Single(gallery.GetItemCalls));

        RenderButton(cut, selections, Provider, SecondModel, Target);
        Assert.False(cut.Instance.HasOpenInteraction);
        host.WaitForAssertion(() => Assert.Empty(dialogService.Dialogs));
        Assert.True(opened.Result.IsCanceled);

        // The retired read completes after the change; the new picker under the new model reports its own selection.
        firstRead.SetResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(ItemId, currentVersionNumber: 1)));
        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        host.Find("[data-testid='prompt-gallery-select']").Click();

        cut.WaitForAssertion(() => Assert.Single(selections));
        Assert.Equal(ItemId, selections[0].ArtifactId);
        Assert.Equal(2, gallery.GetItemCalls.Count);
        // The retired read stopped at its fence: only the current interaction fetched the version snapshot.
        Assert.Equal(new[] { ItemId }, versionReads);
        Assert.Empty(dialogService.Dialogs);
    }

    [Fact]
    public void Returning_to_the_previous_model_does_not_revive_the_retired_picker()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var selections = new List<PromptGallerySelection>();
        var cut = RenderButton(context, selections, Provider, FirstModel, Target);

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        var opened = Assert.Single(dialogService.Dialogs);

        RenderButton(cut, selections, Provider, SecondModel, Target);
        RenderButton(cut, selections, Provider, FirstModel, Target);

        Assert.False(cut.Instance.HasOpenInteraction);
        host.WaitForAssertion(() => Assert.Empty(dialogService.Dialogs));
        Assert.True(opened.Result.IsCanceled);

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        var reopened = Assert.Single(dialogService.Dialogs);
        Assert.NotSame(opened, reopened);
        Assert.Equal(FirstModel, (string?)reopened.Parameters[nameof(PromptGalleryPickerDialog.Model)]);
    }

    [Fact]
    public void Initial_configuration_of_the_named_target_keeps_the_picker_until_a_different_configured_pair_appears()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var selections = new List<PromptGallerySelection>();
        // The owner names its target with a fallback model before its provider options have loaded (the workflow
        // canvas), then replaces the fallback with the provider's default when the provider resolves.
        var cut = RenderButton(context, selections, provider: null, model: "fallback-model", Target);

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        var opened = Assert.Single(dialogService.Dialogs);

        RenderButton(cut, selections, Provider, model: string.Empty, Target);
        RenderButton(cut, selections, Provider, FirstModel, Target);

        Assert.True(cut.Instance.HasOpenInteraction);
        Assert.Same(opened, Assert.Single(dialogService.Dialogs));
        Assert.False(opened.Result.IsCompleted);

        // A known value going absent is unknown, not a change: the picker stays and the configured pair is remembered.
        RenderButton(cut, selections, Provider, model: null, Target);

        Assert.True(cut.Instance.HasOpenInteraction);
        Assert.Same(opened, Assert.Single(dialogService.Dialogs));

        // A different pair after the gap is still recognized as a change of the active context.
        RenderButton(cut, selections, Provider, SecondModel, Target);

        Assert.False(cut.Instance.HasOpenInteraction);
        host.WaitForAssertion(() => Assert.Empty(dialogService.Dialogs));
        Assert.True(opened.Result.IsCanceled);
    }

    [Fact]
    public void Without_a_target_key_the_provider_and_model_are_the_identity_and_their_resolution_retires_the_picker()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var selections = new List<PromptGallerySelection>();
        var cut = RenderButton(context, selections, provider: null, model: null, targetKey: null);

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        var opened = Assert.Single(dialogService.Dialogs);

        RenderButton(cut, selections, Provider, FirstModel, targetKey: null);

        Assert.False(cut.Instance.HasOpenInteraction);
        host.WaitForAssertion(() => Assert.Empty(dialogService.Dialogs));
        Assert.True(opened.Result.IsCanceled);
    }

    [Fact]
    public void A_consumer_change_for_the_same_target_and_model_retires_the_picker()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var selections = new List<PromptGallerySelection>();
        var cut = RenderButton(context, selections, Provider, FirstModel, Target, PromptGalleryConsumer.Workflow);

        cut.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForElement("[data-testid='prompt-gallery-select']");
        var opened = Assert.Single(dialogService.Dialogs);

        RenderButton(cut, selections, Provider, FirstModel, Target, PromptGalleryConsumer.Chat);

        Assert.False(cut.Instance.HasOpenInteraction);
        host.WaitForAssertion(() => Assert.Empty(dialogService.Dialogs));
        Assert.True(opened.Result.IsCanceled);
    }

    private static IRenderedComponent<PromptGalleryPickerButton> RenderButton(
        BunitContext context,
        List<PromptGallerySelection> selections,
        string? provider,
        string? model,
        object? targetKey,
        PromptGalleryConsumer consumer = PromptGalleryConsumer.Chat)
        => context.Render<PromptGalleryPickerButton>(parameters => Configure(parameters, selections, provider, model, targetKey, consumer));

    private static void RenderButton(
        IRenderedComponent<PromptGalleryPickerButton> cut,
        List<PromptGallerySelection> selections,
        string? provider,
        string? model,
        object? targetKey,
        PromptGalleryConsumer consumer = PromptGalleryConsumer.Chat)
        => cut.Render(parameters => Configure(parameters, selections, provider, model, targetKey, consumer));

    private static void Configure(
        ComponentParameterCollectionBuilder<PromptGalleryPickerButton> parameters,
        List<PromptGallerySelection> selections,
        string? provider,
        string? model,
        object? targetKey,
        PromptGalleryConsumer consumer)
        => parameters
            .Add(component => component.Consumer, consumer)
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, model)
            .Add(component => component.TargetKey, targetKey)
            .Add(component => component.Selected, selection => selections.Add(selection));

    private static BunitContext CreateContext(ScriptedPromptGalleryService gallery)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);
        return context;
    }
}
