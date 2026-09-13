using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench.AgentContext;

internal sealed class ProjectStructureGanttObservationCodec : IAgentChatContextAttachmentCodec {
    private const int LegacyPayloadVersion = 1;
    private const int PayloadVersion = 2;
    public AgentChatContextAttachmentKind Kind => new(ProjectStructureGanttObservationContributor.AttachmentKind);

    public AgentToolProtocolEnvelope Capture(AgentChatContextAttachmentEnvelope attachment) {
        if (attachment.Kind != Kind || !attachment.TryGetAttachment<ProjectStructureGanttObservationAttachment>(out var value) ||
            !Enum.IsDefined(value.Observation.Completeness)) {
            throw new InvalidDataException("The Gantt context attachment has an unsupported owner payload.");
        }
        var expected = ProjectStructureGanttObservationContributor.BuildPublication(value.Observation,
            attachment.DatabaseProfileGeneration, attachment.CapturedAtUtc,
            attachment.FreshUntilUtc ?? throw new InvalidDataException("The captured Gantt context has no freshness deadline."))
            .AttachmentDrafts.Single();
        if (expected.ContentFingerprint != attachment.ContentFingerprint || expected.CoverageFingerprint != attachment.CoverageFingerprint ||
            expected.FreshnessFingerprint != attachment.FreshnessFingerprint) {
            throw new InvalidDataException("The Gantt context attachment does not match its captured fingerprints.");
        }
        return AgentToolProtocolEnvelope.Create(Kind.Value,
            value.Observation.ObservedProjectLifetime is null ? LegacyPayloadVersion : PayloadVersion, JsonSerializer.Serialize(value.Observation));
    }

    public IAgentChatContextAttachment Restore(AgentToolProtocolEnvelope payload) {
        if (payload.Format != Kind.Value || payload.Version is not (LegacyPayloadVersion or PayloadVersion)) {
            throw new InvalidDataException("The saved Gantt context attachment version is unsupported.");
        }
        var observation = JsonSerializer.Deserialize<ProjectStructureGanttObservation>(payload.PayloadJson)
            ?? throw new InvalidDataException("The saved Gantt context attachment is empty.");
        if ((payload.Version == PayloadVersion) != (observation.ObservedProjectLifetime is not null)) {
            throw new InvalidDataException("The saved Gantt attachment lifetime does not match its version.");
        }
        return new ProjectStructureGanttObservationAttachment(observation);
    }
}
