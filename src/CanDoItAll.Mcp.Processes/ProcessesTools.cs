using System.ComponentModel;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.Processes;

[McpServerToolType]
public sealed class ProcessesTools(IProcessesCoordinator coordinator, ILogger<ProcessesTools> logger)
{
    [McpServerTool(Name = "processes_definitions_list", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists process definitions. Use projectId to scope the list to one project.")]
    public Task<McpToolEnvelope<IReadOnlyList<ProcessDefinitionListItem>>> ProcessesDefinitionsListAsync(Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_definitions_list", () => coordinator.ListDefinitionsAsync(projectId, cancellationToken));
    }

    [McpServerTool(Name = "processes_definition_editor_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Loads the full process-definition editor model. Omit definitionId to get a blank editor template optionally scoped to projectId.")]
    public Task<McpToolEnvelope<ProcessDefinitionEditorModel>> ProcessesDefinitionEditorGetAsync(Guid? definitionId = null, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_definition_editor_get", () => coordinator.GetDefinitionEditorAsync(definitionId, projectId, cancellationToken));
    }

    [McpServerTool(Name = "processes_definition_save", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Creates or updates a process definition from the editor model and returns the definition id.")]
    public Task<McpToolEnvelope<Guid>> ProcessesDefinitionSaveAsync(ProcessDefinitionEditorModel model, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_definition_save", () => coordinator.SaveDefinitionAsync(model, cancellationToken));
    }

    [McpServerTool(Name = "processes_definition_publish", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Publishes the current draft version of a process definition and makes it runtime-usable.")]
    public Task<McpToolEnvelope<Guid>> ProcessesDefinitionPublishAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            "processes_definition_publish",
            async () =>
            {
                await coordinator.PublishDefinitionAsync(definitionId, cancellationToken);
                return definitionId;
            });
    }

    [McpServerTool(Name = "processes_definition_delete", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Deletes a process definition and its related runtime records.")]
    public Task<McpToolEnvelope<Guid>> ProcessesDefinitionDeleteAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            "processes_definition_delete",
            async () =>
            {
                await coordinator.DeleteDefinitionAsync(definitionId, cancellationToken);
                return definitionId;
            });
    }

    [McpServerTool(Name = "processes_definition_export", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Exports a process definition into the CanDoItAll process import-export envelope.")]
    public Task<McpToolEnvelope<ProcessImportExportEnvelope>> ProcessesDefinitionExportAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_definition_export", () => coordinator.ExportDefinitionAsync(definitionId, cancellationToken));
    }

    [McpServerTool(Name = "processes_definition_import", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Imports a process definition from a CanDoItAll process import-export envelope and returns the imported definition id.")]
    public Task<McpToolEnvelope<Guid>> ProcessesDefinitionImportAsync(ProcessImportExportEnvelope envelope, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_definition_import", () => coordinator.ImportDefinitionAsync(envelope, cancellationToken));
    }

    [McpServerTool(Name = "processes_runs_list", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists process runs. Use definitionId or projectId to narrow the scope.")]
    public Task<McpToolEnvelope<IReadOnlyList<ProcessRunListItem>>> ProcessesRunsListAsync(Guid? definitionId = null, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_runs_list", () => coordinator.ListRunsAsync(definitionId, projectId, cancellationToken));
    }

    [McpServerTool(Name = "processes_run_detail_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Loads a process run with step runs, decisions, artifacts, assignments, work briefs, conformance observations, and improvements.")]
    public Task<McpToolEnvelope<ProcessRunDetailToolData>> ProcessesRunDetailGetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_run_detail_get", () => coordinator.GetRunDetailAsync(runId, cancellationToken));
    }

    [McpServerTool(Name = "processes_analytics_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns summary analytics for process definitions or project-scoped process execution.")]
    public Task<McpToolEnvelope<ProcessAnalyticsSummary>> ProcessesAnalyticsGetAsync(Guid? definitionId = null, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_analytics_get", () => coordinator.GetAnalyticsAsync(definitionId, projectId, cancellationToken));
    }

    [McpServerTool(Name = "processes_run_start", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts a new process run from a published definition and returns the run id.")]
    public Task<McpToolEnvelope<Guid>> ProcessesRunStartAsync(ProcessRunStartRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_run_start", () => coordinator.StartRunAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "processes_step_transition", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Transitions a process step run to the requested status and records the decision evidence.")]
    public Task<McpToolEnvelope<Guid>> ProcessesStepTransitionAsync(ProcessStepTransitionRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            "processes_step_transition",
            async () =>
            {
                await coordinator.TransitionStepAsync(request, cancellationToken);
                return request.StepRunId;
            });
    }

    [McpServerTool(Name = "processes_assignment_resolve", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Resolves or updates a runtime role assignment for a process run.")]
    public Task<McpToolEnvelope<Guid>> ProcessesAssignmentResolveAsync(ProcessAssignmentResolutionRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            "processes_assignment_resolve",
            async () =>
            {
                await coordinator.ResolveAssignmentAsync(request, cancellationToken);
                return request.ProcessRunId;
            });
    }

    [McpServerTool(Name = "processes_artifact_record", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Records artifact metadata against a process run or step and returns the artifact id.")]
    public Task<McpToolEnvelope<Guid>> ProcessesArtifactRecordAsync(ProcessArtifactRecordRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_artifact_record", () => coordinator.RecordArtifactAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "processes_party_options_list", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists project party options that can be used for process assignment decisions.")]
    public Task<McpToolEnvelope<IReadOnlyList<ProjectPartyOption>>> ProcessesPartyOptionsListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_party_options_list", () => coordinator.ListPartyOptionsAsync(projectId, cancellationToken));
    }

    [McpServerTool(Name = "processes_executor_options_list", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists executor registry options available to process runtime assignment flows.")]
    public Task<McpToolEnvelope<IReadOnlyList<ProcessExecutorRegistryOption>>> ProcessesExecutorOptionsListAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("processes_executor_options_list", () => coordinator.ListExecutorOptionsAsync(cancellationToken));
    }

    private async Task<McpToolEnvelope<T>> ExecuteAsync<T>(string toolName, Func<Task<T>> callback)
    {
        var correlationId = CorrelationIdFactory.Create("processes");

        try
        {
            var data = await callback();
            return McpToolEnvelope<T>.Success(toolName, correlationId, data);
        }
        catch (ToolInvocationException ex)
        {
            logger.LogWarning(ex, "{ToolName} failed with a deterministic tool error {Code}.", toolName, ex.Code);
            return McpToolEnvelope<T>.Failure(
                toolName,
                correlationId,
                new ToolError(ex.Code, ex.Message, ex.Details),
                status: MapFailureStatus(ex.Code),
                summary: ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ToolName} failed unexpectedly.", toolName);
            return McpToolEnvelope<T>.Failure(
                toolName,
                correlationId,
                new ToolError("InternalError", ex.Message),
                status: "failed",
                summary: "The tool failed unexpectedly.");
        }
    }

    private static string MapFailureStatus(string code)
    {
        if (code.Contains("notfound", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("not-found", StringComparison.OrdinalIgnoreCase))
        {
            return "not_found";
        }

        if (code.Contains("required", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("immutable", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("processes.", StringComparison.OrdinalIgnoreCase))
        {
            return "validation_error";
        }

        return "failed";
    }
}
