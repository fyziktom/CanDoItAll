using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentToolAdmissionJournal {
    private enum BackgroundCheck {
        Dispatch,
        Read,
        RecordOutcome
    }

    private readonly IReadOnlyDictionary<string, IAgentToolBackgroundSourcePolicy> backgroundSources;

    public bool SupportsBackgroundSource(string sourceKind) => backgroundSources.ContainsKey(sourceKind);

    public async Task<AgentToolJournalRecord> CreateForNewBackgroundRunAsync(ExecutionRunRecord run, string input,
        AgentToolAdmissionSupport support = AgentToolAdmissionSupport.Recoverable,
        AgentRuntimeTransientContext? runtimeContext = null, CancellationToken cancellationToken = default) {
        if (run.ToolAdmission is not null || run.ChatSessionId is not null || run.PendingApprovals.Count != 0 ||
                run.Revision != 1 || run.State != ExecutionState.Preparing || run.Outcome is not null ||
                await reader.GetExecutionRunAsync(run.Id, cancellationToken) is not null) {
            throw new AgentToolAdmissionException("tool-admission.legacy-run",
                "Only a new owner-created background execution can receive an initial tool journal.");
        }
        if (AgentTurnContextMetadata.ContainsTurnContextReference(run.MetadataJson) ||
                AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson) is not null) {
            throw Failure("A background source cannot substitute an interactive turn or authority projection.");
        }
        if (runtimeContext is not null && (!runtimeContext.Attachments.IsEmpty ||
                AgentChatContextDigest.Compute(runtimeContext) != ExecutionInvocationMetadata.ResolveTransientContextDigest(run))) {
            throw Failure("The background runtime context has no matching retained input digest.");
        }
        var observation = await RequireBackgroundPolicy(run.SourceKind).ObserveAsync(run, profile, cancellationToken);
        if (!observation.ReadAllowed || !observation.DispatchAllowed) {
            throw Failure("The source owner denied the new background execution.");
        }
        var binding = new AgentToolBackgroundSourceBinding(run.SourceKind, run.SourceId, observation.OwnerFingerprint,
            BackgroundExecutionFingerprint(run));
        var session = new AgentToolSessionAdmission(new(run.Id, Guid.Empty, default, binding), run.AgentId,
            AgentRuntimeContextPurpose.GovernedProcessAutomation, profile);
        var journal = new AgentToolJournalRecord(AgentToolJournalRecord.BackgroundSchemaVersion, 1, session, [], [], Support: support,
            RuntimeContext: runtimeContext is null ? null : new(runtimeContext.Content, runtimeContext.WorkspaceScope),
            BackgroundInput: new(input));
        journal.Validate();
        return journal;
    }

    public async Task<bool> CanReadBackgroundResultAsync(ExecutionRunRecord run, CancellationToken cancellationToken = default) {
        var journal = run.ToolAdmission ?? throw Failure("The execution has no saved tool journal.");
        journal.Validate();
        var background = journal.Session.Reference.BackgroundSource ?? throw Failure("The execution is not admitted from a background source.");
        RequireBackgroundIdentity(journal.Session, run, background);
        var current = await RequireBackgroundPolicy(background.SourceKind).ObserveAsync(run, profile, cancellationToken);
        return current.OwnerFingerprint == background.OwnerFingerprint && current.ReadAllowed;
    }

    public async Task<AgentToolBackgroundReservation> PrepareBackgroundReservationAsync(ExecutionRunRecord candidate,
        CancellationToken cancellationToken = default) {
        var journal = candidate.ToolAdmission ?? throw Failure("A background reservation requires a saved owner admission.");
        journal.Validate();
        var source = journal.Session.Reference.BackgroundSource ?? throw Failure("An interactive journal cannot reserve a background source.");
        RequireBackgroundIdentity(journal.Session, candidate, source);
        if (RequireBackgroundPolicy(source.SourceKind) is not IAgentToolBackgroundReservationPolicy policy) {
            throw new AgentToolAdmissionException("tool-admission.reservation-policy-missing",
                "The background owner has no admitted execution reservation policy.");
        }
        var observation = await policy.ObserveReservationAsync(candidate, profile, cancellationToken);
        if (observation.OwnerFingerprint != source.OwnerFingerprint || observation.AcknowledgedExecutionRunIds.Contains(Guid.Empty)) {
            throw Failure("The background reservation does not match its current owner claim or exact result acknowledgements.");
        }
        await RequireBackgroundDispatchAsync(candidate, cancellationToken);
        return new(candidate.Id, profile, source.OwnerFingerprint, observation.AcknowledgedExecutionRunIds.ToHashSet());
    }

    public Task RequireBackgroundDispatchAsync(ExecutionRunRecord run, CancellationToken cancellationToken = default)
        => RequireBackgroundSourceAsync(run, readOnly: false, cancellationToken);

    private async Task RequireBackgroundSourceAsync(ExecutionRunRecord run, bool readOnly, CancellationToken cancellationToken) {
        if (run.ToolAdmission?.Session.Reference.BackgroundSource is not { } source) {
            return;
        }
        RequireBackgroundIdentity(run.ToolAdmission.Session, run, source);
        var current = await RequireBackgroundPolicy(source.SourceKind).ObserveAsync(run, profile, cancellationToken);
        if (current.OwnerFingerprint != source.OwnerFingerprint || !current.ReadAllowed || !readOnly && !current.DispatchAllowed) {
            throw Failure("The original background source binding or current owner authorization no longer permits this operation.");
        }
    }

    private void RequireBackgroundIdentity(AgentToolSessionAdmission session, ExecutionRunRecord run, AgentToolBackgroundSourceBinding source) {
        if (session.Profile != profile || session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                session.Reference.ChatSessionId != Guid.Empty || session.Reference.AuthorityId.Value != Guid.Empty ||
                run.Id != session.Reference.ExecutionRunId || run.AgentId != session.AgentId || run.ChatSessionId is not null ||
                run.SourceKind != source.SourceKind || run.SourceId != source.SourceId ||
                BackgroundExecutionFingerprint(run) != source.ExecutionFingerprint) {
            throw Failure("The saved background execution, source, profile or immutable input binding changed.");
        }
    }

    private IAgentToolBackgroundSourcePolicy RequireBackgroundPolicy(string sourceKind)
        => backgroundSources.TryGetValue(sourceKind, out var source) ? source :
            throw new AgentToolAdmissionException("tool-admission.source-policy-missing", "The saved background source has no installed owner policy.");

    private static AgentToolSemanticDigest BackgroundExecutionFingerprint(ExecutionRunRecord run)
        => AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            run.Id, run.AgentId, run.SourceKind, run.SourceId, run.CorrelationId, run.CausationId, run.RequestedBy,
            run.RequestedByKind, run.ProcessRunId, run.ProcessStepId, run.SchedulerRunId, run.MetadataJson, run.CreatedAtUtc
        }));
}
