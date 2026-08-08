using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.Tests.Unit;

public sealed class ManagedSeedExecutionCredentialBoundaryTests
{
    [Fact]
    public async Task Managed_seed_openai_chat_reaches_runtime_without_mutating_process_environment()
    {
        using var workspace = new TemporaryWorkspace();
        var workspaceScope = WorkspaceScopeDescriptor.Project(
            "managed-seed-credential-boundary");
        var workspaceIdentity = new AgentExecutionActivityWorkspaceIdentity(
            Guid.NewGuid(),
            workspaceScope,
            new DatabaseProfileGeneration(0));
        var store = new FileSandboxWorkspaceStore(
            workspace.Path,
            workspaceScope);
        var credentialVariableName = $"CANDOITALL_CREDENTIAL_BOUNDARY_{Guid.NewGuid():N}";
        var originalOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var resolvedApiKey = $"unit-test-api-key-{Guid.NewGuid():N}";
        var provider = CreateProvider() with
        {
            ApiKeyEnvironmentVariable = credentialVariableName
        };
        Assert.Null(Environment.GetEnvironmentVariable(credentialVariableName));
        var agent = CreateAgent(provider.Id);
        await store.SaveCatalogAsync(
            new SandboxWorkspaceCatalog(
                Version: "1.0",
                Agents: [agent],
                Providers: [provider],
                Capabilities: [],
                Memory: []));
        var credentialResolver = new CountingCredentialResolver(resolvedApiKey);
        var runtime = new RuntimeBoundaryProbe(credentialResolver);
        using var preparationCache = new AgentExecutionPreparationCache(
            AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(
            store,
            runtime,
            workspaceIdentity,
            preparationCache,
            credentialResolver);

        var result = await service.SendMessageAsync(
            agent.Id,
            chatSessionId: null,
            "Confirm the runtime boundary.",
            new AgentChatRunOptions(AgentExecutionOperationId.New()));

        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.Equal(1, runtime.RunCallCount);
        Assert.Equal(1, runtime.CredentialResolutionCountAtEntry);
        Assert.Equal(1, credentialResolver.CallCount);
        Assert.Equal(provider.Id, runtime.ProviderAtEntry?.Id);
        Assert.Equal(provider.Name, runtime.ProviderAtEntry?.Name);
        Assert.Equal(ProviderKind.OpenAi, runtime.ProviderAtEntry?.Kind);
        Assert.NotEqual(
            ManagedSeedProviderFallbacks.FallbackModel,
            runtime.AgentAtEntry?.Model);
        Assert.Null(Environment.GetEnvironmentVariable(credentialVariableName));
        Assert.Equal(originalOpenAiApiKey, Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    [Fact]
    public void Maf_openai_agent_factory_resolves_runtime_credential_exactly_once()
    {
        var provider = CreateProvider();
        var credentialResolver = new CountingCredentialResolver();
        var credentialService = new MafProviderCredentialService(credentialResolver);
        var factory = new MafProviderAgentFactory(
            credentialService,
            NoOpMafProviderStreamingDispatchGate.Instance);
        var options = new ChatClientAgentOptions
        {
            Id = Guid.NewGuid().ToString("D"),
            Name = "Credential boundary runtime agent",
            Description = "Validates runtime-owned credential resolution.",
            ChatOptions = new ChatOptions
            {
                Instructions = "Return a concise response."
            }
        };

        var frameworkAgent = factory.CreateFrameworkAgent(
            provider,
            provider.DefaultModel,
            options,
            frameworkManagedHistory: false,
            allowBackgroundResponses: false);

        Assert.NotNull(frameworkAgent);
        Assert.Equal(1, credentialResolver.CallCount);
        Assert.Equal(provider.Id, credentialResolver.LastProvider?.Id);
    }

    private static AgentFrameworkWorkspaceService CreateService(
        ISandboxWorkspaceStore store,
        IFakeAgentRuntime runtime,
        AgentExecutionActivityWorkspaceIdentity workspaceIdentity,
        IAgentExecutionPreparationCache preparationCache,
        IAgentProviderCredentialResolver credentialResolver)
    {
        var coordinator = new AgentExecutionActivityCoordinator(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                TimeProvider.System),
            TimeProvider.System);
        // SB18: the workspace service consumes the narrow runtime ports; the test-only adapter
        // adapts the fake runtime for all four ports.
        var portFacade = new FakeAgentRuntimePortAdapter(runtime);
        return new(
            store,
            CreateUnexpectedDependency<IAgentPackageService>(),
            portFacade,
            portFacade,
            portFacade,
            portFacade,
            CreateUnexpectedDependency<ICapabilityProofService>(),
            NullLogger<AgentFrameworkWorkspaceService>.Instance,
            coordinator,
            workspaceIdentity,
            preparationCache,
            new FixedAgentExecutionProfileGenerationSource(default),
            SuccessfulWorkspaceExecutionRunProcessLeaseCleaner.Instance,
            providerCredentialResolver: credentialResolver);
    }

    private static T CreateUnexpectedDependency<T>()
        where T : class
    {
        return DispatchProxy.Create<T, UnexpectedCallProxy>();
    }

    private static ProviderProfile CreateProvider()
    {
        return new(
            Guid.Parse("c1c103db-707e-3f52-8809-8d804fc171d1"),
            ManagedSeedProviderFallbacks.OpenAiDefaultProviderName,
            ProviderKind.OpenAi,
            ManagedSeedProviderFallbacks.OpenAiBaseUrl,
            "OPENAI_API_KEY",
            ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{\"history\":\"service-managed\"}",
            Notes: "Managed-seed OpenAI provider.",
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels:
            [
                ManagedSeedProviderFallbacks.OpenAiDefaultModel
            ]);
    }

    private static AgentDefinition CreateAgent(Guid providerProfileId)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            Guid.NewGuid(),
            "Delivery QA Observer",
            "QA lead and browser-proof reviewer",
            "Managed-seed credential-boundary agent.",
            "Return a concise response.",
            AgentLifecycleStatus.Active,
            providerProfileId,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            AgentWorkloadKind.Qa,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson:
                "{\"managedSeedVersion\":\"2026-04-serious-delivery-v25\"}",
            IsTemplate: false,
            TemplateKey: "delivery-qa-observer",
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private sealed class CountingCredentialResolver(
        string apiKey = "unit-test-api-key") :
        IAgentProviderCredentialResolver
    {
        private int callCount;

        public int CallCount => Volatile.Read(ref callCount);

        public ProviderProfile? LastProvider { get; private set; }

        public ProviderCredentialResolution Resolve(ProviderProfile provider)
        {
            LastProvider = provider;
            Interlocked.Increment(ref callCount);
            return new(
                apiKey,
                "unit-test credential resolver",
                string.Empty);
        }
    }

    private sealed class RuntimeBoundaryProbe(
        CountingCredentialResolver credentialResolver) : IFakeAgentRuntime
    {
        private int runCallCount;

        public int RunCallCount => Volatile.Read(ref runCallCount);

        public int CredentialResolutionCountAtEntry { get; private set; } = -1;

        public AgentDefinition? AgentAtEntry { get; private set; }

        public ProviderProfile? ProviderAtEntry { get; private set; }

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
            CredentialResolutionCountAtEntry =
                credentialResolver.CallCount;
            AgentAtEntry = agent;
            ProviderAtEntry = provider;
            Interlocked.Increment(ref runCallCount);
            return Task.FromResult(
                new AgentRuntimeResponse(
                    "Runtime boundary reached.",
                    InputTokens: 4,
                    OutputTokens: 3,
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
                $"candoitall-managed-seed-credential-{Guid.NewGuid():N}");
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
