using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public interface IProviderAdministrationService
{
    IReadOnlyList<ConnectorPluginManifest> ListProviderManifests();

    Task<IReadOnlyList<ProviderProfileSummary>> ListProviderProfilesAsync(
        CancellationToken cancellationToken = default);

    Task<ProviderProfileEditorModel> GetProviderAsync(
        Guid? id,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> SaveProviderAsync(
        ProviderProfileEditorModel model,
        CancellationToken cancellationToken = default);

    Task<Result<ProviderModelPricingRefreshResult>> RefreshProviderModelPricesAsync(
        ProviderProfileEditorModel model,
        CancellationToken cancellationToken = default);

    Task DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task DeleteProviderAsync(Guid id, Guid expectedConcurrencyToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider administration owner does not support conditional deletion.");

    Task<ProviderHealthCheckResult> TestProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecretListItem>> ListSecretsAsync(
        CancellationToken cancellationToken = default);
}
