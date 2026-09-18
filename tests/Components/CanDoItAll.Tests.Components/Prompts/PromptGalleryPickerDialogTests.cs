using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptGalleryPickerDialogTests
{
    [Fact]
    public async Task Edit_requested_override_closes_picker_before_invoking_callback()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        gallery.Search = (query, _) => Task.FromResult(ScriptedPromptGalleryService.Page(query, [ScriptedPromptGalleryService.Item(itemId)]));
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        Guid? receivedItemId = null;
        var dialogCountDuringCallback = -1;
        var pickerWasCompletedDuringCallback = false;
        Task<object?>? pickerTask = null;
        var editRequested = EventCallback.Factory.Create<Guid>(
            this,
            (Guid id) =>
            {
                receivedItemId = id;
                dialogCountDuringCallback = dialogService.Dialogs.Count;
                pickerWasCompletedDuringCallback = pickerTask?.IsCompleted is true;
            });

        pickerTask = dialogService.OpenAsync<PromptGalleryPickerDialog>(
            "Choose a prompt",
            new Dictionary<string, object?>
            {
                [nameof(PromptGalleryPickerDialog.Consumer)] = PromptGalleryConsumer.Workflow,
                [nameof(PromptGalleryPickerDialog.EditRequested)] = editRequested
            },
            new DialogOptions { TestId = "prompt-gallery-picker-dialog" });

        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='prompt-gallery-edit']")));

        host.Find("[data-testid='prompt-gallery-edit']").Click();
        await pickerTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(itemId, receivedItemId);
        Assert.Equal(0, dialogCountDuringCallback);
        Assert.True(pickerWasCompletedDuringCallback);
        Assert.Empty(dialogService.Dialogs);
    }

    [Fact]
    public async Task Selection_with_a_final_version_inserts_the_immutable_snapshot()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        gallery.Search = (query, _) => Task.FromResult(ScriptedPromptGalleryService.Page(query, [ScriptedPromptGalleryService.Item(itemId)]));
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(id, currentVersionNumber: 2)));
        var requestedVersions = new List<int>();
        gallery.GetVersionSnapshotByNumber = (id, number) =>
        {
            requestedVersions.Add(number);
            return Task.FromResult(Result<PromptVersionSnapshot>.Success(ScriptedPromptGalleryService.Snapshot(id, number)));
        };
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();

        var pickerTask = dialogService.OpenAsync<PromptGalleryPickerDialog>("Choose a prompt");
        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='prompt-gallery-select']")));
        host.Find("[data-testid='prompt-gallery-select']").Click();

        var selection = Assert.IsType<PromptGallerySelection>(await pickerTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, Assert.Single(requestedVersions));
        Assert.Equal(itemId, selection.ArtifactId);
        Assert.Equal(2, selection.VersionNumber);
        Assert.NotNull(selection.VersionId);
        Assert.Equal("Final content", selection.Content);
        Assert.Equal("Final title", selection.Title);
        Assert.Equal(0.1, selection.Recommendations.Temperature);
        Assert.Equal(["workflow"], selection.Tags);
    }

    [Fact]
    public void Missing_final_version_does_not_substitute_the_draft()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        gallery.Search = (query, _) => Task.FromResult(ScriptedPromptGalleryService.Page(query, [ScriptedPromptGalleryService.Item(itemId)]));
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(id, currentVersionNumber: 1)));
        gallery.GetVersionSnapshotByNumber = (_, _) => Task.FromResult(Result<PromptVersionSnapshot>.Failure(
            Error.Failure("Prompt version was not found.", "prompts.version.not-found")));
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var host = context.Render<DialogHost>();

        var pickerTask = dialogService.OpenAsync<PromptGalleryPickerDialog>("Choose a prompt");
        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='prompt-gallery-select']")));
        host.Find("[data-testid='prompt-gallery-select']").Click();

        host.WaitForAssertion(() => Assert.Contains(notifications.Messages, message => message.Summary == "Prompt version unavailable"));
        Assert.False(pickerTask.IsCompleted);
        Assert.Single(dialogService.Dialogs);
    }

    [Fact]
    public async Task Draft_only_item_inserts_the_draft_content()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        gallery.Search = (query, _) => Task.FromResult(ScriptedPromptGalleryService.Page(query, [ScriptedPromptGalleryService.Item(itemId)]));
        gallery.GetVersionSnapshotByNumber = (_, _) => throw new InvalidOperationException("A draft-only item must not request a version.");
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();

        var pickerTask = dialogService.OpenAsync<PromptGalleryPickerDialog>("Choose a prompt");
        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='prompt-gallery-select']")));
        host.Find("[data-testid='prompt-gallery-select']").Click();

        var selection = Assert.IsType<PromptGallerySelection>(await pickerTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Null(selection.VersionNumber);
        Assert.Null(selection.VersionId);
        Assert.Equal("Loaded content", selection.Content);
    }

    [Fact]
    public async Task Nested_editor_commit_refreshes_the_picker_list_and_leaves_unrelated_dialogs_open()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        gallery.Search = (query, _) => Task.FromResult(ScriptedPromptGalleryService.Page(query, [ScriptedPromptGalleryService.Item(itemId)]));
        gallery.SaveDraft = draft => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(draft.Id!.Value, DateTimeOffset.UnixEpoch.AddMinutes(1))));
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var unrelated = dialogService.OpenAsync(
            "Unrelated",
            _ => builder => builder.AddMarkupContent(0, "<p data-testid='unrelated-dialog-content'>Unrelated</p>"),
            new DialogOptions { TestId = "unrelated-dialog" });
        var pickerTask = dialogService.OpenAsync<PromptGalleryPickerDialog>("Choose a prompt");
        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='prompt-gallery-edit']")));
        var queriesBeforeEdit = gallery.Queries.Count;

        host.Find("[data-testid='prompt-gallery-edit']").Click();
        host.WaitForAssertion(() => Assert.Equal(3, dialogService.Dialogs.Count));
        var editor = host.FindComponent<PromptGalleryItemEditorHost>();
        editor.WaitForElement("[data-testid='prompt-gallery-editor-title']").Change("Edited from picker");
        await editor.Find("form").SubmitAsync();
        editor.WaitForAssertion(() => Assert.Single(gallery.SavedDrafts));

        await host.InvokeAsync(() => editor.Find("[data-testid='prompt-gallery-editor-cancel']").ClickAsync());

        host.WaitForAssertion(() => Assert.Equal(queriesBeforeEdit + 1, gallery.Queries.Count));
        Assert.Equal(2, dialogService.Dialogs.Count);
        Assert.False(unrelated.IsCompleted);
        Assert.False(pickerTask.IsCompleted);
        Assert.NotNull(host.Find("[data-testid='unrelated-dialog-content']"));
        Assert.Same(host.FindComponent<PromptGallerySearchHost>().Instance, host.FindComponent<PromptGallerySearchHost>().Instance);
    }

    [Fact]
    public void Disposing_the_picker_button_closes_only_its_own_dialog_and_never_reports_a_selection()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var selections = new List<PromptGallerySelection>();
        var unrelated = dialogService.OpenAsync(
            "Unrelated",
            _ => builder => builder.AddMarkupContent(0, "<p>Unrelated</p>"),
            new DialogOptions { TestId = "unrelated-dialog" });
        var probe = context.Render<ConditionalRenderHost>(parameters => parameters
            .AddChildContent<PromptGalleryPickerButton>(button => button
                .Add(component => component.Selected, EventCallback.Factory.Create<PromptGallerySelection>(this, selections.Add))));

        probe.Find("[data-testid='prompt-gallery-picker-button']").Click();
        host.WaitForAssertion(() => Assert.Equal(2, dialogService.Dialogs.Count));
        var picker = dialogService.Dialogs.Single(dialog => dialog.ComponentType == typeof(PromptGalleryPickerDialog));

        probe.InvokeAsync(() => probe.Instance.Hide()).GetAwaiter().GetResult();

        Assert.True(picker.Result.IsCanceled);
        Assert.Single(dialogService.Dialogs);
        Assert.False(unrelated.IsCompleted);
        Assert.Empty(selections);
    }

    private static BunitContext CreateContext(ScriptedPromptGalleryService gallery)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);
        return context;
    }
}
