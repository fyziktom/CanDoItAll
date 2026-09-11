using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.SchedulerPlanner;

public enum SchedulerFireAdmissionState {
    Dispatching,
    Prepared,
    Accepted,
    Observed,
    ReconciliationRequired
}

public sealed class SchedulerFireAdmissionRecord {
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public string DedupeKey { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public string SnapshotFingerprint { get; set; } = string.Empty;
    public Guid PreparedWorkflowRunId { get; set; }
    public Guid? AcceptedWorkflowRunId { get; set; }
    public SchedulerFireAdmissionState State { get; set; }
    public long Generation { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? NextObservationAtUtc { get; set; }
    public string? OutcomeJson { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class SchedulerFireAdmissionRecordConfiguration : IEntityTypeConfiguration<SchedulerFireAdmissionRecord> {
    public void Configure(EntityTypeBuilder<SchedulerFireAdmissionRecord> builder) {
        builder.ToTable("SchedulerPlanner_FireAdmissions");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.DedupeKey).HasMaxLength(260).IsRequired();
        builder.Property(row => row.SnapshotJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.SnapshotFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(row => row.OutcomeJson).HasColumnType("TEXT");
        builder.Property(row => row.LastError).HasColumnType("TEXT");
        builder.HasIndex(row => row.DedupeKey).IsUnique();
        builder.HasIndex(row => row.PreparedWorkflowRunId).IsUnique();
        builder.HasIndex(row => new { row.State, row.NextObservationAtUtc, row.LeaseExpiresAtUtc });
        builder.HasIndex(row => row.PlanId);
    }
}

public sealed record SchedulerFireSnapshot(
    Guid PlanId,
    Guid PlanRunId,
    Guid FireId,
    Guid CorrelationId,
    DateTimeOffset FiredAtUtc,
    DateTimeOffset? NextPlannedFireAtUtc,
    string PlanName,
    SchedulerPlanTargetKind TargetKind,
    Guid TargetId,
    Guid? TargetVersionId,
    string TargetName,
    string InputJson,
    WorkflowStructureAuthority? Authority) {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool LegacyObservationOnly { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
    public string Fingerprint() => Hash(ToJson());
    public static SchedulerFireSnapshot Parse(string json) => JsonSerializer.Deserialize<SchedulerFireSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("The saved Scheduler fire snapshot is missing.");
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string? SerializeAuthority(WorkflowStructureAuthority? authority) {
        if (authority is null) {
            return null;
        }
        if (authority.SchedulerAuthority is not null || authority.ProcessAuthority is not null) {
            throw new InvalidOperationException("A schedule requires its admitted authorizer ceiling, without a previous background execution binding.");
        }
        return JsonSerializer.Serialize(authority, JsonOptions);
    }

    public static WorkflowStructureAuthority? ParseAuthority(string? json) => json is null ? null :
        JsonSerializer.Deserialize<WorkflowStructureAuthority>(json, JsonOptions)
            ?? throw new InvalidOperationException("The saved Scheduler authorizer ceiling is invalid.");

    public SchedulerPlan ToPlan() => new() {
        Id = PlanId,
        Name = PlanName,
        TargetKind = TargetKind,
        TargetId = TargetId,
        TargetVersionId = TargetVersionId,
        TargetNameSnapshot = TargetName,
        InputJson = InputJson
    };

    public SchedulerTargetLaunchContext ToContext(Guid preparedRunId, bool mayLaunch) => new(
        PlanId, PlanRunId, new WorkflowSchedulerFireId(FireId), FiredAtUtc, new WorkflowLaunchCorrelationId(CorrelationId)) {
        PreparedRunId = new(preparedRunId),
        MayLaunch = mayLaunch && !LegacyObservationOnly,
        StructureAuthority = Authority is null ? null : Authority with {
            SchedulerAuthority = new(PlanId, PlanRunId, Hash(SerializeAuthority(Authority)!))
        }
    };
}

public sealed record SchedulerFireClaim(SchedulerFireSnapshot Snapshot, Guid PreparedRunId, Guid Owner, long Generation, bool MayLaunch);

public sealed class SchedulerFireAdmissionStore(
    IDbContextFactory<SchedulerPlannerDbContext> factory,
    IWorkflowCatalogService catalog,
    IWorkflowRuntimeManager runtimeManager,
    IClock clock) {
    private static readonly TimeSpan ClaimDuration = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlySet<string> AdmissionConstraints = new HashSet<string>(StringComparer.Ordinal) {
        "IX_SchedulerPlanner_FireAdmissions_DedupeKey", "IX_SchedulerPlanner_Runs_DedupeKey"
    };

    public Task<SchedulerFireClaim?> AcquireAsync(SchedulerPlanFireRequest request, CancellationToken cancellationToken = default) {
        if (request.PlanId == Guid.Empty || request.SchedulerFireId == Guid.Empty || request.FiredAtUtc == default) {
            throw new ArgumentException("Scheduler fire requires a plan, fire identity and timestamp.", nameof(request));
        }
        return RetryConflictAsync(() => AcquireCoreAsync(request, cancellationToken));
    }

    private async Task<SchedulerFireClaim?> AcquireCoreAsync(SchedulerPlanFireRequest request, CancellationToken cancellationToken) {
        var dedupeKey = BuildDedupeKey(request.PlanId, request.FiredAtUtc);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database, dedupeKey, cancellationToken);
        var now = clock.GetUtcNow();
        var admission = await database.Set<SchedulerFireAdmissionRecord>().SingleOrDefaultAsync(row => row.DedupeKey == dedupeKey, cancellationToken);
        if (admission is { State: SchedulerFireAdmissionState.Observed or SchedulerFireAdmissionState.ReconciliationRequired } ||
            admission?.LeaseOwner is not null && admission.LeaseExpiresAtUtc > now) {
            return null;
        }
        var plan = await database.Set<SchedulerPlan>().SingleOrDefaultAsync(row => row.Id == request.PlanId, cancellationToken);
        var history = await database.Set<SchedulerPlanRun>().SingleOrDefaultAsync(row => row.DedupeKey == dedupeKey, cancellationToken);
        if (admission is null) {
            if (history is not null) {
                if (history.Status is SchedulerPlanRunDispatchStatus.Dispatched or SchedulerPlanRunDispatchStatus.NoMessages or SchedulerPlanRunDispatchStatus.WaitingForApproval) {
                    return null;
                }
                if (history.TargetRunId is not { } knownRunId) {
                    throw new InvalidOperationException($"Legacy Scheduler run '{history.Id:D}' has no frozen fire snapshot or saved Workflow ID. Reconcile its saved Workflow identity before retrying; today's edited plan is not the original intent.");
                }
                var existingRun = await runtimeManager.GetRunAsync(new(knownRunId), cancellationToken);
                if (existingRun?.Origin is not WorkflowLaunchOrigin.SchedulerPlanRun origin || origin.PlanId != history.PlanId ||
                    origin.PlanRunId != history.Id || origin.FireId.Value != history.SchedulerFireId || origin.FiredAtUtc != history.FiredAtUtc) {
                    throw new InvalidOperationException("The legacy Scheduler run does not have matching saved Workflow launch lineage.");
                }
                if (!history.CorrelationId.HasValue && !Guid.TryParse(origin.CorrelationId.Value, out _)) {
                    throw new InvalidOperationException("The legacy Scheduler correlation has no reconstructable saved GUID identity.");
                }
                var observed = new SchedulerFireSnapshot(history.PlanId, history.Id, history.SchedulerFireId,
                    history.CorrelationId ?? Guid.Parse(origin.CorrelationId.Value), history.FiredAtUtc.ToUniversalTime(), null,
                    plan?.Name ?? "Deleted schedule", SchedulerPlanTargetKind.Workflow, existingRun.WorkflowId.Value,
                    existingRun.VersionId.Value, plan?.TargetNameSnapshot ?? string.Empty, "{}", null) { LegacyObservationOnly = true };
                admission = new() {
                    Id = history.Id, PlanId = history.PlanId, DedupeKey = dedupeKey, SnapshotJson = observed.ToJson(),
                    SnapshotFingerprint = observed.Fingerprint(), PreparedWorkflowRunId = knownRunId, AcceptedWorkflowRunId = knownRunId,
                    CreatedAtUtc = now
                };
                database.Add(admission);
            }
            if (admission is null) {
                if (plan is null) {
                    throw new KeyNotFoundException($"Scheduler plan '{request.PlanId:D}' was not found.");
                }
                if (!plan.IsEnabled) {
                    return null;
                }
                var versionId = plan.TargetVersionId;
                if (plan.TargetKind == SchedulerPlanTargetKind.Workflow && versionId is null) {
                    var definition = await catalog.GetLatestDefinitionByStatusAsync(new(plan.TargetId), WorkflowLifecycleStatus.Active, cancellationToken)
                        ?? throw new InvalidOperationException("The legacy unpinned schedule has no active Workflow version to prepare.");
                    versionId = definition.Definition.VersionId.Value;
                }
                var id = Guid.NewGuid();
                var snapshot = new SchedulerFireSnapshot(plan.Id, id, request.SchedulerFireId, request.CorrelationId ?? request.SchedulerFireId,
                    request.FiredAtUtc.ToUniversalTime(), request.NextPlannedFireAtUtc?.ToUniversalTime(), plan.Name, plan.TargetKind,
                    plan.TargetId, versionId, plan.TargetNameSnapshot, WorkflowLaunchIdempotencyRequestFactory.CanonicalizeInputJson(plan.InputJson),
                    SchedulerFireSnapshot.ParseAuthority(plan.StructureAuthorityJson));
                admission = new() {
                    Id = id, PlanId = plan.Id, DedupeKey = dedupeKey, SnapshotJson = snapshot.ToJson(),
                    SnapshotFingerprint = snapshot.Fingerprint(), PreparedWorkflowRunId = Guid.NewGuid(), CreatedAtUtc = now
                };
                database.Add(admission);
                history = new() {
                    Id = id, PlanId = plan.Id, DedupeKey = dedupeKey, SchedulerFireId = snapshot.FireId,
                    CorrelationId = snapshot.CorrelationId, FiredAtUtc = snapshot.FiredAtUtc, CreatedAtUtc = now
                };
                database.Add(history);
            }
        }
        var prepared = ReadSnapshot(admission);
        admission.Generation++;
        admission.LeaseOwner = Guid.NewGuid();
        admission.LeaseExpiresAtUtc = now.Add(ClaimDuration);
        admission.State = SchedulerFireAdmissionState.Dispatching;
        admission.UpdatedAtUtc = now;
        if (history is not null) {
            history.AttemptCount++;
            history.Status = SchedulerPlanRunDispatchStatus.Dispatching;
            history.UpdatedAtUtc = now;
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(prepared, admission.PreparedWorkflowRunId, admission.LeaseOwner.Value, admission.Generation, !prepared.LegacyObservationOnly && plan?.IsEnabled == true);
    }

    public Task CompleteAsync(SchedulerFireClaim claim, SchedulerTargetLaunchResult? result, Exception? failure,
        CancellationToken cancellationToken = default) => RetryConflictAsync(async () => {
            if (result is null && failure is null) {
                throw new ArgumentException("A Scheduler completion requires an outcome or failure.");
            }
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var dedupeKey = BuildDedupeKey(claim.Snapshot.PlanId, claim.Snapshot.FiredAtUtc);
            await using var transaction = await SerializableMutationScope.BeginAsync(database, dedupeKey, cancellationToken);
            var admission = await database.Set<SchedulerFireAdmissionRecord>().SingleAsync(row => row.Id == claim.Snapshot.PlanRunId, cancellationToken);
            if (admission.LeaseOwner != claim.Owner || admission.Generation != claim.Generation) {
                return false;
            }
            ReadSnapshot(admission);
            var now = clock.GetUtcNow();
            var knownRunId = result?.TargetRunId ?? (failure as SchedulerTargetLaunchException)?.TargetRunId;
            if (knownRunId.HasValue && knownRunId != admission.PreparedWorkflowRunId) {
                throw new InvalidOperationException("The Scheduler launcher returned a different Workflow run from its prepared admission.");
            }
            admission.AcceptedWorkflowRunId ??= knownRunId;
            var terminalFailure = failure is SchedulerTargetLaunchException { TargetRunId: not null };
            var needsObservation = result?.RequiresObservation == true || result is null && admission.AcceptedWorkflowRunId.HasValue && !terminalFailure;
            admission.State = terminalFailure ? SchedulerFireAdmissionState.Observed : needsObservation ? SchedulerFireAdmissionState.Accepted
                : result is null ? SchedulerFireAdmissionState.Prepared : SchedulerFireAdmissionState.Observed;
            admission.OutcomeJson = result is null ? admission.OutcomeJson : JsonSerializer.Serialize(result, JsonOptions);
            admission.LastError = (result?.ObservationException ?? failure)?.Message ?? string.Empty;
            admission.NextObservationAtUtc = needsObservation ? now.AddSeconds(10) : null;
            admission.LeaseOwner = null;
            admission.LeaseExpiresAtUtc = null;
            admission.UpdatedAtUtc = now;
            var history = await database.Set<SchedulerPlanRun>().SingleOrDefaultAsync(row => row.Id == admission.Id, cancellationToken);
            if (history is not null) {
                history.Status = result?.DispatchStatus ?? (needsObservation ? SchedulerPlanRunDispatchStatus.ObservationPending : SchedulerPlanRunDispatchStatus.Failed);
                history.TargetRunId = admission.AcceptedWorkflowRunId;
                history.TargetRunKind = history.TargetRunId.HasValue ? claim.Snapshot.TargetKind.ToString() : string.Empty;
                history.Summary = result?.Summary ?? history.Summary;
                history.ErrorMessage = admission.LastError;
                history.Route = result?.Route ?? SchedulerPlanRunRoutes.Failed;
                history.RetryCategory = result?.RetryCategory ?? SchedulerPlanRunRetryClassifier.Classify(failure!);
                history.DispatchedAtUtc = history.TargetRunId.HasValue ? history.DispatchedAtUtc ?? now : null;
                history.UpdatedAtUtc = now;
            }
            var plan = await database.Set<SchedulerPlan>().SingleOrDefaultAsync(row => row.Id == admission.PlanId, cancellationToken);
            if (plan is not null && (!plan.LastFiredAtUtc.HasValue || plan.LastFiredAtUtc <= claim.Snapshot.FiredAtUtc)) {
                plan.LastFiredAtUtc = claim.Snapshot.FiredAtUtc;
                plan.NextPlannedFireAtUtc = claim.Snapshot.NextPlannedFireAtUtc;
                plan.LastError = admission.LastError;
                plan.UpdatedAtUtc = now;
            }
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });

    public async Task<IReadOnlyList<SchedulerPlanFireRequest>> ListRecoveryAsync(int take, CancellationToken cancellationToken = default) {
        if (take is < 1 or > 100) {
            throw new ArgumentOutOfRangeException(nameof(take));
        }
        var now = clock.GetUtcNow();
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<SchedulerFireAdmissionRecord>().AsNoTracking().Where(row =>
            row.State == SchedulerFireAdmissionState.Accepted && row.NextObservationAtUtc <= now ||
            row.State == SchedulerFireAdmissionState.Dispatching && row.LeaseExpiresAtUtc <= now)
            .OrderBy(row => row.UpdatedAtUtc).ThenBy(row => row.Id).Take(take).ToArrayAsync(cancellationToken);
        return rows.Select(row => {
            var snapshot = ReadSnapshot(row);
            return new SchedulerPlanFireRequest(snapshot.PlanId, snapshot.FireId, snapshot.CorrelationId, snapshot.FiredAtUtc, snapshot.NextPlannedFireAtUtc);
        }).ToArray();
    }

    internal static SchedulerFireSnapshot ReadSnapshot(SchedulerFireAdmissionRecord row) {
        var snapshot = SchedulerFireSnapshot.Parse(row.SnapshotJson);
        if (snapshot.PlanId != row.PlanId || snapshot.PlanRunId != row.Id || snapshot.Fingerprint() != row.SnapshotFingerprint ||
            row.PreparedWorkflowRunId == Guid.Empty) {
            throw new InvalidOperationException("The saved Scheduler fire snapshot does not match its durable identity or fingerprint.");
        }
        return snapshot;
    }

    private static string BuildDedupeKey(Guid planId, DateTimeOffset firedAtUtc) => $"scheduler-planner:{planId:N}:{firedAtUtc.UtcTicks}";

    private static async Task<T> RetryConflictAsync<T>(Func<Task<T>> action) {
        for (var attempt = 0; ; attempt++) {
            try {
                return await action();
            } catch (Exception exception) when (attempt < 3 &&
                (SerializableMutationScope.IsConflict(exception) || SerializableMutationScope.IsUniqueConstraintConflict(exception, AdmissionConstraints))) {
            }
        }
    }
}

public sealed class SchedulerWorkflowAuthorityPolicy(
    IDbContextFactory<SchedulerPlannerDbContext> factory,
    DbContextOptions<SchedulerPlannerDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IWorkflowScheduledAuthorityPolicy {
    public bool AllowsScheduling(AgentExecutionGovernanceSnapshot authority)
        => authority.AllowedOperations.Count == 0 ||
            authority.AllowedOperations.Contains(AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate);

    public async Task RequireCurrentAsync(WorkflowStructureSchedulerAuthority authority, CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await RequireAsync(database, authority, false, cancellationToken);
    }

    public async Task RequireCurrentForMutationAsync(WorkflowStructureSchedulerAuthority authority, CancellationToken cancellationToken = default) {
        await using var database = await transactions.CreateEnlistedAsync(options, static value => new SchedulerPlannerDbContext(value), cancellationToken);
        await RequireAsync(database, authority, true, cancellationToken);
    }

    private static async Task RequireAsync(SchedulerPlannerDbContext database, WorkflowStructureSchedulerAuthority authority,
        bool forMutation, CancellationToken cancellationToken) {
        var admission = await database.Set<SchedulerFireAdmissionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == authority.FireAdmissionId && row.PlanId == authority.PlanId, cancellationToken);
        var savedAuthority = admission is null ? null : SchedulerFireAdmissionStore.ReadSnapshot(admission).Authority;
        var plans = forMutation && database.Database.IsRelational()
            ? database.Set<SchedulerPlan>().FromSqlInterpolated($"SELECT * FROM \"SchedulerPlanner_Plans\" WHERE \"Id\" = {authority.PlanId} FOR SHARE")
            : database.Set<SchedulerPlan>();
        var current = await plans.AsNoTracking().Where(row => row.Id == authority.PlanId)
            .Select(row => new { row.IsEnabled, row.StructureAuthorityJson }).SingleOrDefaultAsync(cancellationToken);
        if (savedAuthority is null || current is not { IsEnabled: true, StructureAuthorityJson: not null } ||
            SchedulerFireSnapshot.Hash(current.StructureAuthorityJson) != authority.AuthorityFingerprint ||
            SchedulerFireSnapshot.Hash(SchedulerFireSnapshot.SerializeAuthority(savedAuthority)!) != authority.AuthorityFingerprint) {
            throw new UnauthorizedAccessException("The scheduled Workflow authorizer ceiling was revoked, changed, or its plan is disabled or deleted.");
        }
    }
}
