using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentContextContributionTests
{
    [Fact]
    public void Contributor_id_rejects_empty_values()
    {
        Assert.Throws<ArgumentException>(() => new AgentContextContributorId(" "));
    }

    [Fact]
    public async Task Maf_provider_converts_successful_contribution_to_chat_messages()
    {
        var contributor = new TestContextContributor(
            "test.context",
            10,
            _ => AgentContextContributionResult.Provided(
            [
                new AgentContextMessage(AgentContextMessageRole.System, "Injected context")
            ],
            new Dictionary<string, string>
            {
                ["source"] = "unit-test"
            }));
        var traceCollector = new AgentContextContributionTraceCollector();
        var provider = CreateProvider(contributor, traceCollector);

        var messages = await provider.ContributeAsync(
        [
            new ChatMessage(ChatRole.User, "Prompt")
        ]);

        var message = Assert.Single(messages);
        Assert.Equal(ChatRole.System, message.Role);
        Assert.Equal("Injected context", message.Text);

        var trace = Assert.Single(traceCollector.Snapshot());
        Assert.Equal(new AgentContextContributorId("test.context"), trace.ContributorId);
        Assert.Equal(AgentContextContributionStatus.Provided, trace.Status);
        Assert.Equal(1, trace.GeneratedMessageCount);
        Assert.Equal("unit-test", trace.TraceMetadata["source"]);
        Assert.True(trace.Elapsed > TimeSpan.Zero);
    }

    [Fact]
    public async Task Maf_provider_records_skipped_contribution_trace()
    {
        var contributor = new TestContextContributor(
            "test.skipped",
            10,
            _ => AgentContextContributionResult.Skipped(new Dictionary<string, string>
            {
                ["reason"] = "not-applicable"
            }));
        var traceCollector = new AgentContextContributionTraceCollector();
        var provider = CreateProvider(contributor, traceCollector);

        var messages = await provider.ContributeAsync([]);

        Assert.Empty(messages);
        var trace = Assert.Single(traceCollector.Snapshot());
        Assert.Equal(new AgentContextContributorId("test.skipped"), trace.ContributorId);
        Assert.Equal(AgentContextContributionStatus.Skipped, trace.Status);
        Assert.Equal(0, trace.GeneratedMessageCount);
        Assert.Equal("not-applicable", trace.TraceMetadata["reason"]);
    }

    [Fact]
    public async Task Maf_provider_surfaces_failed_result_as_typed_exception()
    {
        var contributor = new TestContextContributor(
            "test.failure",
            10,
            _ => AgentContextContributionResult.Failed("Policy denied context."));
        var traceCollector = new AgentContextContributionTraceCollector();
        var provider = CreateProvider(contributor, traceCollector);

        var exception = await Assert.ThrowsAsync<AgentContextContributionException>(async () =>
            await provider.ContributeAsync([]));

        Assert.Equal(new AgentContextContributorId("test.failure"), exception.ContributorId);
        Assert.Contains("Policy denied context", exception.Message, StringComparison.Ordinal);

        var trace = Assert.Single(traceCollector.Snapshot());
        Assert.Equal(new AgentContextContributorId("test.failure"), trace.ContributorId);
        Assert.Equal(AgentContextContributionStatus.Failed, trace.Status);
        Assert.Equal("Policy denied context.", trace.FailureMessage);
    }

    [Fact]
    public async Task Maf_provider_honors_cancellation()
    {
        var contributor = new TestContextContributor(
            "test.cancellation",
            10,
            request =>
            {
                _ = request;
                return AgentContextContributionResult.Provided([]);
        });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        contributor.CancellationProbe = token => token.ThrowIfCancellationRequested();
        var traceCollector = new AgentContextContributionTraceCollector();
        var provider = CreateProvider(contributor, traceCollector);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await provider.ContributeAsync([], cancellation.Token));
        Assert.Empty(traceCollector.Snapshot());
    }

    [Fact]
    public async Task Maf_runtime_attaches_enabled_contributors_in_deterministic_order()
    {
        var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("late", 20, _ => AgentContextContributionResult.Skipped()));
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("disabled", 0, _ => AgentContextContributionResult.Skipped(), enabled: false));
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("early", 10, _ => AgentContextContributionResult.Skipped()));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            CreateAgent(),
            CreateProviderProfile(),
            progressMessages);

        var contextProviders = Assert.IsAssignableFrom<IEnumerable<AIContextProvider>>(
            state.GetType().GetProperty("ContextProviders", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var contributorIds = contextProviders
            .OfType<MafAgentContextContributionProvider>()
            .Select(provider => provider.ContributorId.Value)
            .ToList();

        Assert.Equal(["early", "late"], contributorIds);
        Assert.All(
            contextProviders.OfType<MafAgentContextContributionProvider>(),
            provider => Assert.Empty(provider.StateKeys));
        Assert.Contains(
            progressMessages,
            message => message.Contains("registered agent context contributor", StringComparison.Ordinal));

        var traceCollector = ReadContextContributionTraceCollector(state);
        var firstProvider = contextProviders
            .OfType<MafAgentContextContributionProvider>()
            .First(provider => provider.ContributorId.Value == "early");
        var messages = await firstProvider.ContributeAsync([]);

        Assert.Empty(messages);
        var trace = Assert.Single(traceCollector.Snapshot());
        Assert.Equal(new AgentContextContributorId("early"), trace.ContributorId);
        Assert.Equal(AgentContextContributionStatus.Skipped, trace.Status);
    }

    [Fact]
    public async Task Disabled_memory_invocation_excludes_legacy_workspace_memory_without_hidden_attachment()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(
            Path.GetTempPath(),
            MafRuntimeTestServices.CreateProviderRuntimeServiceCollection().BuildServiceProvider());
        var agent = CreateAgent();
        var legacyMemory = new AgentMemoryRecord(
            Guid.NewGuid(),
            agent.Id,
            MemoryKind.Fact,
            "Legacy note",
            "Hidden legacy memory must not reach the model.",
            "unit",
            5,
            "{}",
            DateTimeOffset.UtcNow);

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            [legacyMemory]);
        var contextProviders = Assert.IsAssignableFrom<IEnumerable<AIContextProvider>>(
            state.GetType().GetProperty("ContextProviders", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var contextSources = Assert.IsAssignableFrom<IEnumerable<AgentRuntimeContextManifestSource>>(
            state.GetType().GetProperty("ContextSources", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));

        Assert.DoesNotContain(contextProviders, provider => provider.GetType().Name == "WorkspaceMemoryContextProvider");
        var trace = Assert.Single(contextSources, source => source.SourceId == "workspace-memory");
        Assert.Equal(AgentRuntimeContextSourceDecision.Excluded, trace.Decision);
        Assert.Contains("disabled", trace.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Maf_runtime_uses_context_workspace_scope_override_for_contributors()
    {
        var projectId = Guid.Parse("29fbb9a8-8422-4b8b-89ed-9d515103b801");
        WorkspaceScopeDescriptor? capturedScope = null;
        var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor(
            "scope.capture",
            10,
            request =>
            {
                capturedScope = request.Policy.WorkspaceScope;
                return AgentContextContributionResult.Skipped();
            }));
        var runtime = RuntimeCapabilityComposer.CreateDefault(
            Path.GetTempPath(),
            services.BuildServiceProvider(),
            WorkspaceScopeDescriptor.Organization("unit-org"));
        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateAgent(),
            CreateProviderProfile(),
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
            []);
        var contextProviders = Assert.IsAssignableFrom<IEnumerable<AIContextProvider>>(
            state.GetType().GetProperty("ContextProviders", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var provider = Assert.Single(contextProviders.OfType<MafAgentContextContributionProvider>());

        await provider.ContributeAsync([new ChatMessage(ChatRole.User, "Summarize workflow input.")]);

        Assert.NotNull(capturedScope);
        Assert.Equal(WorkspaceScopeKind.Project, capturedScope!.Kind);
        Assert.Equal(projectId.ToString("D"), capturedScope.Key);
    }

    [Fact]
    public async Task Maf_runtime_rejects_duplicate_contributor_ids()
    {
        var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("duplicate", 10, _ => AgentContextContributionResult.Skipped()));
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("duplicate", 20, _ => AgentContextContributionResult.Skipped()));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeCreateCapabilityStateAsync(
                runtime,
                CreateAgent(),
                CreateProviderProfile(),
                []));

        Assert.Contains("must be unique", exception.Message, StringComparison.Ordinal);
    }

    private static MafAgentContextContributionProvider CreateProvider(
        IAgentContextContributor contributor,
        IAgentContextContributionTraceSink? traceSink = null)
        => new(
            contributor,
            CreateAgent(),
            CreateProviderProfile(),
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Sandbox),
            traceSink);

    private static AgentDefinition CreateAgent()
        => new(
            Id: Guid.NewGuid(),
            Name: "Context Agent",
            RoleTitle: "Tester",
            Summary: "Tests context contribution.",
            Instructions: "Use supplied context.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: string.Empty,
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default with
            {
                CanUseTools = false,
                CanAskOtherAgents = false,
                RequiresApprovalForExternalCalls = false
            },
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

    private static ProviderProfile CreateProviderProfile(
        ProviderKind kind = ProviderKind.OpenAi,
        string baseUrl = "https://api.openai.com/v1",
        Guid? providerId = null)
        => new(
            providerId ?? Guid.NewGuid(),
            "Unit Provider",
            kind,
            baseUrl,
            "OPENAI_API_KEY",
            "gpt-4.1",
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "Not checked",
            null,
            []);

    private static async Task<object> InvokeCreateCapabilityStateAsync(
        RuntimeCapabilityComposer composer,
        AgentDefinition agent,
        ProviderProfile provider,
        List<string> progressMessages,
        IReadOnlyList<AgentMemoryRecord>? memory = null)
    {
        return await composer.CreateCapabilityStateAsync(
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            memory ?? Array.Empty<AgentMemoryRecord>(),
            (_, _, message) =>
            {
                progressMessages.Add(message);
                return Task.CompletedTask;
            },
            CancellationToken.None,
            suppressApprovalRequirements: false);
    }

    private static async Task<object> InvokeCreateCapabilityStateCoreAsync(
        RuntimeCapabilityComposer composer,
        AgentDefinition agent,
        ProviderProfile provider,
        WorkspaceScopeDescriptor contextWorkspaceScope,
        List<string> progressMessages)
    {
        return await composer.CreateCapabilityStateCoreAsync(
            agent,
            provider,
            provider.DefaultModel,
            Array.Empty<CapabilityCatalogItem>(),
            Array.Empty<AgentMemoryRecord>(),
            (_, _, message) =>
            {
                progressMessages.Add(message);
                return Task.CompletedTask;
            },
            CancellationToken.None,
            suppressApprovalRequirements: false,
            contextWorkspaceScope,
            AgentRuntimeContextIntent.Empty);
    }

    private static AgentContextContributionTraceCollector ReadContextContributionTraceCollector(object state)
        => Assert.IsType<AgentContextContributionTraceCollector>(
            state.GetType().GetProperty("ContextContributionTraceCollector", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));

    private sealed class TestContextContributor(
        string id,
        int order,
        Func<AgentContextContributionRequest, AgentContextContributionResult> resultFactory,
        bool enabled = true) : IAgentContextContributor
    {
        public Action<CancellationToken>? CancellationProbe { get; set; }

        public AgentContextContributorDescriptor Descriptor { get; } = new(
            new AgentContextContributorId(id),
            id,
            order,
            enabled);

        public ValueTask<AgentContextContributionResult> ContributeAsync(
            AgentContextContributionRequest request,
            CancellationToken cancellationToken = default)
        {
            CancellationProbe?.Invoke(cancellationToken);
            return ValueTask.FromResult(resultFactory(request));
        }
    }

}
