using System.Collections.Immutable;

namespace CanDoItAll.Modules.Projects;

public enum ProjectWorkPartyMergeMember { Assignment, WorkItem }

public sealed record ProjectWorkPartyMergeVersion(ProjectWorkPartyMergeMember Member, Guid Id, Guid ProjectId,
    Guid? ProjectLifetimeId, string NodeKey, long Version);

public sealed record ProjectWorkPartyMergeObservation(ImmutableArray<ProjectWorkPartyMergeVersion> Rows) {
    public bool Matches(ProjectWorkPartyMergeObservation current) => !Rows.IsDefault && Rows.SequenceEqual(current.Rows);
}
