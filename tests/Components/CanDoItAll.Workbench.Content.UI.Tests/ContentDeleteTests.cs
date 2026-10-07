using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Content.UI.Analysis;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkbenchContent;

public sealed class ContentDeleteTests {
    [Theory]
    [InlineData(1, 0, 1, "Delete node only", "Delete node and file")]
    [InlineData(1, 0, 2, "Delete node only", "Delete node and files")]
    [InlineData(1, 2, 1, "Delete nodes only", "Delete nodes and files")]
    [InlineData(2, 0, 1, "Delete nodes only", "Delete nodes and files")]
    public async Task Managed_files_require_explicit_disposition(int selected, int descendants, int files,
        string retainLabel, string deleteLabel) {
        using var context = Context();
        var choices = new List<ContentStorageDisposition>();
        var cut = context.Render<ContentDeleteDialog>(p => p
            .Add(c => c.State, new("Original node", "Only eligible owned managed files can be removed.", selected, descendants, files, "Admission denied."))
            .Add(c => c.Actions, new(choice => {
                choices.Add(choice);
                return Task.CompletedTask;
            }, () => Task.CompletedTask)));
        Assert.Contains(retainLabel, cut.Markup, StringComparison.Ordinal);
        Assert.Contains(deleteLabel, cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Admission denied.", cut.Find("[data-testid='project-structure-delete-failure']").TextContent);
        await cut.Find("[data-testid='content-delete-retain']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='content-delete-files']").ClickAsync(new MouseEventArgs());
        Assert.Equal([ContentStorageDisposition.RetainManagedFiles, ContentStorageDisposition.DeleteOwnedManagedFiles], choices);
    }

    [Fact]
    public async Task Retained_button_callback_cannot_rebind_to_a_successor() {
        using var context = Context();
        var original = 0;
        var successor = 0;
        var cut = context.Render<ContentDeleteDialog>(p => p
            .Add(c => c.State, new("Original", "Original impact", 1, 0, 0))
            .Add(c => c.Actions, new(_ => {
                original++;
                return Task.CompletedTask;
            }, () => Task.CompletedTask)));
        var retained = cut.FindComponents<Button>().Single(c => c.Instance.Text == "Delete").Instance.Click;
        cut.Render(p => p.Add(c => c.State, new("Successor", "Successor impact", 2, 0, 0))
            .Add(c => c.Actions, new(_ => {
                successor++;
                return Task.CompletedTask;
            }, () => Task.CompletedTask)));
        await cut.InvokeAsync(() => retained.InvokeAsync(new MouseEventArgs()));
        await cut.Find("[data-testid='content-delete-cancel']").ClickAsync(new MouseEventArgs());
        Assert.Equal(1, original);
        Assert.Equal(0, successor);
        Assert.Empty(cut.FindAll("[data-testid='content-delete-retain']"));
        Assert.Contains("Delete selected", cut.Markup, StringComparison.Ordinal);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
