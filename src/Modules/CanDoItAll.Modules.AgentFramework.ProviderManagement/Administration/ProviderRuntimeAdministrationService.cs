using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

using RuntimeProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using RuntimeProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

internal sealed class ProviderRuntimeAdministrationService(
    IProviderProfileService providerProfileService,
    IProviderDiagnosticsService providerDiagnosticsService,
    IProviderProfileRegistry providerRegistry,
    IProviderRuntimeProfileSource providerSource) :
    IProviderRuntimeAdministrationService
{
    public Task<IReadOnlyList<RuntimeProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default)
        => providerRegistry.ListProvidersAsync(cancellationToken);

    public Task<RuntimeProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default)
        => providerRegistry.GetProviderEditorAsync(providerId, cancellationToken);

    public Task<Guid> SaveProviderAsync(
        RuntimeProviderProfileEditorModel model,
        CancellationToken cancellationToken = default)
        => providerRegistry.SaveProviderAsync(model, cancellationToken);

    public Task DeleteProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
        => providerRegistry.DeleteProviderAsync(providerId, cancellationToken);

    public async Task<ProviderHealthResult> TestProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetRequiredRuntimeProviderAsync(
            providerId,
            cancellationToken);
        if (IsSourceManagedProvider(provider) && !provider.IsEnabled)
        {
            return new ProviderHealthResult(
                false,
                $"The source-managed provider is unavailable ({provider.HealthStatus}).",
                provider.SuggestedModels);
        }

        ProviderHealthResult result;
        try
        {
            result = await providerDiagnosticsService.TestProviderAsync(
                provider,
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException &&
                IsSourceManagedProvider(provider))
        {
            result = new ProviderHealthResult(
                false,
                ProviderFailureDisclosurePolicy.SelectMessage(
                    provider,
                    ProviderFailureOperation.HealthCheck,
                    exception.Message),
                provider.SuggestedModels);
        }

        if (IsSourceManagedProvider(provider))
        {
            return ProviderFailureDisclosurePolicy.SanitizeHealthResult(
                provider,
                result);
        }

        var checkedAtUtc = DateTimeOffset.UtcNow;
        await providerRegistry.UpdateProviderAsync(
            providerId,
            currentProvider => providerProfileService.ApplyHealthResult(
                currentProvider,
                result,
                checkedAtUtc),
            cancellationToken);
        return result;
    }

    public async Task<ProviderTestChatResult> RunProviderTestChatAsync(
        Guid providerId,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetRequiredRuntimeProviderAsync(
            providerId,
            cancellationToken);
        EnsureProviderAvailable(provider);

        try
        {
            return await providerDiagnosticsService.RunProviderTestChatAsync(
                provider,
                request,
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException &&
                IsSourceManagedProvider(provider))
        {
            throw ProviderFailureDisclosurePolicy.CreateBoundaryException(
                provider,
                ProviderFailureOperation.RuntimeRequest);
        }
    }

    public async Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        Guid providerId,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetRequiredRuntimeProviderAsync(
            providerId,
            cancellationToken);
        EnsureProviderAvailable(provider);

        var result = await providerDiagnosticsService.CreateOrUpdateProviderModelAsync(
            provider,
            request,
            cancellationToken);
        var checkedAtUtc = DateTimeOffset.UtcNow;
        await providerRegistry.UpdateProviderAsync(
            providerId,
            currentProvider => providerProfileService.ApplyProviderModelMaintenanceResult(
                currentProvider,
                result,
                checkedAtUtc),
            cancellationToken);
        return result;
    }

    private async Task<RuntimeProviderProfile> GetRequiredRuntimeProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken)
        => await providerSource.GetProviderAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");

    private static bool IsSourceManagedProvider(RuntimeProviderProfile provider)
        => ProviderFailureDisclosurePolicy.RequiresSanitization(provider);

    private static void EnsureProviderAvailable(RuntimeProviderProfile provider)
    {
        if (!provider.IsEnabled)
        {
            throw new ProviderRuntimeProfileUnavailableException(provider.Id);
        }
    }
}
