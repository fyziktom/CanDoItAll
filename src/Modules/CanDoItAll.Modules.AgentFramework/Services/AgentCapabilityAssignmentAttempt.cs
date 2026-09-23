using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentCapabilityAssignmentAttempt {
    private readonly AgentEditorModel submission;

    public AgentCapabilityAssignmentAttempt(AgentEditorModel draft, Guid capabilityId) {
        AgentId = draft.Id is { } id && id != Guid.Empty ? id : throw new ArgumentException("An existing agent is required.", nameof(draft));
        ExpectedUpdatedAtUtc = draft.ExpectedUpdatedAtUtc ?? throw new ArgumentException("An authoritative agent revision is required.", nameof(draft));
        ArgumentOutOfRangeException.ThrowIfEqual(capabilityId, Guid.Empty);
        AttemptId = Guid.NewGuid();
        CapabilityId = capabilityId;
        Before = draft.SelectedCapabilityIds.ToImmutableHashSet();
        Desired = Before.Contains(capabilityId) ? Before.Remove(capabilityId) : Before.Add(capabilityId);
        submission = AgentEditorDraftPolicy.Copy(draft);
        submission.SelectedCapabilityIds = Desired.Order().ToList();
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(submission)));
    }

    public Guid AttemptId { get; }
    public Guid AgentId { get; }
    public Guid CapabilityId { get; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; }
    public ImmutableHashSet<Guid> Before { get; }
    public ImmutableHashSet<Guid> Desired { get; }
    public string Fingerprint { get; }
    public AgentEditorModel CreateRequest() => AgentEditorDraftPolicy.Copy(submission);

    public AgentCapabilityOperationStatus Classify(AgentEditorModel current) {
        if (current.Id != AgentId || current.ExpectedUpdatedAtUtc is not { } revision ||
            current.SelectedCapabilityIds.Any(id => id == Guid.Empty)) {
            return AgentCapabilityOperationStatus.Unconfirmed;
        }
        if (revision > ExpectedUpdatedAtUtc && Desired.SetEquals(current.SelectedCapabilityIds)) {
            return AgentCapabilityOperationStatus.DesiredStateSatisfied;
        }
        if (revision == ExpectedUpdatedAtUtc && Before.SetEquals(current.SelectedCapabilityIds)) {
            return AgentCapabilityOperationStatus.DefinitelyNotCommitted;
        }
        return revision < ExpectedUpdatedAtUtc
            ? AgentCapabilityOperationStatus.Unconfirmed : AgentCapabilityOperationStatus.Superseded;
    }
}
