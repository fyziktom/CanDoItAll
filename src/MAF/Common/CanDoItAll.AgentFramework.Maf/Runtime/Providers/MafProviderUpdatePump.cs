using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.AgentFramework.Maf;

internal sealed record MafProviderUpdatePumpContext(
    ProviderProfile Provider,
    string Model,
    AgentSession SourceSession,
    string? RuntimeSessionKey,
    IReadOnlyList<AgentResponseUpdate> UsageUpdates,
    string UsageSourcePhase,
    Func<IReadOnlyList<AgentToolInvocationTrace>> SnapshotToolInvocationTraces,
    ProviderRequestCompatibilityEvidence? EntryAgentRequestCompatibilityEvidence);

internal sealed class MafProviderUpdatePump
{
    public async Task<TResult?> PumpAsync<TResult>(
        IAsyncEnumerable<AgentResponseUpdate> updates,
        MafProviderUpdatePumpContext context,
        Func<AgentResponseUpdate, Task<TResult?>> handleUpdate,
        Func<Task<TResult?>> handleCompletion,
        CancellationToken cancellationToken)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(handleUpdate);
        ArgumentNullException.ThrowIfNull(handleCompletion);

        var enumerator = updates.GetAsyncEnumerator(cancellationToken);
        Exception? primaryFailure = null;
        try
        {
            while (await MoveNextAsync(enumerator, context, cancellationToken))
            {
                var result = await handleUpdate(enumerator.Current);
                if (result is not null)
                {
                    return result;
                }
            }

            return await handleCompletion();
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            await DisposeAsync(enumerator, primaryFailure);
        }
    }

    internal static string BuildFailureDiagnostic(
        Exception exception,
        AgentRuntimeFailureOrigin failureOrigin)
        => failureOrigin switch
        {
            AgentRuntimeFailureOrigin.Provider =>
                MafRuntimeResponseAssembler.BuildProviderFailureDiagnostic(exception),
            AgentRuntimeFailureOrigin.ProviderConfiguration =>
                $"Provider configuration validation failed. FailureType={exception.GetType().Name}.",
            AgentRuntimeFailureOrigin.Tool =>
                $"A tool invocation failed while advancing the agent stream. FailureType={exception.GetType().Name}.",
            AgentRuntimeFailureOrigin.Finalizer =>
                $"The governed finalizer failed. FailureType={exception.GetType().Name}.",
            _ =>
                $"The agent runtime failed outside provider transport. FailureType={exception.GetType().Name}."
        };

    private static async ValueTask<bool> MoveNextAsync(
        IAsyncEnumerator<AgentResponseUpdate> enumerator,
        MafProviderUpdatePumpContext context,
        CancellationToken cancellationToken)
    {
        var failedToolCountBeforeAdvance =
            MafRuntimeFailureOriginClassifier.CountCompletedFailedTools(
                context.SnapshotToolInvocationTraces());
        try
        {
            return await enumerator.MoveNextAsync();
        }
        catch (RequiredFinalizerCapturedException)
        {
            throw;
        }
        catch (AgentRuntimeUsageException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var toolInvocationTraces = context.SnapshotToolInvocationTraces();
            var failureOrigin = MafRuntimeFailureOriginClassifier.ResolveProviderAdvanceFailure(
                failedToolCountBeforeAdvance,
                toolInvocationTraces,
                exception);
            throw new AgentRuntimeUsageException(
                "Provider response streaming failed. Usage was captured when available.",
                exception,
                MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                    context.Provider,
                    context.Model,
                    context.SourceSession,
                    context.RuntimeSessionKey,
                    context.UsageUpdates,
                    context.UsageSourcePhase,
                    BuildFailureDiagnostic(exception, failureOrigin)),
                toolInvocationTraces,
                context.EntryAgentRequestCompatibilityEvidence,
                failureOrigin,
                MafRuntimeFailureOriginClassifier.ResolveProviderFailureIdentity(
                    exception,
                    failureOrigin));
        }
    }

    private static async ValueTask DisposeAsync(
        IAsyncEnumerator<AgentResponseUpdate> enumerator,
        Exception? primaryFailure)
    {
        try
        {
            await enumerator.DisposeAsync();
        }
        catch (Exception disposalFailure) when (primaryFailure is not null)
        {
            primaryFailure.Data[MafProviderTransportException.DisposalFailureTypeDataKey] =
                MafProviderTransportException.ResolveDiagnosticFailureType(disposalFailure);
        }
    }
}
