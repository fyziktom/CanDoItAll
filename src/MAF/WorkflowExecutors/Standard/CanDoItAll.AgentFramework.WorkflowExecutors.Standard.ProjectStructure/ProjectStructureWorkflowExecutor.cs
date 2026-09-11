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
    internal static WorkflowExecutorDescriptor ReadAwareDescriptor { get; } = BuiltInWorkflowExecutorDescriptors.ProjectStructure with {
        ProviderReadOwner = WorkflowStructureReadContext.DisclosureOwner
    };

    public WorkflowExecutorDescriptor Descriptor => ReadAwareDescriptor;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowProjectStructureExecutorSettings>(context.SettingsJson);
        List<WorkflowProviderReadEvidence> readEvidence = [];

        object result = settings.Operation switch
        {
            WorkflowProjectStructureOperation.ListProjects => await projectStructureGateway.ListWorkflowProjectsAsync(RequireReadContext(context, readEvidence.Add), cancellationToken),
            WorkflowProjectStructureOperation.ReadTree => await projectStructureGateway.ReadWorkflowStructureAsync(
                RequireProjectId(settings, input),
                new ProjectStructureRuntimeReadRequest(
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true,
                    Take: 250),
                RequireReadContext(context, readEvidence.Add),
                cancellationToken),
            WorkflowProjectStructureOperation.ReadNode => await projectStructureGateway.ReadWorkflowStructureAsync(
                RequireProjectId(settings, input),
                new ProjectStructureRuntimeReadRequest(
                    NodeIds: [RequireNodeId(settings, input)],
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true),
                RequireReadContext(context, readEvidence.Add),
                cancellationToken),
            WorkflowProjectStructureOperation.CreateAsset => await projectStructureGateway.CreateWorkflowAssetAsync(
                RequireProjectId(settings, input),
                BuildAssetRequest(settings, input),
                RequireEffectContext(context, 0),
                cancellationToken),
            WorkflowProjectStructureOperation.CreateTaskNodes => await CreateTaskNodesAsync(
                projectStructureGateway,
                context,
                settings,
                input,
                cancellationToken),
            _ => throw new InvalidOperationException($"Project-structure operation '{settings.Operation}' is not supported.")
        };

        result = settings.IncludeInputPayload
            ? IncludeInputPayload(result, input)
            : result;

        return WorkflowExecutorJson.Result(context, result) with { ProviderReadEvidence = readEvidence };
    }

    private static WorkflowStructureReadContext RequireReadContext(WorkflowExecutorExecutionContext context,
        Action<WorkflowProviderReadEvidence> capture) {
        var occurrence = context.ExecutionOccurrence
            ?? throw new InvalidOperationException("This saved Workflow message has no trusted execution occurrence. Reconcile the saved checkpoint before reading Structure data.");
        if (context.RunId != occurrence.RunId) {
            throw new InvalidOperationException("Workflow read occurrence does not match its admitted execution run.");
        }
        return new(occurrence, context.Definition.VersionId, context.Node.Id) { CaptureReadEvidence = capture };
    }

    private static WorkflowStructureEffectContext RequireEffectContext(WorkflowExecutorExecutionContext context, int slot) {
        var occurrence = context.ExecutionOccurrence
            ?? throw new InvalidOperationException("This saved workflow message has no trusted execution occurrence. Reconcile the saved checkpoint before creating new Structure outputs.");
        if (context.RunId.HasValue && context.RunId != occurrence.RunId) {
            throw new InvalidOperationException("Workflow output occurrence does not match its admitted execution run.");
        }

        return new(occurrence, context.Definition.VersionId, context.Node.Id, slot);
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
