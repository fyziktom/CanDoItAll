using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectTaskPlanCallerKind { OrdinaryAgent = 1 }
internal enum ProjectTaskPlanSourceKind { OrdinaryAgentDecision = 1 }
internal enum ProjectTaskPlanRefusal {
    InvalidInput, DatasetChanged, CallerChanged, EpochUnavailable, BasisChanged, PolicyChanged,
    BudgetElapsed, AuthorityDenied, SourceChanged, TransactionConflict, Unavailable, IdentityConflict,
    DetailUnavailable, CommitUnconfirmed, CleanupIncomplete
}
internal enum ProjectTaskPlanLookupStatus {
    Found, Open, Draining, ClosedAbsent, Expired, DatasetChanged, CallerContextChanged,
    InvalidContext, DetailUnavailable, Unavailable
}
internal enum ProjectTaskPlanDetailFailure { AccessDenied, TargetUnavailable, UnsupportedVersion, InvalidStoredResult }

internal sealed class ProjectTaskPlanCaller {
    internal ProjectTaskPlanCaller(ProjectTaskPlanCallerKind kind, ReadOnlySpan<byte> key) {
        if (kind != ProjectTaskPlanCallerKind.OrdinaryAgent || key.Length != 32) {
            throw new ArgumentException("A task-plan caller requires a supported owner kind and a 32-byte trusted key.");
        }
        Kind = kind;
        Key = ImmutableArray.CreateRange(key.ToArray());
    }

    internal ProjectTaskPlanCallerKind Kind { get; }
    internal ImmutableArray<byte> Key { get; }
    internal string Bind(DatasetNamespaceId datasetNamespace) {
        ArgumentNullException.ThrowIfNull(datasetNamespace);
        var domain = $"workbench:task-plan:caller:v1:{WorkbenchTaskPlanFiniteOperations.Pool.Value:N}:{WorkbenchTaskPlanFiniteOperations.Family.Value:N}:{datasetNamespace.Value:N}:{(int)Kind}:{Convert.ToHexStringLower(Key.AsSpan())}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(domain)));
    }
}

internal sealed record ProjectTaskPlanOperation {
    public ProjectTaskPlanOperation(DatasetNamespaceId @namespace, long epoch, Guid id, string callerBinding) {
        ArgumentNullException.ThrowIfNull(@namespace);
        if (epoch <= 0 || id == Guid.Empty || !ProjectTaskPlanOperationRules.IsDigest(callerBinding)) {
            throw new ArgumentException("A task-plan operation requires its original namespace, epoch, identity and caller binding.");
        }
        Namespace = @namespace;
        Epoch = epoch;
        Id = id;
        CallerBinding = callerBinding;
    }

    public DatasetNamespaceId Namespace { get; }
    public long Epoch { get; }
    public Guid Id { get; }
    public string CallerBinding { get; }
}

internal sealed record ProjectTaskPlanSource {
    public ProjectTaskPlanSource(Guid decisionId, long generation, DateTimeOffset decisionStartedAtUtc,
        ProjectTaskPlanSourceKind kind, int version, string fingerprint) {
        if (decisionId == Guid.Empty || generation < 1 || decisionStartedAtUtc == default || decisionStartedAtUtc.Offset != TimeSpan.Zero ||
                kind != ProjectTaskPlanSourceKind.OrdinaryAgentDecision || version != 1 || !ProjectTaskPlanOperationRules.IsDigest(fingerprint)) {
            throw new ArgumentException("A task-plan source requires the original supported decision occurrence and trusted fingerprint.");
        }
        DecisionId = decisionId;
        Generation = generation;
        DecisionStartedAtUtc = decisionStartedAtUtc;
        Kind = kind;
        Version = version;
        Fingerprint = fingerprint;
    }

    public Guid DecisionId { get; }
    public long Generation { get; }
    public DateTimeOffset DecisionStartedAtUtc { get; }
    public ProjectTaskPlanSourceKind Kind { get; }
    public int Version { get; }
    public string Fingerprint { get; }
}

internal sealed class ProjectTaskPlanCommand {
    internal ProjectTaskPlanCommand(ProjectTaskPlanOperation operation, ProjectTaskPlanBasis basis, ProjectTaskPlanProposal proposal,
        DecisionDataPolicy policy, ProjectTaskPlanSource source, int schemaVersion = 1, int normalizationVersion = 1) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(source);
        if (schemaVersion != 1 || normalizationVersion != 1 || operation.Namespace != basis.Namespace) {
            throw new ArgumentException("A task-plan command requires the original supported interpretation and dataset.");
        }
        Operation = operation;
        Basis = basis;
        Proposal = proposal;
        Policy = policy;
        Source = source;
        SchemaVersion = schemaVersion;
        NormalizationVersion = normalizationVersion;
    }

    internal ProjectTaskPlanOperation Operation { get; }
    internal ProjectTaskPlanBasis Basis { get; }
    internal ProjectTaskPlanProposal Proposal { get; }
    internal DecisionDataPolicy Policy { get; }
    internal ProjectTaskPlanSource Source { get; }
    internal int SchemaVersion { get; }
    internal int NormalizationVersion { get; }
}

internal sealed record ProjectTaskPlanWriteContext(DatasetNamespaceId Namespace, long Epoch,
    string CallerBinding, DateTimeOffset AdmissionClosesAtUtc, DateTimeOffset ReplayUntilUtc);

