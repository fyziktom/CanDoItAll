using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentProjectDeletionRecoveryInspector(DbContextOptions<AgentProjectAccessDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IProjectDeletionLegacyParticipant {
    public ProjectDeletionParticipantId Id { get; } = new(AgentProjectStructureAccessDeletionParticipant.ParticipantIdValue);
    public int ProtocolVersion => AgentProjectDeletionBinding.ProtocolVersion;
    public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys => [];

    public Task<ProjectDeletionRecoveryParticipantEvidence> InspectAsync(ProjectDeletionPreparationContext original,
        Guid recoveryId, ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken = default)
        => InOwnerAsync(async context => {
            var row = (await ReadAsync(context, original.Project, recoveryId, mode, cancellationToken)).Single();
            var evidence = Evidence(row, original.Project);
            if (evidence.Binding != original) {
                throw new InvalidDataException($"Agent deletion {row.Id} has a different original completion binding.");
            }
            return evidence.State;
        }, cancellationToken);

    public Task<IReadOnlyList<ProjectDeletionLegacyParticipantEvidence>> InspectLegacyAsync(ProjectWriteAdmission project,
        ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken = default)
        => InOwnerAsync<IReadOnlyList<ProjectDeletionLegacyParticipantEvidence>>(async context =>
            (await ReadAsync(context, project, null, mode, cancellationToken)).Select(row => Evidence(row, project)).ToArray(), cancellationToken);

    public async Task BindLegacyAsync(ProjectDeletionPreparationContext original, ProjectDeletionLegacyParticipantEvidence expected,
        CancellationToken cancellationToken = default) {
        await InOwnerAsync(async context => {
            var row = (await ReadAsync(context, original.Project, expected.State.Obligation.RecoveryId,
                ProjectDeletionRecoveryReadMode.Exclusive, cancellationToken)).Single();
            var actual = Evidence(row, original.Project);
            if (expected.Binding is not null || actual != expected || expected.State.Obligation.Participant != Id) {
                throw new InvalidDataException($"Agent deletion {row.Id} changed before legacy binding.");
            }
            int changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "AgentFramework_ProjectAccessRevocations"
                SET "CompletionNamespaceId" = {original.Completion.Namespace.Value}, "CompletionId" = {original.Completion.Id.Value}
                WHERE "Id" = {row.Id} AND "CompletionNamespaceId" IS NULL AND "CompletionId" IS NULL
                """, cancellationToken);
            if (changed != 1) {
                throw new InvalidDataException($"Agent deletion {row.Id} could not be bound exactly once.");
            }
            return changed;
        }, cancellationToken);
    }

    private static async Task<AgentProjectStructureAccessRevocationRecord[]> ReadAsync(AgentProjectAccessDbContext context,
        ProjectWriteAdmission project, Guid? recoveryId, ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(project);
        if (!Enum.IsDefined(mode) || recoveryId == Guid.Empty) {
            throw new ArgumentException("Agent deletion inspection requires a supported mode and exact nonempty identity.");
        }
        string identity = recoveryId is null ? "" : "AND \"Id\" = {1}";
        string query = $$"""
            SELECT "Id", "ProjectId", "DatabaseProfileId", "ProjectLifetimeId", "CompletionNamespaceId", "CompletionId",
                "Status", "CreatedAtUtc", "UpdatedAtUtc", "AttemptCount", "LastAttemptAtUtc", "CompletedAtUtc", "LastFailureCode"
            FROM "AgentFramework_ProjectAccessRevocations" WHERE "ProjectId" = {0} {{identity}}
            ORDER BY "ProjectLifetimeId" LIMIT {{ProjectDeletionCompletionBinding.MaximumParticipants + 1}}
            """;
        if (mode == ProjectDeletionRecoveryReadMode.Exclusive) {
            query += " FOR UPDATE NOWAIT";
        }
        var rows = await context.Set<AgentProjectStructureAccessRevocationRecord>().FromSqlRaw(query,
            recoveryId is { } id ? [project.ProjectId, id] : [project.ProjectId]).AsNoTracking().ToArrayAsync(cancellationToken);
        if (rows.Length > ProjectDeletionCompletionBinding.MaximumParticipants || recoveryId is not null && rows.Length != 1) {
            throw new InvalidDataException($"Agent deletion inspection for project {project.ProjectId} has missing or excessive native records.");
        }
        return rows;
    }

    private ProjectDeletionLegacyParticipantEvidence Evidence(AgentProjectStructureAccessRevocationRecord record, ProjectWriteAdmission project) {
        if (record.Id == Guid.Empty || record.ProjectId != project.ProjectId || record.DatabaseProfileId != project.DatabaseProfileId ||
                record.ProjectLifetimeId != project.LifetimeId || !Enum.IsDefined(record.Status) || record.AttemptCount < 0 ||
                !IsFinite(record.CreatedAtUtc) || !IsFinite(record.UpdatedAtUtc) || record.LastFailureCode?.Length > 256 ||
                record.LastAttemptAtUtc is { } attempted && !IsFinite(attempted) ||
                record.CompletedAtUtc is { } completed && !IsFinite(completed) ||
                record.Status == AgentProjectStructureAccessRevocationStatus.Completed && record.CompletedAtUtc is null) {
            throw new InvalidDataException($"Agent deletion {record.Id} has an invalid original lifetime or native state.");
        }
        var binding = AgentProjectDeletionBinding.Read(record, project.DatabaseProfileId);
        byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(new {
            protocol = ProtocolVersion, record.Id, record.ProjectId, record.DatabaseProfileId, record.ProjectLifetimeId,
            record.Status, record.CreatedAtUtc, record.UpdatedAtUtc, record.AttemptCount, record.LastAttemptAtUtc,
            record.CompletedAtUtc, record.LastFailureCode
        });
        return new(new(new(Id, record.Id), ProtocolVersion,
            record.Status == AgentProjectStructureAccessRevocationStatus.Completed, new(SHA256.HashData(encoded))), binding);
    }

    private async Task<T> InOwnerAsync<T>(Func<AgentProjectAccessDbContext, Task<T>> action, CancellationToken cancellationToken) {
        var context = await transactions.CreateEnlistedAsync(options, static configured => new AgentProjectAccessDbContext(configured), cancellationToken);
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
                throw new AggregateException("Agent deletion recovery and enlisted-context release both failed.", primary, cleanup);
            }
        }
    }

    private static bool IsFinite(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero && value != DateTimeOffset.MinValue && value != DateTimeOffset.MaxValue;
}
