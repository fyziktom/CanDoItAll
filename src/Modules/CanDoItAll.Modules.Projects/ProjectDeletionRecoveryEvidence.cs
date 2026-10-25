using System.Collections.Immutable;
using System.Text;

namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectDeletionCallerExclusion {
    internal ProjectDeletionCallerExclusion(ProjectMutationCallerKind kind, ProjectCallerKey key) {
        ArgumentNullException.ThrowIfNull(key);
        if (!Enum.IsDefined(kind) || !key.ToArray().AsSpan().ContainsAnyExcept((byte)0) ||
                kind == ProjectMutationCallerKind.OpenApi && key != ProjectMutationCallerScope.OpenApi.Key ||
                kind == ProjectMutationCallerKind.LocalWorkspace && key != ProjectMutationCallerScope.LocalWorkspace.Key) {
            throw new ArgumentException("Deletion recovery requires a supported original caller exclusion identity.");
        }
        Kind = kind;
        Key = key;
    }

    internal ProjectMutationCallerKind Kind { get; }
    internal ProjectCallerKey Key { get; }

    internal string RequireOperationKey(ProjectLifecycleOperation original) {
        if (ProjectMutationCallerScope.BindKey(original.Namespace, Kind, Key) != original.CallerBinding) {
            throw new ArgumentException("Deletion recovery caller exclusion differs from the original submission binding.");
        }
        return ProjectMutationScopeKeys.ForLifecycleOperation(original, Kind, Key);
    }
}

internal abstract record ProjectDeletionOriginEvidence {
    private ProjectDeletionOriginEvidence() {
    }

    internal sealed record Ordinary : ProjectDeletionOriginEvidence {
        internal Ordinary(ProjectLifecycleSubmission submission, ProjectDeletionCallerExclusion? callerExclusion = null) {
            ArgumentNullException.ThrowIfNull(submission);
            if (submission.Command is not ProjectLifecycleCommand.Delete ||
                    submission.Interpretation != ProjectLifecycleInterpretation.VersionOne) {
                throw new ArgumentException("Deletion recovery requires the supported original delete submission.", nameof(submission));
            }
            Submission = submission;
            _ = callerExclusion?.RequireOperationKey(submission.Operation);
            CallerExclusion = callerExclusion;
        }

        internal ProjectLifecycleSubmission Submission { get; }
        internal ProjectDeletionCallerExclusion? CallerExclusion { get; }

        internal string RequireOperationKey()
            => CallerExclusion?.RequireOperationKey(Submission.Operation)
                ?? throw new NotSupportedException("Retained deletion evidence lacks its original caller exclusion identity.");
    }

    internal sealed record Compensation : ProjectDeletionOriginEvidence {
        internal Compensation(ProjectWriteAdmission project, string fingerprint, ProjectCreationReservation? reservation) {
            ArgumentNullException.ThrowIfNull(project);
            RequireDigest(fingerprint);
            if (reservation is not null && (reservation.Id == Guid.Empty || reservation.RequesterId == Guid.Empty ||
                    reservation.DatabaseProfileId != project.DatabaseProfileId || reservation.ProjectId != project.ProjectId ||
                    reservation.LifetimeId != project.LifetimeId ||
                    reservation.ParentProjectId.HasValue != reservation.ParentLifetimeId.HasValue ||
                    reservation.ParentProjectId == Guid.Empty || reservation.ParentLifetimeId == Guid.Empty ||
                    reservation.ParentProjectId == project.ProjectId)) {
                throw new ArgumentException("Compensation recovery requires the exact original creation reservation.", nameof(reservation));
            }
            Project = project;
            Fingerprint = fingerprint;
            Reservation = reservation is null ? null : new(reservation.Id, reservation.DatabaseProfileId,
                reservation.ProjectId, reservation.LifetimeId, reservation.RequesterId, reservation.ParentProjectId, reservation.ParentLifetimeId);
        }

        internal ProjectWriteAdmission Project { get; }
        internal string Fingerprint { get; }
        internal ProjectCreationReservation? Reservation { get; }
    }

    internal sealed record ReservedCancellation : ProjectDeletionOriginEvidence {
        internal ReservedCancellation(ProjectReservationCancellationTarget target) {
            ArgumentNullException.ThrowIfNull(target);
            Target = target;
        }

        internal ProjectReservationCancellationTarget Target { get; }
    }

    internal sealed record LegacyInstallation : ProjectDeletionOriginEvidence {
        internal LegacyInstallation(string installationPlanDigest, ProjectDeletionLegacyPrecondition? precondition = null) {
            RequireDigest(installationPlanDigest);
            if (precondition is not null and not ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped) {
                throw new ArgumentOutOfRangeException(nameof(precondition));
            }
            InstallationPlanDigest = installationPlanDigest;
            Precondition = precondition;
        }

        internal string InstallationPlanDigest { get; }
        internal ProjectDeletionLegacyPrecondition? Precondition { get; }
    }

    private static void RequireDigest(string digest) {
        ArgumentNullException.ThrowIfNull(digest);
        if (digest.Length != 64 || digest.AsSpan().ContainsAnyExcept("0123456789abcdef")) {
            throw new ArgumentException("Recovery evidence requires an exact lowercase SHA-256 digest.", nameof(digest));
        }
    }
}

