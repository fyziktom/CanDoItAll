using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Native MAF implementation of <see cref="IProviderDiagnosticsRuntime"/> over
/// <see cref="IMafProviderRuntimeGateway"/>, including the current provider test
/// model resolution rules owned by <see cref="ProviderRuntimeDiagnostics"/>.
/// </summary>
internal sealed class MafProviderDiagnosticsAdapter(
    IMafProviderRuntimeGateway providerRuntimeGateway) : IProviderDiagnosticsRuntime
{
    private readonly IMafProviderRuntimeGateway providerRuntimeGateway =
        providerRuntimeGateway ?? throw new ArgumentNullException(nameof(providerRuntimeGateway));

    public Task<ProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
        => ProviderRuntimeDiagnostics.TestProviderAsync(
            providerRuntimeGateway,
            provider,
            cancellationToken);

    public Task<ProviderTestChatResult> RunProbeAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
        => ProviderRuntimeDiagnostics.RunProviderTestChatAsync(
            providerRuntimeGateway,
            provider,
            request,
            cancellationToken);
}
