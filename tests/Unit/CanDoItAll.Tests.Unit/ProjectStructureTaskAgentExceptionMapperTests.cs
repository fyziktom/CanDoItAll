using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureTaskAgentExceptionMapperTests
{
    [Fact]
    public void Details_concurrency_conflict_maps_to_http_conflict()
    {
        var exception = new ProjectStructureTaskDetailsException(
            ProjectStructureTaskDetailsErrorCode.ConcurrencyConflict,
            "The task changed.");

        var mapped = ProjectStructureTaskAgentExceptionMapper.Map(exception);

        Assert.Equal(409, mapped.StatusCode);
        Assert.Equal(
            nameof(ProjectStructureTaskDetailsErrorCode.ConcurrencyConflict),
            mapped.ErrorCode);
    }
}
