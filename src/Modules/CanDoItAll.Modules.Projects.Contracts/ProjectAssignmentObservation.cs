using System.Collections.Immutable;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectAssignmentObservation(
    Guid Id,
    Guid ProjectId,
    Guid? ProjectLifetimeId,
    Guid PartyId,
    Guid? PartyAffiliationId,
    ProjectPartyAssignmentRole Role,
    string NodeKey,
    string PhaseName,
    Guid? OpportunityId,
    bool IsPrimary,
    decimal? AllocationPercent,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string Source,
    string Notes);

public sealed record ProjectAssignmentPrimaryObservation(
    Guid ProjectId,
    Guid ProjectLifetimeId,
    ProjectPartyAssignmentRole Role,
    string NodeKey,
    ImmutableArray<Guid> PrimaryIds) {
    public bool Matches(ProjectWriteAdmission project, ProjectPartyAssignmentRole role, string nodeKey,
        IEnumerable<Guid> currentPrimaryIds) =>
        ProjectId == project.ProjectId && ProjectLifetimeId == project.LifetimeId && Role == role && NodeKey == nodeKey &&
        !PrimaryIds.IsDefault && PrimaryIds.AsSpan().SequenceEqual(currentPrimaryIds.Distinct().Order().ToArray());
}

public static class ProjectAssignmentEditingErrors {
    public const string OriginalRequired = "projects.assignment.original-required";
    public const string IdentityInvalid = "projects.assignment.identity-invalid";
    public const string Conflict = "projects.assignment.conflict";
    public const string Duplicate = "projects.assignment.duplicate";
    public const string PrimaryConflict = "projects.assignment.primary-conflict";

    public static Error? ValidateOriginal(ProjectPartyAssignmentUpsertRequest request) => request switch {
        { AssignmentId: not null, Original: null } => Error.Validation(
            "Read the assignment before replacing it.", OriginalRequired),
        { Original: { } original } when original.Id == Guid.Empty || request.AssignmentId != original.Id ||
            request.ProjectId != original.ProjectId || request.ExpectedProjectAdmission?.LifetimeId != original.ProjectLifetimeId =>
            Error.Validation("The assignment identity or project differs from its shown original.", IdentityInvalid),
        _ => null
    };

    public static bool RequiresPrimaryObservation(ProjectPartyAssignmentUpsertRequest request, string nodeKey) =>
        request.IsPrimary && (request.Original is not { IsPrimary: true } original ||
            original.Role != request.Role || original.NodeKey != nodeKey);

    public static Error Stale() => Error.Failure("The assignment changed. Refresh its displayed row before trying again.", Conflict);

    public static Error DuplicateAssignment() => Error.Validation(
        "This party already has an assignment in that role and scope. Refresh the assignments.", Duplicate);

    public static Error StalePrimary() => Error.Failure(
        "The primary assignment changed. Review the current primary before promoting another assignment.", PrimaryConflict);
}

public sealed class ProjectAssignmentConflictException(Exception? inner = null)
    : InvalidOperationException("The assignment changed. Refresh its displayed row before trying again.", inner);
