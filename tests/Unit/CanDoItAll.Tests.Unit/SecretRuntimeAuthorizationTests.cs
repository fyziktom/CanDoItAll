using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class SecretRuntimeAuthorizationTests
{
    private const string PluginId = "plugin.external-webhook";
    private const string ConnectionId = "primary";

    [Fact]
    public async Task SecretRuntimeResolver_allows_plugin_connection_bound_secret()
    {
        var fixture = await CreateBoundPluginSecretAsync();

        var resolved = await fixture.Resolver.ResolveValueAsync(new SecretRuntimeRequest(
            fixture.SecretId,
            SecretRuntimePurposes.PluginConnectionSecret,
            ConsumerType: SecretRuntimeConsumerTypes.PluginConnection,
            ConsumerId: SecretRuntimeConsumerIds.PluginConnection(PluginId, ConnectionId)));

        Assert.Equal("plugin-secret", resolved);
    }

    [Fact]
    public async Task SecretRuntimeResolver_rejects_wrong_plugin_consumer_without_leaking_value()
    {
        var fixture = await CreateBoundPluginSecretAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Resolver.ResolveValueAsync(new SecretRuntimeRequest(
                fixture.SecretId,
                SecretRuntimePurposes.PluginConnectionSecret,
                ConsumerType: SecretRuntimeConsumerTypes.PluginConnection,
                ConsumerId: SecretRuntimeConsumerIds.PluginConnection(PluginId, "other"))));

        Assert.Contains("not authorized", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("plugin-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecretRuntimeResolver_rejects_wrong_plugin_purpose()
    {
        var fixture = await CreateBoundPluginSecretAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Resolver.ResolveValueAsync(new SecretRuntimeRequest(
                fixture.SecretId,
                SecretRuntimePurposes.PluginWorkflowExecutorSecret,
                ConsumerType: SecretRuntimeConsumerTypes.PluginConnection,
                ConsumerId: SecretRuntimeConsumerIds.PluginConnection(PluginId, ConnectionId))));
    }

    [Fact]
    public async Task PluginSecretBroker_resolves_bound_secret_and_lists_redacted_summaries()
    {
        var fixture = await CreateBoundPluginSecretAsync();
        var broker = new PluginSecretBroker(fixture.Resolver, fixture.SecretService);

        var resolved = await broker.ResolveSecretAsync(new PluginSecretResolveRequest(
            new PluginSecretReference(fixture.SecretId, PluginId, ConnectionId, "apiKey"),
            PluginSecretResolutionPurpose.ConnectionHealthCheck));
        var summaries = await broker.ListAllowedSecretsAsync(PluginId, ConnectionId);

        Assert.Equal("plugin-secret", resolved);
        var summary = Assert.Single(summaries);
        Assert.Equal(fixture.SecretId, summary.SecretId);
        Assert.Equal("Plugin API", summary.NameSnapshot);
        Assert.DoesNotContain("plugin-secret", JsonSerializer.Serialize(summaries), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PluginSecretBroker_returns_null_for_deleted_bound_secret()
    {
        var fixture = await CreateBoundPluginSecretAsync();
        var broker = new PluginSecretBroker(fixture.Resolver, fixture.SecretService);

        await fixture.SecretService.DeleteAsync(fixture.SecretId);

        var resolved = await broker.ResolveSecretAsync(new PluginSecretResolveRequest(
            new PluginSecretReference(fixture.SecretId, PluginId, ConnectionId),
            PluginSecretResolutionPurpose.ConnectionHealthCheck));

        Assert.Null(resolved);
    }

    private static async Task<SecretAuthorizationFixture> CreateBoundPluginSecretAsync()
    {
        var vault = new InMemorySecretVault();
        var factory = CreateDbContextFactory();
        var protector = new TestSecretProtector();
        var secretService = new SecretService(
            factory,
            vault,
            protector,
            new TestClock(new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero)),
            new NullActivityStream());
        var saveResult = await secretService.SaveAsync(new SecretEditorModel
        {
            Name = "Plugin API",
            Kind = SecretKind.ApiKey,
            SecretValue = "plugin-secret",
            Scope = "plugin"
        });
        Assert.True(saveResult.IsSuccess);
        var secretId = saveResult.Value;
        var bindResult = await secretService.BindSecretAsync(new SecretBindingCreateRequest(
            secretId,
            SecretRuntimeConsumerTypes.PluginConnection,
            SecretRuntimeConsumerIds.PluginConnection(PluginId, ConnectionId),
            SecretRuntimePurposes.PluginConnectionSecret));
        Assert.True(bindResult.IsSuccess);

        return new SecretAuthorizationFixture(
            secretId,
            secretService,
            new SecretRuntimeResolver(factory, vault, protector));
    }

    private static TestDbContextFactory CreateDbContextFactory()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(SecretRecord).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"secret-runtime-authorization-{Guid.NewGuid():N}")
            .Options;

        return new TestDbContextFactory(options);
    }

    private sealed record SecretAuthorizationFixture(
        Guid SecretId,
        SecretService SecretService,
        SecretRuntimeResolver Resolver);

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(options));
    }

    private sealed class TestSecretProtector : ISecretProtector
    {
        private const string Prefix = "legacy:";

        public string Protect(string plainText) => $"{Prefix}{plainText}";

        public string Unprotect(string protectedValue)
            => protectedValue.StartsWith(Prefix, StringComparison.Ordinal)
                ? protectedValue[Prefix.Length..]
                : throw new InvalidOperationException("Unsupported legacy test payload.");
    }

    private sealed class TestClock(DateTimeOffset currentUtc) : IClock
    {
        public DateTimeOffset GetUtcNow() => currentUtc;
    }
}
