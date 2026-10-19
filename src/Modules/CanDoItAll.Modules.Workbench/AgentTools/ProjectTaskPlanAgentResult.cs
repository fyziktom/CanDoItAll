using System.Collections.Immutable;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectTaskPlanAgentResultKind { Published = 1, Refused = 2, Unconfirmed = 3 }

internal sealed record ProjectTaskPlanAgentResult(ProjectTaskPlanAgentResultKind Kind, Guid OperationId,
    ProjectTaskPlanRefusal? Refusal, ImmutableArray<Guid> TaskIds, bool Replayed, bool CleanupIncomplete)
    : IAgentToolInvocationResultEvidence, IAgentToolOwnerObservationEvidence, IAgentToolOwnerResultEvidence {
    [JsonIgnore]
    public AgentToolInvocationOutcome Outcome => Kind == ProjectTaskPlanAgentResultKind.Published
        ? AgentToolInvocationOutcome.Succeeded : Kind == ProjectTaskPlanAgentResultKind.Refused
            ? AgentToolInvocationOutcome.Failed : AgentToolInvocationOutcome.Unknown;
    [JsonIgnore]
    public AgentToolEffectState EffectState => Kind == ProjectTaskPlanAgentResultKind.Published
        ? AgentToolEffectState.Committed : Kind == ProjectTaskPlanAgentResultKind.Refused
            ? AgentToolEffectState.NotCommitted : AgentToolEffectState.Unknown;
    [JsonIgnore]
    public string FailureCode => Refusal?.ToString() ?? string.Empty;
    [JsonIgnore]
    public string SafeMessage => Kind == ProjectTaskPlanAgentResultKind.Published ? "The original task plan was published."
        : Kind == ProjectTaskPlanAgentResultKind.Refused ? "The original task plan was refused without an effect."
        : "The original task plan needs receipt reconciliation before any further work.";
    [JsonIgnore]
    public bool CanRetryWithCorrectedInput => false;
    [JsonIgnore]
    public bool RequiresOwnerReconciliation => Kind == ProjectTaskPlanAgentResultKind.Unconfirmed;
    [JsonIgnore]
    public AgentToolProtocolEnvelope OwnerResult => ProjectTaskPlanAgentCodec.Write(ProjectTaskPlanAgentCodec.ResultFormat, this, 65_536);

    internal static AgentToolReceiptObservation Receipt(ProjectTaskPlanAgentCommand original, ProjectTaskPlanAccepted accepted) {
        if (accepted.Operation != original.Command.Operation || accepted.Source != original.Command.Source ||
            accepted.SemanticDigest != ProjectTaskPlanOperationCodec.Digest(original.Command)) {
            throw new InvalidDataException("The owner receipt differs from the original planning publication.");
        }
        return new(new(ProjectTaskPlanAgentCodec.EffectSourceKind, accepted.Operation.Id.ToString("D")),
            From(new ProjectTaskPlanOutcome.Accepted(accepted, true)).OwnerResult);
    }

    internal static AgentPlanningPublicationAssessment Classify(AgentToolSessionObservation original,
        AgentToolPreparedPayload payload, AgentSavedEffectDispatchResult response) {
        var planning = original.Planning ?? throw ProjectTaskPlanAgentCodec.Denied("The original planning observation is missing.");
        var command = ProjectTaskPlanAgentCodec.Resolve(planning, original.Session, payload, response.Binding);
        var result = ProjectTaskPlanAgentCodec.Read<ProjectTaskPlanAgentResult>(
            response.OwnerResult ?? throw ProjectTaskPlanAgentCodec.Denied("The saved publication has no acknowledged owner result."),
            ProjectTaskPlanAgentCodec.ResultFormat, 65_536);
        if (result.OperationId != command.Command.Operation.Id || result.TaskIds.IsDefault ||
            result.TaskIds.Any(id => id == Guid.Empty) || result.TaskIds.Distinct().Count() != result.TaskIds.Length) {
            throw ProjectTaskPlanAgentCodec.Denied("The saved outcome does not identify the original task-plan operation.");
        }
        if (result.Kind == ProjectTaskPlanAgentResultKind.Published && response.EffectState == AgentToolEffectState.Committed &&
            result.Refusal is null && result.TaskIds.Length == command.Command.Proposal.Tasks.Count) {
            return new(AgentPlanningPublicationOutcome.Committed, result.CleanupIncomplete
                ? "The original task plan was published; owner cleanup still needs attention." : result.SafeMessage);
        }
        if (result.Kind == ProjectTaskPlanAgentResultKind.Refused && response.EffectState == AgentToolEffectState.NotCommitted &&
            result.Refusal is { } refusal && Enum.IsDefined(refusal) && result.TaskIds.IsEmpty && !result.Replayed && !result.CleanupIncomplete) {
            return new(refusal == ProjectTaskPlanRefusal.BasisChanged ? AgentPlanningPublicationOutcome.BasisChanged
                : AgentPlanningPublicationOutcome.Refused, refusal == ProjectTaskPlanRefusal.BasisChanged
                    ? "The task basis changed; the original plan was not published." : result.SafeMessage);
        }
        throw ProjectTaskPlanAgentCodec.Denied("The saved outcome is inconsistent or uncertain; inspect the original receipt before further work.");
    }

    internal static ProjectTaskPlanAgentResult From(ProjectTaskPlanOutcome outcome) => outcome switch {
        ProjectTaskPlanOutcome.Accepted accepted => new(ProjectTaskPlanAgentResultKind.Published, accepted.Acknowledgement.Operation.Id,
            null, accepted.Acknowledgement.Tasks.Select(task => task.Fact.RecordId).ToImmutableArray(), accepted.Replayed, accepted.CleanupIncomplete),
        ProjectTaskPlanOutcome.Rejected refused => new(ProjectTaskPlanAgentResultKind.Refused, refused.Operation.Id, refused.Reason, [], false, false),
        ProjectTaskPlanOutcome.Unconfirmed unconfirmed => new(ProjectTaskPlanAgentResultKind.Unconfirmed, unconfirmed.Operation.Id, unconfirmed.Reason, [], false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}
