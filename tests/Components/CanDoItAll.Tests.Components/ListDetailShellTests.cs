using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ListDetailShellTests
{
    [Fact]
    public void Explicit_min_height_classes_replace_the_default_pane_min_height()
    {
        using var context = new TestContext();
        RenderFragment listContent = builder => builder.AddMarkupContent(0, "<div>List</div>");
        RenderFragment detailContent = builder => builder.AddMarkupContent(0, "<div>Detail</div>");
        var cut = context.RenderComponent<ListDetailShell>(parameters => parameters
            .Add(component => component.ListPaneClass, "h-full min-h-0")
            .Add(component => component.DetailPaneClass, "h-full min-h-0")
            .Add(component => component.ListContent, listContent)
            .Add(component => component.DetailContent, detailContent));

        var sections = cut.FindAll("section");
        Assert.Equal(2, sections.Count);

        foreach (var section in sections)
        {
            Assert.Contains("min-h-0", section.ClassName, StringComparison.Ordinal);
            Assert.DoesNotContain("min-h-[26rem]", section.ClassName, StringComparison.Ordinal);
        }
    }
}
