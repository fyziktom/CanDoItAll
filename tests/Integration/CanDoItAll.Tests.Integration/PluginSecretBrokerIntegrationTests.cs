using CanDoItAll.Modules.Security;
using CanDoItAll.Security.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PluginSecretBrokerIntegrationTests
{
    [Fact]
    public async Task PluginSecretBroker_resolves_only_connection_bound_secret()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var secrets = scope.ServiceProvider.GetRequiredService<SecretService>();
        var broker = scope.ServiceProvider.GetRequiredService<IPluginSecretBroker>();
        var secretResult = await secrets.SaveAsync(new SecretEditorModel
        {
            Name = "Integration plugin API",
            Kind = SecretKind.ApiKey,
            SecretValue = "integration-plugin-secret",
            Scope = "plugin"
        });
        Assert.True(secretResult.IsSuccess);

        var secretId = secretResult.Value;
        var bindResult = await secrets.BindSecretAsync(new SecretBindingCreateRequest(
            secretId,
            SecretRuntimeConsumerTypes.PluginConnection,
            SecretRuntimeConsumerIds.PluginConnection("plugin.integration", "primary"),
            SecretRuntimePurposes.PluginConnectionSecret));
        Assert.True(bindResult.IsSuccess);

        var resolved = await broker.ResolveSecretAsync(new PluginSecretResolveRequest(
            new PluginSecretReference(secretId, "plugin.integration", "primary"),
            PluginSecretResolutionPurpose.ConnectionHealthCheck));

        Assert.Equal("integration-plugin-secret", resolved);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            broker.ResolveSecretAsync(new PluginSecretResolveRequest(
                new PluginSecretReference(secretId, "plugin.integration", "other"),
                PluginSecretResolutionPurpose.ConnectionHealthCheck)));
    }
}
