using System.Collections;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentExecutionPreparationCacheTests
{
    [Fact]
    public async Task Same_version_uses_one_shared_load_and_returns_reused_disposition()
    {
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var request = CreateRequest();
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCount = 0;

        Task<AgentExecutionPreparationBlueprint> LoadAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref loadCount);
            return CompleteAsync();

            async Task<AgentExecutionPreparationBlueprint> CompleteAsync()
            {
                await release.Task.WaitAsync(cancellationToken);
                return CreateBlueprint(request);
            }
        }

        var firstTask = cache.AcquireAsync(request, LoadAsync);
        var secondTask = cache.AcquireAsync(request, LoadAsync);
        release.SetResult();

        var first = AssertAcquired(await firstTask);
        var second = AssertAcquired(await secondTask);

        Assert.Equal(1, Volatile.Read(ref loadCount));
        Assert.Same(first.Blueprint, second.Blueprint);
        Assert.Equal(AgentExecutionPreparationCacheDisposition.Refreshed, first.Disposition);
        Assert.Equal(AgentExecutionPreparationCacheDisposition.Reused, second.Disposition);
    }

    [Fact]
    public async Task Different_keys_load_without_a_global_gate()
    {
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var firstRequest = CreateRequest(agentId: Guid.NewGuid());
        var secondRequest = CreateRequest(agentId: Guid.NewGuid());
        var bothEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredCount = 0;

        async Task<AgentExecutionPreparationBlueprint> LoadAsync(
            AgentExecutionPreparationRequest request,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref enteredCount) == 2)
            {
                bothEntered.SetResult();
            }

            await release.Task.WaitAsync(cancellationToken);
            return CreateBlueprint(request);
        }

        var firstTask = cache.AcquireAsync(
            firstRequest,
            token => LoadAsync(firstRequest, token));
        var secondTask = cache.AcquireAsync(
            secondRequest,
            token => LoadAsync(secondRequest, token));

        await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        release.SetResult();

        Assert.IsType<AgentExecutionPreparationAcquired>(await firstTask);
        Assert.IsType<AgentExecutionPreparationAcquired>(await secondTask);
    }

    [Fact]
    public async Task Cancelling_first_waiter_does_not_cancel_or_remove_shared_load()
    {
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var request = CreateRequest();
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCount = 0;
        CancellationToken sharedToken = default;

        async Task<AgentExecutionPreparationBlueprint> LoadAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref loadCount);
            sharedToken = cancellationToken;
            entered.SetResult();
            await release.Task.WaitAsync(cancellationToken);
            return CreateBlueprint(request);
        }

        using var firstWaiter = new CancellationTokenSource();
        var firstTask = cache.AcquireAsync(request, LoadAsync, firstWaiter.Token);
        await entered.Task;
        var secondTask = cache.AcquireAsync(request, LoadAsync);

        firstWaiter.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstTask);
        Assert.False(sharedToken.IsCancellationRequested);

        release.SetResult();
        var second = AssertAcquired(await secondTask);
        var third = AssertAcquired(await cache.AcquireAsync(request, LoadAsync));

        Assert.Equal(1, Volatile.Read(ref loadCount));
        Assert.Equal(AgentExecutionPreparationCacheDisposition.Reused, second.Disposition);
        Assert.Equal(AgentExecutionPreparationCacheDisposition.Reused, third.Disposition);
    }

    [Fact]
    public async Task Failed_shared_load_is_removed_and_can_be_retried()
    {
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var request = CreateRequest();
        var loadCount = 0;

        Task<AgentExecutionPreparationBlueprint> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref loadCount) == 1)
            {
                throw new InvalidOperationException("controlled load failure");
            }

            return Task.FromResult(CreateBlueprint(request));
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.AcquireAsync(request, LoadAsync));
        Assert.Equal("controlled load failure", exception.Message);

        var retry = AssertAcquired(await cache.AcquireAsync(request, LoadAsync));

        Assert.Equal(2, Volatile.Read(ref loadCount));
        Assert.Equal(AgentExecutionPreparationCacheDisposition.Refreshed, retry.Disposition);
    }

    [Fact]
    public async Task Invalidation_during_load_fences_stale_completion()
    {
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var request = CreateRequest();
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCount = 0;

        async Task<AgentExecutionPreparationBlueprint> LoadAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref loadCount);
            entered.SetResult();
            await release.Task;
            return CreateBlueprint(request);
        }

        var staleTask = cache.AcquireAsync(request, LoadAsync);
        await entered.Task;

        cache.Invalidate(request.Key);
        release.SetResult();

        await Assert.ThrowsAsync<AgentExecutionPreparationInvalidatedException>(
            () => staleTask);

        var fresh = AssertAcquired(await cache.AcquireAsync(
            request,
            _ => Task.FromResult(CreateBlueprint(request))));

        Assert.Equal(AgentExecutionPreparationCacheDisposition.Refreshed, fresh.Disposition);
    }

    [Fact]
    public async Task Each_validity_stamp_change_refreshes_the_key()
    {
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(4));
        var initial = CreateRequest();
        var catalogChanged = new AgentExecutionPreparationRequest(
            initial.Key,
            initial.Version with
            {
                CatalogRevision = new CatalogDataRevision(
                    initial.Version.CatalogRevision.Value + 1)
            });
        var profileChanged = new AgentExecutionPreparationRequest(
            initial.Key,
            catalogChanged.Version with
            {
                DatabaseProfileGeneration = new DatabaseProfileGeneration(
                    catalogChanged.Version.DatabaseProfileGeneration.Value + 1)
            });
        var providerChanged = new AgentExecutionPreparationRequest(
            initial.Key,
            profileChanged.Version with
            {
                ProviderFingerprint = new ProviderConfigurationFingerprint("provider-v2")
            });
        var loadCount = 0;

        async Task AcquireAsync(AgentExecutionPreparationRequest request)
        {
            var result = AssertAcquired(await cache.AcquireAsync(
                request,
                _ =>
                {
                    Interlocked.Increment(ref loadCount);
                    return Task.FromResult(CreateBlueprint(request));
                }));
            Assert.Equal(
                AgentExecutionPreparationCacheDisposition.Refreshed,
                result.Disposition);
        }

        await AcquireAsync(initial);
        await AcquireAsync(catalogChanged);
        await AcquireAsync(profileChanged);
        await AcquireAsync(providerChanged);

        Assert.Equal(4, Volatile.Read(ref loadCount));
    }

    [Fact]
    public async Task Capacity_rejects_explicitly_when_every_entry_is_loading()
    {
        using var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(2));
        var firstRequest = CreateRequest(agentId: Guid.NewGuid());
        var secondRequest = CreateRequest(agentId: Guid.NewGuid());
        var rejectedRequest = CreateRequest(agentId: Guid.NewGuid());
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Task<AgentExecutionPreparationBlueprint> WaitAsync(
            AgentExecutionPreparationRequest request,
            CancellationToken cancellationToken)
        {
            return CompleteAsync();

            async Task<AgentExecutionPreparationBlueprint> CompleteAsync()
            {
                await release.Task.WaitAsync(cancellationToken);
                return CreateBlueprint(request);
            }
        }

        var firstTask = cache.AcquireAsync(
            firstRequest,
            token => WaitAsync(firstRequest, token));
        var secondTask = cache.AcquireAsync(
            secondRequest,
            token => WaitAsync(secondRequest, token));

        var rejected = Assert.IsType<AgentExecutionPreparationRejected>(
            await cache.AcquireAsync(
                rejectedRequest,
                _ => Task.FromResult(CreateBlueprint(rejectedRequest))));
        Assert.Equal(
            AgentExecutionPreparationRejectionReason.CapacityExhausted,
            rejected.Reason);

        release.SetResult();
        await firstTask;
        await secondTask;

        var acceptedAfterCompletion = AssertAcquired(await cache.AcquireAsync(
            rejectedRequest,
            _ => Task.FromResult(CreateBlueprint(rejectedRequest))));
        Assert.Equal(
            AgentExecutionPreparationCacheDisposition.Refreshed,
            acceptedAfterCompletion.Disposition);
    }

    [Fact]
    public async Task Dispose_cancels_shared_work_is_idempotent_and_rejects_new_work()
    {
        var cache = new AgentExecutionPreparationCache(
            new AgentExecutionPreparationCachePolicy(2));
        var request = CreateRequest();
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<AgentExecutionPreparationBlueprint> LoadAsync(CancellationToken cancellationToken)
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateBlueprint(request);
        }

        var pending = cache.AcquireAsync(request, LoadAsync);
        await entered.Task;

        cache.Dispose();
        cache.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => cache.AcquireAsync(request, LoadAsync));
    }

    [Fact]
    public void Blueprint_is_a_deep_data_snapshot_without_live_runtime_resources()
    {
        var request = CreateRequest();
        var tags = new List<string> { "original" };
        var capabilities = new List<AgentCapabilityAssignment>
        {
            new(
                Guid.NewGuid(),
                "workspace",
                CapabilityKind.Tool,
                CapabilityProofStatus.Verified,
                DateTimeOffset.UtcNow,
                "verified")
        };
        var providerModels = new List<string> { "gpt-5.4-mini" };
        var agent = CreateAgent(request.Key.AgentId) with
        {
            Capabilities = capabilities,
            Tags = tags
        };
        var provider = CreateProvider() with
        {
            SuggestedModels = providerModels
        };

        var blueprint = AgentExecutionPreparationBlueprint.Create(
            request,
            agent,
            provider,
            [],
            []);

        tags.Add("mutated");
        capabilities.Clear();
        providerModels.Add("mutated-model");

        Assert.Equal(["original"], blueprint.Agent.Tags);
        Assert.Single(blueprint.Agent.Capabilities);
        Assert.Equal(["gpt-5.4-mini"], blueprint.Provider.SuggestedModels);
        Assert.Throws<NotSupportedException>(
            () => ((IList)blueprint.Agent.Tags).Add("blocked"));

        var forbiddenTypeNames = new[]
        {
            "DbContext",
            "ChatSession",
            "Authentication",
            "HttpClient",
            "AgentRuntime"
        };
        var retainedTypes = WalkPropertyTypes(
            typeof(AgentExecutionPreparationBlueprint),
            maximumDepth: 4);

        Assert.DoesNotContain(
            retainedTypes,
            type => typeof(IDisposable).IsAssignableFrom(type));
        Assert.DoesNotContain(
            retainedTypes,
            type => forbiddenTypeNames.Any(
                forbidden => type.Name.Contains(
                    forbidden,
                    StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Provider_fingerprint_ignores_health_but_tracks_runtime_configuration()
    {
        var provider = CreateProvider();
        var initial = ProviderConfigurationFingerprintFactory.Create(provider);
        var healthChanged = ProviderConfigurationFingerprintFactory.Create(provider with
        {
            HealthStatus = "degraded",
            LastCheckedAtUtc = DateTimeOffset.UtcNow
        });
        var modelChanged = ProviderConfigurationFingerprintFactory.Create(provider with
        {
            DefaultModel = "gpt-5.4"
        });

        Assert.Equal(initial, healthChanged);
        Assert.NotEqual(initial, modelChanged);
    }

    [Fact]
    public void Provider_fingerprint_canonicalizes_equivalent_configuration_json()
    {
        var provider = CreateProvider() with
        {
            ConfigurationJson =
                """{"timeoutSeconds":120,"modelParameters":{"temperature":0.2,"reasoning":"medium"}}"""
        };
        var reordered = provider with
        {
            ConfigurationJson =
                """
                {
                  "modelParameters": {
                    "reasoning": "medium",
                    "temperature": 0.2
                  },
                  "timeoutSeconds": 120
                }
                """
        };
        var changed = provider with
        {
            ConfigurationJson =
                """{"timeoutSeconds":121,"modelParameters":{"temperature":0.2,"reasoning":"medium"}}"""
        };

        Assert.Equal(
            ProviderConfigurationFingerprintFactory.Create(provider),
            ProviderConfigurationFingerprintFactory.Create(reordered));
        Assert.NotEqual(
            ProviderConfigurationFingerprintFactory.Create(provider),
            ProviderConfigurationFingerprintFactory.Create(changed));
    }

    private static AgentExecutionPreparationAcquired AssertAcquired(
        AgentExecutionPreparationAcquireResult result)
    {
        return Assert.IsType<AgentExecutionPreparationAcquired>(result);
    }

    private static AgentExecutionPreparationRequest CreateRequest(
        Guid? agentId = null)
    {
        return new AgentExecutionPreparationRequest(
            new AgentExecutionPreparationKey(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                WorkspaceScopeDescriptor.Project("project-42"),
                agentId ?? Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            new AgentExecutionPreparationVersion(
                new CatalogDataRevision(7),
                new DatabaseProfileGeneration(3),
                new ProviderConfigurationFingerprint("provider-v1")));
    }

    private static AgentExecutionPreparationBlueprint CreateBlueprint(
        AgentExecutionPreparationRequest request)
    {
        return AgentExecutionPreparationBlueprint.Create(
            request,
            CreateAgent(request.Key.AgentId),
            CreateProvider(),
            [],
            []);
    }

    private static AgentDefinition CreateAgent(Guid id)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            id,
            "Prepared agent",
            "Specialist",
            "Summary",
            "Instructions",
            AgentLifecycleStatus.Active,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "gpt-5.4-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            0.2,
            false,
            false,
            "{}",
            false,
            string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "OpenAI",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            "gpt-5.4-mini",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            true,
            true,
            "{}",
            "notes",
            "healthy",
            DateTimeOffset.UtcNow,
            ["gpt-5.4-mini"]);
    }

    private static HashSet<Type> WalkPropertyTypes(
        Type root,
        int maximumDepth)
    {
        var visited = new HashSet<Type>();
        Visit(root, maximumDepth);
        return visited;

        void Visit(Type type, int remainingDepth)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (!visited.Add(type) || remainingDepth == 0 || type == typeof(string))
            {
                return;
            }

            if (type.IsArray)
            {
                Visit(type.GetElementType()!, remainingDepth - 1);
                return;
            }

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    Visit(argument, remainingDepth - 1);
                }
            }

            foreach (var property in type.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance))
            {
                Visit(property.PropertyType, remainingDepth - 1);
            }
        }
    }
}
