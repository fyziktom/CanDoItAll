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
