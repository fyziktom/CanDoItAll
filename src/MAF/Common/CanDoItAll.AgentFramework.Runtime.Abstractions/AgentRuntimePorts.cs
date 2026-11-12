using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

public interface IAgentExecutionRuntime
{
    Task<AgentRuntimeResponse> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentContinuationRuntime
{
    AgentSavedEffectDispatchSupport SavedEffectDispatchSupport => AgentSavedEffectDispatchSupport.Unsupported;

    Task<AgentSavedEffectDispatchResult> DispatchSavedEffectAsync(AgentSavedEffectDispatchRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This runtime does not support dispatch of an original saved effect.");

    Task<AgentRuntimeResponse> ContinueAsync(
        AgentRuntimeContinuationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProviderDiagnosticsRuntime
{
    Task<ProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default);

    Task<ProviderTestChatResult> RunProbeAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProviderModelAdministrationRuntime
{
    Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default);
}
