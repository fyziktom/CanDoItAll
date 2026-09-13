using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench.ProjectStructure;

internal sealed class ProjectStructureInvocationSnapshotCodec : IAgentChatContextAttachmentCodec {
    private const int LegacyPayloadVersion = 1;
    private const int PayloadVersion = 2;
    public AgentChatContextAttachmentKind Kind => new(ProjectStructureInvocationSnapshotMapper.AttachmentKindValue);

    public AgentToolProtocolEnvelope Capture(AgentChatContextAttachmentEnvelope attachment) {
        if (attachment.Kind != Kind || !attachment.TryGetAttachment<ProjectStructureInvocationSnapshot>(out var value) ||
            value.Nodes.Length > ProjectStructureInvocationSnapshotMapper.MaximumCapturedNodeCount ||
            value.Links.Length > ProjectStructureInvocationSnapshotMapper.MaximumCapturedLinkCount ||
            value.Nodes.Length != value.Coverage.CapturedNodeCount || value.Links.Length != value.Coverage.CapturedLinkCount) {
            throw new InvalidDataException("The Structure context attachment has an unsupported owner payload or coverage.");
        }
        var content = ProjectStructureInvocationSnapshotMapper.ComputeContentFingerprint(value);
        var coverage = ProjectStructureInvocationSnapshotMapper.ComputeCoverageFingerprint(value);
        if (attachment.ContentFingerprint != content || attachment.CoverageFingerprint != coverage ||
            attachment.FreshnessFingerprint != ProjectStructureInvocationSnapshotMapper.ComputeFreshnessFingerprint(
                content, coverage, attachment.DatabaseProfileGeneration)) {
            throw new InvalidDataException("The Structure context attachment does not match its captured fingerprints.");
        }
        return AgentToolProtocolEnvelope.Create(Kind.Value,
            value.ObservedProjectLifetime is null ? LegacyPayloadVersion : PayloadVersion, JsonSerializer.Serialize(Payload.From(value)));
    }

    public IAgentChatContextAttachment Restore(AgentToolProtocolEnvelope payload) {
        if (payload.Format != Kind.Value || payload.Version is not (LegacyPayloadVersion or PayloadVersion)) {
            throw new InvalidDataException("The saved Structure context attachment version is unsupported.");
        }
        var value = JsonSerializer.Deserialize<Payload>(payload.PayloadJson)
            ?? throw new InvalidDataException("The saved Structure context attachment is empty.");
        if ((payload.Version == PayloadVersion) != (value.ObservedProjectLifetime is not null)) {
            throw new InvalidDataException("The saved Structure attachment lifetime does not match its version.");
        }
        return new ProjectStructureInvocationSnapshot(value.ProjectId, value.ProjectName, value.ActiveView,
            value.Nodes, value.Links, value.SelectedNodeIds, value.Coverage.ToCoverage(), value.ObservedProjectLifetime);
    }

    private sealed record Payload(Guid ProjectId, string ProjectName, ProjectStructureAgentChatView ActiveView,
        ProjectStructureInvocationSnapshotNode[] Nodes, ProjectStructureInvocationSnapshotLink[] Links,
        string[] SelectedNodeIds, CoveragePayload Coverage,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentProjectStructureLifetime? ObservedProjectLifetime = null) {
        internal static Payload From(ProjectStructureInvocationSnapshot value) => new(value.ProjectId, value.ProjectName,
            value.ActiveView, value.Nodes.ToArray(), value.Links.ToArray(), value.SelectedNodeIds.ToArray(), new(
                value.Coverage.FieldProfile, value.Coverage.Omissions.ToArray(), value.Coverage.HasCompleteHierarchy,
                value.Coverage.HasCompleteLinks, value.Coverage.HasCompleteSelection, value.Coverage.HasCompletePriorityDerivation,
                value.Coverage.SourceNodeCount, value.Coverage.CapturedNodeCount, value.Coverage.SourceLinkCount, value.Coverage.CapturedLinkCount),
            value.ObservedProjectLifetime);
    }

    private sealed record CoveragePayload(ProjectStructureInvocationSnapshotFieldProfile FieldProfile,
        ProjectStructureInvocationSnapshotOmission[] Omissions, bool HasCompleteHierarchy, bool HasCompleteLinks,
        bool HasCompleteSelection, bool HasCompletePriorityDerivation, int SourceNodeCount, int CapturedNodeCount,
        int SourceLinkCount, int CapturedLinkCount) {
        internal ProjectStructureInvocationSnapshotCoverage ToCoverage() => new(FieldProfile, Omissions,
            HasCompleteHierarchy, HasCompleteLinks, HasCompleteSelection, HasCompletePriorityDerivation,
            SourceNodeCount, CapturedNodeCount, SourceLinkCount, CapturedLinkCount);
    }
}
