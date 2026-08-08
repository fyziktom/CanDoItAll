using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowFailureDiagnosticMapper
{
    public const string ExceptionDataKey = "CanDoItAll.WorkflowFailureDiagnostics";

    public static InvalidOperationException CreateValidationException(
        string message,
        WorkflowValidationResult validation,
        string correlationId,
        WorkflowId? workflowId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var exception = new InvalidOperationException(message);
        exception.Data[ExceptionDataKey] = FromValidationResult(validation, correlationId, workflowId);
        return exception;
    }

    public static IReadOnlyList<WorkflowFailureDiagnosticEnvelope> GetDiagnostics(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.Data[ExceptionDataKey] is IReadOnlyList<WorkflowFailureDiagnosticEnvelope> diagnostics
            ? diagnostics
            : [];
    }

    public static IReadOnlyList<WorkflowFailureDiagnosticEnvelope> FromValidationResult(
        WorkflowValidationResult validation,
        string correlationId,
        WorkflowId? workflowId = null)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return validation.Issues
            .Select(issue => FromValidationIssue(issue, correlationId, workflowId))
            .ToArray();
    }

    public static WorkflowFailureDiagnosticEnvelope FromValidationIssue(
        WorkflowValidationIssue issue,
        string correlationId,
        WorkflowId? workflowId = null)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return WorkflowFailureDiagnosticEnvelope.Validation(
            issue.Message,
            ResolveRepairHint(issue),
            correlationId,
            workflowId,
            issue.NodeId)
            with
            {
                RedactedTechnicalDetail = WorkflowExecutorRedaction.RedactText(issue.Message),
                Source = ResolveSource(issue)
            };
    }

    public static string ToUserMessage(WorkflowFailureDiagnosticEnvelope diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var location = diagnostic.NodeId is { } nodeId
            ? $"Step '{nodeId}'"
            : "Workflow";
        var source = diagnostic.ExecutorId is { } executorId
            ? $" while running executor '{executorId}'"
            : string.Empty;
        var detail = string.IsNullOrWhiteSpace(diagnostic.RepairHint) ||
                     string.Equals(diagnostic.Message, diagnostic.RepairHint, StringComparison.Ordinal)
            ? diagnostic.Message
            : $"{diagnostic.Message} {diagnostic.RepairHint}";

        return $"{location}{source} failed: {detail}";
    }

    private static WorkflowFailureSourceContext ResolveSource(WorkflowValidationIssue issue)
    {
        if (issue.Code == WorkflowValidationIssueCode.InvalidExecutorReference && issue.NodeId is not null)
        {
            return new WorkflowFailureSourceContext(
                WorkflowFailureSourceKind.Executor,
                issue.NodeId.Value.Value,
                SourceName: string.Empty,
                PluginId: string.Empty,
                PackageId: string.Empty,
                ExecutorType: string.Empty,
                ToolName: string.Empty,
                Operation: string.Empty,
                TemplateKey: string.Empty,
                TemplateFile: string.Empty,
                BackendKind: null);
        }

        return WorkflowFailureSourceContext.ForWorkflow();
    }

    private static string ResolveRepairHint(WorkflowValidationIssue issue)
        => issue.Code switch
        {
            WorkflowValidationIssueCode.MissingName => "Set a workflow name before saving or publishing.",
            WorkflowValidationIssueCode.MissingStartNode => "Add a start node and make the graph start id point to that node.",
            WorkflowValidationIssueCode.MissingEndNode => "Add at least one end node so the workflow can complete.",
            WorkflowValidationIssueCode.EmptyGraph => "Add start, work, and end nodes before saving the workflow.",
            WorkflowValidationIssueCode.DuplicateNodeId => "Give every workflow node a unique id.",
            WorkflowValidationIssueCode.DuplicateEdgeId => "Give every workflow edge a unique id.",
            WorkflowValidationIssueCode.DisconnectedNode => "Connect the node to the start path or remove it.",
            WorkflowValidationIssueCode.UnknownEdgeEndpoint => "Update the edge so both source and target nodes exist.",
            WorkflowValidationIssueCode.InvalidComponentReference => "Select an existing LLM component for the node.",
            WorkflowValidationIssueCode.InvalidExecutorReference => "Select a registered runnable executor for the executor node.",
            WorkflowValidationIssueCode.InvalidExecutorSettings => "Fix the executor settings JSON and required fields.",
            WorkflowValidationIssueCode.InvalidExecutionPolicy => "Use a retry and timeout policy allowed by the executor side-effect contract.",
            WorkflowValidationIssueCode.InvalidProviderModel => "Fix the provider model, pricing, or provider profile configuration.",
            WorkflowValidationIssueCode.InvalidWorkflowSettings => "Fix workflow settings before saving or running the workflow.",
            WorkflowValidationIssueCode.InvalidRouteDefinition => "Fix route metadata, JSON path, expected value, or route grouping.",
            WorkflowValidationIssueCode.UnsupportedRuntimeBackend => "Select a runtime backend registered in this host.",
            WorkflowValidationIssueCode.UnsupportedModality => "Use a supported component modality for workflow execution.",
            WorkflowValidationIssueCode.UnsupportedNodeKind => "Use an executable node kind or keep the workflow as draft.",
            WorkflowValidationIssueCode.ShapeMismatch => "Align source output and target input shapes on the connected nodes.",
            _ => "Fix the workflow definition and retry."
        };
}