internal sealed record ProjectDeletionRecoveryEvidence {
    internal const int MaximumProjects = 256;
    internal const int MaximumSourceTextBytes = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal ProjectDeletionRecoveryEvidence(ProjectDeletionPreparationContext original, ProjectDeletionOriginEvidence origin,
        ProjectDeletionSourceRecovery source, IReadOnlyCollection<ProjectWriteAdmission> requiredProjects,
        IReadOnlyCollection<ProjectDeletionPreparationRegistration>? preparationParticipants = null) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(requiredProjects);
        var count = requiredProjects.Count;
        if (count is < 1 or > MaximumProjects) {
            throw new ArgumentException("Deletion recovery requires a bounded complete original project set.", nameof(requiredProjects));
        }
        var captured = requiredProjects.Take(MaximumProjects + 1).ToArray();
        if (captured.Length != count || requiredProjects.Count != count || captured.Any(project => project is null ||
                project.DatabaseProfileId != original.Project.DatabaseProfileId) || !captured.Contains(original.Project) ||
                captured.Select(project => project.ProjectId).Distinct().Count() != count) {
            throw new ArgumentException("Deletion recovery projects must be distinct original lifetimes in the original profile.", nameof(requiredProjects));
        }
        switch (origin) {
            case ProjectDeletionOriginEvidence.Ordinary ordinary when
                    ordinary.Submission.Operation.DatabaseProfileId == original.Completion.DatabaseProfileId &&
                    ordinary.Submission.Operation.Namespace == original.Completion.Namespace &&
                    ((ProjectLifecycleCommand.Delete)ordinary.Submission.Command).Expected.Project == original.Project:
                break;
            case ProjectDeletionOriginEvidence.Compensation compensation when compensation.Project == original.Project &&
                    (compensation.Reservation?.ParentProjectId is not { } parent || captured.Contains(new(
                        original.Project.DatabaseProfileId, parent, compensation.Reservation.ParentLifetimeId!.Value))):
                break;
            case ProjectDeletionOriginEvidence.ReservedCancellation cancellation when cancellation.Target.Project == original.Project &&
                    (cancellation.Target.Parent is null || captured.Contains(cancellation.Target.Parent)):
                break;
            case ProjectDeletionOriginEvidence.LegacyInstallation:
                break;
            default:
                throw new ArgumentException("Deletion recovery origin does not match its original completion and project.", nameof(origin));
        }
        switch (source) {
            case ProjectDeletionSourceRecovery.NoExternalSourceLease:
                break;
            case ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog catalog when
                    catalog.DatabaseProfileId == original.Completion.DatabaseProfileId:
                RequireSourceText(catalog.RootPath);
                RequireSourceText(catalog.HostBindingId);
                break;
            case ProjectDeletionSourceRecovery.CanonicalInvocation invocation when
                    invocation.Catalog.DatabaseProfileId == original.Completion.DatabaseProfileId:
                RequireSourceText(invocation.Catalog.RootPath);
                RequireSourceText(invocation.Catalog.HostBindingId);
                RequireSourceText(invocation.Execution.RootPath);
                RequireSourceText(invocation.Execution.HostBindingId);
                break;
            default:
                throw new ArgumentException("Deletion recovery source is unsupported or belongs to another profile.", nameof(source));
        }
        if (origin is ProjectDeletionOriginEvidence.LegacyInstallation { Precondition: not null } &&
                source is not ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog) {
            throw new ArgumentException("Legacy installation requires its captured canonical organization catalog.", nameof(source));
        }
        Original = original;
        Origin = origin;
        Source = source;
        RequiredProjects = captured.OrderBy(project => project.ProjectId).ToImmutableArray();
        if (preparationParticipants is not null) {
            var registrations = preparationParticipants.Take(ProjectDeletionCompletionBinding.MaximumParticipants + 1).ToArray();
            if (registrations.Length > ProjectDeletionCompletionBinding.MaximumParticipants ||
                registrations.Length != preparationParticipants.Count || registrations.Any(value => value is null) ||
                registrations.Select(value => value.Id).Distinct().Count() != registrations.Length) {
                throw new ArgumentException("Deletion recovery requires the complete distinct preparation registration.");
            }
            PreparationParticipants = registrations.OrderBy(value => value.Id.Value, StringComparer.Ordinal).ToImmutableArray();
        }
    }

    internal ProjectDeletionPreparationContext Original { get; }
    internal ProjectDeletionOriginEvidence Origin { get; }
    internal ProjectDeletionSourceRecovery Source { get; }
    internal ImmutableArray<ProjectWriteAdmission> RequiredProjects { get; }
    internal ImmutableArray<ProjectDeletionPreparationRegistration> PreparationParticipants { get; }

    internal ImmutableArray<ProjectDeletionPreparationRegistration> RequirePreparationParticipants()
        => PreparationParticipants.IsDefault
            ? throw new NotSupportedException("Retained deletion evidence lacks its complete preparation registration.")
            : PreparationParticipants;

    private static void RequireSourceText(string value) {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumSourceTextBytes ||
                StrictUtf8.GetByteCount(value) > MaximumSourceTextBytes) {
            throw new ArgumentException("Deletion recovery source identity exceeds its exact UTF-8 bound.");
        }
    }
}
