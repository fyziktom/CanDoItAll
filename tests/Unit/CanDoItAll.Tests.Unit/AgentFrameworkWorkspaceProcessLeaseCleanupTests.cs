using System.Collections.Concurrent;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFrameworkWorkspaceProcessLeaseCleanupTests
{
    [Fact]
    public async Task Persisted_completed_execution_cleans_process_leases_once()
    {
        var cleaner = new RecordingProcessLeaseCleaner();
        using var context = await CreateContextAsync(
            new StubAgentRuntime(CreateCompletedResponse),
            cleaner);

        var result = await context.Service.ExecuteRunAsync(
            CreateRequest(context.Agent.Id));
        var persistedRun = Assert.IsType<ExecutionRunRecord>(
            await context.Store.GetExecutionRunAsync(result.ExecutionRunId));

        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.Equal(ExecutionState.Completed, persistedRun.State);
        Assert.Equal(
            result.ExecutionRunId,
            Assert.Single(cleaner.ExecutionRunIds));
    }

    [Fact]
    public async Task Persisted_failed_execution_cleans_process_leases_once()
    {
        var cleaner = new RecordingProcessLeaseCleaner();
        var runtimeFailure = new InvalidOperationException(
            "The runtime failed before producing a response.");
        using var context = await CreateContextAsync(
            new StubAgentRuntime(() => throw runtimeFailure),
            cleaner);

        var exception = await Assert.ThrowsAsync<AgentRunFailedException>(
            () => context.Service.ExecuteRunAsync(
                CreateRequest(context.Agent.Id)));
        var persistedRun = Assert.IsType<ExecutionRunRecord>(
            await context.Store.GetExecutionRunAsync(exception.ExecutionRunId));

        Assert.Same(runtimeFailure, exception.InnerException);
        Assert.Equal(ExecutionState.Failed, persistedRun.State);
        Assert.Equal(
            exception.ExecutionRunId,
            Assert.Single(cleaner.ExecutionRunIds));
    }

    [Fact]
    public async Task Waiting_on_tool_execution_and_nonterminal_continuation_do_not_clean_process_leases()
    {
        var cleaner = new RecordingProcessLeaseCleaner();
        var runtime = new StubAgentRuntime(
            () => CreateWaitingResponse("approval-initial"),
            () => CreateWaitingResponse("approval-continuation"));
        using var context = await CreateContextAsync(runtime, cleaner);

        var initialResult = await context.Service.ExecuteRunAsync(
            CreateRequest(context.Agent.Id));
        var continuationResult =
            await context.Service.ContinueExecutionRunAsync(
                initialResult.ExecutionRunId,
                AgentExecutionOperationId.New(),
                decisions: [new PendingToolApprovalDecision("approval-initial", Approved: true)]);
        var persistedRun = Assert.IsType<ExecutionRunRecord>(
            await context.Store.GetExecutionRunAsync(
                continuationResult.ExecutionRunId));

        Assert.Equal(ExecutionState.WaitingOnTool, initialResult.State);
        Assert.Equal(ExecutionState.WaitingOnTool, continuationResult.State);
        Assert.Equal(ExecutionState.WaitingOnTool, persistedRun.State);
        Assert.Equal(1, runtime.ContinuationCallCount);
        Assert.Empty(cleaner.ExecutionRunIds);
    }

    [Fact]
    public async Task Cleanup_failure_does_not_mask_persisted_completed_execution_outcome()
    {
        var cleanupFailure = new InvalidOperationException(
            "The process lease cleaner failed.");
        var cleaner = new RecordingProcessLeaseCleaner(
            _ => Task.FromException<WorkspaceExecutionRunProcessCleanupResult>(
                cleanupFailure));
        using var context = await CreateContextAsync(
            new StubAgentRuntime(CreateCompletedResponse),
            cleaner);

        var result = await context.Service.ExecuteRunAsync(
            CreateRequest(context.Agent.Id));
        var persistedRun = Assert.IsType<ExecutionRunRecord>(
            await context.Store.GetExecutionRunAsync(result.ExecutionRunId));

        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.Equal(ExecutionState.Completed, persistedRun.State);
        Assert.Equal(
            result.ExecutionRunId,
            Assert.Single(cleaner.ExecutionRunIds));
    }

    private static ExecutionRunRequest CreateRequest(Guid agentId)
    {
        return new(
            agentId,
            "Return the terminal process lease test response.",
            AgentExecutionOperationId.New());
    }

    private static AgentRuntimeResponse CreateCompletedResponse()
    {
        return new(
            "The execution completed.",
            InputTokens: 8,
            OutputTokens: 4,
            ToolCalls: 0,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static AgentRuntimeResponse CreateWaitingResponse(
        string approvalId)
    {
        return new(
            "Approval is required.",
            InputTokens: 8,
            OutputTokens: 4,
            ToolCalls: 1,
            RuntimeSessionKey: "runtime-session",
            SerializedSessionStateJson: "{}",
            PendingApprovals:
            [
                new PendingToolApprovalRecord(
                    approvalId,
                    $"call-{approvalId}",
                    "workspace_test_operation",
                    "function",
                    "Approve the unit-test operation.",
                    "{}")
            ]);
    }

    private static async Task<TestContext> CreateContextAsync(
        IFakeAgentRuntime runtime,
        IWorkspaceExecutionRunProcessLeaseCleaner cleaner)
    {
        var workspace = new TemporaryWorkspace();
        var workspaceScope = WorkspaceScopeDescriptor.Project(
            $"process-lease-cleanup-{Guid.NewGuid():N}");
        var workspaceIdentity = new AgentExecutionActivityWorkspaceIdentity(
            Guid.NewGuid(),
            workspaceScope,
            new DatabaseProfileGeneration(0));
        var store = new FileSandboxWorkspaceStore(
            workspace.Path,
            workspaceScope);
        var provider = CreateProvider();
        var agent = CreateAgent(provider.Id);
        await store.SaveCatalogAsync(
            new SandboxWorkspaceCatalog(
                Version: "1.0",
                Agents: [agent],
                Providers: [provider],
                Capabilities: [],
                Memory: []));
        var preparationCache = new AgentExecutionPreparationCache(
            AgentExecutionPreparationCachePolicy.Default);
        // SB18: the workspace service consumes the narrow runtime ports; the test-only adapter
        // adapts the fake runtime for all four ports.
        var portFacade = new FakeAgentRuntimePortAdapter(runtime);
        var service = new AgentFrameworkWorkspaceService(
            store,
            CreateUnexpectedDependency<IAgentPackageService>(),
            portFacade,
            portFacade,
            portFacade,
            portFacade,
            CreateUnexpectedDependency<ICapabilityProofService>(),
            NullLogger<AgentFrameworkWorkspaceService>.Instance,
            CreateActivityCoordinator(),
            workspaceIdentity,
            preparationCache,
            new FixedAgentExecutionProfileGenerationSource(default),
            cleaner,
            providerCredentialResolver:
                FixedAgentProviderCredentialResolver.Instance);

        return new(
            workspace,
            store,
            agent,
            preparationCache,
            service);
    }

    private static AgentExecutionActivityCoordinator
        CreateActivityCoordinator()
    {
        return new(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                TimeProvider.System),
            TimeProvider.System);
    }

    private static ProviderProfile CreateProvider()
    {
        return new(
            Guid.NewGuid(),
            "Process lease cleanup test provider",
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "CANDOITALL_PROCESS_LEASE_CLEANUP_TEST_API_KEY",
            "test-model",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: ["test-model"]);
    }

    private static AgentDefinition CreateAgent(Guid providerProfileId)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            Guid.NewGuid(),
            "Process lease cleanup test agent",
            "Test agent",
            "Exercises terminal process lease cleanup.",
            "Return a concise response.",
            AgentLifecycleStatus.Active,
            providerProfileId,
            "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
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

    private static T CreateUnexpectedDependency<T>()
        where T : class
    {
        return DispatchProxy.Create<T, UnexpectedCallProxy>();
    }

    private sealed record TestContext(
        TemporaryWorkspace Workspace,
        FileSandboxWorkspaceStore Store,
        AgentDefinition Agent,
        AgentExecutionPreparationCache PreparationCache,
        AgentFrameworkWorkspaceService Service) : IDisposable
    {
        public void Dispose()
        {
            Service.Dispose();
            PreparationCache.Dispose();
            Workspace.Dispose();
        }
    }

    private sealed class RecordingProcessLeaseCleaner :
        IWorkspaceExecutionRunProcessLeaseCleaner
    {
        private readonly ConcurrentQueue<Guid> executionRunIds = new();
        private readonly Func<
            Guid,
            Task<WorkspaceExecutionRunProcessCleanupResult>> cleanup;

        public RecordingProcessLeaseCleaner(
            Func<
                Guid,
                Task<WorkspaceExecutionRunProcessCleanupResult>>? cleanup = null)
        {
            this.cleanup = cleanup
                ?? (executionRunId => Task.FromResult(
                    WorkspaceExecutionRunProcessCleanupResult.Empty(
                        executionRunId)));
        }

        public IReadOnlyList<Guid> ExecutionRunIds =>
            executionRunIds.ToArray();

        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(
            Guid executionRunId)
        {
            executionRunIds.Enqueue(executionRunId);
            return cleanup(executionRunId);
        }
    }

    private sealed class StubAgentRuntime : IFakeAgentRuntime
    {
        private readonly Func<AgentRuntimeResponse> run;
        private readonly Func<AgentRuntimeResponse>? continuation;
        private int continuationCallCount;

        public StubAgentRuntime(
            Func<AgentRuntimeResponse> run,
            Func<AgentRuntimeResponse>? continuation = null)
        {
            this.run = run;
            this.continuation = continuation;
        }

        public int ContinuationCallCount =>
            Volatile.Read(ref continuationCallCount);

        public Task<AgentRuntimeResponse> RunAsync(
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
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(run());
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
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref continuationCallCount);
            var response = continuation
                ?? throw new InvalidOperationException(
                    "The test did not configure an approval continuation.");
            return Task.FromResult(response());
        }

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderModelMaintenanceEditorResult>
            CreateOrUpdateProviderModelAsync(
                ProviderProfile provider,
                ProviderModelMaintenanceEditorRequest request,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedAgentProviderCredentialResolver :
        IAgentProviderCredentialResolver
    {
        public static FixedAgentProviderCredentialResolver Instance { get; } =
            new();

        private FixedAgentProviderCredentialResolver()
        {
        }

        public ProviderCredentialResolution Resolve(ProviderProfile provider)
        {
            return new(
                "unit-test-api-key",
                "unit-test credential resolver",
                string.Empty);
        }
    }

    private class UnexpectedCallProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            throw new InvalidOperationException(
                $"Dependency member '{targetMethod?.Name}' was not expected.");
        }
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"candoitall-process-lease-cleanup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
