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
    IProviderRuntimeProfileSource providerSource,
    ProviderMutationWitness witnesses) :
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

    public Task<RuntimeProviderProfileEditorModel> SaveProviderEditorAsync(RuntimeProviderProfileEditorModel model, CancellationToken cancellationToken = default)
        => providerRegistry.SaveProviderEditorAsync(model, cancellationToken);

    public Task DeleteProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
        => providerRegistry.DeleteProviderAsync(providerId, cancellationToken);

    public Task DeleteProviderAsync(Guid providerId, Guid expectedConcurrencyToken, CancellationToken cancellationToken = default)
        => providerRegistry.DeleteProviderAsync(providerId, expectedConcurrencyToken, cancellationToken);

    public async Task<ProviderHealthResult> TestProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await providerRegistry.GetProviderObservationAsync(providerId, cancellationToken);
        var provider = snapshot.Profile;
        if (IsSourceManagedProvider(provider) && !provider.IsEnabled)
        {
            return new ProviderHealthResult(
                false,
                $"The source-managed provider is unavailable ({provider.HealthStatus}).",
                provider.SuggestedModels);
        }

        ProviderHealthResult result;
        if (!IsSourceManagedProvider(provider)) {
            RequireSavedConfiguration(snapshot);
        }
        try
        {
            result = await providerDiagnosticsService.TestProviderAsync(
                provider,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception exception) {
            if (!IsSourceManagedProvider(provider)) {
                throw new ProviderHealthDiagnosticException(exception);
            }
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
        await providerRegistry.RecordProviderHealthAsync(snapshot, result, checkedAtUtc, cancellationToken);
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
                ProviderFailureOperation.RuntimeRequest,
                exception);
        }
    }

    public async Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        Guid providerId,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await providerRegistry.GetProviderObservationAsync(providerId, cancellationToken);
        var provider = snapshot.Profile;
        if (IsSourceManagedProvider(provider)) {
            throw new ProviderProfileValidationException(
                "Source-managed model maintenance is not supported. Manage remote models at their source.");
        }

        EnsureProviderAvailable(provider);
        RequireSavedConfiguration(snapshot);

        var result = await providerDiagnosticsService.CreateOrUpdateProviderModelAsync(
            provider,
            request,
            cancellationToken);
        var checkedAtUtc = DateTimeOffset.UtcNow;
        try {
            var updated = providerProfileService.ApplyProviderModelMaintenanceResult(provider, result, checkedAtUtc);
            var editor = providerProfileService.CreateEditor(updated);
            editor.ExpectedConcurrencyToken = snapshot.ExpectedConcurrencyToken;
            await providerRegistry.SaveProviderEditorAsync(editor, cancellationToken);
        } catch (ProviderMutationCommittedException exception) {
            throw new ProviderModelMaintenancePersistenceException(providerId, result,
                ProviderModelMaintenancePersistenceState.CommittedWithWarning, exception);
        } catch (ProviderMutationUnconfirmedException exception) {
            throw new ProviderModelMaintenancePersistenceException(providerId, result, ProviderModelMaintenancePersistenceState.Unconfirmed,
                new ProviderMutationUnconfirmedException(witnesses.Sign(exception.Attempt with { Kind = ProviderMutationKind.ModelMaintenancePersistence }), exception));
        } catch (ProviderConfigurationConflictException exception) {
            throw new ProviderModelMaintenancePersistenceException(providerId, result,
                ProviderModelMaintenancePersistenceState.Conflict, exception);
        } catch (Exception exception) when (exception is ProviderProfileValidationException or UnauthorizedAccessException) {
            throw new ProviderModelMaintenancePersistenceException(providerId, result,
                ProviderModelMaintenancePersistenceState.Rejected, exception);
        } catch (Exception exception) {
            throw new ProviderModelMaintenancePersistenceException(providerId, result,
                ProviderModelMaintenancePersistenceState.Unconfirmed, exception);
        }
        return result;
    }

    private static void RequireSavedConfiguration(ProviderProfileObservation observation) {
        if (observation.ExpectedConcurrencyToken == Guid.Empty) {
            throw new ProviderProfileValidationException("Save this provider configuration before running a persistent diagnostic or model-maintenance command.");
        }
    }

    private async Task<RuntimeProviderProfile> GetRequiredRuntimeProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken)
        => await providerSource.GetProviderAsync(providerId, cancellationToken)
            ?? throw new KeyNotFoundException("Provider profile was not found.");

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
