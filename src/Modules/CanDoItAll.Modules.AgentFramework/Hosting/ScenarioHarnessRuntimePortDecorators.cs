using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

/// <summary>
/// SB10 port-level decorator that intercepts scenario harness providers on the narrow
/// execution/continuation runtime ports and reuses the deterministic interception bodies of
/// <see cref="ScenarioHarnessAgentRuntime"/>; every other provider delegates to the inner ports.
/// </summary>
internal sealed class ScenarioHarnessExecutionDecorator(
    IAgentExecutionRuntime innerExecution,
    IAgentContinuationRuntime innerContinuation,
    ScenarioHarnessAgentRuntime scenarioHarness) :
    IAgentExecutionRuntime,
    IAgentContinuationRuntime
{
    public Task<AgentRuntimeResponse> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ScenarioHarnessAgentRuntime.IsScenarioProvider(request.Provider))
        {
            return innerExecution.ExecuteAsync(request, cancellationToken);
        }

        return scenarioHarness.ExecuteScenarioRunAsync(
            request.Prompt,
            request.ProgressCallback,
            request.SuppressApprovalRequirements,
            request.StructuredOutput,
            request.ExecutionOptions);
    }

    public Task<AgentRuntimeResponse> ContinueAsync(
        AgentRuntimeContinuationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ScenarioHarnessAgentRuntime.IsScenarioProvider(request.Provider))
        {
            return innerContinuation.ContinueAsync(request, cancellationToken);
        }

        return scenarioHarness.ExecuteScenarioApprovalContinuationAsync(
            request.Session,
            request.Decisions.All(decision => decision.Approved),
            request.ProgressCallback,
            request.StructuredOutput,
            request.ExecutionOptions);
    }
}

/// <summary>
/// SB10 port-level decorator that answers provider diagnostics/administration for scenario
/// harness providers deterministically and delegates every other provider to the inner ports.
/// </summary>
internal sealed class ScenarioHarnessDiagnosticsDecorator(
    IProviderDiagnosticsRuntime innerDiagnostics,
    IProviderModelAdministrationRuntime innerModelAdministration) :
    IProviderDiagnosticsRuntime,
    IProviderModelAdministrationRuntime
{
    public Task<ProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
        => ScenarioHarnessAgentRuntime.IsScenarioProvider(provider)
            ? ScenarioHarnessAgentRuntime.CreateScenarioProviderHealthResultAsync()
            : innerDiagnostics.TestHealthAsync(provider, cancellationToken);

    public Task<ProviderTestChatResult> RunProbeAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
        => ScenarioHarnessAgentRuntime.IsScenarioProvider(provider)
            ? ScenarioHarnessAgentRuntime.CreateScenarioProviderTestChatResultAsync()
            : innerDiagnostics.RunProbeAsync(provider, request, cancellationToken);

    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
        => ScenarioHarnessAgentRuntime.IsScenarioProvider(provider)
            ? throw ScenarioHarnessAgentRuntime.CreateScenarioModelMaintenanceUnsupportedError()
            : innerModelAdministration.CreateOrUpdateModelAsync(provider, request, cancellationToken);
}
