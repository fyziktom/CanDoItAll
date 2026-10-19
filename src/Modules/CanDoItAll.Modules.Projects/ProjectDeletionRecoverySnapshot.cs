using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectDeletionRecoverySnapshot(ProjectDeletionCompletionBinding Completion,
    ProjectDeletionRecoveryEvidence Evidence, ProjectDeletionCompletionRow Row,
    ImmutableArray<ProjectDeletionRecoveryParticipantEvidence> Participants, ProjectDeletionRecoveryDigest Digest) {
    internal ProjectDeletionRecoveryPlan Plan => new(Completion, Digest, Row.ReleasedAtUtc, Participants);

    internal bool MatchesReleaseIdentity(ProjectDeletionRecoverySnapshot expected)
        => Completion.Original == expected.Completion.Original && MatchesReleaseIdentity(expected.Row);

    internal bool MatchesReleaseIdentity(ProjectDeletionCompletionRow row)
        => Row.CreatedAtUtc == row.CreatedAtUtc && Row.OriginKind == row.OriginKind && Row.SchemaVersion == row.SchemaVersion &&
            row.ReferencePayload is not null && row.RecoveryPayload is not null && row.RecoveryDigest is not null &&
            Row.ReferencePayload!.AsSpan().SequenceEqual(row.ReferencePayload) &&
            Row.RecoveryPayload!.AsSpan().SequenceEqual(row.RecoveryPayload) &&
            Row.RecoveryDigest!.AsSpan().SequenceEqual(row.RecoveryDigest);

    internal static async Task<ProjectDeletionRecoverySnapshot> ReadAsync(ProjectsDbContext context,
        CoordinatedDatabaseTransaction transactions, ProjectDeletionCompletionAddress address,
        DatasetActivationFileObservation activation, ProjectDeletionRecoveryDigest profileIdentity,
        IReadOnlyDictionary<ProjectDeletionParticipantId, IProjectDeletionRecoveryParticipant> registrations,
        ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken) {
        await RequireInstalledProtocolAsync(context, address, cancellationToken);
        var subject = await context.Set<ProjectDeletionCompletionRecord>().AsNoTracking()
            .Where(row => row.NamespaceId == address.Namespace.Value && row.CompletionId == address.Id.Value)
            .Select(row => new { row.DatabaseProfileId, row.ProjectId, row.LifetimeId }).SingleOrDefaultAsync(cancellationToken);
        Require(subject is not null, ProjectDeletionRecoveryRefusal.MissingCompletion);
        Require(subject!.DatabaseProfileId == address.DatabaseProfileId, ProjectDeletionRecoveryRefusal.BindingChanged);
        var original = new ProjectDeletionPreparationContext(address, new(address.DatabaseProfileId, subject.ProjectId, subject.LifetimeId));
        var row = await ProjectDeletionCompletionReader.ReadForRecoveryAsync(context, original, cancellationToken);
        Require(row is not null && row.Matches(activation.Record.Plan.Binding), ProjectDeletionRecoveryRefusal.BindingChanged);
        Require(row!.ReferencePayload is not null && row.RecoveryPayload is not null && row.RecoveryDigest is { Length: 32 } &&
            CryptographicOperations.FixedTimeEquals(SHA256.HashData(row.RecoveryPayload), row.RecoveryDigest),
            ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
        var completion = ProjectDeletionCompletionCodec.Read(row.ReferencePayload!, original);
        Require(row.Interpret(completion) is ProjectDeletionCompletionObservation.AwaitingOwnerRelease or
            ProjectDeletionCompletionObservation.Released, ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
        var evidence = ProjectDeletionRecoveryCodec.Read(row.RecoveryPayload!, original);
        Require((int)ProjectDeletionReleasePersistence.Origin(evidence.Origin) == row.OriginKind,
            ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
        if (evidence.Origin is ProjectDeletionOriginEvidence.Ordinary ordinary) {
            _ = ordinary.RequireOperationKey();
        }
        var preparation = evidence.RequirePreparationParticipants();
        Require(preparation.All(item => registrations.TryGetValue(item.Id, out var registered) &&
            registered.ProtocolVersion == item.ProtocolVersion) &&
            completion.Obligations.All(item => preparation.Any(prepared => prepared.Id == item.Participant)),
            ProjectDeletionRecoveryRefusal.ParticipantMismatch);
        var inspected = ImmutableArray.CreateBuilder<ProjectDeletionRecoveryParticipantEvidence>(completion.Obligations.Count);
        using (transactions.Enter(context)) {
            foreach (var obligation in completion.Obligations.OrderBy(value => value.Participant.Value, StringComparer.Ordinal)) {
                var inspector = registrations[obligation.Participant];
                var participant = await inspector.InspectAsync(original, obligation.RecoveryId, mode, cancellationToken);
                Require(participant.Obligation == obligation && participant.ProtocolVersion == inspector.ProtocolVersion,
                    ProjectDeletionRecoveryRefusal.ParticipantMismatch);
                inspected.Add(participant);
            }
        }
        var participants = inspected.ToImmutable();
        var digest = DigestPlan(row, activation, profileIdentity, participants);
        return new(completion, evidence, row, participants, digest);
    }

    internal static async Task RequireInstalledProtocolAsync(ProjectsDbContext context,
        ProjectDeletionCompletionAddress address, CancellationToken cancellationToken) {
        int? version = await context.Database.SqlQuery<int?>($"""
            SELECT "RetentionProtocolVersion" AS "Value" FROM "Infrastructure_FiniteOperationState"
            WHERE "NamespaceId" = {address.Namespace.Value} AND "PoolId" = {ProjectFiniteOperationPool.Pool.Value}
            """).SingleOrDefaultAsync(cancellationToken);
        Require(version == ProjectFiniteOperationPool.ProtocolVersion, ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
    }

    internal string[] ExclusionKeys(IReadOnlyDictionary<ProjectDeletionParticipantId, IProjectDeletionRecoveryParticipant> registrations) {
        var keys = Evidence.RequiredProjects.Select(project => ProjectMutationScopeKeys.ForProject(project.ProjectId))
            .Append(ProjectMutationScopeKeys.Hierarchy)
            .Concat(Evidence.RequirePreparationParticipants().SelectMany(participant =>
                registrations[participant.Id].PreparationScopeKeys.Select(key => key.Value)));
        if (Evidence.Origin is ProjectDeletionOriginEvidence.Ordinary ordinary) {
            keys = keys.Append(ordinary.RequireOperationKey());
        }
        return keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static ProjectDeletionRecoveryDigest DigestPlan(ProjectDeletionCompletionRow row,
        DatasetActivationFileObservation activation, ProjectDeletionRecoveryDigest profileIdentity,
        ImmutableArray<ProjectDeletionRecoveryParticipantEvidence> participants) {
        byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "projects:deletion-recovery-plan:v1",
            ProfileIdentity = profileIdentity.Hex,
            Activation = activation,
            ProjectFiniteOperationPool.ProtocolVersion,
            row.DatabaseProfileId, row.ActiveNamespaceId, row.CompletionId, row.ProjectId, row.LifetimeId,
            row.SchemaVersion, row.OriginKind, row.CreatedAtUtc, row.ReleaseState, row.ReleasedAtUtc,
            References = Convert.ToHexStringLower(SHA256.HashData(row.ReferencePayload!)),
            Recovery = Convert.ToHexStringLower(row.RecoveryDigest!),
            Participants = participants
        });
        return new(SHA256.HashData(encoded));
    }

    internal static void Require(bool condition, ProjectDeletionRecoveryRefusal reason) {
        if (!condition) {
            throw new ProjectDeletionRecoveryRefusedException(reason);
        }
    }
}
