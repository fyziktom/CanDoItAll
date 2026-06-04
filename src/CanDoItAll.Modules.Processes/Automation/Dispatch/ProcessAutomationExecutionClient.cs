using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessAutomationExecutionClient
{
    Task<ExecutionRunResult> ExecuteRunAsync(
        ProcessAutomationExecutionRequest request,
        CancellationToken cancellationToken = default);

    Task<ExecutionRunDetail> GetExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ExecutionRunQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
        bool includeTemplates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default);

    Task<ProviderHealthResult> TestProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<AgentEditorModel> GetAgentEditorAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<Guid> SaveAgentAsync(
        AgentEditorModel model,
        CancellationToken cancellationToken = default);
}

internal sealed class ProcessAutomationExecutionClient(
    IAgentFrameworkWorkspaceService workspaceService) : IProcessAutomationExecutionClient
{
    public Task<ExecutionRunResult> ExecuteRunAsync(
        ProcessAutomationExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        return workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                request.AgentId,
                request.Prompt,
                ChatSessionId: null,
                Context: new ExecutionInvocationContext(
                    SourceKind: request.Source.SourceKind,
                    SourceId: request.Source.SourceId,
                    CorrelationId: request.Source.CorrelationId,
                    CausationId: request.Source.CausationId,
                    RequestedBy: request.Source.RequestedBy,
                    RequestedByKind: request.Source.RequestedByKind,
                    MetadataJson: request.Source.MetadataJson,
                    ProcessRunId: request.Source.ProcessRunId,
                    ProcessStepId: request.Source.ProcessStepId,
                    SchedulerRunId: request.Source.SchedulerRunId,
                    MessageId: request.Source.MessageId,
                    Policy: new ExecutionInvocationPolicy(
                        MapFinalizerMode(request.Policy.FinalizerMode),
                        request.Policy.MaxStructuredOutputRepairAttempts,
                        request.Policy.RequireStructuredOutputValidation)),
                AutoApprovePendingToolCalls: request.AutoApprovePendingToolCalls,
                StructuredOutput: MapStructuredOutput(request.StructuredOutputKind)),
            cancellationToken);
    }

    public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
    {
        return workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
    }

    public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ExecutionRunQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return workspaceService.ListExecutionRunsAsync(query, cancellationToken);
    }

    public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
        bool includeTemplates,
        CancellationToken cancellationToken = default)
    {
        return workspaceService.ListAgentsAsync(includeTemplates, cancellationToken);
    }

    public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        return workspaceService.ListProvidersAsync(cancellationToken);
    }

    public Task<ProviderHealthResult> TestProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        return workspaceService.TestProviderAsync(providerId, cancellationToken);
    }

    public Task<AgentEditorModel> GetAgentEditorAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return workspaceService.GetAgentEditorAsync(agentId, cancellationToken);
    }

    public Task<Guid> SaveAgentAsync(
        AgentEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        return workspaceService.SaveAgentAsync(model, cancellationToken);
    }

    private static AgentFinalizerMode? MapFinalizerMode(ProcessAutomationFinalizerMode? finalizerMode)
    {
        return finalizerMode switch
        {
            null => null,
            ProcessAutomationFinalizerMode.Required => AgentFinalizerMode.Required,
            _ => throw new InvalidOperationException($"Unsupported process automation finalizer mode '{finalizerMode}'.")
        };
    }

    private static AgentStructuredOutputContract? MapStructuredOutput(ProcessAutomationStructuredOutputKind structuredOutputKind)
    {
        return structuredOutputKind switch
        {
            ProcessAutomationStructuredOutputKind.None => null,
            ProcessAutomationStructuredOutputKind.ProcessStepOutcomeResult => AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            _ => throw new InvalidOperationException($"Unsupported process automation structured output kind '{structuredOutputKind}'.")
        };
    }

    private static void ValidateRequest(ProcessAutomationExecutionRequest request)
    {
        if (request.AgentId == Guid.Empty)
        {
            throw new ArgumentException("Process automation execution requires a technical agent id.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new ArgumentException("Process automation execution requires a prompt.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Policy);
    }
}
