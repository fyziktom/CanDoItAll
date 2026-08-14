using System.Text.Json;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectObjectMetadataSerializerTests
{
    [Fact]
    public void Infrastructure_storage_prefix_serializes_as_a_canonical_logical_path()
    {
        var metadata = new ProjectObjectMetadataEnvelope
        {
            Infrastructure = new ProjectInfrastructureMetadata
            {
                StoragePathPrefix = @"deliveries\reports"
            }
        };

        var json = ProjectObjectMetadataSerializer.Serialize(metadata);
        var roundTrip = ProjectObjectMetadataSerializer.Parse(json);

        Assert.Equal("deliveries/reports", roundTrip.Infrastructure?.StoragePathPrefix);
        Assert.DoesNotContain(@"deliveries\reports", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_preserving_unknown_properties_replaces_typed_families_without_losing_extensions()
    {
        const string originalJson = """
            {
              "Environment": {
                "environmentKind": "dotNetRuntime",
                "projectPath": "Legacy.csproj"
              },
              "script": {
                "scriptKind": "powerShell",
                "command": "Write-Output"
              },
              "runtimeAuditExtension": {
                "correlationId": "runtime-repair-42"
              },
              "extensionRevision": 7
            }
            """;
        var metadata = new ProjectObjectMetadataEnvelope
        {
            Environment = new ProjectEnvironmentMetadata
            {
                EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                ProjectPath = "Calculator.csproj"
            }
        };

        var json = ProjectObjectMetadataSerializer.SerializePreservingUnknownProperties(
            originalJson,
            metadata);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(
            "Calculator.csproj",
            root.GetProperty("environment").GetProperty("projectPath").GetString());
        Assert.False(root.TryGetProperty("Environment", out _));
        Assert.False(root.TryGetProperty("script", out _));
        Assert.Equal(
            "runtime-repair-42",
            root.GetProperty("runtimeAuditExtension").GetProperty("correlationId").GetString());
        Assert.Equal(7, root.GetProperty("extensionRevision").GetInt32());
    }

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
                ExpectedCostCurrencyCode = " eur ",
                ExecutionState = ProjectTaskExecutionState.NotStarted,
                ExpectedCostBasis = new ProjectTaskExpectedCostBasis
                {
                    ResourceKind = ProjectStructureTaskResourceKind.Person,
                    ResourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Source = ProjectStructureTaskResourceCostSource.CrmWorkforceRate,
                    CalculatedAtUtc = DateTimeOffset.UnixEpoch
                }
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
        Assert.NotNull(roundTrip.ExpectedCostBasis);
        Assert.Equal(
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate,
            roundTrip.ExpectedCostBasis!.Source);
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
        Assert.Equal(ProjectTaskExecutionState.Unknown, workItem.ExecutionState);
        Assert.Null(workItem.ActualStartedAtUtc);
        Assert.Null(workItem.ActualEndedAtUtc);
    }

    [Fact]
    public void Validate_and_serialize_round_trips_explicit_task_execution_state()
    {
        var startedAtUtc = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        var metadata = new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExecutionState = ProjectTaskExecutionState.Completed,
                ActualStartedAtUtc = startedAtUtc,
                ActualEndedAtUtc = startedAtUtc.AddHours(2)
            }
        };

        var json = ProjectObjectMetadataSerializer.ValidateAndSerialize(
            CanDoItAll.SharedKernel.ProjectObjectType.WorkItem,
            "task",
            ProjectObjectMetadataSerializer.Serialize(metadata));
        var roundTrip = ProjectObjectMetadataSerializer.Parse(json).WorkItem;

        Assert.NotNull(roundTrip);
        Assert.Equal(ProjectTaskExecutionState.Completed, roundTrip!.ExecutionState);
        Assert.Equal(startedAtUtc, roundTrip.ActualStartedAtUtc);
        Assert.Equal(startedAtUtc.AddHours(2), roundTrip.ActualEndedAtUtc);
    }

    [Fact]
    public void Validate_rejects_invalid_task_execution_state_metadata()
    {
        var metadata = new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExecutionState = ProjectTaskExecutionState.Started
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProjectObjectMetadataSerializer.Validate(
                CanDoItAll.SharedKernel.ProjectObjectType.WorkItem,
                "task",
                metadata));

        Assert.Contains("actual start", exception.Message, StringComparison.Ordinal);
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

        var exception = Assert.Throws<ProjectObjectMetadataPayloadException>(
            () => ProjectObjectMetadataSerializer.Parse(json));

        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
        Assert.Contains("script.arguments", exception.JsonPath, StringComparison.OrdinalIgnoreCase);
    }
}
