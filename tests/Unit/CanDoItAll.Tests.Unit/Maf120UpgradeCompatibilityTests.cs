using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Workbench;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class Maf120UpgradeCompatibilityTests
{
    [Fact]
    public void Resolved_runtime_uses_the_coherent_Maf_120_and_Meai_109_family()
    {
        Assert.Equal(new Version(1, 20, 0, 0), typeof(AIAgent).Assembly.GetName().Version);
        Assert.Equal(new Version(1, 20, 0, 0), typeof(RunStatus).Assembly.GetName().Version);
        Assert.Equal(new Version(10, 9, 0, 0), typeof(AIFunction).Assembly.GetName().Version);
    }

    [Fact]
    public void Asset_description_and_schema_agree_on_the_nested_camel_case_contract()
    {
        var function = CreateAssetTool(_ => { });
        var schema = function.JsonSchema;
        var properties = schema.GetProperty("properties");
        var required = schema.GetProperty("required").EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        var request = properties.GetProperty("request");
        var requestProperties = request.GetProperty("properties");
        var requestRequired = request.GetProperty("required").EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("projectId", required);
        Assert.Contains("request", required);
        Assert.Contains("objectType", requestRequired);
        Assert.Contains("title", requestRequired);
        Assert.Contains("parentNodeKey", requestRequired);
        Assert.DoesNotContain("subtitle", requestRequired);
        Assert.DoesNotContain("notes", requestRequired);
        Assert.DoesNotContain("media", requestRequired);
        Assert.True(requestProperties.TryGetProperty("parentNodeKey", out _));
        Assert.True(requestProperties.TryGetProperty("sourceWorkspacePath", out _));
        Assert.False(properties.TryGetProperty("project_id", out _));
        Assert.False(properties.TryGetProperty("parentNodeKey", out _));
        Assert.Contains(@"""projectId""", function.Description, StringComparison.Ordinal);
        Assert.Contains(@"""request""", function.Description, StringComparison.Ordinal);
        Assert.Contains("request.parentNodeKey", function.Description, StringComparison.Ordinal);
        Assert.Contains("request.sourceWorkspacePath", function.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenAi_function_declaration_preserves_the_native_asset_schema()
    {
        var function = CreateAssetTool(_ => { });
        var declaration = ChatTool.CreateFunctionTool(
            function.Name,
            function.Description,
            BinaryData.FromString(function.JsonSchema.GetRawText()));

        Assert.Equal(function.Name, declaration.FunctionName);
        Assert.Equal(function.Description, declaration.FunctionDescription);
        Assert.Equal(
            JsonSerializer.Serialize(function.JsonSchema),
            JsonSerializer.Serialize(
                JsonDocument.Parse(declaration.FunctionParameters.ToString()).RootElement));
    }

    [Fact]
    public async Task Malformed_flat_arguments_do_not_execute_but_corrected_nested_arguments_do()
    {
        var invocationCount = 0;
        var function = CreateAssetTool(_ => invocationCount++);
        var projectId = Guid.NewGuid();
        var malformed = new AIFunctionArguments
        {
            ["project_id"] = projectId,
            ["parentNodeKey"] = "main",
            ["sourceWorkspacePath"] = "docs/architecture/architecture_overview.md"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => function.InvokeAsync(malformed, CancellationToken.None).AsTask());

        Assert.Contains("projectId", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, invocationCount);

        var request = new ProjectStructureAgentAssetCreateInput(
            ProjectObjectType.File,
            "Architecture overview",
            string.Empty,
            string.Empty,
            Media: null,
            ParentNodeKey: "main",
            SourceWorkspacePath: "docs/architecture/architecture_overview.md");
        await function.InvokeAsync(
            new AIFunctionArguments
            {
                ["projectId"] = projectId,
                ["request"] = JsonSerializer.SerializeToElement(request)
            },
            CancellationToken.None);

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void Failed_tool_result_remains_observable_after_the_sdk_upgrade()
    {
        var result = JsonSerializer.SerializeToElement(new
        {
            succeeded = false,
            errorCode = "InvalidArguments",
            message = "Required argument 'projectId' is missing.",
            canRetryWithCorrectedInput = true
        });

        Assert.False(MafRuntimeToolInvocationResultClassifier.IsSuccessful(result));
        Assert.Equal(
            "Required argument 'projectId' is missing.",
            MafRuntimeToolInvocationResultClassifier.ResolveFailureMessage(result));
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
}


