using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests.Security;

public sealed class MemoryFeedbackHandleSecurityTests
{
    private const string InjectedHandle = "provider-controlled:provider.other:operation.other";

    [Fact]
    public void Feedback_handle_rejects_oversized_or_control_character_values()
    {
        var oversized = Assert.Throws<ArgumentException>(() =>
            MemoryFeedbackHandle.Parse(new string('x', 257)));
        var controlCharacters = Assert.Throws<ArgumentException>(() =>
            MemoryFeedbackHandle.Parse("feedback-safe\r\nprovider-injected"));

        Assert.Contains("at most 256", oversized.Message, StringComparison.Ordinal);
        Assert.Contains("control characters", controlCharacters.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Query_replaces_provider_handle_with_host_owned_operation_correlation()
    {
        var driver = new InjectingFeedbackHandleDriver();
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-feedback-security-{Guid.NewGuid():N}"));
        services.AddGenericMemoryModule();
        services.AddSingleton<IMemoryProviderDriver>(driver);
        services.AddSingleton<IMemoryProviderFeedbackDeliveryDriver>(driver);
        using var root = services.BuildServiceProvider(validateScopes: true);
        using var scope = root.CreateScope();
        var provider = scope.ServiceProvider;
        var profile = CreateProfile();
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, DateTimeOffset.UtcNow);

        var result = await provider.GetRequiredService<IMemoryOperationHandler>()
            .ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
                MemoryOperationCaller.Tool("memory.feedback.security", CreateRequester()),
                MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
                {
                    ExplicitProviderId = profile.InstanceId
                },
                new MemoryContextQueryRequest(
                    "feedback security",
                    [MemoryCapabilityIds.ContextQuerySync],
                    MemorySourceProvenance.None),
                MemoryLedgerRetentionPolicy.Expiring(
                    DateTimeOffset.UtcNow.AddHours(1),
                    DateTimeOffset.UtcNow.AddHours(2))));

        Assert.Equal(MemoryOperationHandlerStatus.Completed, result.Status);
        var operation = Assert.IsType<MemoryOperationRecord>(result.OperationRecord);
        var contextPack = Assert.IsType<MemoryContextPack>(result.Output);
        var handle = Assert.IsType<MemoryFeedbackHandle>(contextPack.FeedbackHandle);
        Assert.Equal(
            $"memory-feedback:{operation.OperationId.Value:D}:{contextPack.ContextPackId.Value:D}",
            handle.Value);
        Assert.DoesNotContain(InjectedHandle, handle.Value, StringComparison.Ordinal);
        Assert.Equal(handle, result.FeedbackHandle);

        var delivery = Assert.IsType<MemoryContextDeliveryMetadata>(
            operation.Extensions.GetContextDelivery());
        Assert.Equal(contextPack.ContextPackId, delivery.ContextPackId);
        Assert.Equal(handle, delivery.FeedbackHandle);
        Assert.Equal(profile.InstanceId, operation.ProviderInstanceId);
        Assert.Equal(CreateRequester(), operation.Requester);
    }

    private static MemoryProviderProfile CreateProfile() =>
        new(
            MemoryProviderInstanceId.Parse("provider.feedback.owner"),
            "Feedback owner",
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock"),
                MemoryProtocolVersion.Current,
                [
                    new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "1", Supported: true),
                    new MemoryCapabilityDescriptor(MemoryCapabilityIds.FeedbackImmediate, "1", Supported: true)
                ],
                new MemoryProviderInteractionSupport(
                    SupportsSynchronousQueries: true,
                    SupportsAsynchronousOperations: false,
                    SupportsSourceRequests: false,
                    SupportsFeedback: true,
                    SupportsProviderEvents: false),
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));

    private static MemoryLedgerRequester CreateRequester() =>
        new(
            RequesterId: "requester-owner",
            AgentId: "agent-owner",
            AgentRole: "developer",
            SessionId: "session-owner",
            WorkflowId: "workflow-owner",
            WorkflowNodeId: "node-owner",
            ProcessId: "process-owner",
            ProcessStepId: "step-owner");

    private sealed class InjectingFeedbackHandleDriver :
        IMemoryProviderDriver,
        IMemoryProviderFeedbackDeliveryDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            MemoryContextQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var contextPack = new MemoryContextPack(
                MemoryContextPackId.New(),
                "Provider context",
                [new MemoryContextSection("Memory", "Context", [], 1m)],
                Warnings: [],
                ProviderConfidence: 1m,
                MemoryFeedbackHandle.Parse(InjectedHandle));
            return Task.FromResult(MemoryProviderDriverResult.ContextPackResult(
                contextPack,
                "provider response"));
        }

        public Task<MemoryProviderQueueDispatchResult> DeliverFeedbackAsync(
            MemoryProviderProfile provider,
            MemoryFeedbackRecord feedback,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MemoryProviderQueueDispatchResult.Succeeded("feedback delivered"));
    }
}
