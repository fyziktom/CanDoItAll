using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory.Tests.Context;

public sealed class MemoryAgentContextFanOutTests
{
    [Fact]
    public async Task Fan_out_is_bounded_and_merged_in_configured_order_not_completion_order()
    {
        var bindings = Enumerable.Range(1, 8)
            .Reverse()
            .Select(index => new AgentMemoryProviderBindingSetting(
                AgentMemoryProviderAlias.Parse($"provider-{index}"),
                MemoryProviderInstanceId.Parse($"memory.provider-{index}")))
            .ToArray();
        var settings = new AgentMemoryAccessSettings
        {
            InvocationMode = AgentMemoryInvocationMode.Automatic,
            ProviderBindings = bindings,
            AllowedProviderInstanceIds = bindings.Select(binding => binding.ProviderInstanceId).ToArray(),
            AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync]
        };
        var handler = new DelayedMemoryOperationHandler();
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);

        var result = await contributor.ContributeAsync(CreateRequest(CreateAgent(settings)));

        Assert.Equal(AgentContextContributionStatus.Provided, result.Status);
        Assert.InRange(handler.MaximumConcurrency, 2, 4);
        var context = Assert.Single(result.Messages).Text;
        var previousIndex = -1;
        foreach (var binding in bindings)
        {
            var currentIndex = context.IndexOf(
                $"Memory provider '{binding.Alias.Value}'",
                StringComparison.Ordinal);
            Assert.True(currentIndex > previousIndex);
            previousIndex = currentIndex;
        }
    }

    private static AgentDefinition CreateAgent(AgentMemoryAccessSettings settings)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Memory fan-out agent",
            "Tester",
            "Tests fan-out.",
            "Use memory.",
            AgentLifecycleStatus.Active,
            Guid.NewGuid(),
            "gpt-5-mini",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.ProviderDefault,
            0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            AgentMemoryAccessMetadata.Write("{}", settings),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
    }

    private static AgentContextContributionRequest CreateRequest(AgentDefinition agent) =>
        new(
            agent,
            new ProviderProfile(
                Guid.NewGuid(),
                "Unit provider",
                ProviderKind.OpenAi,
                "https://api.openai.com",
                "OPENAI_API_KEY",
                "gpt-5-mini",
                ProviderTransportKind.ChatCompletions,
                IsEnabled: true,
                SupportsStreaming: true,
                SupportsTools: true,
                PreferFrameworkManagedChatHistory: true,
                SupportsBackgroundResponses: false,
                ConfigurationJson: string.Empty,
                Notes: string.Empty,
                HealthStatus: string.Empty,
                LastCheckedAtUtc: null,
                SuggestedModels: [],
                ProviderProfilePurpose.Chat),
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "recall this")],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit")));

    private sealed class DelayedMemoryOperationHandler : IMemoryOperationHandler
    {
        private int activeQueries;
        private int maximumConcurrency;

        public int MaximumConcurrency => Volatile.Read(ref maximumConcurrency);

        public async Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteQueryAsync(
            MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
            CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref activeQueries);
            UpdateMaximum(active);
            try
            {
                var providerId = request.SelectionPolicy.ExplicitProviderId
                    ?? throw new InvalidOperationException("Expected an explicit provider.");
                var index = int.Parse(providerId.Value.Split('-').Last());
                await Task.Delay(TimeSpan.FromMilliseconds(index * 15), cancellationToken);
                var pack = new MemoryContextPack(
                    MemoryContextPackId.New(),
                    $"Context from {providerId.Value}",
                    [],
                    [],
                    0.9m,
                    MemoryFeedbackHandle.Parse("feedback-" + Guid.NewGuid().ToString("N")));
                return new MemoryOperationHandlerResult<MemoryContextPack>(
                    MemoryOperationHandlerStatus.Completed,
                    MemoryProviderSelectionResult.Selected(
                        CreateProvider(providerId),
                        MemoryProviderSelectionReason.ExplicitProvider,
                        MemoryCapabilityIds.ContextQuerySync),
                    OperationRecord: null,
                    pack,
                    AcceptedOperation: null,
                    pack.FeedbackHandle,
                    DriverDispatchAttempted: true,
                    Diagnostic: "Completed.");
            }
            finally
            {
                Interlocked.Decrement(ref activeQueries);
            }
        }

        public Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureSourceForIngestionAsync(
            MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitFeedbackAsync(
            MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
            MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> CancelAsync(
            MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeEventAsync(
            MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private void UpdateMaximum(int value)
        {
            var observed = Volatile.Read(ref maximumConcurrency);
            while (value > observed)
            {
                observed = Interlocked.CompareExchange(ref maximumConcurrency, value, observed);
            }
        }

        private static MemoryProviderProfile CreateProvider(MemoryProviderInstanceId providerId) =>
            new(
                providerId,
                providerId.Value,
                MemoryProviderDriverKind.Mock,
                IsEnabled: true,
                MemoryProviderHealthState.Healthy,
                MemoryProviderWorkspaceScope.AllWorkspaces,
                SelectionTags: [],
                MemoryProviderProfilePolicy.Default,
                new MemoryProviderManifest(
                    MemoryProviderKind.Parse("mock.memory"),
                    MemoryProtocolVersion.Current,
                    [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "v1", Supported: true)],
                    MemoryProviderInteractionSupport.SyncQueryOnly,
                    UiSurfaces: [],
                    MemoryProviderLimits.Default,
                    MemoryExtensionData.Empty));
    }
}
