using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

internal static class ProcessRuntimeEventFactory {
    internal static ProcessRuntimeEventEnvelope Create(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessEventType eventType, string payloadHash) {
        var envelope = new ProcessRuntimeEventEnvelope(RuntimeEventId.New(), state.RootRunId, state.RunId,
            context.CorrelationId, null, context.Actor, ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal, context.OccurredAtUtc, eventType, payloadHash);
        var validation = ProcessRuntimeEventRules.Validate(envelope);
        if (!validation.IsValid) {
            throw new InvalidOperationException(validation.Failures[0].Message);
        }
        return envelope;
    }
}
