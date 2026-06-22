using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ProviderRuntimeLifecycleTests
{
    private static readonly IAgentProviderFactory EmptyProviderFactory = new AgentProviderDriverRegistryBuilder().Build();

    [Fact]
    public async Task RuntimePool_ReusesHandleUntilConfigFingerprintChanges()
    {
        var providerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var descriptorSource = new MutableDescriptorSource(CreateDescriptor(providerId, configurationJson: """{"version":1}"""));
        var pool = new ProviderRuntimePool(descriptorSource, new ProviderRuntimeHandleFactory(EmptyProviderFactory));

        await using (pool)
        {
            var first = await pool.GetRequiredAsync(providerId);
            var second = await pool.GetRequiredAsync(providerId);

            Assert.Same(first, second);
            Assert.False(first.IsDisposed);

            descriptorSource.Descriptor = CreateDescriptor(providerId, configurationJson: """{"version":2}""");
            var replacement = await pool.GetRequiredAsync(providerId);

            Assert.NotSame(first, replacement);
            Assert.True(first.IsDisposed);
            Assert.False(replacement.IsDisposed);
            Assert.NotEqual(first.Key.ConfigFingerprint, replacement.Key.ConfigFingerprint);
        }
    }

    [Fact]
    public async Task RuntimePool_InvalidateDisposesCachedHandle()
    {
        var providerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var descriptorSource = new MutableDescriptorSource(CreateDescriptor(providerId));
        var pool = new ProviderRuntimePool(descriptorSource, new ProviderRuntimeHandleFactory(EmptyProviderFactory));

        await using (pool)
        {
            var first = await pool.GetRequiredAsync(providerId);

            await pool.InvalidateAsync(providerId, ProviderRuntimeInvalidationReason.ProfileSaved);
            var second = await pool.GetRequiredAsync(providerId);

            Assert.NotSame(first, second);
            Assert.True(first.IsDisposed);
            Assert.False(second.IsDisposed);
        }
    }

    [Fact]
    public async Task RuntimeHandle_ConcurrentDispatchCorrelatesResponses()
    {
        var provider = CreateProvider(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        await using var handle = new ProviderRuntimeHandle(
            ProviderRuntimeDescriptor.FromProfile(provider),
            EmptyProviderFactory);

        var tasks = Enumerable.Range(0, 50)
            .Select(index => handle.DispatchAsync(
                new ProviderRuntimeDispatchRequest<int>(
                    CreateDispatchQuery(provider, "batch-model"),
                    index,
                    Guid.NewGuid()),
                async (context, cancellationToken) =>
                {
                    await Task.Delay(25 - index % 10, cancellationToken);
                    return (context.CorrelationId, Value: context.Payload * 2);
                }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(50, results.Length);
        Assert.Equal(Enumerable.Range(0, 50).Select(value => value * 2).Order(), results.Select(result => result.Value).Order());
        Assert.Equal(50, results.Select(result => result.CorrelationId).Distinct().Count());
        Assert.Equal(0, handle.InFlightRequestCount);
    }

    [Fact]
    public async Task RuntimeHandle_CancelOneRequestDoesNotCancelUnrelatedRequest()
    {
        var provider = CreateProvider(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));
        await using var handle = new ProviderRuntimeHandle(
            ProviderRuntimeDescriptor.FromProfile(provider),
            EmptyProviderFactory);
        using var canceledRequest = new CancellationTokenSource();
        var neverCompletes = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var canceledTask = handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<int>(
                CreateDispatchQuery(provider, "single-model"),
                1,
                Guid.NewGuid()),
            async (context, cancellationToken) =>
            {
                await neverCompletes.Task.WaitAsync(cancellationToken);
                return context.Payload;
            },
            canceledRequest.Token);

        var unrelatedTask = handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<int>(
                CreateDispatchQuery(provider, "single-model"),
                41,
                Guid.NewGuid()),
            (context, cancellationToken) => Task.FromResult(context.Payload + 1));

        await canceledRequest.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledTask);
        Assert.Equal(42, await unrelatedTask);
        Assert.Equal(0, handle.InFlightRequestCount);
    }

    [Fact]
    public void RuntimeLifecycle_DoesNotCaptureScopedServices()
    {
        var bannedTypeFragments = new[]
        {
            "DbContext",
            "IProviderProfileRegistry",
            "IProviderRuntimeGateway",
            "NavigationManager",
            "AuthenticationStateProvider"
        };
        var runtimeTypes = new[]
        {
            typeof(ProviderRuntimePool),
            typeof(ProviderRuntimeHandle),
            typeof(ProviderRuntimeHandleFactory)
        };

        foreach (var constructor in runtimeTypes.SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                Assert.DoesNotContain(
                    bannedTypeFragments,
                    fragment => parameter.ParameterType.Name.Contains(fragment, StringComparison.Ordinal));
            }
        }

        var root = FindRepositoryRoot();
        var runtimeSource = Directory
            .EnumerateFiles(Path.Combine(root, "src", "CanDoItAll.AgentFramework.Providers", "Runtime"), "*.cs")
            .Select(File.ReadAllText);
        foreach (var source in runtimeSource)
        {
            Assert.DoesNotContain("AppDbContext", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RuntimeLifecycle_DoesNotAdoptExistingConsumers()
    {
        var root = FindRepositoryRoot();
        var filesThatMustNotAdoptProviderProject = new[]
        {
            "src/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj",
            "src/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj"
        };

        foreach (var relativePath in filesThatMustNotAdoptProviderProject)
        {
            var text = File.ReadAllText(Path.Combine(root, relativePath));
            Assert.DoesNotContain("CanDoItAll.AgentFramework.Providers", text, StringComparison.Ordinal);
        }
    }

    private static ProviderRuntimeDescriptor CreateDescriptor(
        Guid providerId,
        string configurationJson = "{}")
    {
        return ProviderRuntimeDescriptor.FromProfile(
            CreateProvider(providerId, configurationJson),
            connectorPluginKey: "test-provider",
            secretReferenceIdentity: "secret-ref",
            timeoutSeconds: 30,
            configSchemaVersion: 1);
    }

    private static ProviderProfile CreateProvider(
        Guid providerId,
        string configurationJson = "{}")
    {
        return new ProviderProfile(
            providerId,
            "Runtime provider",
            ProviderKind.OpenAi,
            "https://example.test",
            "FAKE_API_KEY",
            "single-model",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: configurationJson,
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["single-model", "batch-model"]);
    }

    private static ProviderDispatchQuery CreateDispatchQuery(
        ProviderProfile provider,
        string model)
    {
        return new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            model);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed class MutableDescriptorSource(ProviderRuntimeDescriptor descriptor) : IProviderRuntimeDescriptorSource
    {
        public ProviderRuntimeDescriptor Descriptor { get; set; } = descriptor;

        public Task<ProviderRuntimeDescriptor> GetRequiredAsync(
            Guid providerProfileId,
            CancellationToken cancellationToken = default)
        {
            if (Descriptor.Key.ProviderProfileId != providerProfileId)
            {
                throw new InvalidOperationException("Requested provider id does not match the mutable descriptor source.");
            }

            return Task.FromResult(Descriptor);
        }
    }
}
