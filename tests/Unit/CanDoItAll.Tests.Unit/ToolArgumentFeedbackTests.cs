using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ToolArgumentFeedbackTests
{
    private static readonly JsonSerializerOptions ArgumentSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Malformed_shape_returns_safe_failure_without_invoking_the_delegate()
    {
        var invocationCount = 0;
        var function = CreateAssetTool(_ => invocationCount++);
        var arguments = CreateMalformedArguments(Guid.NewGuid());

        var mapped = MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(
            function,
            arguments,
            out var failure);

        Assert.True(mapped);
        Assert.Equal(0, invocationCount);
        Assert.False(failure.Succeeded);
        Assert.Equal("InvalidToolArguments", failure.ErrorCode);
        Assert.Equal(AgentToolEffectState.NotCommitted, failure.EffectState);
        Assert.True(failure.CanRetryWithCorrectedInput);
    }

    [Fact]
    public async Task Corrected_nested_call_invokes_once_and_correlates_with_the_legacy_shape()
    {
        var invocationCount = 0;
        var function = CreateAssetTool(_ => invocationCount++);
        var projectId = Guid.NewGuid();
        var malformed = CreateMalformedArguments(projectId);
        var corrected = CreateCorrectedArguments(projectId, "docs/architecture/overview.md");

        var rejected = MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(
            function,
            corrected,
            out var failure);
        Assert.False(rejected, failure?.Message);

        await function.InvokeAsync(corrected, CancellationToken.None);

        Assert.Equal(1, invocationCount);
        var malformedKey = MafToolInvocationCorrelationKey.Create(function.Name, malformed);
        var correctedKey = MafToolInvocationCorrelationKey.Create(function.Name, corrected);
        var unrelatedKey = MafToolInvocationCorrelationKey.Create(
            function.Name,
            CreateCorrectedArguments(projectId, "docs/architecture/unrelated.md"));
        Assert.NotEmpty(malformedKey);
        Assert.Equal(malformedKey, correctedKey);
        Assert.NotEqual(correctedKey, unrelatedKey);
    }

    [Fact]
    public void Validation_feedback_exposes_only_the_schema_path_and_never_the_supplied_value()
    {
        const string secretValue = "database-password=super-secret";
        var function = CreateAssetTool(_ => { });
        using var document = JsonDocument.Parse(
            $$"""
            {
              "projectId": "{{Guid.NewGuid():D}}",
              "request": {
                "objectType": "{{secretValue}}",
                "title": "Architecture",
                "subtitle": "",
                "notes": "",
                "media": null,
                "parentNodeKey": "main",
                "sourceWorkspacePath": "docs/architecture/overview.md"
              }
            }
            """);
        var arguments = ToArguments(document.RootElement);

        var mapped = MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(
            function,
            arguments,
            out var failure);

        Assert.True(mapped);
        Assert.Contains("$.request.objectType", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secretValue, failure.Message, StringComparison.Ordinal);
        Assert.Equal(AgentToolEffectState.NotCommitted, failure.EffectState);
    }

    [Theory]
    [InlineData(
        """
        {
          "projectId": 42,
          "request": {
            "objectType": "File",
            "title": "Architecture",
            "subtitle": "",
            "notes": "",
            "media": null,
            "parentNodeKey": "main",
            "sourceWorkspacePath": "docs/architecture/overview.md"
          }
        }
        """,
        "$.projectId")]
    [InlineData(
        """
        {
          "projectId": "5dd73c07-758c-462b-95a4-5e76da901867",
          "request": {
            "objectType": "File",
            "title": "Architecture",
            "subtitle": "",
            "notes": "",
            "media": null,
            "sourceWorkspacePath": "docs/architecture/overview.md"
          }
        }
        """,
        "$.request.parentNodeKey")]
    public void Invalid_type_or_missing_nested_value_is_rejected_before_invocation(
        string json,
        string expectedPath)
    {
        var function = CreateAssetTool(_ => { });
        using var document = JsonDocument.Parse(json);

        var mapped = MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(
            function,
            ToArguments(document.RootElement),
            out var failure);

        Assert.True(mapped);
        Assert.Contains(expectedPath, failure.Message, StringComparison.Ordinal);
        Assert.Equal(AgentToolEffectState.NotCommitted, failure.EffectState);
    }

    [Fact]
    public void Unknown_mutation_result_is_never_classified_as_success()
    {
        var assessment = MafRuntimeToolInvocationResultClassifier.Assess(
            "project_structure_asset_create",
            ToolInvocationClassification.Mutation,
            new object());

        Assert.Equal(AgentToolInvocationOutcome.Unknown, assessment.Outcome);
        Assert.Equal(AgentToolEffectState.Unknown, assessment.EffectState);
        Assert.False(assessment.Succeeded);
        Assert.Equal("UnverifiedToolResult", assessment.FailureCode);

        using var effectScope = AgentToolInvocationEffectScope.Begin();
        AgentToolInvocationEffectScope.RecordCommitted("project-structure", "project-42");
        var effectState = assessment.EffectState;
        var sourceKind = "project-structure";
        var sourceId = "active-project";
        MafRuntimeAgentFactory.ApplyCommittedEffectCapture(
            ToolInvocationClassification.Mutation,
            effectScope,
            ref effectState,
            ref sourceKind,
            ref sourceId);
        Assert.Equal(AgentToolEffectState.Committed, effectState);
        Assert.Equal("project-structure", sourceKind);
        Assert.Equal("project-42", sourceId);
    }

    [Fact]
    public void Supported_read_result_and_optional_arguments_remain_compatible()
    {
        var function = AIFunctionFactory.Create(
            (Guid projectId, ProjectStructureReadRequest? request = null, CancellationToken cancellationToken = default) =>
                Task.FromResult<ProjectStructureReadToolData>(null!),
            "project_structure_read");
        var arguments = new AIFunctionArguments
        {
            ["projectId"] = Guid.NewGuid(),
            ["request"] = new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeLayout: true,
                IncludeMetadata: true,
                IncludeNotes: true,
                IncludeAssets: true,
                Source: ProjectStructureReadSource.CanonicalCurrent)
        };

        var rejected = MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(
            function,
            arguments,
            out var failure);

        Assert.False(rejected, failure?.Message);
        var assessment = MafRuntimeToolInvocationResultClassifier.Assess(
            "project_structure_read",
            ToolInvocationClassification.Read,
            new object());

        Assert.Equal(AgentToolInvocationOutcome.Succeeded, assessment.Outcome);
        Assert.Equal(AgentToolEffectState.None, assessment.EffectState);
        Assert.True(assessment.Succeeded);
    }

    [Fact]
    public void Boolean_subschema_accepts_typed_node_create_arguments()
    {
        var function = AIFunctionFactory.Create(
            (Guid projectId, ProjectStructureNodeCreateInput request, CancellationToken cancellationToken = default) =>
                Task.FromResult<ProjectStructureNodeSummary>(null!),
            "project_structure_node_create");
        var arguments = new AIFunctionArguments
        {
            ["projectId"] = Guid.NewGuid(),
            ["request"] = new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                "Architecture",
                string.Empty,
                "Canonical persistence validation.",
                "main")
        };

        var rejected = MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(
            function,
            arguments,
            out var failure);

        Assert.False(rejected, failure?.Message);
    }

    private static AIFunction CreateAssetTool(Action<ProjectStructureAgentAssetCreateInput> onInvoke)
    {
        return ProjectStructureAgentRuntimeToolProvider.CreateProjectStructureAssetCreateTool(
            (projectId, request, estimatedMinutes, cancellationToken) =>
            {
                Assert.NotEqual(Guid.Empty, projectId);
                Assert.Null(estimatedMinutes);
                cancellationToken.ThrowIfCancellationRequested();
                onInvoke(request);
                return Task.FromResult<ProjectStructureNodeSummary>(null!);
            });
    }

    private static AIFunctionArguments CreateMalformedArguments(Guid projectId)
    {
        return new AIFunctionArguments
        {
            ["project_id"] = projectId,
            ["parentNodeKey"] = "main",
            ["sourceWorkspacePath"] = "docs/architecture/overview.md"
        };
    }

    private static AIFunctionArguments CreateCorrectedArguments(
        Guid projectId,
        string sourceWorkspacePath)
    {
        var request = new ProjectStructureAgentAssetCreateInput(
            ProjectObjectType.File,
            "Architecture overview",
            string.Empty,
            string.Empty,
            Media: null,
            ParentNodeKey: "main",
            SourceWorkspacePath: sourceWorkspacePath);
        return new AIFunctionArguments
        {
            ["projectId"] = projectId,
            ["request"] = JsonSerializer.SerializeToElement(request, ArgumentSerializerOptions)
        };
    }

    private static AIFunctionArguments ToArguments(JsonElement root)
    {
        var arguments = new AIFunctionArguments();
        foreach (var property in root.EnumerateObject())
        {
            arguments[property.Name] = property.Value.Clone();
        }

        return arguments;
    }
}