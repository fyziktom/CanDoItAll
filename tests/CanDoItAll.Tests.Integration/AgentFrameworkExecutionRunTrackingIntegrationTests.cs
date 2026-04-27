using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentFrameworkExecutionRunTrackingIntegrationTests
{
    [Fact]
    public async Task ExecuteRunAsync_refreshes_run_header_while_progress_logs_are_streaming()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-run-tracking");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var executionTask = workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Implement the current process step.",
                session.Id,
                new ExecutionInvocationContext(
                    SourceKind: "process-step",
                    SourceId: "step-001",
                    CorrelationId: "corr-001",
                    CausationId: "cause-001",
                    RequestedBy: "process-automation-dispatch",
                    RequestedByKind: "system",
                    MetadataJson: "{}",
                    ProcessRunId: "run-001",
                    ProcessStepId: "step-001"),
                AutoApprovePendingToolCalls: true));

        var executionRunId = await WaitForExecutionRunIdAsync(runtime, executionTask);
        await runtime.ProgressPersisted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Running, detail.Run.State);
        Assert.NotEmpty(detail.ExecutionLog);
        Assert.True(detail.Run.UpdatedAtUtc >= detail.ExecutionLog.Max(item => item.CreatedAtUtc));
        Assert.NotNull(detail.ChatSession);
        Assert.Equal(executionRunId, detail.ChatSession!.LatestExecutionRunId);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.ExecutionRunId == executionRunId &&
                     entry.State == ExecutionState.Running &&
                     entry.Phase == "Implementation");

        runtime.AllowCompletion.TrySetResult(true);

        var result = await executionTask;
        Assert.Equal(executionRunId, result.ExecutionRunId);
    }

    private static async Task<Guid> WaitForExecutionRunIdAsync(
        FakeProgressAgentRuntime runtime,
        Task<ExecutionRunResult> executionTask)
    {
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(runtime.ExecutionRunIdObserved.Task, executionTask, timeoutTask);
        if (completedTask == executionTask)
        {
            await executionTask;
        }

        if (completedTask == timeoutTask)
        {
            throw new TimeoutException("Timed out waiting for the fake runtime to observe the execution run id.");
        }

        return await runtime.ExecutionRunIdObserved.Task;
    }

    private sealed class FakeProgressAgentRuntime : IAgentRuntime
    {
        public TaskCompletionSource<Guid> ExecutionRunIdObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ProgressPersisted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResult(true, "ok", []));
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderTestChatResult(provider.DefaultModel, "ok", 1, 1));
        }

        public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(
            ProviderProfile provider,
            OllamaModelfileRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OllamaModelfileResult(request.TargetModel, request.BaseModel, request.SystemPrompt, request.ContextLength, string.Empty, "ok"));
        }

        public async Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null)
        {
            if (!session.LatestExecutionRunId.HasValue)
            {
                throw new InvalidOperationException("Expected a chat-backed execution run to populate the latest execution run id.");
            }

            ExecutionRunIdObserved.TrySetResult(session.LatestExecutionRunId.Value);
            await progressCallback(ExecutionState.Running, "Implementation", "Applying the current implementation plan.");
            ProgressPersisted.TrySetResult(true);
            await AllowCompletion.Task.WaitAsync(cancellationToken);

            return new AgentRuntimeResponse(
                ResponseText: "Completed successfully.",
                InputTokens: 10,
                OutputTokens: 20,
                ToolCalls: 0,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: []);
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null)
        {
            throw new NotSupportedException("Pending approval continuation is not used by this regression test.");
        }
    }
}
