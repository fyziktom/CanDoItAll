using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

[Description("Untrusted original run reference and optional explicit page position. This read cannot resume execution or authorize mutation.")]
public sealed record AgentHistoricalReceiptReviewRequest {
    public const int MaximumUtf8Bytes = 4 * 1024;

    public AgentHistoricalReceiptReviewRequest(Guid executionRunId, Guid? chatSessionId, Guid agentId, DateTimeOffset executionCreatedAtUtc,
        AgentHistoricalReceiptCursor? cursor = null) {
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(executionCreatedAtUtc, default);
        if (chatSessionId == Guid.Empty) {
            throw new ArgumentException("A background review omits its chat identity.", nameof(chatSessionId));
        }
        ExecutionRunId = executionRunId;
        ChatSessionId = chatSessionId;
        AgentId = agentId;
        ExecutionCreatedAtUtc = executionCreatedAtUtc;
        Cursor = cursor;
        if (JsonSerializer.SerializeToUtf8Bytes(this).Length > MaximumUtf8Bytes) {
            throw new ArgumentException("Historical receipt review request exceeds its bounded reference size.");
        }
    }

    [Description("Exact original execution run identity; never replaced by a current run.")]
    public Guid ExecutionRunId { get; }
    [Description("Exact original chat identity, or null for a background run.")]
    public Guid? ChatSessionId { get; }
    [Description("Exact original executor Agent identity.")]
    public Guid AgentId { get; }
    [Description("Original run creation timestamp used as an identity witness.")]
    public DateTimeOffset ExecutionCreatedAtUtc { get; }
    [Description("Exact cursor from the preceding accepted page, or null for an explicit first-page read.")]
    public AgentHistoricalReceiptCursor? Cursor { get; }
}

[Description("Untrusted page position bound to the exact saved run and journal revisions and current read context. It grants no authority.")]
public sealed record AgentHistoricalReceiptCursor {
    public const int MaximumProposalOrdinal = AgentToolJournalRecord.MaximumBatches * AgentToolJournalRecord.MaximumCallsPerBatch;

