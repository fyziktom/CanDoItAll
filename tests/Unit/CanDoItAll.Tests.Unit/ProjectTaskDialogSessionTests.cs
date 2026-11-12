using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskDialogSessionTests {
    [Fact]
    public async Task A_known_assignment_conflict_requires_explicit_comparison_and_a_fresh_operation_identity() {
        var session = new ProjectTaskDialogSession(static () => true, static () => Task.CompletedTask,
            NullLogger.Instance, ["original-task"]);
        var attempts = 0;
        Task Save() {
            attempts++;
            throw new ProjectStructureTaskApplicationException(ProjectStructureTaskApplicationErrorCode.AssignmentConflict,
                "The original assignment changed before commit.");
        }
        var rejected = await session.SubmitAsync(Save);
        Assert.Equal(PlanningTaskSaveStatus.Rejected, rejected.Status);
        Assert.Empty(rejected.Phases);
        Assert.True(session.CanAcceptComparison);
        await session.SubmitAsync(Save);
        Assert.Equal(1, attempts);
        session.AcceptComparison();
        Assert.Null(session.Result);
        var corrected = await session.SubmitAsync(Save);
        Assert.Equal(2, attempts);
        Assert.Equal(PlanningTaskSaveStatus.Rejected, corrected.Status);
        Assert.NotEqual(rejected.OperationId, corrected.OperationId);
    }
}
