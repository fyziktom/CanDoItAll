using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

using RuntimeProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using RuntimeProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

public interface IProviderRuntimeAdministrationService
{
    Task<IReadOnlyList<RuntimeProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default);

    Task<RuntimeProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default);

    Task<Guid> SaveProviderAsync(
        RuntimeProviderProfileEditorModel model,
        CancellationToken cancellationToken = default);

    Task DeleteProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<ProviderHealthResult> TestProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<ProviderTestChatResult> RunProviderTestChatAsync(
        Guid providerId,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        Guid providerId,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default);
}
