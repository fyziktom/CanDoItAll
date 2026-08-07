using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

/// <summary>
/// SB10 port-level decorator that intercepts process mock providers on the narrow
/// execution/continuation runtime ports and reuses the deterministic interception bodies of
/// <see cref="ProcessMockAgentRuntime"/>; every other provider delegates to the inner ports.
/// </summary>
internal sealed class ProcessMockExecutionDecorator(
    IAgentExecutionRuntime innerExecution,
    IAgentContinuationRuntime innerContinuation,
    ProcessMockAgentRuntime processMock) :
    IAgentExecutionRuntime,
    IAgentContinuationRuntime
{
    public Task<AgentRuntimeResponse> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProcessMockAgentCatalog.IsProcessMockProvider(request.Provider))
        {
            return innerExecution.ExecuteAsync(request, cancellationToken);
        }

        return processMock.ExecuteProcessMockRunAsync(
            request.Agent,
            request.Prompt,
            request.RuntimeSessionKey,
            request.ProgressCallback,
            request.StructuredOutput,
            request.ExecutionOptions);
    }

    public Task<AgentRuntimeResponse> ContinueAsync(
        AgentRuntimeContinuationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProcessMockAgentCatalog.IsProcessMockProvider(request.Provider))
        {
            return innerContinuation.ContinueAsync(request, cancellationToken);
        }

        throw ProcessMockAgentRuntime.CreateApprovalContinuationUnsupportedError();
    }
}

/// <summary>
/// SB10 port-level decorator that answers provider diagnostics/administration for process mock
/// providers deterministically and delegates every other provider to the inner ports.
/// </summary>
internal sealed class ProcessMockDiagnosticsDecorator(
    IProviderDiagnosticsRuntime innerDiagnostics,
    IProviderModelAdministrationRuntime innerModelAdministration,
    ProcessMockAgentRuntime processMock) :
    IProviderDiagnosticsRuntime,
    IProviderModelAdministrationRuntime
{
    public Task<ProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
        => ProcessMockAgentCatalog.IsProcessMockProvider(provider)
            ? processMock.CreateProcessMockProviderHealthResultAsync()
            : innerDiagnostics.TestHealthAsync(provider, cancellationToken);

    public Task<ProviderTestChatResult> RunProbeAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
        => ProcessMockAgentCatalog.IsProcessMockProvider(provider)
            ? processMock.CreateProcessMockProviderTestChatResultAsync()
            : innerDiagnostics.RunProbeAsync(provider, request, cancellationToken);

    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
        => ProcessMockAgentCatalog.IsProcessMockProvider(provider)
            ? throw ProcessMockAgentRuntime.CreateModelMaintenanceUnsupportedError()
            : innerModelAdministration.CreateOrUpdateModelAsync(provider, request, cancellationToken);
}
