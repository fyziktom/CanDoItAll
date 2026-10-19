using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchDeletionRecoveryInspector(DbContextOptions<WorkbenchDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IProjectDeletionLegacyParticipant {
    internal const int MaximumPayloadBytes = 2 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public ProjectDeletionParticipantId Id { get; } = new(ProjectWorkbenchDeletionParticipant.ParticipantIdValue);
    public int ProtocolVersion => ProjectWorkbenchDeletionPayload.RecoveryProtocolVersion;
    public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; } =
        [new(ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey)];

    public Task<ProjectDeletionRecoveryParticipantEvidence> InspectAsync(ProjectDeletionPreparationContext original,
        Guid recoveryId, ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken = default)
        => InOwnerAsync(async context => {
            var evidence = Evidence(await ReadAsync(context, recoveryId, mode, cancellationToken), original.Project);
            if (evidence.Binding != original) {
                throw new InvalidDataException($"Workbench deletion {recoveryId} has a different original completion binding.");
            }
            return evidence.State;
        }, cancellationToken);

    public Task<IReadOnlyList<ProjectDeletionLegacyParticipantEvidence>> InspectLegacyAsync(ProjectWriteAdmission project,
        ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken = default)
        => InOwnerAsync<IReadOnlyList<ProjectDeletionLegacyParticipantEvidence>>(async context => {
            ArgumentNullException.ThrowIfNull(project);
            RequireMode(mode);
            if (await context.Set<ProjectObjectRecord>().AnyAsync(row => row.ProjectId == project.ProjectId, cancellationToken)) {
                throw new InvalidDataException($"Workbench project {project.ProjectId} has residual native state; legacy deletion binding is refused.");
            }
            string query = $$"""
                SELECT "Id" AS "Value" FROM "Workbench_ProjectCrossModuleMutations"
                WHERE "ProjectId" = {0} AND "MutationKind" = {{(int)ProjectCrossModuleMutationKind.DeleteProject}}
                ORDER BY "Id" LIMIT {{ProjectDeletionCompletionBinding.MaximumParticipants + 1}}
                """;
            var ids = await context.Database.SqlQueryRaw<Guid>(query +
                (mode == ProjectDeletionRecoveryReadMode.Exclusive ? " FOR UPDATE NOWAIT" : ""), project.ProjectId).ToArrayAsync(cancellationToken);
            if (ids.Length > ProjectDeletionCompletionBinding.MaximumParticipants) {
                throw new InvalidDataException($"Workbench project {project.ProjectId} exceeds the bounded legacy deletion inspection.");
            }
            var result = new List<ProjectDeletionLegacyParticipantEvidence>(ids.Length);
            foreach (Guid id in ids) {
                result.Add(Evidence(await ReadAsync(context, id, mode, cancellationToken), project));
            }
            return result;
        }, cancellationToken);

    public async Task BindLegacyAsync(ProjectDeletionPreparationContext original, ProjectDeletionLegacyParticipantEvidence expected,
        CancellationToken cancellationToken = default) {
        await InOwnerAsync(async context => {
            var row = await ReadAsync(context, expected.State.Obligation.RecoveryId, ProjectDeletionRecoveryReadMode.Exclusive, cancellationToken);
            var actual = Evidence(row, original.Project);
            if (expected.Binding is not null || actual != expected || expected.State.Obligation.Participant != Id) {
                throw new InvalidDataException($"Workbench deletion {row.Id} changed before legacy binding.");
            }
            int changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Workbench_ProjectCrossModuleMutations"
                SET "CompletionNamespaceId" = {original.Completion.Namespace.Value}, "CompletionId" = {original.Completion.Id.Value}
                WHERE "Id" = {row.Id} AND "CompletionNamespaceId" IS NULL AND "CompletionId" IS NULL
                """, cancellationToken);
            if (changed != 1) {
                throw new InvalidDataException($"Workbench deletion {row.Id} could not be bound exactly once.");
            }
            return changed;
        }, cancellationToken);
    }

    private static async Task<RecoveryRow> ReadAsync(WorkbenchDbContext context, Guid recoveryId,
        ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(recoveryId, Guid.Empty);
        RequireMode(mode);
        const string query = """
            SELECT "Id", "ProjectId", "ScopeNodeKey", "MutationKind", "Status", "ApprovalState", "AttemptCount",
                "CompletionNamespaceId", "CompletionId", "CreatedAtUtc", "UpdatedAtUtc", "LastAttemptAtUtc", "CompletedAtUtc",
                encode(sha256(convert_to("ErrorMessage", 'UTF8')), 'hex') AS "ClaimDigest",
                CASE WHEN octet_length("PayloadJson") BETWEEN 1 AND {1} THEN "PayloadJson" ELSE NULL END AS "PayloadJson"
            FROM "Workbench_ProjectCrossModuleMutations" WHERE "Id" = {0}
            """;
        return await context.Database.SqlQueryRaw<RecoveryRow>(query +
            (mode == ProjectDeletionRecoveryReadMode.Exclusive ? " FOR UPDATE NOWAIT" : ""), recoveryId, MaximumPayloadBytes)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidDataException($"Workbench deletion {recoveryId} is missing.");
    }

    private ProjectDeletionLegacyParticipantEvidence Evidence(RecoveryRow row, ProjectWriteAdmission project) {
        if (row.Id == Guid.Empty || row.ProjectId != project.ProjectId || row.ScopeNodeKey != ProjectWorkbenchDeletionParticipant.ProjectDeletionScopeNodeKey ||
                row.MutationKind != (int)ProjectCrossModuleMutationKind.DeleteProject || row.PayloadJson is null ||
                !Enum.IsDefined((ProjectCrossModuleMutationStatus)row.Status) || !Enum.IsDefined((ProjectCrossModuleMutationApprovalState)row.ApprovalState) ||
                row.AttemptCount < 0 || !IsFinite(row.CreatedAtUtc) || !IsFinite(row.UpdatedAtUtc) ||
                row.LastAttemptAtUtc is { } attempted && !IsFinite(attempted) ||
                row.CompletedAtUtc is { } completed && !IsFinite(completed) ||
                row.Status == (int)ProjectCrossModuleMutationStatus.Completed && row.CompletedAtUtc is null) {
            throw new InvalidDataException($"Workbench deletion {row.Id} has invalid native state or exceeds its payload bound.");
        }
        _ = new ProjectDeletionRecoveryDigest(row.ClaimDigest);
        var payload = ProjectWorkbenchDeletionPayload.Read(row.PayloadJson, row.CompletionNamespaceId, row.CompletionId);
        if (payload.SourceReference != ProjectAssignmentReference.From(project) ||
                payload.DeletedNodeKeys is null || payload.ManagedStorageObjects is null ||
                payload.CompletionContext is { } bound && !ProjectWorkbenchDeletionPayload.Matches(payload, bound)) {
            throw new InvalidDataException($"Workbench deletion {row.Id} has an invalid original lifetime or completion binding.");
        }
        byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(new {
            protocol = ProtocolVersion, row.Id, row.ProjectId, row.ScopeNodeKey, row.MutationKind, row.Status, row.ApprovalState,
            row.AttemptCount, row.CreatedAtUtc, row.UpdatedAtUtc, row.LastAttemptAtUtc, row.CompletedAtUtc, row.ClaimDigest,
            payload = Convert.ToHexStringLower(SHA256.HashData(StrictUtf8.GetBytes(row.PayloadJson)))
        });
        return new(new(new(Id, row.Id), ProtocolVersion, row.Status == (int)ProjectCrossModuleMutationStatus.Completed,
            new(SHA256.HashData(encoded))), payload.CompletionContext);
    }

    private async Task<T> InOwnerAsync<T>(Func<WorkbenchDbContext, Task<T>> action, CancellationToken cancellationToken) {
        var context = await transactions.CreateEnlistedAsync(options, static configured => new WorkbenchDbContext(configured), cancellationToken);
        Exception? primary = null;
        try {
            transactions.RequireOwnerProfile(context);
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is null) {
                throw new InvalidOperationException("Deletion recovery requires its existing PostgreSQL owner transaction.");
            }
            context.Database.SetCommandTimeout(5);
            return await action(context);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanup) when (primary is not null) {
                throw new AggregateException("Workbench deletion recovery and enlisted-context release both failed.", primary, cleanup);
            }
        }
    }

    private static void RequireMode(ProjectDeletionRecoveryReadMode mode) {
        if (!Enum.IsDefined(mode)) {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static bool IsFinite(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero && value != DateTimeOffset.MinValue && value != DateTimeOffset.MaxValue;

    private sealed class RecoveryRow {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? CompletionNamespaceId { get; set; }
        public Guid? CompletionId { get; set; }
        public string ScopeNodeKey { get; set; } = "";
        public int MutationKind { get; set; }
        public int Status { get; set; }
        public int ApprovalState { get; set; }
        public int AttemptCount { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public DateTimeOffset? LastAttemptAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public string ClaimDigest { get; set; } = "";
        public string? PayloadJson { get; set; }
    }
}
