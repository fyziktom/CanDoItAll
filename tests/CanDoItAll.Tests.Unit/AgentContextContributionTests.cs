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
        var provider = CreateProvider(contributor);

        var messages = await provider.ContributeAsync(
        [
            new ChatMessage(ChatRole.User, "Prompt")
        ]);

        var message = Assert.Single(messages);
        Assert.Equal(ChatRole.System, message.Role);
        Assert.Equal("Injected context", message.Text);
    }

    [Fact]
    public async Task Maf_provider_surfaces_failed_result_as_typed_exception()
    {
        var contributor = new TestContextContributor(
            "test.failure",
            10,
            _ => AgentContextContributionResult.Failed("Policy denied context."));
        var provider = CreateProvider(contributor);

        var exception = await Assert.ThrowsAsync<AgentContextContributionException>(async () =>
            await provider.ContributeAsync([]));

        Assert.Equal(new AgentContextContributorId("test.failure"), exception.ContributorId);
        Assert.Contains("Policy denied context", exception.Message, StringComparison.Ordinal);
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
        var provider = CreateProvider(contributor);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await provider.ContributeAsync([], cancellation.Token));
    }

    [Fact]
    public async Task Maf_runtime_attaches_enabled_contributors_in_deterministic_order()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("late", 20, _ => AgentContextContributionResult.Skipped()));
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("disabled", 0, _ => AgentContextContributionResult.Skipped(), enabled: false));
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("early", 10, _ => AgentContextContributionResult.Skipped()));
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services.BuildServiceProvider());
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
        Assert.Contains(
            progressMessages,
            message => message.Contains("registered agent context contributor", StringComparison.Ordinal));
    }

    private static MafAgentContextContributionProvider CreateProvider(IAgentContextContributor contributor)
        => new(
            contributor,
            CreateAgent(),
            CreateProviderProfile(),
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Sandbox));

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

    private static ProviderProfile CreateProviderProfile()
        => new(
            Guid.NewGuid(),
            "Unit Provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
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
        MafAgentRuntime runtime,
        AgentDefinition agent,
        ProviderProfile provider,
        List<string> progressMessages)
    {
        var method = typeof(MafAgentRuntime).GetMethod(
                         "CreateCapabilityStateAsync",
                         BindingFlags.NonPublic | BindingFlags.Instance)
                     ?? throw new InvalidOperationException("CreateCapabilityStateAsync method was not found.");
        var invocation = method.Invoke(
            runtime,
            [
                agent,
                provider,
                Array.Empty<CapabilityCatalogItem>(),
                Array.Empty<AgentMemoryRecord>(),
                (Func<ExecutionState, string, string, Task>)((_, _, message) =>
                {
                    progressMessages.Add(message);
                    return Task.CompletedTask;
                }),
                CancellationToken.None,
                false
            ]);
        var task = Assert.IsAssignableFrom<Task>(invocation);
        await task;

        return task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetValue(task)
               ?? throw new InvalidOperationException("CreateCapabilityStateAsync did not produce a result.");
    }

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
