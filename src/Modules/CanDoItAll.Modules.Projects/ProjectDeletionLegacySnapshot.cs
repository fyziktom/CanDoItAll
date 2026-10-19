using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Modules.Projects.ProjectDeletionRecoverySnapshot;

namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectDeletionLegacySnapshot(ProjectDeletionPreparationContext Original,
    ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog Source, DatasetActivationFileObservation Activation,
    ImmutableArray<ProjectDeletionLegacyParticipantEvidence> Participants, ProjectDeletionRecoveryDigest Digest,
    ProjectDeletionCompletionRow? InstalledRow) {
    internal ProjectDeletionLegacyPlan Plan => new(Original, Digest, InstalledRow is not null,
        Participants.Select(item => item.State).ToImmutableArray());
    internal ProjectDeletionCompletionBinding Completion => new(Original, Participants.Select(item => item.State.Obligation).ToArray());

    internal ProjectDeletionRecoveryEvidence Evidence(IReadOnlyCollection<IProjectDeletionLegacyParticipant> registrations)
        => new(Original, new ProjectDeletionOriginEvidence.LegacyInstallation(Digest.Hex,
            ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped), Source, [Original.Project],
            Preparation(registrations));

    internal static async Task<ProjectDeletionLegacySnapshot> ReadAsync(ProjectsDbContext context,
        CoordinatedDatabaseTransaction transactions, ProjectDeletionPreparationContext original,
        ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog source, DatasetActivationFileObservation activation,
        ProjectDeletionRecoveryDigest profileIdentity, IReadOnlyCollection<IProjectDeletionLegacyParticipant> registrations,
        ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken) {
        await RequireInstalledProtocolAsync(context, original.Completion, cancellationToken);
        var row = await ProjectDeletionCompletionReader.ReadForRecoveryAsync(context, original, cancellationToken);
        Require(row is not null && row.Matches(activation.Record.Plan.Binding), ProjectDeletionRecoveryRefusal.BindingChanged);
        Require(!await context.Set<Project>().AnyAsync(project => project.Id == original.Project.ProjectId, cancellationToken),
            ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
        var retirement = await context.Set<ProjectRetirementRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.LifetimeId == original.Project.LifetimeId, cancellationToken);
        Require(retirement is not null && retirement.ProjectId == original.Project.ProjectId && retirement.ImportedHistory is null &&
            retirement.RetiredAtUtc.Offset == TimeSpan.Zero && retirement.RetiredAtUtc > DateTimeOffset.MinValue &&
            retirement.RetiredAtUtc < DateTimeOffset.MaxValue, ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
        var captured = ImmutableArray.CreateBuilder<ProjectDeletionLegacyParticipantEvidence>();
        using (transactions.Enter(context)) {
            foreach (var registered in registrations.OrderBy(item => item.Id.Value, StringComparer.Ordinal)) {
                var found = await registered.InspectLegacyAsync(original.Project, mode, cancellationToken);
                var items = found.Take(ProjectDeletionCompletionBinding.MaximumParticipants + 1).ToArray();
                Require(items.Length == found.Count && items.Length <= 1 && items.All(item => item is not null &&
                    item.State.Obligation.Participant == registered.Id && item.State.ProtocolVersion == registered.ProtocolVersion),
                    ProjectDeletionRecoveryRefusal.ParticipantMismatch);
                captured.AddRange(items);
            }
        }
        Require(captured.Count is > 0 and <= ProjectDeletionCompletionBinding.MaximumParticipants,
            ProjectDeletionRecoveryRefusal.ParticipantMismatch);
        var participants = captured.ToImmutable();
        var completion = new ProjectDeletionCompletionBinding(original, participants.Select(item => item.State.Obligation).ToArray());
        if (row!.CompletionId is not null) {
            Require(participants.All(item => item.Binding == original) &&
                row.Interpret(completion) is ProjectDeletionCompletionObservation.AwaitingOwnerRelease or ProjectDeletionCompletionObservation.Released &&
                row.OriginKind == (int)ProjectDeletionCompletionOrigin.LegacyInstallation &&
                row.RecoveryPayload is not null && row.RecoveryDigest is { Length: 32 } &&
                CryptographicOperations.FixedTimeEquals(SHA256.HashData(row.RecoveryPayload), row.RecoveryDigest),
                ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
            var evidence = ProjectDeletionRecoveryCodec.Read(row.RecoveryPayload!, original);
            Require(evidence.Origin is ProjectDeletionOriginEvidence.LegacyInstallation {
                    Precondition: ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped } &&
                evidence.Source == source && evidence.RequiredProjects.SequenceEqual([original.Project]) &&
                evidence.RequirePreparationParticipants().SequenceEqual(Preparation(registrations)),
                ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
            var legacy = (ProjectDeletionOriginEvidence.LegacyInstallation)evidence.Origin;
            return new(original, source, activation, participants, new(legacy.InstallationPlanDigest), row);
        }
        Require(participants.All(item => item.Binding is null) && participants.Any(item => !item.State.Completed),
            ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
        Require(!await context.Set<ProjectDeletionCompletionRecord>().AnyAsync(item =>
            item.NamespaceId == original.Completion.Namespace.Value && item.ProjectId == original.Project.ProjectId &&
            item.LifetimeId == original.Project.LifetimeId, cancellationToken), ProjectDeletionRecoveryRefusal.PlanChanged);
        byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "projects:legacy-deletion-installation:v1", original, activation, profileIdentity, source,
            ProjectFiniteOperationPool.ProtocolVersion, retirement!.RetiredAtUtc,
            Precondition = ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped,
            Preparation = Preparation(registrations), Participants = participants.Select(item => item.State).ToArray()
        });
        Require(encoded.Length <= ProjectDeletionRecoveryCodec.MaximumEncodedBytes, ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
        var snapshot = new ProjectDeletionLegacySnapshot(original, source, activation, participants, new(SHA256.HashData(encoded)), null);
        _ = ProjectDeletionRecoveryCodec.Write(snapshot.Evidence(registrations));
        _ = ProjectDeletionCompletionCodec.Write(completion);
        return snapshot;
    }

    internal bool MatchesInstalled(ProjectDeletionLegacySnapshot expected)
        => InstalledRow is not null && Original == expected.Original && Digest == expected.Digest &&
            Source == expected.Source && Completion.Obligations.SequenceEqual(expected.Completion.Obligations) &&
            Participants.All(item => item.Binding == Original);

    private static ProjectDeletionPreparationRegistration[] Preparation(IReadOnlyCollection<IProjectDeletionLegacyParticipant> registrations)
        => registrations.OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .Select(item => new ProjectDeletionPreparationRegistration(item.Id, item.ProtocolVersion)).ToArray();
}
