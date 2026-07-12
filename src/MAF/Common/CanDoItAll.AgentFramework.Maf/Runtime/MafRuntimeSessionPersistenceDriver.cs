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
        var serializedSessionJson = await TrySerializeRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            cancellationToken);
        if (serializedSessionJson is null)
        {
            await progressCallback(
                ExecutionState.Persisting,
                "Session",
                "Microsoft Agent Framework session serialization did not complete within the bounded timeout. Continuing without serialized session state.");
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

    private static async Task<string?> TrySerializeRuntimeSessionAsync(
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        CancellationToken cancellationToken)
    {
        try
        {
            var serializedSession = await Task.Run(
                async () => await runtimeAgent.SerializeSessionAsync(
                    runtimeSession,
                    cancellationToken: cancellationToken),
                cancellationToken).WaitAsync(
                SessionSerializationTimeout,
                cancellationToken);
            return JsonSerializer.Serialize(serializedSession, SerializerOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }
}
