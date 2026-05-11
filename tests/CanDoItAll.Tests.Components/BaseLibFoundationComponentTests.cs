using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Tests.Components;

public sealed class BaseLibFoundationComponentTests
{
    [Fact]
    public void TreeView_RendersEmptyStateWhenNoItems()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<TreeView>(parameters => parameters
            .Add(component => component.Items, [])
            .Add(component => component.EmptyText, "No hierarchy yet"));

        Assert.Contains("No hierarchy yet", cut.Markup);
        Assert.NotNull(cut.Find(".cda-treeview__empty"));
    }

    [Fact]
    public void TreeView_TogglesBranchFromKeyboardArrows()
    {
        using var context = new TestContext();
        string? toggled = null;
        var items = new[]
        {
            new TreeViewNode
            {
                Id = "root",
                Text = "Root",
                Children =
                [
                    new TreeViewNode
                    {
                        Id = "child",
                        Text = "Child"
                    }
                ]
            }
        };

        var cut = context.RenderComponent<TreeView>(parameters => parameters
            .Add(component => component.Items, items)
            .Add(component => component.OnToggle, id => toggled = id));

        cut.Find("[title='Root']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("root", toggled);
    }

    [Fact]
    public void DialogScaffold_RendersRailMainReviewAndStickyFooter()
    {
        using var context = new TestContext();
        RenderFragment rail = builder => builder.AddMarkupContent(0, "<span>Mode</span>");
        RenderFragment review = builder => builder.AddMarkupContent(0, "<span>Impact</span>");
        RenderFragment footer = builder => builder.AddMarkupContent(0, "<button>Confirm</button>");

        var cut = context.RenderComponent<DialogScaffold>(parameters => parameters
            .Add(component => component.Title, "Send transaction")
            .Add(component => component.ContextRail, rail)
            .Add(component => component.ReviewPanel, review)
            .Add(component => component.Footer, footer)
            .AddChildContent("<p>Composer</p>"));

        Assert.Contains("Send transaction", cut.Markup);
        Assert.NotNull(cut.Find(".cda-dialog-scaffold__rail"));
        Assert.NotNull(cut.Find(".cda-dialog-scaffold__main"));
        Assert.NotNull(cut.Find(".cda-dialog-scaffold__review"));
        Assert.NotNull(cut.Find(".cda-dialog-scaffold__footer"));
    }

    [Fact]
    public void FoundationPrimitives_RenderTimelineDiffPickerAndStatus()
    {
        using var context = new TestContext();

        var timeline = context.RenderComponent<Timeline>(parameters => parameters
            .Add(component => component.Items,
            [
                new TimelineItem
                {
                    Label = "Created",
                    Description = "Ledger entry accepted.",
                    Tone = "success"
                }
            ]));
        var diff = context.RenderComponent<DiffViewer>(parameters => parameters
            .Add(component => component.BeforeText, "{\"status\":\"draft\"}")
            .Add(component => component.AfterText, "{\"status\":\"approved\"}"));
        var picker = context.RenderComponent<EntityPicker>(parameters => parameters
            .Add(component => component.Items,
            [
                new EntityPickerItem
                {
                    Id = "alice",
                    Label = "Alice"
                }
            ]));
        var checks = context.RenderComponent<StatusCheckList>(parameters => parameters
            .Add(component => component.Items,
            [
                new StatusCheckItem
                {
                    Label = "Route configured",
                    Status = "Pass",
                    Tone = "success",
                    Complete = true
                }
            ]));

        Assert.Contains("Ledger entry accepted.", timeline.Markup);
        Assert.Contains("1 changed line(s)", diff.Markup);
        Assert.Contains("Alice", picker.Markup);
        Assert.Contains("Route configured", checks.Markup);
    }

    [Fact]
    public void CopyableMonoValue_RendersTruncatedValueAndCopyButton()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<CopyableMonoValue>(parameters => parameters
            .Add(component => component.Value, "0123456789abcdef0123456789abcdef")
            .Add(component => component.StartChars, 6)
            .Add(component => component.EndChars, 4));

        Assert.Contains("012345...cdef", cut.Markup);
        Assert.Contains("Copy value", cut.Markup);
    }
}
