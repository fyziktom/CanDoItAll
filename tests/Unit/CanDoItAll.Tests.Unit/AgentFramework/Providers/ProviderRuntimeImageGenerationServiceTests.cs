using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ProviderRuntimeImageGenerationServiceTests
{
    [Fact]
    public async Task ImageGenerationService_PreparesExactNestedCredentialScope_AndRestoresOuterScope()
    {
        var chatProvider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderProfilePurpose.Chat,
            "gpt-chat-test");
        var requestedImageProvider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderProfilePurpose.ImageGeneration,
            "gpt-image-requested");
        var unrelatedImageProvider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderProfilePurpose.ImageGeneration,
            "gpt-image-unrelated");
        var credentialResolver = new ScopedCredentialResolver();
        var driver = new CredentialResolvingImageProviderDriver(
            ProviderKind.OpenAi,
            credentialResolver);
        var descriptorStore = new ProviderProfileRuntimeDescriptorStore();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorStore,
            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
                .AddDriver(driver)
                .Build()));
        var service = new ProviderRuntimeImageGenerationService(
            descriptorStore,
            runtimePool,
            credentialResolver);

        using (var outerPreparation =
               await credentialResolver.PrepareAsync([chatProvider]))
        using (outerPreparation.BeginScope())
        {
            var outerResolution = credentialResolver.Resolve(chatProvider);

            var result = await service.GenerateAsync(
                new AgentImageGenerationRequest(
                    requestedImageProvider,
                    requestedImageProvider.DefaultModel,
                    "draw the scoped image",
                    "1024x1024",
                    "low",
                    AgentGeneratedImageFormat.Png,
                    []));

            var imagePreparation = Assert.Single(
                credentialResolver.PreparedProviderIdBatches.Skip(1));
            Assert.Equal([requestedImageProvider.Id], imagePreparation);
            Assert.DoesNotContain(unrelatedImageProvider.Id, imagePreparation);
            Assert.Equal(2, driver.ObservedScopeDepth);
            Assert.True(driver.CredentialResolution.IsResolved);
            Assert.DoesNotContain(
                "not prepared",
                driver.CredentialResolution.FailureMessage,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                driver.CredentialResolution.ApiKey,
                Encoding.UTF8.GetString(Assert.Single(result.Images).Bytes));

            Assert.Equal(1, credentialResolver.CurrentScopeDepth);
            Assert.Equal(
                outerResolution.ApiKey,
                credentialResolver.Resolve(chatProvider).ApiKey);
            Assert.Contains(
                "not prepared",
                credentialResolver.Resolve(requestedImageProvider).FailureMessage,
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(0, credentialResolver.CurrentScopeDepth);
    }

    [Fact]
    public async Task ImageGenerationService_DispatchesConcurrentRequestsThroughSharedRuntimeHandle()
    {
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderProfilePurpose.ImageGeneration,
            "gpt-image-test");
        var driver = new ConcurrentImageProviderDriver(ProviderKind.OpenAi);
        var descriptorStore = new ProviderProfileRuntimeDescriptorStore();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorStore,
            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
                .AddDriver(driver)
                .Build()));
        var service = new ProviderRuntimeImageGenerationService(descriptorStore, runtimePool);

        var tasks = Enumerable.Range(0, 8)
            .Select(index => service.GenerateAsync(new AgentImageGenerationRequest(
                provider,
                "gpt-image-test",
                $"prompt-{index}",
                "1024x1024",
                "low",
                AgentGeneratedImageFormat.Png,
                [])))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(
            Enumerable.Range(0, 8).Select(index => $"prompt-{index}").Order(),
            results.Select(result => Encoding.UTF8.GetString(Assert.Single(result.Images).Bytes)).Order());
        Assert.True(driver.MaxObservedInFlight > 1);
        var handle = await runtimePool.GetRequiredAsync(provider.Id);
        Assert.Equal(0, handle.InFlightRequestCount);
    }

    [Fact]
    public async Task ImageGenerationService_ThrowsUnsupportedCapability_WhenProviderKindHasNoImageDriver()
    {
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderProfilePurpose.ImageGeneration,
            "local-image");
        var descriptorStore = new ProviderProfileRuntimeDescriptorStore();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorStore,
            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
                .AddDriver(new ConcurrentImageProviderDriver(ProviderKind.OpenAi))
                .Build()));
        var service = new ProviderRuntimeImageGenerationService(descriptorStore, runtimePool);

        var exception = await Assert.ThrowsAsync<UnsupportedProviderCapabilityException>(() => service.GenerateAsync(new AgentImageGenerationRequest(
            provider,
            "local-image",
            "draw a cube",
            "1024x1024",
            "low",
            AgentGeneratedImageFormat.Png,
            [])));

        Assert.Equal(ProviderKind.Ollama, exception.ProviderKind);
        Assert.Equal(AgentProviderCapabilityKind.ImageGeneration, exception.Capability);
    }

    [Fact]
    public async Task ImageGenerationService_RejectsNonImageProviderPurpose()
    {
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderProfilePurpose.Chat,
            "gpt-5-mini");
        var descriptorStore = new ProviderProfileRuntimeDescriptorStore();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorStore,
            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
                .AddDriver(new ConcurrentImageProviderDriver(ProviderKind.OpenAi))
                .Build()));
        var service = new ProviderRuntimeImageGenerationService(descriptorStore, runtimePool);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(new AgentImageGenerationRequest(
            provider,
            "gpt-5-mini",
            "draw a cube",
            "1024x1024",
            "low",
            AgentGeneratedImageFormat.Png,
            [])));

        Assert.Contains("image-generation provider profile", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        ProviderProfilePurpose purpose,
        string defaultModel)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            $"{kind} {purpose} provider",
            kind,
            "https://provider.example.test",
            "PROVIDER_API_KEY",
            defaultModel,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [defaultModel],
            Purpose: purpose);
    }

    private sealed class ScopedCredentialResolver :
        IAgentProviderCredentialResolver,
        IAgentProviderCredentialDispatchScopeFactory
    {
        private readonly AsyncLocal<CredentialScope?> currentScope = new();
        private readonly List<IReadOnlyList<Guid>> preparedProviderIdBatches = [];

        public int CurrentScopeDepth
        {
            get
            {
                var depth = 0;
                for (var scope = currentScope.Value;
                     scope is not null;
                     scope = scope.Parent)
                {
                    depth++;
                }

                return depth;
            }
        }

        public IReadOnlyList<IReadOnlyList<Guid>> PreparedProviderIdBatches =>
            preparedProviderIdBatches;

        public ValueTask<IAgentProviderCredentialDispatchScopePreparation> PrepareAsync(
            IReadOnlyList<ProviderProfile> providers,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preparedProviders = providers.ToArray();
            preparedProviderIdBatches.Add(
                preparedProviders.Select(provider => provider.Id).ToArray());
            return ValueTask.FromResult<
                IAgentProviderCredentialDispatchScopePreparation>(
                new CredentialScopePreparation(this, preparedProviders));
        }

        public ProviderCredentialResolution Resolve(ProviderProfile provider)
        {
            if (currentScope.Value?.Providers.Contains(provider.Id) == true)
            {
                return new ProviderCredentialResolution(
                    $"credential:{provider.Id:D}",
                    "Scoped test resolver",
                    string.Empty);
            }

            return new ProviderCredentialResolution(
                string.Empty,
                "Scoped test resolver",
                $"Provider '{provider.Id:D}' was not prepared for the active dispatch.");
        }

        private IAgentProviderCredentialDispatchScope BeginScope(
            IReadOnlyList<ProviderProfile> providers)
        {
            var scope = new CredentialScope(
                this,
                currentScope.Value,
                providers.Select(provider => provider.Id).ToHashSet());
            currentScope.Value = scope;
            return scope;
        }

        private void EndScope(CredentialScope scope)
        {
            if (!ReferenceEquals(currentScope.Value, scope))
            {
                throw new InvalidOperationException(
                    "Credential test scopes must be disposed in nesting order.");
            }

            currentScope.Value = scope.Parent;
        }

        private sealed class CredentialScopePreparation(
            ScopedCredentialResolver owner,
            IReadOnlyList<ProviderProfile> providers) :
            IAgentProviderCredentialDispatchScopePreparation
        {
            public IAgentProviderCredentialDispatchScope BeginScope()
            {
                return owner.BeginScope(providers);
            }

            public void Dispose()
            {
            }
        }

        private sealed class CredentialScope(
            ScopedCredentialResolver owner,
            CredentialScope? parent,
            IReadOnlySet<Guid> providers) :
            IAgentProviderCredentialDispatchScope
        {
            private bool disposed;

            public CredentialScope? Parent { get; } = parent;

            public IReadOnlySet<Guid> Providers { get; } = providers;

            public ProviderCredentialResolution Resolve(ProviderProfile provider)
            {
                return owner.Resolve(provider);
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                owner.EndScope(this);
                disposed = true;
            }
        }
    }

    private sealed class CredentialResolvingImageProviderDriver(
        ProviderKind providerKind,
        ScopedCredentialResolver credentialResolver) :
        IProviderImageGenerationDriver
    {
        public ProviderKind ProviderKind { get; } = providerKind;

        public IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; } =
            new HashSet<AgentProviderCapabilityKind>
            {
                AgentProviderCapabilityKind.ImageGeneration
            };

        public ProviderCredentialResolution CredentialResolution { get; private set; } =
            new(
                string.Empty,
                "Not called",
                "The driver has not resolved credentials.");

        public int ObservedScopeDepth { get; private set; }

        public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
        {
            return ProviderDispatchLimits.Unbatched(
                TimeSpan.FromSeconds(30),
                maxInFlightRequests: 1);
        }

        public Task<ProviderImageGenerationResult> GenerateImageAsync(
            ProviderImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedScopeDepth = credentialResolver.CurrentScopeDepth;
            CredentialResolution = credentialResolver.Resolve(request.Provider);
            if (!CredentialResolution.IsResolved)
            {
                throw new InvalidOperationException(
                    CredentialResolution.FailureMessage);
            }

            return Task.FromResult(
                new ProviderImageGenerationResult(
                    request.Model,
                    request.Format,
                    [
                        new ProviderGeneratedImage(
                            "image/png",
                            Encoding.UTF8.GetBytes(CredentialResolution.ApiKey),
                            request.Prompt)
                    ]));
        }
    }

    private sealed class ConcurrentImageProviderDriver(ProviderKind providerKind) : IProviderImageGenerationDriver
    {
        private int inFlight;
        private int maxObservedInFlight;

        public ProviderKind ProviderKind { get; } = providerKind;

        public IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; } =
            new HashSet<AgentProviderCapabilityKind> { AgentProviderCapabilityKind.ImageGeneration };

        public int MaxObservedInFlight => Volatile.Read(ref maxObservedInFlight);

        public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
        {
            return ProviderDispatchLimits.Unbatched(
                TimeSpan.FromSeconds(30),
                maxInFlightRequests: 8);
        }

        public async Task<ProviderImageGenerationResult> GenerateImageAsync(
            ProviderImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref inFlight);
            ObserveMaxInFlight(current);
            try
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                return new ProviderImageGenerationResult(
                    request.Model,
                    request.Format,
                    [new ProviderGeneratedImage("image/png", Encoding.UTF8.GetBytes(request.Prompt), request.Prompt)]);
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }

        private void ObserveMaxInFlight(int current)
        {
            while (true)
            {
                var observed = Volatile.Read(ref maxObservedInFlight);
                if (current <= observed ||
                    Interlocked.CompareExchange(ref maxObservedInFlight, current, observed) == observed)
                {
                    return;
                }
            }
        }
    }
}
