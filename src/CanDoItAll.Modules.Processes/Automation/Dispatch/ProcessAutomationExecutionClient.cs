using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessAutomationExecutionClient
{
    Task<ProcessAutomationExecutionRunResult> ExecuteRunAsync(
        ProcessAutomationExecutionRequest request,
        CancellationToken cancellationToken = default);

    Task<ProcessAutomationExecutionRunDetail> GetExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessAutomationExecutionRunRecord>> ListExecutionRunsAsync(
        ProcessAutomationExecutionRunQuery query,
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
    public async Task<ProcessAutomationExecutionRunResult> ExecuteRunAsync(
        ProcessAutomationExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        try
        {
            var result = await workspaceService.ExecuteRunAsync(
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

            return MapResult(result);
        }
        catch (AgentChatRunFailedException exception)
        {
            throw MapFailure(exception, "chat-run");
        }
        catch (AgentRunFailedException exception)
        {
            throw MapFailure(exception, "run");
        }
    }

    public async Task<ProcessAutomationExecutionRunDetail> GetExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
    {
        var detail = await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
        return MapDetail(detail);
    }

    public async Task<IReadOnlyList<ProcessAutomationExecutionRunRecord>> ListExecutionRunsAsync(
        ProcessAutomationExecutionRunQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var runs = await workspaceService.ListExecutionRunsAsync(MapQuery(query), cancellationToken);
        return runs.Select(MapRun).ToList();
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

    private static ExecutionRunQuery MapQuery(ProcessAutomationExecutionRunQuery query)
    {
        return new ExecutionRunQuery(
            AgentId: query.AgentId,
            ChatSessionId: query.ChatSessionId,
            CorrelationId: query.CorrelationId,
            SourceKind: query.SourceKind,
            SourceId: query.SourceId,
            Take: query.Take,
            ProcessRunId: query.ProcessRunId,
            ProcessStepId: query.ProcessStepId,
            SchedulerRunId: query.SchedulerRunId,
            MessageId: query.MessageId,
            State: query.State.HasValue ? MapState(query.State.Value) : null,
            Outcome: query.Outcome.HasValue ? MapOutcome(query.Outcome.Value) : null,
            CreatedFromUtc: query.CreatedFromUtc,
            CreatedToUtc: query.CreatedToUtc,
            UpdatedFromUtc: query.UpdatedFromUtc,
            UpdatedToUtc: query.UpdatedToUtc);
    }

    private static ProcessAutomationExecutionRunResult MapResult(ExecutionRunResult result)
    {
        return new ProcessAutomationExecutionRunResult(
            result.ExecutionRunId,
            result.ChatSessionId,
            result.ResponseText,
            MapMetric(result.Metric));
    }

    private static ProcessAutomationExecutionRunDetail MapDetail(ExecutionRunDetail detail)
    {
        return new ProcessAutomationExecutionRunDetail(
            MapRun(detail.Run),
            MapChatSession(detail.ChatSession),
            detail.ExecutionLog.Select(MapExecutionLogEntry).ToList(),
            detail.Metrics.Select(MapMetric).ToList())
        {
            Artifacts = detail.Artifacts.Select(MapArtifact).ToList(),
            ToolReceipts = detail.ToolReceipts.Select(MapToolReceipt).ToList(),
            UsageObservations = detail.UsageObservations.Select(MapUsageObservation).ToList()
        };
    }

    private static ProcessAutomationExecutionRunRecord MapRun(ExecutionRunRecord run)
    {
        return new ProcessAutomationExecutionRunRecord(
            run.Id,
            run.AgentId,
            run.ChatSessionId,
            run.Title,
            run.SourceKind,
            run.SourceId,
            run.CorrelationId,
            run.CausationId,
            run.RequestedBy,
            run.RequestedByKind,
            run.MetadataJson,
            run.InputSummary,
            run.ResultSummary,
            run.ProviderName,
            run.Model,
            MapState(run.State),
            run.Outcome.HasValue ? MapOutcome(run.Outcome.Value) : null,
            run.CreatedAtUtc,
            run.UpdatedAtUtc,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.RuntimeSessionKey,
            run.SerializedSessionStateJson,
            run.PendingApprovals.Select(MapPendingApproval).ToList(),
            run.AutoApprovePendingToolCalls,
            run.ProcessRunId,
            run.ProcessStepId,
            run.SchedulerRunId,
            run.MessageId,
            run.Revision,
            run.StructuredOutputContractKey,
            run.StructuredOutputTypeName,
            run.StructuredOutputSchemaName,
            run.StructuredOutputSchemaDescription);
    }

    private static ProcessAutomationChatSession? MapChatSession(ChatSessionRecord? session)
    {
        return session is null
            ? null
            : new ProcessAutomationChatSession(
                session.Id,
                session.AgentId,
                session.Title,
                session.CreatedAtUtc,
                session.UpdatedAtUtc,
                session.Messages.Select(MapChatMessage).ToList(),
                session.LatestExecutionRunId);
    }

    private static ProcessAutomationChatMessage MapChatMessage(ChatMessageRecord message)
    {
        return new ProcessAutomationChatMessage(
            message.Id,
            MapChatMessageRole(message.Role),
            message.Content,
            message.CreatedAtUtc,
            message.TokenEstimate);
    }

    private static ProcessAutomationPendingToolApproval MapPendingApproval(PendingToolApprovalRecord approval)
    {
        return new ProcessAutomationPendingToolApproval(
            approval.ApprovalId,
            approval.CallId,
            approval.ToolName,
            approval.ToolKind,
            approval.Details,
            approval.ArgumentsJson);
    }

    private static ProcessAutomationExecutionLogEntry MapExecutionLogEntry(ExecutionLogEntry entry)
    {
        return new ProcessAutomationExecutionLogEntry(
            entry.Id,
            entry.AgentId,
            entry.ChatSessionId,
            entry.CreatedAtUtc,
            MapState(entry.State),
            entry.Phase,
            entry.Message)
        {
            ExecutionRunId = entry.ExecutionRunId
        };
    }

    private static ProcessAutomationRunMetric MapMetric(AgentRunMetric metric)
    {
        return new ProcessAutomationRunMetric(
            metric.Id,
            metric.AgentId,
            metric.ChatSessionId,
            metric.CreatedAtUtc,
            MapOutcome(metric.Outcome),
            metric.ProviderName,
            metric.Model,
            metric.DurationMs,
            metric.InputTokens,
            metric.OutputTokens,
            metric.ToolCalls)
        {
            ExecutionRunId = metric.ExecutionRunId,
            CachedInputTokens = metric.CachedInputTokens,
            CostUsd = metric.CostUsd
        };
    }

    private static ProcessAutomationExecutionArtifact MapArtifact(ExecutionArtifactRecord artifact)
    {
        return new ProcessAutomationExecutionArtifact(
            artifact.Id,
            artifact.ExecutionRunId,
            artifact.ArtifactKind,
            artifact.DisplayName,
            artifact.RelativePath,
            artifact.ContentType,
            artifact.ProducedBy,
            artifact.Summary,
            artifact.CreatedAtUtc);
    }

    private static ProcessAutomationToolExecutionReceipt MapToolReceipt(ToolExecutionReceiptRecord receipt)
    {
        return new ProcessAutomationToolExecutionReceipt(
            receipt.Id,
            receipt.ExecutionRunId,
            receipt.ToolFamily,
            receipt.ToolName,
            receipt.RiskClass,
            receipt.ApprovalMode,
            receipt.IsolationGuarantee,
            receipt.RequestSummary,
            receipt.WorkingDirectory,
            receipt.ExitSummary,
            receipt.StartedAtUtc,
            receipt.CompletedAtUtc)
        {
            RuntimeToolProviderKey = receipt.RuntimeToolProviderKey,
            RuntimeToolProviderName = receipt.RuntimeToolProviderName
        };
    }

    private static ProcessAutomationProviderUsageObservation MapUsageObservation(ProviderUsageObservation observation)
    {
        return new ProcessAutomationProviderUsageObservation(
            observation.Id,
            observation.CreatedAtUtc,
            observation.ProviderName,
            observation.ProviderKind.ToString(),
            observation.Model,
            observation.TransportKind.ToString(),
            observation.SourcePhase,
            MapUsageStatus(observation.UsageStatus),
            observation.InputTokens,
            observation.CachedInputTokens,
            observation.OutputTokens,
            observation.ReasoningTokens,
            observation.TotalTokens,
            observation.ToolCallCount)
        {
            ExecutionRunId = observation.ExecutionRunId,
            AgentId = observation.AgentId,
            ChatSessionId = observation.ChatSessionId,
            ProviderResponseId = observation.ProviderResponseId,
            ProviderRequestId = observation.ProviderRequestId,
            RuntimeSessionKey = observation.RuntimeSessionKey,
            ProcessRunId = observation.ProcessRunId,
            ProcessStepId = observation.ProcessStepId,
            WorkflowRunId = observation.WorkflowRunId,
            WorkflowNodeId = observation.WorkflowNodeId,
            CorrelationId = observation.CorrelationId,
            ProviderCostUsd = observation.ProviderCostUsd,
            CalculatedCostUsd = observation.CalculatedCostUsd,
            PricingProfileHash = observation.PricingProfileHash,
            PricingVersion = observation.PricingVersion,
            RawUsageJson = observation.RawUsageJson,
            DiagnosticsJson = observation.DiagnosticsJson
        };
    }

    private static ProcessAutomationExecutionState MapState(ExecutionState state)
    {
        return state switch
        {
            ExecutionState.Idle => ProcessAutomationExecutionState.Idle,
            ExecutionState.Preparing => ProcessAutomationExecutionState.Preparing,
            ExecutionState.Running => ProcessAutomationExecutionState.Running,
            ExecutionState.WaitingOnTool => ProcessAutomationExecutionState.WaitingOnTool,
            ExecutionState.Persisting => ProcessAutomationExecutionState.Persisting,
            ExecutionState.Completed => ProcessAutomationExecutionState.Completed,
            ExecutionState.Failed => ProcessAutomationExecutionState.Failed,
            _ => throw new InvalidOperationException($"Unsupported execution state '{state}'.")
        };
    }

    private static ExecutionState MapState(ProcessAutomationExecutionState state)
    {
        return state switch
        {
            ProcessAutomationExecutionState.Idle => ExecutionState.Idle,
            ProcessAutomationExecutionState.Preparing => ExecutionState.Preparing,
            ProcessAutomationExecutionState.Running => ExecutionState.Running,
            ProcessAutomationExecutionState.WaitingOnTool => ExecutionState.WaitingOnTool,
            ProcessAutomationExecutionState.Persisting => ExecutionState.Persisting,
            ProcessAutomationExecutionState.Completed => ExecutionState.Completed,
            ProcessAutomationExecutionState.Failed => ExecutionState.Failed,
            _ => throw new InvalidOperationException($"Unsupported process automation execution state '{state}'.")
        };
    }

    private static ProcessAutomationRunOutcome MapOutcome(RunOutcome outcome)
    {
        return outcome switch
        {
            RunOutcome.Succeeded => ProcessAutomationRunOutcome.Succeeded,
            RunOutcome.Failed => ProcessAutomationRunOutcome.Failed,
            RunOutcome.Cancelled => ProcessAutomationRunOutcome.Cancelled,
            _ => throw new InvalidOperationException($"Unsupported run outcome '{outcome}'.")
        };
    }

    private static RunOutcome MapOutcome(ProcessAutomationRunOutcome outcome)
    {
        return outcome switch
        {
            ProcessAutomationRunOutcome.Succeeded => RunOutcome.Succeeded,
            ProcessAutomationRunOutcome.Failed => RunOutcome.Failed,
            ProcessAutomationRunOutcome.Cancelled => RunOutcome.Cancelled,
            _ => throw new InvalidOperationException($"Unsupported process automation run outcome '{outcome}'.")
        };
    }

    private static ProcessAutomationChatMessageRole MapChatMessageRole(ChatMessageRole role)
    {
        return role switch
        {
            ChatMessageRole.System => ProcessAutomationChatMessageRole.System,
            ChatMessageRole.User => ProcessAutomationChatMessageRole.User,
            ChatMessageRole.Assistant => ProcessAutomationChatMessageRole.Assistant,
            _ => throw new InvalidOperationException($"Unsupported chat message role '{role}'.")
        };
    }

    private static ProcessAutomationProviderUsageStatus MapUsageStatus(ProviderUsageObservationStatus status)
    {
        return status switch
        {
            ProviderUsageObservationStatus.Observed => ProcessAutomationProviderUsageStatus.Observed,
            ProviderUsageObservationStatus.MissingAfterProviderActivity => ProcessAutomationProviderUsageStatus.MissingAfterProviderActivity,
            ProviderUsageObservationStatus.UsageUnavailable => ProcessAutomationProviderUsageStatus.UsageUnavailable,
            ProviderUsageObservationStatus.EstimatedFromMetric => ProcessAutomationProviderUsageStatus.EstimatedFromMetric,
            ProviderUsageObservationStatus.ObservedFromMetric => ProcessAutomationProviderUsageStatus.ObservedFromMetric,
            _ => throw new InvalidOperationException($"Unsupported provider usage status '{status}'.")
        };
    }

    private static ProcessAutomationExecutionFailedException MapFailure(
        AgentChatRunFailedException exception,
        string failureKind)
    {
        return new ProcessAutomationExecutionFailedException(
            exception.AgentId,
            exception.ExecutionRunId,
            exception.ChatSessionId,
            exception.ProviderName,
            exception.ModelName,
            failureKind,
            exception.Message,
            exception);
    }

    private static ProcessAutomationExecutionFailedException MapFailure(
        AgentRunFailedException exception,
        string failureKind)
    {
        return new ProcessAutomationExecutionFailedException(
            exception.AgentId,
            exception.ExecutionRunId,
            exception.ChatSessionId,
            exception.ProviderName,
            exception.ModelName,
            failureKind,
            exception.Message,
            exception);
    }
}
