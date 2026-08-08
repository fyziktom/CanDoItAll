using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Native MAF implementation of <see cref="IProviderModelAdministrationRuntime"/> over
/// <see cref="IMafProviderRuntimeGateway"/>.
/// </summary>
internal sealed class MafProviderModelAdministrationAdapter(
    IMafProviderRuntimeGateway providerRuntimeGateway) : IProviderModelAdministrationRuntime
{
    private readonly IMafProviderRuntimeGateway providerRuntimeGateway =
        providerRuntimeGateway ?? throw new ArgumentNullException(nameof(providerRuntimeGateway));

    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
        => ProviderRuntimeDiagnostics.CreateOrUpdateProviderModelAsync(
            providerRuntimeGateway,
            provider,
            request,
            cancellationToken);
}
