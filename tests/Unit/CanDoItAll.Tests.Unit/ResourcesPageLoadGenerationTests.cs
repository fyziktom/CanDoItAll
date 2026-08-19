using CanDoItAll.Modules.Resources.Pages;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class ResourcesPageLoadGenerationTests
{
    [Fact]
    public async Task Older_save_refresh_cannot_replace_a_newer_editor_selection()
    {
        var generation = new ResourcesPageLoadGeneration();
        var savedEditorCompletion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var selectedEditorCompletion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var committedEditors = new List<string>();

        var saveStamp = generation.Begin();
        var saveRefresh = ApplyWhenCompletedAsync(saveStamp, savedEditorCompletion.Task);
        var selectionStamp = generation.Begin();
        var selectionRefresh = ApplyWhenCompletedAsync(selectionStamp, selectedEditorCompletion.Task);

        selectedEditorCompletion.SetResult("resource-b");
        await selectionRefresh;
        savedEditorCompletion.SetResult("resource-a");
        await saveRefresh;

        Assert.Equal(["resource-b"], committedEditors);

        async Task ApplyWhenCompletedAsync(ResourcesPageLoadStamp stamp, Task<string> editorTask)
        {
            var loadedEditor = await editorTask;
            generation.TryCommit(stamp, () => committedEditors.Add(loadedEditor));
        }
    }

    [Fact]
    public void A_new_load_invalidates_every_older_stamp()
    {
        var generation = new ResourcesPageLoadGeneration();
        var first = generation.Begin();
        var second = generation.Begin();

        Assert.False(generation.IsCurrent(first));
        Assert.True(generation.IsCurrent(second));
        Assert.False(generation.TryCommit(first, () => { }));
        Assert.True(generation.TryCommit(second, () => { }));
    }
}
