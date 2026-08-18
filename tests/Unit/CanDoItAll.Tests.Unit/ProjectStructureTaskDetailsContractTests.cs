using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Workbench;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureTaskDetailsContractTests
{
    [Fact]
    public void Missing_current_cost_basis_snapshot_is_rejected_during_deserialization()
    {
        var json = Assert.IsType<JsonObject>(
            JsonSerializer.SerializeToNode(CreateRequest()));
        Assert.True(json.Remove(nameof(ProjectStructureTaskDetailsUpdateRequest.CurrentCostBasis)));

        var exception = Assert.Throws<JsonException>(() =>
            json.Deserialize<ProjectStructureTaskDetailsUpdateRequest>());

        Assert.Contains(
            nameof(ProjectStructureTaskDetailsUpdateRequest.CurrentCostBasis),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_null_current_cost_basis_snapshot_is_accepted()
    {
        var json = JsonSerializer.Serialize(CreateRequest());

        var request = JsonSerializer.Deserialize<ProjectStructureTaskDetailsUpdateRequest>(json);

        Assert.NotNull(request);
        Assert.Null(request.CurrentCostBasis);
    }

    [Theory]
    [InlineData(nameof(ProjectStructureTaskResourceAttachRequest.Resource))]
    [InlineData(nameof(ProjectStructureTaskResourceAttachRequest.CurrentExecution))]
    public void Missing_required_task_resource_attachment_snapshot_is_rejected_during_deserialization(
        string propertyName)
    {
        var json = Assert.IsType<JsonObject>(
            JsonSerializer.SerializeToNode(CreateAttachRequest()));
        Assert.True(json.Remove(propertyName));

        var exception = Assert.Throws<JsonException>(() =>
            json.Deserialize<ProjectStructureTaskResourceAttachRequest>());

        Assert.Contains(propertyName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Task_resource_attachment_boundary_rejects_null_required_values()
    {
        var service = new ProjectStructureTaskResourceAttachmentService(
            null!,
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<
                ProjectStructureTaskResourceAttachmentService>.Instance);
        var validRequest = CreateAttachRequest();
        var cases = new[]
        {
            (
                Request: (ProjectStructureTaskResourceAttachRequest)null!,
                ErrorCode: "TaskResourceAttachRequestRequired"),
            (
                Request: validRequest with { Resource = null! },
                ErrorCode: "TaskResourceRequired"),
            (
                Request: validRequest with { CurrentExecution = null! },
                ErrorCode: "TaskExecutionSnapshotRequired")
        };

        foreach (var testCase in cases)
        {
            var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                service.AttachAsync(
                    Guid.NewGuid(),
                    "task-1",
                    testCase.Request,
                    null!));

            Assert.Equal(400, exception.StatusCode);
            Assert.Equal(testCase.ErrorCode, exception.ErrorCode);
        }
    }

    private static ProjectStructureTaskDetailsUpdateRequest CreateRequest()
        => new(
            new GanttTaskId("task-1"),
            "Task",
            "Task",
            0,
            0,
            ProjectTaskEstimate.Empty(),
            ProjectTaskEstimate.Empty(),
            ScheduleChange: null,
            AssigneeChanged: false,
            ProposedAssignee: null,
            CurrentExecution: ProjectTaskExecutionSnapshot.Unknown,
            ProposedExecution: ProjectTaskExecutionSnapshot.Unknown,
            CurrentCostBasis: null,
            CurrentDirectAssignmentRevision: 0);

    private static ProjectStructureTaskResourceAttachRequest CreateAttachRequest()
        => new(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Process,
                Guid.Parse("10000000-0000-0000-0000-000000000001")),
            ProjectTaskExecutionSnapshot.NotStarted);
}
