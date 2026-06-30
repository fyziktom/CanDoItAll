using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;

public sealed partial class ProjectStructureWorkflowExecutor(IProjectStructureRuntimeGateway projectStructureGateway) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.ProjectStructure;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowProjectStructureExecutorSettings>(context.SettingsJson);

        object result = settings.Operation switch
        {
            WorkflowProjectStructureOperation.ListProjects => await projectStructureGateway.ListProjectsAsync(cancellationToken),
            WorkflowProjectStructureOperation.ReadTree => await projectStructureGateway.ReadStructureAsync(
                RequireProjectId(settings, input),
                new ProjectStructureRuntimeReadRequest(
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true,
                    Take: 250),
                cancellationToken),
            WorkflowProjectStructureOperation.ReadNode => await projectStructureGateway.ReadStructureAsync(
                RequireProjectId(settings, input),
                new ProjectStructureRuntimeReadRequest(
                    NodeIds: [RequireNodeId(settings, input)],
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true),
                cancellationToken),
            WorkflowProjectStructureOperation.CreateAsset => await projectStructureGateway.CreateAssetAsync(
                RequireProjectId(settings, input),
                BuildAssetRequest(settings, input),
                BuildAgentContext(input),
                cancellationToken),
            WorkflowProjectStructureOperation.CreateTaskNodes => await CreateTaskNodesAsync(
                projectStructureGateway,
                settings,
                input,
                cancellationToken),
            _ => throw new InvalidOperationException($"Project-structure operation '{settings.Operation}' is not supported.")
        };

        result = settings.IncludeInputPayload
            ? IncludeInputPayload(result, input)
            : result;

        return WorkflowExecutorJson.Result(context, result);
    }

    private static object IncludeInputPayload(
        object result,
        WorkflowNodeInput input)
    {
        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return new
            {
                result,
                inputPayload = (JsonNode?)null
            };
        }

        try
        {
            return new
            {
                result,
                inputPayload = JsonNode.Parse(input.PayloadJson)
            };
        }
        catch (JsonException)
        {
            return new
            {
                result,
                inputPayload = input.PayloadJson
            };
        }
    }

}
