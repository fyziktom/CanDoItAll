using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentFrameworkWorkspaceActivityAdmissionTests
{
    [Fact]
    public async Task Raw_core_send_admits_before_execution_and_publishes_one_success_terminal()
    {
        using var workspace = new TemporaryWorkspace();
        var workspaceScope = WorkspaceScopeDescriptor.Project("raw-core-send");
        var workspaceIdentity = new AgentExecutionActivityWorkspaceIdentity(
            Guid.NewGuid(),
            workspaceScope,
            new DatabaseProfileGeneration(0));
        var coordinator = CreateCoordinator();
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
        using var preparationCache = new AgentExecutionPreparationCache(
            AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(
            store,
            new SuccessfulAgentRuntime(),
            coordinator,
            workspaceIdentity,
            preparationCache);
        var operationId = AgentExecutionOperationId.New();

        var result = await service.SendMessageAsync(
            agent.Id,
            chatSessionId: null,
            "Summarize the current workspace.",
            new AgentChatRunOptions(operationId));
        var events = await ReadEventsAsync(
            coordinator,
            workspaceIdentity.CreateStreamId(operationId));

        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.Equal(
            AgentExecutionActivityPhase.Accepted,
            events[0].Event.Phase);
        var terminal = Assert.Single(
            events,
            envelope => envelope.Event.IsTerminal);
        Assert.Equal(
            AgentExecutionActivityPhase.Completed,
            terminal.Event.Phase);
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Succeeded,
            terminal.Event.TerminalOutcome);
        Assert.Equal(agent.Id, terminal.Event.AgentId);
        Assert.Equal(result.ExecutionRunId, terminal.Event.ExecutionRunId);
    }

    [Fact]
    public async Task Operation_bound_send_rejects_foreign_workspace_scope_before_dependency_access()
    {
        var store = CreateUnexpectedDependency<ISandboxWorkspaceStore>();
        var packageService =
            CreateUnexpectedDependency<IAgentPackageService>();
        var runtime = CreateUnexpectedDependency<IFakeAgentRuntime>();
        var capabilityProofService =
            CreateUnexpectedDependency<ICapabilityProofService>();
        var coordinator = CreateCoordinator();
        var databaseProfileId = Guid.NewGuid();
        var serviceIdentity = new AgentExecutionActivityWorkspaceIdentity(
            databaseProfileId,
            WorkspaceScopeDescriptor.Project("service-workspace"),
            new DatabaseProfileGeneration(0));
        var foreignIdentity = new AgentExecutionActivityWorkspaceIdentity(
            databaseProfileId,
            WorkspaceScopeDescriptor.Project("foreign-workspace"),
            new DatabaseProfileGeneration(0));
        using var preparationCache = new AgentExecutionPreparationCache(
            AgentExecutionPreparationCachePolicy.Default);
        // SB18: the workspace service consumes the narrow runtime ports; the test-only adapter
        // adapts the unexpected-call runtime proxy without invoking it.
        var portFacade = new FakeAgentRuntimePortAdapter(runtime.Service);
        using var service = new AgentFrameworkWorkspaceService(
            store.Service,
            packageService.Service,
            portFacade,
            portFacade,
            portFacade,
            portFacade,
            capabilityProofService.Service,
            NullLogger<AgentFrameworkWorkspaceService>.Instance,
            coordinator,
            serviceIdentity,
            preparationCache,
            new FixedAgentExecutionProfileGenerationSource(default),
            SuccessfulWorkspaceExecutionRunProcessLeaseCleaner.Instance,
            new ExternalTargetPathRegistryFactory());
        var agentId = Guid.NewGuid();
        var operationId = AgentExecutionOperationId.New();
        using var operation = Assert.IsType<AgentExecutionActivityAdmitted>(
            coordinator.AdmitOperation(
                foreignIdentity.CreateStreamId(operationId),
                agentId,
                chatSessionId: null,
                "Foreign operation accepted."))
            .Operation;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendMessageWithinOperationAsync(
                operation,
                agentId,
                chatSessionId: null,
                "This must not reach storage.",
                new AgentChatRunOptions(operationId)));

        Assert.Contains(
            "workspace scope",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, store.Proxy.CallCount);
        Assert.Equal(0, packageService.Proxy.CallCount);
        Assert.Equal(0, runtime.Proxy.CallCount);
        Assert.Equal(0, capabilityProofService.Proxy.CallCount);
    }

    private static AgentFrameworkWorkspaceService CreateService(
        ISandboxWorkspaceStore store,
        IFakeAgentRuntime runtime,
        IAgentExecutionActivityCoordinator coordinator,
        AgentExecutionActivityWorkspaceIdentity workspaceIdentity,
        IAgentExecutionPreparationCache preparationCache)
    {
        // SB18: the workspace service consumes the narrow runtime ports; the test-only adapter
        // adapts the fake runtime for all four ports.
        var portFacade = new FakeAgentRuntimePortAdapter(runtime);
        return new(
            store,
            CreateUnexpectedDependency<IAgentPackageService>().Service,
            portFacade,
            portFacade,
            portFacade,
            portFacade,
            CreateUnexpectedDependency<ICapabilityProofService>().Service,
            NullLogger<AgentFrameworkWorkspaceService>.Instance,
            coordinator,
            workspaceIdentity,
            preparationCache,
            new FixedAgentExecutionProfileGenerationSource(default),
            SuccessfulWorkspaceExecutionRunProcessLeaseCleaner.Instance,
            new ExternalTargetPathRegistryFactory());
    }

    private static AgentExecutionActivityCoordinator CreateCoordinator()
    {
        return new(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                TimeProvider.System),
            TimeProvider.System);
    }

    private static async Task<IReadOnlyList<
        SequencedStreamEnvelope<AgentExecutionActivity>>> ReadEventsAsync(
        IAgentExecutionActivityReader reader,
        AgentExecutionActivityStreamId streamId)
    {
        await using var streamReader = reader.OpenReader(
            streamId,
            StreamSequence.Beginning);
        var events = Assert.IsType<
            SequencedStreamEvents<AgentExecutionActivity>>(
            await streamReader.ReadAsync());
        return events.Items;
    }

    private static ProviderProfile CreateProvider()
    {
        return new(
            Guid.NewGuid(),
            "Raw Core test provider",
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "CANDOITALL_RAW_CORE_TEST_API_KEY",
            "test-model",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
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
            "Raw Core test agent",
            "Test agent",
            "Exercises raw Core activity admission.",
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

    private static UnexpectedDependency<T> CreateUnexpectedDependency<T>()
        where T : class
    {
        var service = DispatchProxy.Create<T, UnexpectedCallProxy>();
        return new(
            service,
            (UnexpectedCallProxy)(object)service);
    }

    private sealed record UnexpectedDependency<T>(
        T Service,
        UnexpectedCallProxy Proxy)
        where T : class;

    private class UnexpectedCallProxy : DispatchProxy
    {
        private int callCount;

        public int CallCount => Volatile.Read(ref callCount);

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            Interlocked.Increment(ref callCount);
            throw new InvalidOperationException(
                $"Dependency member '{targetMethod?.Name}' was not expected.");
        }
    }

    private sealed class SuccessfulAgentRuntime : IFakeAgentRuntime
    {
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
            return Task.FromResult(
                new AgentRuntimeResponse(
                    "The workspace is ready.",
                    InputTokens: 8,
                    OutputTokens: 5,
                    ToolCalls: 0,
                    RuntimeSessionKey: string.Empty,
                    SerializedSessionStateJson: null,
                    PendingApprovals: []));
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
            throw new NotSupportedException();
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

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"candoitall-raw-core-activity-{Guid.NewGuid():N}");
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
