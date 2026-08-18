using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructurePageTitleBuilderTests
{
    [Fact]
    public void Build_uses_project_structure_fallback_when_project_name_is_missing()
    {
        Assert.Equal("PS - Project Structure", ProjectStructurePageTitleBuilder.Build(" "));
    }

    [Fact]
    public void Build_truncates_long_project_names_with_ellipsis()
    {
        var title = ProjectStructurePageTitleBuilder.Build("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

        Assert.Equal("PS - ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrs...", title);
    }
}