internal sealed class ProjectTaskPlanAccepted {
    internal ProjectTaskPlanAccepted(ProjectTaskPlanOperation operation, ProjectDefinitionExpectation originalDefinition,
        ProjectTaskPlanSource source, string semanticDigest, DateTimeOffset acceptedAtUtc, DateTimeOffset replayUntilUtc,
        IReadOnlyList<ProjectTaskPlanStagedTask> tasks) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(originalDefinition);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tasks);
        if (!ProjectTaskPlanOperationRules.IsDigest(semanticDigest) || acceptedAtUtc == default || acceptedAtUtc.Offset != TimeSpan.Zero ||
                replayUntilUtc.Offset != TimeSpan.Zero || replayUntilUtc <= acceptedAtUtc || tasks.Count is < 1 or > ProjectTaskPlanProposal.MaximumTasks) {
            throw new ArgumentException("A task-plan acceptance requires bounded exact committed evidence.");
        }
        var captured = tasks.ToArray();
        var ids = new HashSet<Guid>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        long bytes = 0;
        for (var index = 0; index < captured.Length; index++) {
            var task = captured[index];
            if (task is null || task.Fact is null || task.Ordinal != index || task.Fact.RecordId == Guid.Empty ||
                    !ids.Add(task.Fact.RecordId) || !keys.Add(task.Fact.NodeKey) || task.CreatedAtUtc != acceptedAtUtc ||
                    !double.IsFinite(task.PositionX) || !double.IsFinite(task.PositionY)) {
                throw new ArgumentException("A task-plan acceptance contains invalid task identities or order.");
            }
            bytes += ProjectTaskPlanBasisLimits.TextBytes(task.MetadataJson);
        }
        ProjectTaskPlanBasisLimits.RequireBytes(bytes, ProjectTaskPlanReceiptRecord.MaximumPayloadBytes);
        _ = ProjectTaskPlanBasisLimits.TaskFactBytes(captured.Select(task => task.Fact).ToArray());
        Operation = operation;
        OriginalDefinition = originalDefinition;
        Source = source;
        SemanticDigest = semanticDigest;
        AcceptedAtUtc = acceptedAtUtc;
        ReplayUntilUtc = replayUntilUtc;
        Tasks = Array.AsReadOnly(captured);
    }

    internal ProjectTaskPlanOperation Operation { get; }
    internal ProjectDefinitionExpectation OriginalDefinition { get; }
    internal ProjectTaskPlanSource Source { get; }
    internal string SemanticDigest { get; }
    internal DateTimeOffset AcceptedAtUtc { get; }
    internal DateTimeOffset ReplayUntilUtc { get; }
    internal IReadOnlyList<ProjectTaskPlanStagedTask> Tasks { get; }
}

internal abstract record ProjectTaskPlanOutcome {
    private ProjectTaskPlanOutcome() {
    }

    internal sealed record Accepted : ProjectTaskPlanOutcome {
        internal Accepted(ProjectTaskPlanAccepted acknowledgement, bool replayed, bool cleanupIncomplete = false) {
            ArgumentNullException.ThrowIfNull(acknowledgement);
            Acknowledgement = acknowledgement;
            Replayed = replayed;
            CleanupIncomplete = cleanupIncomplete;
        }
        internal ProjectTaskPlanAccepted Acknowledgement { get; }
        internal bool Replayed { get; }
        internal bool CleanupIncomplete { get; }
    }

    internal sealed record Rejected : ProjectTaskPlanOutcome {
        internal Rejected(ProjectTaskPlanOperation operation, ProjectTaskPlanRefusal reason, bool requiresReview = false) {
            ArgumentNullException.ThrowIfNull(operation);
            if (!Enum.IsDefined(reason) || reason is ProjectTaskPlanRefusal.CommitUnconfirmed or ProjectTaskPlanRefusal.CleanupIncomplete ||
                    requiresReview && reason != ProjectTaskPlanRefusal.BasisChanged) {
                throw new ArgumentException("A task-plan refusal requires definite attempt-scoped non-effect evidence.");
            }
            Operation = operation;
            Reason = reason;
            RequiresReview = requiresReview;
        }
        internal ProjectTaskPlanOperation Operation { get; }
        internal ProjectTaskPlanRefusal Reason { get; }
        internal bool RequiresReview { get; }
    }

    internal sealed record Unconfirmed : ProjectTaskPlanOutcome {
        internal Unconfirmed(ProjectTaskPlanOperation operation, ProjectTaskPlanRefusal reason) {
            ArgumentNullException.ThrowIfNull(operation);
            if (reason is not (ProjectTaskPlanRefusal.CommitUnconfirmed or ProjectTaskPlanRefusal.CleanupIncomplete or ProjectTaskPlanRefusal.DetailUnavailable)) {
                throw new ArgumentException("An unconfirmed task-plan result must retain its unresolved effect knowledge.");
            }
            Operation = operation;
            Reason = reason;
        }
        internal ProjectTaskPlanOperation Operation { get; }
        internal ProjectTaskPlanRefusal Reason { get; }
    }
}

internal sealed record ProjectTaskPlanLookup(ProjectTaskPlanLookupStatus Status, ProjectTaskPlanAccepted? Accepted = null,
    ProjectTaskPlanDetailFailure? DetailFailure = null, DateTimeOffset? ClosedAtUtc = null,
    DateTimeOffset? ObservedAtUtc = null, DateTimeOffset? RetainedUntilUtc = null);

internal static class ProjectTaskPlanOperationRules {
    internal static bool IsDigest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
