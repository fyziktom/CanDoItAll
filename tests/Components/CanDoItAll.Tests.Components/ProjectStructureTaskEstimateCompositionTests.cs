using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureTaskEstimateCompositionTests
{
    [Fact]
    public void Create_composer_persists_canonical_effort_and_expected_cost()
    {
        var definition = ResolveTaskDefinition();
        var request = CreateRequest(
            [
                Input(ProjectTaskEstimateInputKeys.ExpectedEffortValue, "1.5"),
                Input(ProjectTaskEstimateInputKeys.ExpectedEffortUnit, "manDays"),
                Input(ProjectTaskEstimateInputKeys.ExpectedCostAmount, "750.25"),
                Input(ProjectTaskEstimateInputKeys.ExpectedCostCurrencyCode, "eur")
            ]);

        var prepared = ProjectStructureCreateRequestComposer.Compose(
            definition,
            request,
            "parent",
            (120, 240));
        var metadata = ProjectObjectMetadataSerializer.Parse(prepared.Request.MetadataJson).WorkItem;

        Assert.NotNull(metadata);
        Assert.Equal(12m, metadata!.ExpectedEffortHours);
        Assert.Equal(ProjectWorkItemEffortUnit.ManDays, metadata.ExpectedEffortUnit);
        Assert.Equal(750.25m, metadata.ExpectedCostAmount);
        Assert.Equal("EUR", metadata.ExpectedCostCurrencyCode);
    }

    [Fact]
    public void Node_editor_reads_preferred_unit_and_writes_canonical_hours()
    {
        var definition = ResolveTaskDefinition();
        var node = CreateTaskNode(new ProjectWorkItemMetadata
        {
            WorkItemKind = ProjectWorkItemKind.Task,
            Description = "Initial notes",
            ExpectedEffortHours = 8m,
            ExpectedEffortUnit = ProjectWorkItemEffortUnit.ManDays,
            ExpectedCostAmount = 600m,
            ExpectedCostCurrencyCode = "USD"
        });

        var initialValues = ProjectStructureNodeEditor.BuildInputValues(definition, node);

        Assert.Contains(initialValues, value =>
            value.Key == ProjectTaskEstimateInputKeys.ExpectedEffortValue && value.Value == "1");
        Assert.Contains(initialValues, value =>
            value.Key == ProjectTaskEstimateInputKeys.ExpectedEffortUnit && value.Value == "manDays");
        Assert.Contains(initialValues, value =>
            value.Key == ProjectTaskEstimateInputKeys.ExpectedCostAmount && value.Value == "600");
        Assert.Contains(initialValues, value =>
            value.Key == ProjectTaskEstimateInputKeys.ExpectedCostCurrencyCode && value.Value == "USD");

        var update = ProjectStructureNodeEditor.ComposeUpdate(
            definition,
            node,
            CreateRequest(
                [
                    Input(ProjectTaskEstimateInputKeys.ExpectedEffortValue, "2"),
                    Input(ProjectTaskEstimateInputKeys.ExpectedEffortUnit, "manDays"),
                    Input(ProjectTaskEstimateInputKeys.ExpectedCostAmount, "900"),
                    Input(ProjectTaskEstimateInputKeys.ExpectedCostCurrencyCode, "gbp")
                ]));
        var metadata = ProjectObjectMetadataSerializer.Parse(update.MetadataJson).WorkItem;

        Assert.NotNull(metadata);
        Assert.Equal(16m, metadata!.ExpectedEffortHours);
        Assert.Equal(ProjectWorkItemEffortUnit.ManDays, metadata.ExpectedEffortUnit);
        Assert.Equal(900m, metadata.ExpectedCostAmount);
        Assert.Equal("GBP", metadata.ExpectedCostCurrencyCode);
    }

    [Fact]
    public void Create_composer_rejects_malformed_estimate_instead_of_dropping_it()
    {
        var definition = ResolveTaskDefinition();
        var request = CreateRequest(
            [
                Input(ProjectTaskEstimateInputKeys.ExpectedEffortValue, "not-a-number"),
                Input(ProjectTaskEstimateInputKeys.ExpectedEffortUnit, "hours")
            ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProjectStructureCreateRequestComposer.Compose(
                definition,
                request,
                "parent",
                (null, null)));

        Assert.Contains("must be a number", exception.Message, StringComparison.Ordinal);
    }

    private static ProjectStructureCreateLeafDefinition ResolveTaskDefinition()
    {
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
            ProjectStructureTaskActionIds.Create,
            out var definition));
        return definition;
    }

    private static CanvasWorkbenchCreateActionRequest CreateRequest(
        IReadOnlyList<CanvasWorkbenchInputValue> inputValues)
        => new(
            ProjectStructureTaskActionIds.Create,
            "parent",
            120,
            240,
            "parent",
            "Task title",
            "Planned",
            "Task notes",
            "child",
            ProjectStructureTaskActionIds.CreateMode,
            "task",
            null,
            inputValues);

    private static CanvasWorkbenchInputValue Input(string key, string value)
        => new()
        {
            Key = key,
            Value = value
        };

    private static ProjectStructureNode CreateTaskNode(ProjectWorkItemMetadata workItem)
        => new(
            "task-1",
            "parent",
            ProjectObjectType.WorkItem,
            "task",
            "Task title",
            "Planned",
            "Ready",
            "Initial notes",
            "/projects/1/structure",
            "WorkItem",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            120,
            240,
            new ProjectObjectVisualProfile("rect", "#0369a1", "TK", "Task"),
            [],
            "progress",
            25,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0,
            MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
            {
                WorkItem = workItem
            }));
}
