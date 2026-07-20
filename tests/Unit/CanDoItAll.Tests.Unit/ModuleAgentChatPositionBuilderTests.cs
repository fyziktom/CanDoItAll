using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Tests.Unit;

public sealed class ModuleAgentChatPositionBuilderTests
{
    [Fact]
    public void Projects_position_identifies_the_selected_project()
    {
        var project = new ProjectSummary(
            Guid.NewGuid(),
            "Quotation delivery",
            ProjectStatus.Active,
            "Implementation",
            4,
            1,
            2,
            DateTimeOffset.UtcNow);

        var position = ProjectsAgentChatContextBuilder.BuildPosition(project);

        Assert.Equal("/projects", position.Route);
        Assert.Equal("portfolio", position.Surface);
        Assert.Equal(project.Id.ToString("D"), position.PrimarySelection?.Id);
        Assert.Contains(position.Facts, fact => fact.Name == "phase" && fact.Value == "Implementation");
    }

    [Fact]
    public void Project_structure_position_replaces_canvas_with_gantt_and_bounds_selection()
    {
        var projectId = Guid.NewGuid();
        var position = ProjectStructureAgentChatContextBuilder.BuildPosition(
            projectId,
            "Delivery",
            ProjectStructureAgentChatView.Gantt,
            [
                new AgentChatContextEntityReference("project-node", "node-b", "Beta"),
                new AgentChatContextEntityReference("project-node", "node-a", "Alpha"),
                new AgentChatContextEntityReference("project-node", "node-a", "Duplicate")
            ]);

        Assert.Equal("gantt", position.View);
        Assert.Equal($"/projects/{projectId:D}/structure", position.Route);
        Assert.Equal(["node-a", "node-b"], position.SelectedEntities.Select(entity => entity.Id));
        Assert.Equal(["Alpha", "Beta"], position.SelectedEntities.Select(entity => entity.DisplayName));
    }

    [Fact]
    public void Project_calendar_position_keeps_the_exact_route_and_canonical_node_key()
    {
        var projectId = Guid.NewGuid();
        var selectedEntities = ProjectStructureAgentChatContextBuilder.BuildSelectedEntities(
            selectedNodes: null,
            [new AgentChatContextEntityReference("project-node", "work-item:machine-details", "Machine details")]);

        var position = ProjectStructureAgentChatContextBuilder.BuildPosition(
            projectId,
            "Quotation",
            ProjectStructureAgentChatView.Calendar,
            selectedEntities);

        Assert.Equal("projects", position.Module);
        Assert.Equal("project-calendar", position.Surface);
        Assert.Equal("calendar", position.View);
        Assert.Equal($"/projects/{projectId:D}/calendar", position.Route);
        Assert.Equal(projectId.ToString("D"), position.PrimarySelection?.Id);
        var selectedNode = Assert.Single(position.SelectedEntities);
        Assert.Equal("project-node", selectedNode.Kind);
        Assert.Equal("work-item:machine-details", selectedNode.Id);
        Assert.Equal("Machine details", selectedNode.DisplayName);
    }

    [Fact]
    public void Scheduler_position_includes_selection_without_schedule_payload()
    {
        var plan = new SchedulerPlanSummary(
            Guid.NewGuid(),
            "Morning intake",
            "Sensitive schedule description",
            SchedulerPlanTargetKind.Process,
            Guid.NewGuid(),
            null,
            "Intake process",
            "0 0 9 ? * MON-FRI",
            "Every weekday",
            "UTC",
            SchedulerPlanMisfirePolicy.FireOnceNow,
            true,
            null,
            null,
            null,
            null,
            "Sensitive failure",
            DateTimeOffset.UtcNow);

        var surface = SchedulerAgentChatContextBuilder.Build(
            SchedulerAgentChatView.Schedules,
            plan,
            null,
            "edit-schedule");
        var json = JsonSerializer.Serialize(surface.Position, AgentOutputJson.SerializerOptions);

        Assert.Equal(plan.Id.ToString("D"), surface.Position.PrimarySelection?.Id);
        Assert.Contains("edit-schedule", json, StringComparison.Ordinal);
        Assert.DoesNotContain(plan.CronExpression, json, StringComparison.Ordinal);
        Assert.DoesNotContain(plan.Description, json, StringComparison.Ordinal);
        Assert.DoesNotContain(plan.LastError, json, StringComparison.Ordinal);
        var schedulerAccess = Assert.Single(surface.AgentAccess);
        Assert.Equal(SchedulerAgentIdentity.AgentId, schedulerAccess.AgentId);
        Assert.Equal(
            AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
            schedulerAccess.Permissions);
        Assert.Equal(
            AgentChatContextCompletionRefreshMode.OnSuccessfulRun,
            surface.CompletionRefreshMode);
    }

    [Fact]
    public void Resources_position_excludes_configuration_location_and_secret_identifiers()
    {
        var resourceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var secretId = Guid.NewGuid();
        var editor = new ResourceEditorModel
        {
            Id = resourceId,
            ProjectId = projectId,
            Name = "Machine PDFs",
            Description = "Sensitive notes",
            LocationOrIdentifier = "C:\\private\\quotes.pdf",
            ConfigJson = "{\"password\":\"secret\"}",
            LinkedSecretId = secretId,
            ValidationStatus = ResourceValidationStatus.Valid,
            Sensitivity = ResourceSensitivity.Restricted
        };

        var surface = ResourcesAgentChatContextBuilder.Build(
            ResourcesAgentChatView.Registry,
            editor,
            "Quotation",
            "File system",
            null);
        var json = JsonSerializer.Serialize(surface.Position, AgentOutputJson.SerializerOptions);

        Assert.Equal(resourceId.ToString("D"), surface.Position.PrimarySelection?.Id);
        Assert.Contains(projectId.ToString("D"), json, StringComparison.Ordinal);
        Assert.DoesNotContain(editor.LocationOrIdentifier, json, StringComparison.Ordinal);
        Assert.DoesNotContain(editor.ConfigJson, json, StringComparison.Ordinal);
        Assert.DoesNotContain(secretId.ToString("D"), json, StringComparison.Ordinal);
        Assert.DoesNotContain(editor.Description, json, StringComparison.Ordinal);
    }

    [Fact]
    public void Resources_browse_sources_of_the_same_class_keep_distinct_stable_ids()
    {
        var firstSourceId = new ResourceBrowseAgentChatSourceId($"storage:{Guid.NewGuid():N}");
        var secondSourceId = new ResourceBrowseAgentChatSourceId($"storage:{Guid.NewGuid():N}");
        var editor = new ResourceEditorModel();

        var first = ResourcesAgentChatContextBuilder.Build(
            ResourcesAgentChatView.Browse,
            editor,
            null,
            string.Empty,
            new ResourceBrowseAgentChatPosition(firstSourceId, "FileSystem", "Files A", null, null));
        var second = ResourcesAgentChatContextBuilder.Build(
            ResourcesAgentChatView.Browse,
            editor,
            null,
            string.Empty,
            new ResourceBrowseAgentChatPosition(secondSourceId, "FileSystem", "Files B", null, null));

        Assert.Equal(firstSourceId.Value, first.Position.PrimarySelection?.Id);
        Assert.Equal(secondSourceId.Value, second.Position.PrimarySelection?.Id);
        Assert.NotEqual(first.Position.PrimarySelection?.Id, second.Position.PrimarySelection?.Id);
        Assert.Contains(first.Position.Facts, fact =>
            fact.Name == "source-class" && fact.Value == "FileSystem");
    }
}
