using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentHistoricalReceiptDeferredOwnerTests {
    [Fact]
    public async Task Current_profile_construction_does_not_resolve_historical_graph_and_uninitialized_read_refuses_before_delegate() {
        var notifications = new DatabaseSwitchNotificationService();
        var state = new DatabaseRuntimeState(notifications);
        var resolved = 0;
        var service = new HistoricalReadService();
        using var owner = new CurrentProfileAgentFrameworkWorkspaceService(Unused<ICanDoItAllAgentWorkspaceFactory>(),
            Unused<IAiTechnicalAgentBridge>(), Unused<IAgentReferenceDataCacheInvalidator>(), Unused<IDatabaseProfileRuntimeAccessor>(),
            notifications, Unused<IAgentExecutionActivityCoordinator>(), Unused<IAgentExecutionProfileGenerationSource>(),
            NullLogger<CurrentProfileAgentFrameworkWorkspaceService>.Instance, () => {
                resolved++;
                return service;
            }, state);
        Assert.Equal(0, resolved);
        var request = Request();
        await Assert.ThrowsAsync<NotSupportedException>(() => owner.ReadSavedEffectReceiptsAsync(request));
        Assert.Equal(0, resolved);
        Initialize(state, service);
        var result = await owner.ReadSavedEffectReceiptsAsync(request);
        Assert.Equal(request, result.Request);
        Assert.Equal(1, resolved);
        Assert.Equal(1, service.Reads);
        await AssertFreshServiceInvocationsAsync();
    }

    private static async Task AssertFreshServiceInvocationsAsync() {
        var root = Path.Combine(Path.GetTempPath(), "historical-read-uncreated-" + Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid();
        var profile = new DatabaseProfileRecord {
            Id = id,
            ProviderKind = DatabaseProviderKind.InMemory,
            SourceKind = DatabaseProfileSourceKind.InMemory,
            InMemory = new() { DatabaseName = id.ToString("N") },
            Storage = new() { WorkspaceRoot = root },
            Runtime = new() { Fingerprint = "historical-per-invocation" }
        };
        var resolved = new ResolvedDatabaseProfile(profile, DatabaseProfileResolutionSource.ExplicitOverride, "historical-per-invocation");
        var state = new DatabaseRuntimeState(new DatabaseSwitchNotificationService());
        state.MarkCurrentProfile(resolved);
        var calls = 0;
        var bases = new List<AgentHistoricalCatalogBasis>();
        var owner = new AgentHistoricalReceiptReviewService(resolved, state, Options.Create(new StorageOptions()), Unused<IHostEnvironment>(), [], basis => {
            bases.Add(basis);
            if (++calls == 1) {
                state.PublishRestartObserved(state.GetSnapshot(), resolved);
            }
            return new([], []);
        });
        var request = Request();
        var interrupted = await owner.ReadSavedEffectReceiptsAsync(request);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ContextChanged, interrupted.Stop);
        Assert.Equal(new DatabaseProfileGeneration(0), interrupted.CurrentContext.Generation);
        var next = await owner.ReadSavedEffectReceiptsAsync(request);
        Assert.Equal(AgentHistoricalReceiptReviewStop.Unsupported, next.Stop);
        Assert.Equal(new DatabaseProfileGeneration(1), next.CurrentContext.Generation);
        Assert.Equal(2, calls);
        Assert.NotSame(bases[0], bases[1]);
        Assert.All(bases, basis => Assert.Throws<InvalidOperationException>(() => basis.RequireCurrent()));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task Native_orchestrator_construction_does_not_resolve_historical_graph_and_reads_only_after_explicit_initialization() {
        var state = new DatabaseRuntimeState(new DatabaseSwitchNotificationService());
        var resolved = 0;
        var service = new HistoricalReadService();
        var owner = new AgentChatExecutionOrchestrator(Unused<IAgentFrameworkWorkspaceActivityExecutionService>(),
            Unused<IAgentTurnContextCaptureService>(), Unused<IAgentChatExecutionNotificationHub>(),
            Unused<IAgentExecutionActivityCoordinator>(), Unused<ICanDoItAllAgentWorkspaceFactory>(),
            Unused<IDatabaseProfileRuntimeAccessor>(), Unused<IAgentExecutionProfileGenerationSource>(),
            historicalReceipts: () => {
                resolved++;
                return service;
            }, historicalRuntimeState: state);
        Assert.Equal(0, resolved);
        var request = Request();
        await Assert.ThrowsAsync<NotSupportedException>(() => owner.ReadSavedEffectReceiptsAsync(request));
        Assert.Equal(0, resolved);
        Initialize(state, service);
        var result = await owner.ReadSavedEffectReceiptsAsync(request);
        Assert.Equal(request, result.Request);
        Assert.Equal(1, resolved);
        Assert.Equal(1, service.Reads);
    }

    private static AgentHistoricalReceiptReviewRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

    private static void Initialize(DatabaseRuntimeState state, HistoricalReadService service) {
        var id = Guid.NewGuid();
        var profile = new DatabaseProfileRecord {
            Id = id,
            ProviderKind = DatabaseProviderKind.InMemory,
            SourceKind = DatabaseProfileSourceKind.InMemory,
            InMemory = new() { DatabaseName = id.ToString("N") },
            Runtime = new() { Fingerprint = "historical-deferred-owner" }
        };
        state.MarkCurrentProfile(new(profile, DatabaseProfileResolutionSource.ExplicitOverride, "historical-deferred-owner"));
        service.Context = new(id, new(0), WorkspaceScopeDescriptor.Organization(id.ToString("N")));
    }

    private static T Unused<T>() where T : class => DispatchProxy.Create<T, UnusedProxy>();

    private class UnusedProxy : DispatchProxy {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException($"Historical-only owner unexpectedly called {targetMethod?.Name}.");
    }

    private sealed class HistoricalReadService : IAgentHistoricalReceiptReviewService {
        internal AgentHistoricalReceiptReadContext? Context { get; set; }
        internal int Reads { get; private set; }

        public Task<AgentHistoricalReceiptReview> ReadSavedEffectReceiptsAsync(AgentHistoricalReceiptReviewRequest request,
            CancellationToken cancellationToken = default) {
            Reads++;
            return Task.FromResult(new AgentHistoricalReceiptReview(request, Context!, null, null,
                AgentHistoricalReceiptReviewStop.Unsupported, []));
        }
    }
}
