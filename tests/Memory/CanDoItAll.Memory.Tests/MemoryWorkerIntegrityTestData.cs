using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

internal static class MemoryWorkerIntegrityTestData
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-12T12:00:00Z");

    public static MemoryAsyncWorkerOptions Options => MemoryAsyncWorkerOptions.Default with
    {
        PollingStaleAfter = TimeSpan.Zero,
        MaxBatchSize = 25,
        MaxRetryAttempts = 3
    };

    public static ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-worker-integrity-{Guid.NewGuid():N}"));
        services.AddGenericMemoryModule(options => options.WorkerOptions = Options);
        configure?.Invoke(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    public static async Task<MemoryProviderProfile> SeedProfileAsync(
        IServiceProvider services,
        string id,
        MemoryProviderDriverKind driverKind,
        params MemoryCapabilityId[] capabilities)
    {
        var profile = new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse(id),
            id,
            driverKind,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse($"memory.{driverKind.ToString().ToLowerInvariant()}"),
                MemoryProtocolVersion.Current,
                capabilities.Select(capability =>
                    new MemoryCapabilityDescriptor(capability, "1", Supported: true)).ToArray(),
                new MemoryProviderInteractionSupport(
                    capabilities.Contains(MemoryCapabilityIds.ContextQuerySync),
                    capabilities.Contains(MemoryCapabilityIds.ContextQueryAsync),
                    SupportsSourceRequests: false,
                    capabilities.Any(capability => capability.Value.StartsWith("feedback.", StringComparison.Ordinal)),
                    capabilities.Any(capability => capability.Value.StartsWith("events.", StringComparison.Ordinal))),
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
        await services.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, Now);
        return profile;
    }

    public static MemoryFeedbackRecord CreateFeedback(MemoryProviderInstanceId providerId) =>
        MemoryFeedbackRecord.CreateUnmatched(
            MemoryFeedbackRecordId.New(),
            providerId,
            MemoryFeedbackStage.ContextUsed,
            MemoryFeedbackOutcome.Useful,
            CreateRequester(),
            "unmatched test feedback",
            MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(1), Now.AddDays(2)),
            Now.AddMinutes(-1));

    public static MemoryEventOutboxRecord CreateOutbox(MemoryProviderInstanceId providerId) =>
        MemoryEventOutboxRecord.CreateAcknowledgement(
            MemoryEventOutboxRecordId.New(),
            providerId,
            MemoryProviderEventId.New(),
            inboxRecordId: null,
            Now.AddMinutes(-1),
            MemoryPayload.FromText("accepted"));

    public static MemoryProviderEvent CreateEvent(string message) =>
        new(
            MemoryProviderEventId.New(),
            MemoryProviderEventKind.MaintenanceSignal,
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            message,
            MemoryPayload.FromText(message));

    public static MemoryLedgerRequester CreateRequester() =>
        new("agent-1", "agent-1", "developer", "session-1", null, null, null, null);

    internal sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
