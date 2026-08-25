using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.Tests.Support;

using RuntimeProviderHealthResult = CanDoItAll.AgentFramework.Models.ProviderHealthResult;

/// <summary>
/// Test-only stand-in for the whole-runtime double shape the deleted production
/// <c>IAgentRuntime</c> interface used to expose (SB18 deleted that interface; production
/// composition sites construct/consume the four narrow ports directly). Many tests still model a
/// single deterministic fake across execution/continuation/diagnostics/administration because that
/// is the simplest shape for a fixture; this interface preserves exactly that five-method shape so
/// every such fixture keeps its existing method bodies unchanged and only its declared base type
/// changes.
/// </summary>
public interface IFakeAgentRuntime
{
    Task<RuntimeProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default);

    Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentRuntimeResponse> RunAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        string prompt,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null);

    Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        bool approved,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null);
}

/// <summary>
/// Test-only bridge from one <see cref="IFakeAgentRuntime"/> onto the four real runtime ports
/// (<see cref="IAgentExecutionRuntime"/>, <see cref="IAgentContinuationRuntime"/>,
/// <see cref="IProviderDiagnosticsRuntime"/>, <see cref="IProviderModelAdministrationRuntime"/>),
/// mirroring the mapping the deleted production
/// <c>LegacyAgentRuntimeCompatibilityFacade</c> used to perform (including its single-boolean
/// approval collapse — no fixture built against the old five-method shape ever needed mixed
/// per-proposal decisions). This class exists only under <c>tests/</c>; production composition
/// never constructs it.
/// </summary>
public sealed class FakeAgentRuntimePortAdapter(IFakeAgentRuntime runtime) :
    IAgentExecutionRuntime,
    IAgentContinuationRuntime,
    IProviderDiagnosticsRuntime,
    IProviderModelAdministrationRuntime
{
    public Task<AgentRuntimeResponse> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return runtime.RunAsync(
            request.Agent,
            request.Provider,
            request.Session,
            request.Capabilities,
            request.Memory,
            request.Prompt,
            request.RuntimeSessionKey,
            request.ProgressCallback,
            cancellationToken,
            request.SuppressApprovalRequirements,
            request.StructuredOutput,
            request.ExecutionOptions);
    }

    public Task<AgentRuntimeResponse> ContinueAsync(
        AgentRuntimeContinuationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var approved = request.Decisions[0].Approved;
        if (request.Decisions.Any(decision => decision.Approved != approved))
        {
            throw new InvalidOperationException(
                "Mixed per-proposal approval decisions are not supported by this test fixture's single-boolean continuation shape.");
        }

        return runtime.RespondToPendingApprovalsAsync(
            request.Agent,
            request.Provider,
            request.Session,
            request.Capabilities,
            request.Memory,
            approved,
            request.RuntimeSessionKey,
            request.ProgressCallback,
            cancellationToken,
            request.SuppressApprovalRequirements,
            request.StructuredOutput,
            request.ExecutionOptions);
    }

    public Task<RuntimeProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
        => runtime.TestProviderAsync(provider, cancellationToken);

    public Task<ProviderTestChatResult> RunProbeAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
        => runtime.RunProviderTestChatAsync(provider, request, cancellationToken);

    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
        => runtime.CreateOrUpdateProviderModelAsync(provider, request, cancellationToken);
}
