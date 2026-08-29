using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistoryAgentRuntime(IAgentExecutionRuntime execution, IAgentContinuationRuntime continuation)
    : IAgentExecutionRuntime, IAgentContinuationRuntime {
    public Task<AgentRuntimeResponse> ExecuteAsync(AgentRuntimeExecutionRequest request, CancellationToken cancellationToken = default) {
        var options = MafRuntimeExecutionOptionsResolver.Normalize(request.StructuredOutput, request.ExecutionOptions);
        return ObserveAsync(options.History,
            () => execution.ExecuteAsync(request with { ExecutionOptions = options }, cancellationToken));
    }

    public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default) {
        var options = MafRuntimeExecutionOptionsResolver.Normalize(request.StructuredOutput, request.ExecutionOptions);
        return ObserveAsync(options.History,
            () => continuation.ContinueAsync(request with { ExecutionOptions = options }, cancellationToken));
    }

    private static async Task<AgentRuntimeResponse> ObserveAsync(HistoryInvocationContext history, Func<Task<AgentRuntimeResponse>> invoke) {
        try {
            var response = await invoke();
            var evidence = HistoryCanonicalInvocation.Capture(history);
            return response with {
                HistoryEvidence = evidence,
                UsageObservations = ProviderUsageHistory.Attach(response.UsageObservations, evidence)
            };
        } catch (AgentRuntimeUsageException exception) {
            var evidence = HistoryCanonicalInvocation.Capture(history);
            if (evidence is null) {
                throw;
            }
            throw new AgentRuntimeUsageException(exception.Message, exception, ProviderUsageHistory.Attach(exception.UsageObservations, evidence),
                exception.ToolInvocationTraces, exception.EntryAgentRequestCompatibilityEvidence,
                exception.FailureOrigin, exception.ProviderFailureIdentity) { HistoryEvidence = evidence };
        } catch (OperationCanceledException exception) {
            var evidence = HistoryCanonicalInvocation.Capture(history);
            if (evidence is null) {
                throw;
            }
            throw new AgentHistoryCancellationException(exception, evidence);
        } catch (Exception exception) {
            var evidence = HistoryCanonicalInvocation.Capture(history);
            if (evidence is null) {
                throw;
            }
            throw new AgentRuntimeUsageException(exception.Message, exception, []) { HistoryEvidence = evidence };
        }
    }
}
