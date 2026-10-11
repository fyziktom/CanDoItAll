using System.Text.Json;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureReadScheduleContractTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Compact_read_preserves_the_schedule_snapshot_required_by_task_updates(bool scheduled) {
        DateTimeOffset? start = scheduled ? new DateTimeOffset(2027, 2, 5, 9, 12, 34, TimeSpan.FromHours(2)).AddTicks(1234560) : null;
        var end = start?.AddHours(3).AddSeconds(17);
        int? duration = scheduled ? 10817 : null;
        var node = new ProjectStructureNodeSummary(
            Id: "custom:9e253ab578de4af79630346fa6b8147e", ParentId: null,
            ObjectType: ProjectObjectType.WorkItem, ObjectSubtype: "task", Title: "Schedule contract",
            Subtitle: "", Status: "Draft", Notes: null, Route: "", ArtifactKind: "", ArtifactId: null,
            MediaRelativePath: null, MediaContentType: null, MediaOriginalFileName: null, Badges: [],
            ProgressMode: "progress", ProgressPercent: 0, MarkerIcon: "", MarkerTone: "", MarkerLabel: "",
            Priority: 0, EffectivePriority: 0, StartUtc: start, EndUtc: end, MetadataJson: "{}",
            ProjectRole: ProjectStructureProjectRole.None, RelatedProjectId: null, ParentProjectCount: 0,
            X: null, Y: null, DurationSeconds: duration);
        var response = new ProjectStructureReadToolData(Guid.NewGuid(), "Schedule project",
            [ProjectStructureAgentToolResponseProjection.MapCompactNode(node)], [], []);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var visibleNode = document.RootElement.GetProperty("nodes")[0];
        var startValue = visibleNode.GetProperty("startUtc");
        var endValue = visibleNode.GetProperty("endUtc");
        if (!scheduled) {
            Assert.Equal(JsonValueKind.Null, startValue.ValueKind);
            Assert.Equal(JsonValueKind.Null, endValue.ValueKind);
            Assert.Equal(JsonValueKind.Null, visibleNode.GetProperty("durationSeconds").ValueKind);
            return;
        }

        var change = new ProjectStructureTaskDateAgentChange(node.Id,
            startValue.GetDateTimeOffset(), endValue.GetDateTimeOffset(),
            start!.Value.AddDays(1), end!.Value.AddDays(1)).ToRequest();

        Assert.Equal(new GanttTaskId(node.Id), change.TaskId);
        Assert.Equal(start.Value, change.PreviousStart);
        Assert.Equal(end.Value, change.PreviousEnd);
        Assert.Equal(start.Value.Offset, change.PreviousStart.Offset);
        Assert.Equal(duration, visibleNode.GetProperty("durationSeconds").GetInt32());
    }
}
