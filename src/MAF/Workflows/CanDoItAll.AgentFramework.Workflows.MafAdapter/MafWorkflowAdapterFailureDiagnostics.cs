using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafWorkflowAdapterFailureDiagnostics
{
    public static WorkflowFailureDiagnosticEnvelope CompilationFailed(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        WorkflowRuntimeBackendKind backend,
        WorkflowCompilationResult compilation,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(compilation);

        if (!compilation.Validation.Succeeded && compilation.Validation.Issues.Count > 0)
        {
            var diagnostic = WorkflowFailureDiagnosticMapper.FromValidationIssue(
                compilation.Validation.Issues[0],
                CreateCorrelationId(),
                definition.Id);

            return diagnostic with
            {
                Message = WorkflowExecutorRedaction.RedactText(diagnostic.Message),
                WorkflowVersionId = definition.VersionId,
                RunId = runId,
                Source = diagnostic.Source with
                {
                    BackendKind = backend
                },
                OccurredAtUtc = occurredAtUtc
            };
        }

        var message = string.IsNullOrWhiteSpace(compilation.ErrorMessage)
            ? "Workflow compilation failed."
            : WorkflowExecutorRedaction.RedactText(compilation.ErrorMessage);

        return new WorkflowFailureDiagnosticEnvelope(
            WorkflowFailureKind.Runtime,
            WorkflowFailureRetryability.RetryableAfterRepair,
            message,
            "Fix the workflow definition or MAF workflow adapter registration before retrying.",
            WorkflowExecutorRedaction.RedactText(compilation.ErrorMessage),
            CreateCorrelationId(),
            definition.Id,
            definition.VersionId,
            runId,
            nodeId: null,
            executorId: null,
            new WorkflowFailureSourceContext(
                WorkflowFailureSourceKind.RuntimeBackend,
                backend.ToString(),
                "MAF in-process workflow runtime",
                PluginId: string.Empty,
                PackageId: string.Empty,
                ExecutorType: typeof(MafInProcessWorkflowExecutionBackend).FullName ?? string.Empty,
                ToolName: string.Empty,
                Operation: "workflow-compilation",
                TemplateKey: string.Empty,
                TemplateFile: string.Empty,
                backend),
            occurredAtUtc);
    }

    private static string CreateCorrelationId()
        => $"maf-workflow-adapter-{Guid.NewGuid():N}";
}
