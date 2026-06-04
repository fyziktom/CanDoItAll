using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessAutomationExecutionClientTests
{
    [Fact]
    public async Task ExecuteRunAsync_SB05_INV_001_delegates_to_agent_framework_workspace_service()
    {
        var workspaceService = CreateWorkspaceService(out var proxy);
        var client = new ProcessAutomationExecutionClient(workspaceService);
        var request = CreateProcessAutomationExecutionRequest();
        var cancellationToken = new CancellationTokenSource().Token;

        var result = await client.ExecuteRunAsync(request, cancellationToken);

        Assert.NotSame(proxy.ExecutionRunResult, result);
        Assert.Equal(proxy.ExecutionRunResult.ExecutionRunId, result.ExecutionRunId);
        Assert.Equal(proxy.ExecutionRunResult.ChatSessionId, result.ChatSessionId);
        Assert.Equal(proxy.ExecutionRunResult.ResponseText, result.ResponseText);
        Assert.NotNull(result.Metric);
        Assert.Equal(ProcessAutomationRunOutcome.Succeeded, result.Metric.Outcome);
        Assert.Equal(proxy.ExecutionRunResult.Metric.ExecutionRunId, result.Metric.ExecutionRunId);
        var call = Assert.Single(proxy.Calls, item => item.MethodName == nameof(IAgentFrameworkWorkspaceService.ExecuteRunAsync));
        var executionRunRequest = Assert.IsType<ExecutionRunRequest>(call.Arguments[0]);
        Assert.Equal(request.AgentId, executionRunRequest.AgentId);
        Assert.Equal(request.Prompt, executionRunRequest.Prompt);
        Assert.True(executionRunRequest.AutoApprovePendingToolCalls);
        Assert.NotNull(executionRunRequest.Context);
        Assert.Equal(request.Source.SourceKind, executionRunRequest.Context.SourceKind);
        Assert.Equal(request.Source.SourceId, executionRunRequest.Context.SourceId);
        Assert.Equal(request.Source.ProcessRunId, executionRunRequest.Context.ProcessRunId);
        Assert.Equal(request.Source.ProcessStepId, executionRunRequest.Context.ProcessStepId);
        Assert.NotNull(executionRunRequest.Context.Policy);
        Assert.Equal(AgentFinalizerMode.Required, executionRunRequest.Context.Policy.FinalizerMode);
        Assert.True(executionRunRequest.Context.Policy.RequireStructuredOutputValidation);
        Assert.Equal(AgentStructuredOutputContracts.ProcessStepOutcomeResultKey, executionRunRequest.StructuredOutput?.ContractKey);
        Assert.Equal(cancellationToken, call.Arguments[1]);
    }

    [Fact]
    public async Task ExecutionQueries_SB05_INV_002_delegate_to_agent_framework_workspace_service()
    {
        var workspaceService = CreateWorkspaceService(out var proxy);
        var client = new ProcessAutomationExecutionClient(workspaceService);
        var executionRunId = proxy.ExecutionRunDetail.Run.Id;
        var query = new ProcessAutomationExecutionRunQuery(
            ProcessRunId: "process-run-001",
            Take: 12,
            State: ProcessAutomationExecutionState.Completed,
            Outcome: ProcessAutomationRunOutcome.Succeeded);

        var detail = await client.GetExecutionRunDetailAsync(executionRunId);
        var runs = await client.ListExecutionRunsAsync(query);

        Assert.NotSame(proxy.ExecutionRunDetail, detail);
        Assert.NotSame(proxy.ExecutionRuns, runs);
        Assert.Equal(ProcessAutomationExecutionState.Completed, detail.Run.State);
        Assert.Equal(ProcessAutomationRunOutcome.Succeeded, detail.Run.Outcome);
        Assert.Equal(proxy.ExecutionRunDetail.Run.Id, detail.Run.Id);
        Assert.Equal(proxy.ExecutionRunDetail.Run.ProcessRunId, detail.Run.ProcessRunId);
        Assert.Equal(proxy.ExecutionRunDetail.Run.StructuredOutputContractKey, detail.Run.StructuredOutputContractKey);
        Assert.Equal(proxy.ExecutionRunDetail.ChatSession!.LatestExecutionRunId, detail.ChatSession!.LatestExecutionRunId);
        Assert.Equal(ProcessAutomationChatMessageRole.User, detail.ChatSession.Messages[0].Role);
        Assert.Equal(ProcessAutomationExecutionState.Running, detail.ExecutionLog[0].State);
        Assert.Equal(ProcessAutomationRunOutcome.Succeeded, detail.Metrics[0].Outcome);
        Assert.Equal("design.md", detail.Artifacts[0].RelativePath);
        Assert.Equal("workspace-write", detail.ToolReceipts[0].RuntimeToolProviderKey);
        Assert.Equal(ProcessAutomationProviderUsageStatus.Observed, detail.UsageObservations[0].UsageStatus);
        var run = Assert.Single(runs);
        Assert.Equal(detail.Run.Id, run.Id);
        Assert.Equal(ProcessAutomationExecutionState.Completed, run.State);
        Assert.Contains(proxy.Calls, call =>
            call.MethodName == nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync) &&
            Equals(executionRunId, call.Arguments[0]));
        var listCall = Assert.Single(proxy.Calls, call =>
            call.MethodName == nameof(IAgentFrameworkWorkspaceService.ListExecutionRunsAsync));
        var executionRunQuery = Assert.IsType<ExecutionRunQuery>(listCall.Arguments[0]);
        Assert.Equal(query.ProcessRunId, executionRunQuery.ProcessRunId);
        Assert.Equal(query.Take, executionRunQuery.Take);
        Assert.Equal(ExecutionState.Completed, executionRunQuery.State);
        Assert.Equal(RunOutcome.Succeeded, executionRunQuery.Outcome);
    }

    [Fact]
    public async Task ExecuteRunAsync_SB06_INV_001_normalizes_agent_framework_execution_failures()
    {
        var workspaceService = CreateWorkspaceService(out var proxy);
        var client = new ProcessAutomationExecutionClient(workspaceService);
        proxy.ExecuteRunFailure = new AgentRunFailedException(
            proxy.ExecutionRunDetail.Run.AgentId,
            proxy.ExecutionRunDetail.Run.Id,
            proxy.ExecutionRunDetail.Run.ChatSessionId,
            "OpenAI",
            "gpt-5-mini",
            new InvalidOperationException("provider quota exceeded"));

        var exception = await Assert.ThrowsAsync<ProcessAutomationExecutionFailedException>(() =>
            client.ExecuteRunAsync(CreateProcessAutomationExecutionRequest()));

        Assert.Equal(proxy.ExecutionRunDetail.Run.AgentId, exception.AgentId);
        Assert.Equal(proxy.ExecutionRunDetail.Run.Id, exception.ExecutionRunId);
        Assert.Equal(proxy.ExecutionRunDetail.Run.ChatSessionId, exception.ChatSessionId);
        Assert.Equal("OpenAI", exception.ProviderName);
        Assert.Equal("gpt-5-mini", exception.ModelName);
        Assert.Equal("run", exception.FailureKind);
        Assert.IsType<AgentRunFailedException>(exception.InnerException);
    }

    [Fact]
    public async Task CatalogAndEditorOperations_SB05_INV_003_delegate_to_agent_framework_workspace_service()
    {
        var workspaceService = CreateWorkspaceService(out var proxy);
        var client = new ProcessAutomationExecutionClient(workspaceService);
        var providerId = proxy.Providers[0].Id;
        var agentId = proxy.Agents[0].Id;

        var agents = await client.ListAgentsAsync(includeTemplates: false);
        var providers = await client.ListProvidersAsync();
        var health = await client.TestProviderAsync(providerId);
        var editor = await client.GetAgentEditorAsync(agentId);
        var savedAgentId = await client.SaveAgentAsync(editor);

        Assert.Same(proxy.Agents, agents);
        Assert.Same(proxy.Providers, providers);
        Assert.Same(proxy.ProviderHealthResult, health);
        Assert.Same(proxy.AgentEditor, editor);
        Assert.Equal(proxy.SavedAgentId, savedAgentId);
        Assert.Contains(proxy.Calls, call =>
            call.MethodName == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) &&
            Equals(false, call.Arguments[0]));
        Assert.Contains(proxy.Calls, call =>
            call.MethodName == nameof(IAgentFrameworkWorkspaceService.ListProvidersAsync));
        Assert.Contains(proxy.Calls, call =>
            call.MethodName == nameof(IAgentFrameworkWorkspaceService.TestProviderAsync) &&
            Equals(providerId, call.Arguments[0]));
        Assert.Contains(proxy.Calls, call =>
            call.MethodName == nameof(IAgentFrameworkWorkspaceService.GetAgentEditorAsync) &&
            Equals(agentId, call.Arguments[0]));
        Assert.Contains(proxy.Calls, call =>
            call.MethodName == nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) &&
            ReferenceEquals(editor, call.Arguments[0]));
    }

    [Fact]
    public void AddProcessesModule_SB05_INV_004_registers_process_owned_execution_client()
    {
        var services = new ServiceCollection();

        services.AddProcessesModule(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IProcessAutomationExecutionClient));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(ProcessAutomationExecutionClient), descriptor.ImplementationType);
    }

    private static IAgentFrameworkWorkspaceService CreateWorkspaceService(out RecordingWorkspaceProxy proxy)
    {
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecordingWorkspaceProxy>();
        proxy = (RecordingWorkspaceProxy)(object)service;
        return service;
    }

    private static ProcessAutomationExecutionRequest CreateProcessAutomationExecutionRequest()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        return new ProcessAutomationExecutionRequest(
            Guid.NewGuid(),
            "Run the process automation step.",
            new ProcessAutomationInvocationSource(
                SourceKind: "process-step",
                SourceId: stepRunId.ToString("D"),
                CorrelationId: $"process-step:{stepRunId:D}",
                CausationId: "test-trigger",
                RequestedBy: "process-automation-dispatch",
                RequestedByKind: "system",
                MetadataJson: "{}",
                ProcessRunId: processRunId.ToString("D"),
                ProcessStepId: stepRunId.ToString("D")),
            new ProcessAutomationInvocationPolicy(
                ProcessAutomationFinalizerMode.Required,
                MaxStructuredOutputRepairAttempts: 2,
                RequireStructuredOutputValidation: true),
            AutoApprovePendingToolCalls: true,
            StructuredOutputKind: ProcessAutomationStructuredOutputKind.ProcessStepOutcomeResult);
    }

    private static ExecutionRunRecord CreateExecutionRunRecord(Guid agentId, Guid? chatSessionId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            agentId,
            chatSessionId,
            "Process automation execution",
            "process",
            "process-run-001",
            "correlation-001",
            "causation-001",
            "process automation",
            "system",
            "{}",
            "Run the process automation step.",
            "Completed.",
            "OpenAI",
            "gpt-5-mini",
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            now,
            now,
            now,
            now,
            string.Empty,
            null,
            []);
    }

    private static AgentRunMetric CreateMetric(Guid agentId, Guid? chatSessionId, Guid executionRunId)
    {
        return new AgentRunMetric(
            Guid.NewGuid(),
            agentId,
            chatSessionId,
            DateTimeOffset.UtcNow,
            RunOutcome.Succeeded,
            "OpenAI",
            "gpt-5-mini",
            DurationMs: 25,
            InputTokens: 10,
            OutputTokens: 5,
            ToolCalls: 1)
        {
            ExecutionRunId = executionRunId
        };
    }

    private static AgentDefinition CreateAgent(Guid agentId)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            agentId,
            "Process Agent",
            "Developer",
            "Executes process automation.",
            "Implement the assigned process step.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-5-mini",
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ProviderProfile CreateProvider(Guid providerId)
    {
        return new ProviderProfile(
            providerId,
            "OpenAI",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private sealed record WorkspaceCall(string MethodName, object?[] Arguments);

    private class RecordingWorkspaceProxy : DispatchProxy
    {
        private readonly Guid agentId = Guid.NewGuid();
        private readonly Guid chatSessionId = Guid.NewGuid();
        private readonly Guid providerId = Guid.NewGuid();

        public RecordingWorkspaceProxy()
        {
            var run = CreateExecutionRunRecord(agentId, chatSessionId);
            var metric = CreateMetric(agentId, chatSessionId, run.Id);
            var chatSession = new ChatSessionRecord(
                chatSessionId,
                agentId,
                "Process execution chat",
                run.CreatedAtUtc,
                run.UpdatedAtUtc,
                [
                    new ChatMessageRecord(
                        Guid.NewGuid(),
                        ChatMessageRole.User,
                        "Run the process step.",
                        run.CreatedAtUtc,
                        TokenEstimate: 4)
                ],
                LatestExecutionRunId: run.Id);
            var log = new ExecutionLogEntry(
                Guid.NewGuid(),
                agentId,
                chatSessionId,
                run.UpdatedAtUtc,
                ExecutionState.Running,
                "Tool",
                "Invoked workspace_write_file.")
            {
                ExecutionRunId = run.Id
            };
            var artifact = new ExecutionArtifactRecord(
                Guid.NewGuid(),
                run.Id,
                "process-evidence",
                "Design",
                "design.md",
                "text/markdown",
                "agent",
                "Created design artifact.",
                run.UpdatedAtUtc);
            var receipt = new ToolExecutionReceiptRecord(
                Guid.NewGuid(),
                run.Id,
                "workspace-process",
                "workspace_write_file",
                "medium",
                "auto",
                "workspace",
                "Write design.md",
                "C:\\repo",
                "Succeeded",
                run.StartedAtUtc ?? run.CreatedAtUtc,
                run.CompletedAtUtc ?? run.UpdatedAtUtc)
            {
                RuntimeToolProviderKey = "workspace-write",
                RuntimeToolProviderName = "Workspace write"
            };
            var usageObservation = new ProviderUsageObservation(
                Guid.NewGuid(),
                run.UpdatedAtUtc,
                "OpenAI",
                ProviderKind.OpenAi,
                "gpt-5-mini",
                ProviderTransportKind.Responses,
                ProviderUsageSourcePhases.AgentRuntime,
                ProviderUsageObservationStatus.Observed,
                InputTokens: 10,
                CachedInputTokens: 2,
                OutputTokens: 5,
                ReasoningTokens: 0,
                TotalTokens: 17,
                ToolCallCount: 1)
            {
                ExecutionRunId = run.Id,
                AgentId = agentId,
                ChatSessionId = chatSessionId,
                ProviderResponseId = "resp-001",
                ProviderRequestId = "req-001",
                RuntimeSessionKey = run.RuntimeSessionKey,
                ProcessRunId = run.ProcessRunId,
                ProcessStepId = run.ProcessStepId,
                CorrelationId = run.CorrelationId,
                ProviderCostUsd = 0.12m,
                CalculatedCostUsd = 0.12m,
                PricingProfileHash = "pricing-hash",
                PricingVersion = "2026-06-04",
                RawUsageJson = "{}",
                DiagnosticsJson = "{}"
            };
            ExecutionRunResult = new ExecutionRunResult(
                run.Id,
                chatSessionId,
                "Completed.",
                null,
                metric);
            ExecutionRunDetail = new ExecutionRunDetail(run, chatSession, [log], [metric])
            {
                Artifacts = [artifact],
                ToolReceipts = [receipt],
                UsageObservations = [usageObservation]
            };
            ExecutionRuns = [run];
            Agents = [CreateAgent(agentId)];
            Providers = [CreateProvider(providerId)];
            ProviderHealthResult = new ProviderHealthResult(true, "Healthy.", []);
            AgentEditor = new AgentEditorModel
            {
                Id = agentId,
                Name = "Process Agent"
            };
            SavedAgentId = agentId;
        }

        public List<WorkspaceCall> Calls { get; } = [];

        public ExecutionRunResult ExecutionRunResult { get; }

        public ExecutionRunDetail ExecutionRunDetail { get; }

        public IReadOnlyList<ExecutionRunRecord> ExecutionRuns { get; }

        public IReadOnlyList<AgentDefinition> Agents { get; }

        public IReadOnlyList<ProviderProfile> Providers { get; }

        public ProviderHealthResult ProviderHealthResult { get; }

        public AgentEditorModel AgentEditor { get; }

        public Guid SavedAgentId { get; }

        public Exception? ExecuteRunFailure { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new InvalidOperationException("Workspace service method was not supplied.");
            }

            var arguments = args ?? [];
            Calls.Add(new WorkspaceCall(targetMethod.Name, arguments));

            return targetMethod.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.ExecuteRunAsync) => ExecuteRunFailure is null
                    ? Task.FromResult(ExecutionRunResult)
                    : Task.FromException<ExecutionRunResult>(ExecuteRunFailure),
                nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync) => Task.FromResult(ExecutionRunDetail),
                nameof(IAgentFrameworkWorkspaceService.ListExecutionRunsAsync) => Task.FromResult(ExecutionRuns),
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) => Task.FromResult(Agents),
                nameof(IAgentFrameworkWorkspaceService.ListProvidersAsync) => Task.FromResult(Providers),
                nameof(IAgentFrameworkWorkspaceService.TestProviderAsync) => Task.FromResult(ProviderHealthResult),
                nameof(IAgentFrameworkWorkspaceService.GetAgentEditorAsync) => Task.FromResult(AgentEditor),
                nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) => Task.FromResult(SavedAgentId),
                "add_ExecutionUpdated" or "remove_ExecutionUpdated" => null,
                _ => throw new NotSupportedException($"Unexpected workspace service call: {targetMethod.Name}.")
            };
        }
    }
}
