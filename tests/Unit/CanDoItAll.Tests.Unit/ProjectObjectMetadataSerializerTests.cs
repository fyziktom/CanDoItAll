using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectObjectMetadataSerializerTests
{
    [Fact]
    public void Validate_and_serialize_round_trips_task_delivery_estimate_metadata()
    {
        var metadata = new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExpectedEffortHours = 8m,
                ExpectedEffortUnit = ProjectWorkItemEffortUnit.ManDays,
                ExpectedCostAmount = 960m,
                ExpectedCostCurrencyCode = " eur "
            }
        };

        var json = ProjectObjectMetadataSerializer.ValidateAndSerialize(
            CanDoItAll.SharedKernel.ProjectObjectType.WorkItem,
            "task",
            ProjectObjectMetadataSerializer.Serialize(metadata));
        var roundTrip = ProjectObjectMetadataSerializer.Parse(json).WorkItem;

        Assert.NotNull(roundTrip);
        Assert.Equal(8m, roundTrip!.ExpectedEffortHours);
        Assert.Equal(ProjectWorkItemEffortUnit.ManDays, roundTrip.ExpectedEffortUnit);
        Assert.Equal(960m, roundTrip.ExpectedCostAmount);
        Assert.Equal("EUR", roundTrip.ExpectedCostCurrencyCode);
    }

    [Fact]
    public void Parse_keeps_legacy_task_metadata_compatible()
    {
        const string json = """
            {
              "workItem": {
                "workItemKind": "task",
                "description": "Legacy task"
              }
            }
            """;

        var workItem = ProjectObjectMetadataSerializer.Parse(json).WorkItem;

        Assert.NotNull(workItem);
        Assert.Null(workItem!.ExpectedEffortHours);
        Assert.Equal(ProjectWorkItemEffortUnit.Hours, workItem.ExpectedEffortUnit);
        Assert.Null(workItem.ExpectedCostAmount);
        Assert.Equal(string.Empty, workItem.ExpectedCostCurrencyCode);
    }

    [Fact]
    public void Validate_rejects_invalid_task_estimate_metadata()
    {
        var metadata = new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExpectedEffortHours = -1m
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProjectObjectMetadataSerializer.Validate(
                CanDoItAll.SharedKernel.ProjectObjectType.WorkItem,
                "task",
                metadata));

        Assert.Contains("greater than zero", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_accepts_script_argument_array_and_normalizes_to_command_line_string()
    {
        const string json = """
            {
              "script": {
                "scriptKind": "console",
                "command": "dotnet test",
                "arguments": [
                  "C:\\workspace path\\tests\\TetrisGame.Tests.csproj",
                  "-c",
                  "Debug",
                  "--no-build",
                  "O'Brien"
                ]
              }
            }
            """;

        var metadata = ProjectObjectMetadataSerializer.Parse(json);

        Assert.NotNull(metadata.Script);
        Assert.Equal(
            "'C:\\workspace path\\tests\\TetrisGame.Tests.csproj' '-c' 'Debug' '--no-build' 'O''Brien'",
            metadata.Script!.Arguments);
    }

    [Fact]
    public void Parse_rejects_script_argument_array_with_non_string_tokens()
    {
        const string json = """
            {
              "script": {
                "scriptKind": "console",
                "command": "dotnet test",
                "arguments": [
                  { "value": "--no-build" }
                ]
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(() => ProjectObjectMetadataSerializer.Parse(json));

        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
    }
}
