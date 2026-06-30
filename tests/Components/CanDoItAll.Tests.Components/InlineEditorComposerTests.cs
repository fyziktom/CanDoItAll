using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class InlineEditorComposerTests
{
    [Fact]
    public void Factory_projects_inline_note_state()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes = [new CanvasWorkbenchNode { Id = "alpha", IsInlineTextNode = true, InlineText = "Draft", InlineTextPlaceholder = "Capture note" }],
            Chrome = new CanvasWorkbenchChrome { ChildNoteActionId = "note-child", SiblingNoteActionId = "note-sibling" }
        };

        var snapshot = InlineEditorComposerFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsEnabled);
        Assert.Equal("Capture note", snapshot.Placeholder);
    }

    [Fact]
    public void Component_renders_inline_editor_shell()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<InlineEditorComposer>(
            parameters => parameters.Add(component => component.Snapshot, new InlineEditorComposerSnapshot
            {
                Title = "Inline note and quick-edit flows now have a reusable editor host instead of only existing inside runtime branches",
                Summary = "Inline editing stays shared.",
                StatePill = "Enabled",
                Metrics = ["1 inline text nodes"],
                DraftLabel = "Draft note",
                Placeholder = "Capture note",
                SubmitLabel = "Save draft",
                IsEnabled = true
            }));

        Assert.Contains("Inline note and quick-edit flows now have a reusable editor host", cut.Markup);
        Assert.Contains("Save draft", cut.Markup);
    }
}


