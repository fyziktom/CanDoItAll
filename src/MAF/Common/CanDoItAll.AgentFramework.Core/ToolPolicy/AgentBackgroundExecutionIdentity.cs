using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentBackgroundExecutionIdentity {
    internal static void Require(AgentToolSessionAdmission session, ExecutionRunRecord run,
        AgentToolBackgroundSourceBinding source, AgentToolProfileBinding profile) {
        if (session.Profile != profile || session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                session.Reference.ChatSessionId != Guid.Empty || session.Reference.AuthorityId.Value != Guid.Empty ||
                run.Id != session.Reference.ExecutionRunId || run.AgentId != session.AgentId || run.ChatSessionId is not null ||
                run.SourceKind != source.SourceKind || run.SourceId != source.SourceId ||
                Fingerprint(run) != source.ExecutionFingerprint) {
            throw new AgentToolAdmissionException("tool-admission.denied",
                "The saved background execution, source, profile or immutable input binding changed.");
        }
    }

    internal static AgentToolSemanticDigest Fingerprint(ExecutionRunRecord run)
        => AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            run.Id, run.AgentId, run.SourceKind, run.SourceId, run.CorrelationId, run.CausationId, run.RequestedBy,
            run.RequestedByKind, run.ProcessRunId, run.ProcessStepId, run.SchedulerRunId, run.MetadataJson, run.CreatedAtUtc
        }));
}
