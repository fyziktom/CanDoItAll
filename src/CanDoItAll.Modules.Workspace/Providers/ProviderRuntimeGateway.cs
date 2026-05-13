using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public interface IProviderRuntimeGateway
{
    Task<ProviderHealthResult> CheckHealthAsync(Guid providerProfileId, CancellationToken cancellationToken = default);

    Task<Result<ProviderExecutionResponse>> SendAsync(ProviderExecutionRequest request, CancellationToken cancellationToken = default);
}

public sealed class LegacyProviderRuntimeGateway(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ProviderRegistry providerRegistry,
    ISecretRuntimeResolver secretRuntimeResolver,
    IActivityStream activityStream,
    IClock clock) : IProviderRuntimeGateway
{
    public async Task<ProviderHealthResult> CheckHealthAsync(Guid providerProfileId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await dbContext.Set<ProviderProfile>()
            .FirstOrDefaultAsync(item => item.Id == providerProfileId, cancellationToken);
        if (provider is null)
        {
            return new ProviderHealthResult(false, "Provider profile not found.");
        }

        var adapter = providerRegistry.Resolve(provider);
        if (adapter is null)
        {
            return new ProviderHealthResult(false, $"No adapter is registered for provider profile '{provider.Name}'.");
        }

        var secretValue = await ResolveProviderSecretValueAsync(provider, cancellationToken);

        try
        {
            var result = await adapter.CheckHealthAsync(provider, secretValue, cancellationToken);
            provider.LastHealthCheckAtUtc = clock.GetUtcNow();
            provider.LastHealthStatus = result.Message;

            await dbContext.SaveChangesAsync(cancellationToken);
            await activityStream.RecordAsync(
                new ActivityWriteRequest(
                    "providers",
                    "health-check",
                    $"Checked provider health for {provider.Name}",
                    provider.LastHealthStatus,
                    ArtifactKind: "provider-profile",
                    ArtifactId: provider.Id,
                    Route: "/settings"),
                cancellationToken);

            return result;
        }
        catch (Exception exception)
        {
            provider.LastHealthCheckAtUtc = clock.GetUtcNow();
            provider.LastHealthStatus = exception.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ProviderHealthResult(false, exception.Message);
        }
    }

    public async Task<Result<ProviderExecutionResponse>> SendAsync(ProviderExecutionRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await dbContext.Set<ProviderProfile>()
            .FirstOrDefaultAsync(item => item.Id == request.ProviderProfileId && item.IsEnabled, cancellationToken);
        if (profile is null)
        {
            return Result<ProviderExecutionResponse>.Failure(Error.Validation("Provider profile not found or disabled."));
        }

        var adapter = providerRegistry.Resolve(profile);
        if (adapter is null)
        {
            return Result<ProviderExecutionResponse>.Failure(Error.Failure(
                $"No adapter is registered for provider profile '{profile.Name}'."));
        }

        var secretValue = await ResolveProviderSecretValueAsync(profile, cancellationToken);

        var result = await adapter.SendAsync(profile, request, secretValue, cancellationToken);
        if (result.IsSuccess)
        {
            await activityStream.RecordAsync(
                new ActivityWriteRequest(
                    "providers",
                    "send",
                    $"Sent prompt through {profile.Name}",
                    $"Plugin: {adapter.Manifest.PluginKey}. Model: {result.Value!.Model}.",
                    Route: "/settings"),
                cancellationToken);
        }

        return result;
    }

    private async Task<string?> ResolveProviderSecretValueAsync(
        ProviderProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile.ApiKeySecretId is not { } secretId)
        {
            return null;
        }

        return await secretRuntimeResolver.ResolveValueAsync(
            new SecretRuntimeRequest(
                secretId,
                SecretRuntimePurposes.AgentProviderApiKey,
                [secretId],
                ConsumerType: SecretRuntimeConsumerTypes.ProviderProfile,
                ConsumerId: SecretRuntimeConsumerIds.ProviderProfile(profile.Id)),
            cancellationToken);
    }
}
