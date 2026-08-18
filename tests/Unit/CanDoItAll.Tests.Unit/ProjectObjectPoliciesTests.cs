using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectObjectPoliciesTests
{
    [Theory]
    [InlineData("task")]
    [InlineData(" Task ")]
    [InlineData("TASK")]
    public void Normalize_canonicalizes_work_item_task_subtype(string value)
    {
        var normalized = ProjectObjectSubtypePolicy.Normalize(ProjectObjectType.WorkItem, value);

        Assert.Equal(ProjectObjectSubtypePolicy.Task, normalized);
    }

    [Fact]
    public void Normalize_preserves_non_task_subtype_casing()
    {
        var normalized = ProjectObjectSubtypePolicy.Normalize(ProjectObjectType.WorkItem, " Feature ");

        Assert.Equal("Feature", normalized);
    }
}
