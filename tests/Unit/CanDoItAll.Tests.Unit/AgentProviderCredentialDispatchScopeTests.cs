using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using Microsoft.Extensions.Configuration;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentProviderCredentialDispatchScopeTests {
    [Fact]
    public void Unscoped_resolution_runs_secret_io_outside_the_callers_synchronization_context() {
        var secretResolver =
            new SynchronizationContextProbeSecretRuntimeResolver();
        var resolver = CreateResolver(secretResolver);
        var provider = CreateProvider();
        var previousContext = SynchronizationContext.Current;
        var callerContext = new SynchronizationContext();

        try {
            SynchronizationContext.SetSynchronizationContext(callerContext);

            var resolution = resolver.Resolve(provider);

            Assert.True(resolution.IsResolved);
            Assert.Null(secretResolver.ObservedSynchronizationContext);
            Assert.Same(callerContext, SynchronizationContext.Current);
        }
        finally {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task Six_resolves_in_one_dispatch_read_the_secret_once() {
        var secretResolver = new CountingSecretRuntimeResolver(
            (_, _) => Task.FromResult<string?>("dispatch-secret"));
        var resolver = CreateResolver(secretResolver);
        var factory =
            Assert.IsAssignableFrom<IAgentProviderCredentialDispatchScopeFactory>(
                resolver);
        var provider = CreateProvider();

        using var preparation =
            await factory.PrepareAsync([provider]);
        using var scope = preparation.BeginScope();

        var resolutions = Enumerable.Range(0, 6)
            .Select(_ => resolver.Resolve(provider))
            .ToArray();

        Assert.Equal(1, secretResolver.CallCount);
        Assert.All(
            resolutions,
            resolution => {
                Assert.Equal("dispatch-secret", resolution.ApiKey);
                Assert.False(resolution.ShouldPromoteToProcessEnvironment);
            });
    }

    [Fact]
    public async Task Next_dispatch_reads_the_secret_again() {
        var secretResolver = new CountingSecretRuntimeResolver(
            (call, _) => Task.FromResult<string?>($"dispatch-secret-{call}"));
        var resolver = CreateResolver(secretResolver);
        var factory =
            (IAgentProviderCredentialDispatchScopeFactory)resolver;
        var provider = CreateProvider();

        var first = await ResolveDispatchAsync(
            factory,
            resolver,
            provider,
            resolveCount: 3);
        var second = await ResolveDispatchAsync(
            factory,
            resolver,
            provider,
            resolveCount: 3);

        Assert.Equal(2, secretResolver.CallCount);
        Assert.All(first, value => Assert.Equal("dispatch-secret-1", value));
        Assert.All(second, value => Assert.Equal("dispatch-secret-2", value));
    }

    [Fact]
    public async Task Unresolved_secret_is_cached_for_the_dispatch() {
        var secretResolver = new CountingSecretRuntimeResolver(
            (_, _) => Task.FromResult<string?>(null));
        var resolver = CreateResolver(secretResolver);
        var factory =
            (IAgentProviderCredentialDispatchScopeFactory)resolver;
        var provider = CreateProvider();

        using var preparation =
            await factory.PrepareAsync([provider]);
        using var scope = preparation.BeginScope();

        var resolutions = Enumerable.Range(0, 6)
            .Select(_ => resolver.Resolve(provider))
            .ToArray();

        Assert.Equal(1, secretResolver.CallCount);
        Assert.All(resolutions, resolution => Assert.False(resolution.IsResolved));
        Assert.All(
            resolutions,
            resolution => Assert.Contains(
                "was not found",
                resolution.FailureMessage,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Parallel_dispatches_keep_credential_results_isolated() {
        var barrier = new TwoParticipantAsyncBarrier();
        var secretResolver = new CountingSecretRuntimeResolver(
            async (call, cancellationToken) => {
                await barrier.SignalAndWaitAsync(cancellationToken);
                return $"dispatch-secret-{call}";
            });
        var resolver = CreateResolver(secretResolver);
        var factory =
            (IAgentProviderCredentialDispatchScopeFactory)resolver;
        var provider = CreateProvider();

        var dispatches = await Task.WhenAll(
            ResolveDispatchAsync(
                factory,
                resolver,
                provider,
                resolveCount: 4),
            ResolveDispatchAsync(
                factory,
                resolver,
                provider,
                resolveCount: 4));

        Assert.Equal(2, secretResolver.CallCount);
        Assert.Single(dispatches[0].Distinct(StringComparer.Ordinal));
        Assert.Single(dispatches[1].Distinct(StringComparer.Ordinal));
        Assert.NotEqual(dispatches[0][0], dispatches[1][0]);
    }

    [Fact]
    public async Task Scope_supports_distinct_handoff_providers() {
        var secretResolver = new CountingSecretRuntimeResolver(
            (call, _) => Task.FromResult<string?>($"provider-secret-{call}"));
        var resolver = CreateResolver(secretResolver);
        var factory =
            (IAgentProviderCredentialDispatchScopeFactory)resolver;
        var firstProvider = CreateProvider();
        var secondProvider = CreateProvider(
            providerId: Guid.NewGuid(),
            secretRecordId: Guid.NewGuid());

        using var preparation =
            await factory.PrepareAsync([firstProvider, secondProvider]);
        using var scope = preparation.BeginScope();

        var first = resolver.Resolve(firstProvider);
        var second = resolver.Resolve(secondProvider);

        Assert.Equal(2, secretResolver.CallCount);
        Assert.NotEqual(first.ApiKey, second.ApiKey);
        Assert.Equal(first.ApiKey, resolver.Resolve(firstProvider).ApiKey);
        Assert.Equal(second.ApiKey, resolver.Resolve(secondProvider).ApiKey);
        Assert.Equal(2, secretResolver.CallCount);
    }

    [Fact]
    public async Task Same_provider_id_with_changed_fingerprint_fails_closed() {
        var secretResolver = new CountingSecretRuntimeResolver(
            (_, _) => Task.FromResult<string?>("dispatch-secret"));
        var resolver = CreateResolver(secretResolver);
        var factory =
            (IAgentProviderCredentialDispatchScopeFactory)resolver;
        var provider = CreateProvider();
        var changedProvider = provider with {
            BaseUrl = "https://changed.example.test/v1"
        };

        using var preparation =
            await factory.PrepareAsync([provider]);
        using var scope = preparation.BeginScope();

        var exception = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve(changedProvider));

        Assert.Contains(
            "changed configuration",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(1, secretResolver.CallCount);
    }

    [Fact]
    public async Task Preparation_propagates_secret_resolution_cancellation() {
        var secretResolver = new CancellationBlockingSecretRuntimeResolver();
        var resolver = CreateResolver(secretResolver);
        var factory =
            (IAgentProviderCredentialDispatchScopeFactory)resolver;
        var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();

        var preparation = factory
            .PrepareAsync([provider], cancellation.Token)
            .AsTask();
        await secretResolver.Entered;
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => preparation);
        Assert.Equal(1, secretResolver.CallCount);
    }

    [Fact]
    public async Task Captured_child_flow_throws_after_scope_disposal() {
        var secretResolver = new CountingSecretRuntimeResolver(
            (_, _) => Task.FromResult<string?>("dispatch-secret"));
        var resolver = CreateResolver(secretResolver);
        var factory =
            (IAgentProviderCredentialDispatchScopeFactory)resolver;
        var provider = CreateProvider();
        using var preparation =
            await factory.PrepareAsync([provider]);
        var scope = preparation.BeginScope();
        var releaseChild = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var childResolution = Task.Run(async () => {
            await releaseChild.Task;
            return resolver.Resolve(provider);
        });

        scope.Dispose();
        releaseChild.SetResult();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => childResolution);
        Assert.Equal(1, secretResolver.CallCount);
    }

    [Fact]
    public async Task Nested_scope_disposal_restores_the_parent_scope() {
        var secretResolver = new CountingSecretRuntimeResolver(
            (call, _) => Task.FromResult<string?>($"dispatch-secret-{call}"));
        var resolver = CreateResolver(secretResolver);
        var factory =
            (IAgentProviderCredentialDispatchScopeFactory)resolver;
        var outerProvider = CreateProvider();
        var innerProvider = CreateProvider(
            providerId: Guid.NewGuid(),
            secretRecordId: Guid.NewGuid());
        using var outerPreparation =
            await factory.PrepareAsync([outerProvider]);
        using var outerScope = outerPreparation.BeginScope();
        var outerResolution = resolver.Resolve(outerProvider);
        using var innerPreparation =
            await factory.PrepareAsync([innerProvider]);
        var innerScope = innerPreparation.BeginScope();

        Assert.NotEqual(
            outerResolution.ApiKey,
            resolver.Resolve(innerProvider).ApiKey);

        innerScope.Dispose();

        Assert.Equal(
            outerResolution.ApiKey,
            resolver.Resolve(outerProvider).ApiKey);
        Assert.Equal(2, secretResolver.CallCount);
    }

    private static SecretStoreAgentProviderCredentialResolver CreateResolver(
        ISecretRuntimeResolver secretResolver) {
        return new(
            secretResolver,
            new ConfigurationBuilder().Build());
    }

    private static ProviderProfile CreateProvider(
        Guid? providerId = null,
        Guid? secretRecordId = null) {
        var resolvedSecretRecordId = secretRecordId ?? Guid.NewGuid();
        return new ProviderProfile(
            Id: providerId ?? Guid.NewGuid(),
            Name: "Credential scope test provider",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.example.test/v1",
            ApiKeyEnvironmentVariable: string.Empty,
            DefaultModel: "test-model",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson:
                $$"""{"secretRecordId":"{{resolvedSecretRecordId:D}}"}""",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: ["test-model"]);
    }

    private static async Task<string[]> ResolveDispatchAsync(
        IAgentProviderCredentialDispatchScopeFactory factory,
        IAgentProviderCredentialResolver resolver,
        ProviderProfile provider,
        int resolveCount) {
        using var preparation =
            await factory.PrepareAsync([provider]);
        using var scope = preparation.BeginScope();
        return Enumerable.Range(0, resolveCount)
            .Select(_ => resolver.Resolve(provider).ApiKey)
            .ToArray();
    }

    private sealed class CountingSecretRuntimeResolver(
        Func<int, CancellationToken, Task<string?>> resolve) :
        ISecretRuntimeResolver {
        private int callCount;

        public int CallCount => Volatile.Read(ref callCount);

        public Task<string?> ResolveValueAsync(
            SecretRuntimeRequest request,
            CancellationToken cancellationToken = default) {
            var call = Interlocked.Increment(ref callCount);
            return resolve(call, cancellationToken);
        }
    }

    private sealed class CancellationBlockingSecretRuntimeResolver :
        ISecretRuntimeResolver {
        private readonly TaskCompletionSource entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int callCount;

        public int CallCount => Volatile.Read(ref callCount);

        public Task Entered => entered.Task;

        public async Task<string?> ResolveValueAsync(
            SecretRuntimeRequest request,
            CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref callCount);
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }

    private sealed class SynchronizationContextProbeSecretRuntimeResolver :
        ISecretRuntimeResolver {
        private SynchronizationContext? observedSynchronizationContext;

        public SynchronizationContext? ObservedSynchronizationContext =>
            Volatile.Read(ref observedSynchronizationContext);

        public Task<string?> ResolveValueAsync(
            SecretRuntimeRequest request,
            CancellationToken cancellationToken = default) {
            Volatile.Write(
                ref observedSynchronizationContext,
                SynchronizationContext.Current);
            return Task.FromResult<string?>("unscoped-secret");
        }
    }

    private sealed class TwoParticipantAsyncBarrier {
        private readonly TaskCompletionSource completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public async Task SignalAndWaitAsync(
            CancellationToken cancellationToken) {
            if (Interlocked.Increment(ref arrivals) == 2) {
                completed.TrySetResult();
            }

            await completed.Task.WaitAsync(cancellationToken);
        }
    }
}
