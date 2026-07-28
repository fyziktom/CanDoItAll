using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IMafRuntimeSessionPersistenceDriver
{
    Task<string?> TrySerializePersistableRuntimeSessionAsync(
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyCollection<PendingToolApprovalRecord> pendingApprovals,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken);

    bool ShouldSkipRuntimeSessionSerialization(
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyCollection<PendingToolApprovalRecord> pendingApprovals);

    string ResolveRuntimeSessionSerializationSkipMessage(AgentRuntimeExecutionOptions runtimeOptions);
}

internal sealed class MafRuntimeSessionPersistenceDriver : IMafRuntimeSessionPersistenceDriver
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan SessionSerializationTimeout = TimeSpan.FromSeconds(5);

    public bool ShouldSkipRuntimeSessionSerialization(
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyCollection<PendingToolApprovalRecord> pendingApprovals)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(pendingApprovals);

        return pendingApprovals.Count == 0 &&
               (runtimeOptions.ContextIntent?.IsGovernedProcessStep == true ||
                InputAttachmentSupport.HasRequestScopedInputAttachments(runtimeOptions));
    }

    public string ResolveRuntimeSessionSerializationSkipMessage(AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        return InputAttachmentSupport.HasRequestScopedInputAttachments(runtimeOptions)
            ? "Skipped Microsoft Agent Framework session serialization because request-scoped input attachments are not persisted into session state. The sandbox transcript keeps the text turn for future replay."
            : "Skipped Microsoft Agent Framework session serialization for a governed process step with no pending approvals. Process state is persisted through the typed outcome and artifacts.";
    }

    public async Task<string?> TrySerializePersistableRuntimeSessionAsync(
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyCollection<PendingToolApprovalRecord> pendingApprovals,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtimeAgent);
        ArgumentNullException.ThrowIfNull(runtimeSession);
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(pendingApprovals);
        ArgumentNullException.ThrowIfNull(progressCallback);

        if (ShouldSkipRuntimeSessionSerialization(runtimeOptions, pendingApprovals))
        {
            await progressCallback(
                ExecutionState.Persisting,
                "Session",
                ResolveRuntimeSessionSerializationSkipMessage(runtimeOptions));
            return null;
        }

        await progressCallback(ExecutionState.Persisting, "Session", "Serializing the Microsoft Agent Framework session.");
        string serializedSessionJson;
        try
        {
            serializedSessionJson = await SerializeRuntimeSessionAsync(
                runtimeAgent,
                runtimeSession,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failure = exception is TimeoutException
                ? "did not complete within the bounded timeout"
                : $"failed with {exception.GetType().Name}";
            await progressCallback(
                ExecutionState.Persisting,
                "Session",
                $"Microsoft Agent Framework session serialization {failure}.");
            ThrowIfApprovalStateUnavailable(pendingApprovals, exception);
            return null;
        }

        if (!InputAttachmentSupport.HasRequestScopedInputAttachments(runtimeOptions))
        {
            return serializedSessionJson;
        }

        var scrubbedSessionJson = RequestScopedSessionContentScrubber.RemoveRequestScopedDataContent(serializedSessionJson);
        if (scrubbedSessionJson is null)
        {
            await progressCallback(
                ExecutionState.Persisting,
                "Session",
                "Dropped serialized Microsoft Agent Framework session state because request-scoped attachment payload scrubbing failed.");
            ThrowIfApprovalStateUnavailable(
                pendingApprovals,
                new InvalidOperationException("Request-scoped session content scrubbing failed."));
            return null;
        }

        if (!string.Equals(serializedSessionJson, scrubbedSessionJson, StringComparison.Ordinal))
        {
            await progressCallback(
                ExecutionState.Persisting,
                "Session",
                "Removed request-scoped attachment payloads from serialized Microsoft Agent Framework session state.");
        }

        return scrubbedSessionJson;
    }

    private static async Task<string> SerializeRuntimeSessionAsync(
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        CancellationToken cancellationToken)
    {
        using var serializationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serializationTask = runtimeAgent.SerializeSessionAsync(
            runtimeSession,
            cancellationToken: serializationCancellation.Token).AsTask();
        try
        {
            var serializedSession = await serializationTask.WaitAsync(
                SessionSerializationTimeout,
                cancellationToken);
            return JsonSerializer.Serialize(serializedSession, SerializerOptions);
        }
        catch (TimeoutException)
        {
            await serializationCancellation.CancelAsync();
            throw;
        }
    }

    private static void ThrowIfApprovalStateUnavailable(
        IReadOnlyCollection<PendingToolApprovalRecord> pendingApprovals,
        Exception serializationException)
    {
        if (pendingApprovals.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Cannot return actionable tool approvals because the Microsoft Agent Framework session and its approval-binding state could not be persisted.",
            serializationException);
    }
}
