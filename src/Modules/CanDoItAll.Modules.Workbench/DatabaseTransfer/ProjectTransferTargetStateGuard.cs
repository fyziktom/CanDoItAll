using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectTransferTargetResidue(
    ProjectTransferTargetStateArea Area,
    string Description);

public sealed class ProjectTransferTargetStateGuard {
    private static readonly ProjectTransferTargetStateArea[] RequiredAreas =
        Enum.GetValues<ProjectTransferTargetStateArea>();
    private readonly IReadOnlyList<IProjectTransferTargetStateParticipant>
        participants;

    private readonly DatabaseTransferOperationRunner operations;

    public ProjectTransferTargetStateGuard(
        IEnumerable<IProjectTransferTargetStateParticipant> participants,
        DatabaseTransferOperationRunner operations) {
        ArgumentNullException.ThrowIfNull(participants);
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));

        var supplied = participants.ToArray();
        var duplicateAreas = supplied
            .GroupBy(participant => participant.Area)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToArray();
        if (duplicateAreas.Length > 0) {
            throw new InvalidOperationException(
                $"Project transfer target-state participants are duplicated for: {string.Join(", ", duplicateAreas)}.");
        }

        var participantsByArea = supplied.ToDictionary(
            participant => participant.Area);
        var missingAreas = RequiredAreas
            .Where(area => !participantsByArea.ContainsKey(area))
            .ToArray();
        if (missingAreas.Length > 0) {
            throw new InvalidOperationException(
                $"Project transfer target-state participants are missing for: {string.Join(", ", missingAreas)}.");
        }

        this.participants = RequiredAreas
            .Select(area => participantsByArea[area])
            .ToArray();
    }

    internal Task<IReadOnlyList<ProjectTransferTargetResidue>> FindPreflightResiduesAsync(
        DatabaseTransferProfileSession session, CancellationToken cancellationToken) {
        if (session.Mode != DatabaseTransferProfileMode.Independent) {
            throw new InvalidOperationException("Project transfer preflight requires the explicit independent target session.");
        }
        return FindResiduesAsync(session, cancellationToken);
    }

    internal Task<IReadOnlyList<ProjectTransferTargetResidue>> FindLockedResiduesAsync(
        DatabaseTransferProfileSession session, CancellationToken cancellationToken) {
        if (session.Mode != DatabaseTransferProfileMode.Serializable) {
            throw new InvalidOperationException("Project transfer final inspection requires the exact locked target session.");
        }
        return FindResiduesAsync(session, cancellationToken);
    }

    private Task<IReadOnlyList<ProjectTransferTargetResidue>> FindResiduesAsync(DatabaseTransferProfileSession session,
        CancellationToken cancellationToken)
        => operations.InspectTargetAsync<IReadOnlyList<ProjectTransferTargetResidue>>(session, async (request, token) => {
            var residues = new List<ProjectTransferTargetResidue>();
            foreach (var participant in participants) {
                var found = await participant.FindResiduesAsync(request, token);
                residues.AddRange(found.Select(residue => new ProjectTransferTargetResidue(participant.Area, residue.Description)));
            }
            return residues;
        }, cancellationToken);

    internal IReadOnlyCollection<Type> EntityTypesToLock => participants.SelectMany(participant => participant.EntityTypesToLock).Distinct().ToArray();

    internal Task<TResult> RunLockedImportAsync<TResult>(ResolvedDatabaseProfile profile,
        Func<DatabaseTransferProfileSession, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
        => operations.RunExclusiveImportAsync(profile, [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey],
            EntityTypesToLock, operation, cancellationToken);

    internal static string Describe(
        IReadOnlyCollection<ProjectTransferTargetResidue> residues)
        => string.Join(
            ", ",
            residues
                .Select(residue => residue.Description)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));

}