    public AgentHistoricalReceiptCursor(Guid executionRunId, long runRevision, long journalRevision, int nextProposalOrdinal,
        AgentHistoricalReceiptReadContext currentContext) {
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(runRevision, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(journalRevision, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(nextProposalOrdinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nextProposalOrdinal, MaximumProposalOrdinal);
        ArgumentNullException.ThrowIfNull(currentContext);
        ExecutionRunId = executionRunId;
        RunRevision = runRevision;
        JournalRevision = journalRevision;
        NextProposalOrdinal = nextProposalOrdinal;
        CurrentContext = currentContext;
    }

    [Description("Exact original execution run identity; never replaced by a current run.")]
    public Guid ExecutionRunId { get; }
    [JsonConverter(typeof(AgentHistoricalReceiptRevisionJsonConverter))]
    [Description("Original positive Int64 run revision encoded as a lossless decimal string. Null in a refused response discloses no witness.")]
    public long RunRevision { get; }
    [JsonConverter(typeof(AgentHistoricalReceiptRevisionJsonConverter))]
    [Description("Original positive Int64 journal revision encoded as a lossless decimal string. Never convert to a JavaScript number.")]
    public long JournalRevision { get; }
    [Description("Canonical flattened saved proposal position, from 0 through 32768. This is not a filtered display index.")]
    public int NextProposalOrdinal { get; }
    [Description("Current sanitized host and workspace read context bound to this exact page.")]
    public AgentHistoricalReceiptReadContext CurrentContext { get; }
}

[Description("Server-only current read fence containing the private runtime fingerprint. Never expose this type in the review wire contract.")]
public sealed record AgentHistoricalReceiptReadFence {
    public AgentHistoricalReceiptReadFence(AgentToolProfileBinding profile, WorkspaceScopeDescriptor scope) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(scope);
        if (!Enum.IsDefined(scope.Kind) || scope.Key is null ||
                Encoding.UTF8.GetByteCount(profile.Fingerprint) + (long)Encoding.UTF8.GetByteCount(scope.Key) > AgentHistoricalReceiptReview.MaximumUtf8Bytes ||
                new WorkspaceScopeDescriptor(scope.Kind, scope.Key) != scope) {
            throw new ArgumentException("Historical receipt review requires a bounded canonical current workspace fence.");
        }
        Profile = profile;
        Scope = scope;
    }

    [Description("Private server-only profile identity, runtime fingerprint and current host generation.")]
    public AgentToolProfileBinding Profile { get; }
    [Description("Current canonical workspace scope observed by the server.")]
    public WorkspaceScopeDescriptor Scope { get; }
    [Description("Sanitized read context with no runtime fingerprint.")]
    public AgentHistoricalReceiptReadContext Context => new(Profile.ProfileId, Profile.Generation, Scope);
}

[Description("Sanitized current read context. A context change invalidates the page; this context grants no mutation authority.")]
public sealed record AgentHistoricalReceiptReadContext {
    public AgentHistoricalReceiptReadContext(Guid profileId, DatabaseProfileGeneration generation, WorkspaceScopeDescriptor scope) {
        ArgumentOutOfRangeException.ThrowIfEqual(profileId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(scope);
        if (!Enum.IsDefined(scope.Kind) || scope.Key is null ||
                Encoding.UTF8.GetByteCount(scope.Key) > AgentHistoricalReceiptReview.MaximumUtf8Bytes ||
                new WorkspaceScopeDescriptor(scope.Kind, scope.Key) != scope) {
            throw new ArgumentException("Historical receipt context requires a bounded canonical scope.", nameof(scope));
        }
        ProfileId = profileId;
        Generation = generation;
        Scope = scope;
    }

    [Description("Current canonical database profile identity.")]
    public Guid ProfileId { get; }
    [Description("Current local host generation. It fences this read and does not replace the saved execution generation.")]
    public DatabaseProfileGeneration Generation { get; }
    [Description("Current canonical workspace scope observed by the server.")]
    public WorkspaceScopeDescriptor Scope { get; }
}

[Description("Read stop: 0 ExhaustedSnapshot, 1 LookupLimit, 2 CooperativeBudget, 3 Cancelled, 4 SnapshotChanged, 5 ReadDenied, 6 ReadUnavailable, 7 Unsupported, 8 ContextChanged. A refused witness has no successor cursor; only explicit next-page reads are allowed.")]
public enum AgentHistoricalReceiptReviewStop {
    ExhaustedSnapshot,
    LookupLimit,
    CooperativeBudget,
    Cancelled,
    SnapshotChanged,
    ReadDenied,
    ReadUnavailable,
    Unsupported,
    ContextChanged
}

[Description("Receipt knowledge: 0 Accepted, 1 NotObserved, 2 ClosedAbsent, 3 Expired, 4 DatasetChanged, 5 ReadDenied, 6 ReadUnavailable, 7 Unsupported, 8 AcceptedDetailUnavailable, 9 AcceptedDetailDenied. Missing or inaccessible detail never authorizes retry.")]
public enum AgentHistoricalReceiptKnowledge {
    Accepted,
    NotObserved,
    ClosedAbsent,
    Expired,
    DatasetChanged,
    ReadDenied,
    ReadUnavailable,
    Unsupported,
    AcceptedDetailUnavailable,
    AcceptedDetailDenied
}

[Description("Current-read-authorized knowledge about one original proposal, without owner payload or source credentials.")]
public sealed record AgentHistoricalReceiptObservation {
    public AgentHistoricalReceiptObservation(AgentToolApprovalBinding binding, AgentHistoricalReceiptKnowledge knowledge) {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.BatchId.Value == Guid.Empty || binding.IntentId.Value == Guid.Empty || binding.SemanticVersion < 1 ||
                string.IsNullOrWhiteSpace(binding.Digest.Value) || !Enum.IsDefined(knowledge)) {
            throw new ArgumentException("A historical observation requires an exact original proposal binding and a known receipt status.");
        }
        Binding = binding;
        Knowledge = knowledge;
    }

    [Description("Exact immutable original proposal binding; no raw arguments or source preparation are exposed.")]
    public AgentToolApprovalBinding Binding { get; }
    [Description("Receipt knowledge: 0 Accepted, 1 NotObserved, 2 ClosedAbsent, 3 Expired, 4 DatasetChanged, 5 ReadDenied, 6 ReadUnavailable, 7 Unsupported, 8 AcceptedDetailUnavailable, 9 AcceptedDetailDenied. No value authorizes retry.")]
    public AgentHistoricalReceiptKnowledge Knowledge { get; }
}

[Description("One bounded ephemeral historical receipt page. No checkpoint, Waiting transition, coordination release or retry permission is implied.")]
public sealed record AgentHistoricalReceiptReview {
    public const int MaximumObservations = 4;
    public const int MaximumUtf8Bytes = 16 * 1024;

    public AgentHistoricalReceiptReview(AgentHistoricalReceiptReviewRequest request, AgentHistoricalReceiptReadContext currentContext,
        long? runRevision, long? journalRevision, AgentHistoricalReceiptReviewStop stop,
        IReadOnlyList<AgentHistoricalReceiptObservation> observations, AgentHistoricalReceiptCursor? nextCursor = null) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentContext);
        ArgumentNullException.ThrowIfNull(observations);
        var count = observations.Count;
        if (!Enum.IsDefined(stop) || count is < 0 or > MaximumObservations ||
                runRevision.HasValue != journalRevision.HasValue || runRevision is < 1 || journalRevision is < 1 ||
                count > 0 && runRevision is null ||
                stop is AgentHistoricalReceiptReviewStop.ReadDenied or AgentHistoricalReceiptReviewStop.ContextChanged or
                    AgentHistoricalReceiptReviewStop.SnapshotChanged or AgentHistoricalReceiptReviewStop.Unsupported &&
                    (count != 0 || runRevision.HasValue || nextCursor is not null) ||
                stop == AgentHistoricalReceiptReviewStop.ExhaustedSnapshot && nextCursor is not null ||
                stop == AgentHistoricalReceiptReviewStop.LookupLimit && (nextCursor is null ||
                    nextCursor.NextProposalOrdinal <= (request.Cursor?.NextProposalOrdinal ?? 0)) ||
                runRevision.HasValue && request.Cursor is { } submitted &&
                    (submitted.RunRevision != runRevision || submitted.JournalRevision != journalRevision ||
                        submitted.CurrentContext != currentContext) ||
                nextCursor is not null && (nextCursor.ExecutionRunId != request.ExecutionRunId ||
                    nextCursor.RunRevision != runRevision || nextCursor.JournalRevision != journalRevision ||
                    nextCursor.CurrentContext != currentContext ||
                    request.Cursor is { } previous && nextCursor.NextProposalOrdinal < previous.NextProposalOrdinal)) {
            throw new ArgumentException("Historical receipt review contains invalid bounded observations or snapshot disclosure.");
        }
        var captured = ImmutableArray.CreateBuilder<AgentHistoricalReceiptObservation>(count);
        var intents = new HashSet<AgentToolBusinessIntentId>();
        for (var index = 0; index < count; index++) {
            var observation = observations[index];
            if (observation is null || !intents.Add(observation.Binding.IntentId)) {
                throw new ArgumentException("Historical observations require distinct original intents.", nameof(observations));
            }
            captured.Add(observation);
        }
        Request = request;
        CurrentContext = currentContext;
        RunRevision = runRevision;
        JournalRevision = journalRevision;
        Stop = stop;
        Observations = captured.MoveToImmutable();
        NextCursor = nextCursor;
        if (JsonSerializer.SerializeToUtf8Bytes(this).Length > MaximumUtf8Bytes) {
            throw new ArgumentException("Historical receipt review exceeds its bounded response size.");
        }
    }

    [Description("Exact submitted original reference and page position; responses must not silently restart or replace it.")]
    public AgentHistoricalReceiptReviewRequest Request { get; }
    [Description("Current sanitized host and workspace read context bound to this exact page.")]
    public AgentHistoricalReceiptReadContext CurrentContext { get; }
    [JsonConverter(typeof(AgentHistoricalReceiptRevisionJsonConverter))]
    [Description("Original positive Int64 run revision encoded as a lossless decimal string. Null in a refused response discloses no witness.")]
    public long? RunRevision { get; }
    [JsonConverter(typeof(AgentHistoricalReceiptRevisionJsonConverter))]
    [Description("Original positive Int64 journal revision encoded as a lossless decimal string. Never convert to a JavaScript number.")]
    public long? JournalRevision { get; }
    [Description("Read stop: 0 ExhaustedSnapshot, 1 LookupLimit, 2 CooperativeBudget, 3 Cancelled, 4 SnapshotChanged, 5 ReadDenied, 6 ReadUnavailable, 7 Unsupported, 8 ContextChanged.")]
    public AgentHistoricalReceiptReviewStop Stop { get; }
    [Description("At most four authorized observations from this exact page. Replace the visible page; do not merge different witnesses or contexts.")]
    public IReadOnlyList<AgentHistoricalReceiptObservation> Observations { get; }
    [Description("Explicit next-page position only after final witness and current-read checks succeed; null when exhausted or refused.")]
    public AgentHistoricalReceiptCursor? NextCursor { get; }
}
