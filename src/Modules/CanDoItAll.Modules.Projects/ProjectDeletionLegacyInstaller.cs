using System.Collections.Immutable;
using System.Data;
using System.Security.Cryptography;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static CanDoItAll.Modules.Projects.ProjectDeletionRecoveryResources;
using static CanDoItAll.Modules.Projects.ProjectDeletionRecoverySnapshot;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDeletionLegacyInstaller {
    private readonly Guid profileId;
    private readonly DbContextOptions<ProjectsDbContext> options;
    private readonly CoordinatedDatabaseTransaction transactions;
    private readonly DatasetActivationService activation;
    private readonly IProjectDeletionRecoveryEnvironment environment;
    private readonly Func<ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog> sourceFactory;
    private readonly ImmutableArray<IProjectDeletionLegacyParticipant> participants;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public ProjectDeletionLegacyInstaller(ResolvedDatabaseProfile profile, DbContextOptions<ProjectsDbContext> options,
        CoordinatedDatabaseTransaction transactions, DatasetActivationService activation, IProjectDeletionRecoveryEnvironment environment,
        Func<ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog> sourceFactory,
        IReadOnlyCollection<IProjectDeletionLegacyParticipant> participants, TimeProvider clock, ILogger logger) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(participants);
        if (profile.Profile.Id == Guid.Empty || profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql ||
                participants.Count is < 1 or > ProjectDeletionCompletionBinding.MaximumParticipants) {
            throw new ArgumentException("Legacy installation requires an explicit PostgreSQL profile and bounded native owners.");
        }
        this.participants = participants.Take(ProjectDeletionCompletionBinding.MaximumParticipants + 1).ToImmutableArray();
        if (this.participants.Length != participants.Count || this.participants.Any(item => item is null || item.ProtocolVersion <= 0 ||
                string.IsNullOrWhiteSpace(item.Id.Value) || item.PreparationScopeKeys.Count > 16 ||
                item.PreparationScopeKeys.Any(key => string.IsNullOrWhiteSpace(key.Value) || key.Value.Length > 256)) ||
                this.participants.Select(item => item.Id).Distinct().Count() != participants.Count) {
            throw new ArgumentException("Legacy installation requires distinct compiled native owners.");
        }
        profileId = profile.Profile.Id;
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        this.activation = activation ?? throw new ArgumentNullException(nameof(activation));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProjectDeletionRecoveryResult> InspectAsync(ProjectDeletionPreparationContext original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        try {
            return new ProjectDeletionRecoveryResult.LegacyObserved((await ObserveAsync(original, cancellationToken)).Plan);
        } catch (Exception failure) {
            return new ProjectDeletionRecoveryResult.Refused(original.Completion, Reason(failure), !Log(original, failure));
        }
    }

    public async Task<ProjectDeletionRecoveryResult> InstallAsync(ProjectDeletionPreparationContext original,
        ProjectDeletionRecoveryDigest expectedPlan, ProjectDeletionLegacyPrecondition precondition,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(expectedPlan);
        if (precondition != ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped) {
            throw new ArgumentException("Legacy installation requires stopped writers and termination of all former source-owner hosts.", nameof(precondition));
        }
        ProjectDeletionLegacySnapshot observed;
        try {
            observed = await ObserveAsync(original, cancellationToken);
            Require(observed.Digest == expectedPlan, ProjectDeletionRecoveryRefusal.PlanChanged);
            if (observed.InstalledRow is not null) {
                return Installed(observed, false, false);
            }
        } catch (Exception failure) {
            return new ProjectDeletionRecoveryResult.Refused(original.Completion, Reason(failure), !Log(original, failure));
        }
        IAsyncDisposable? selection = null;
        IAsyncDisposable? source = null;
        ProjectDeletionRecoveryTransaction? sql = null;
        ProjectDeletionLegacySnapshot? current = null;
        Exception? failureBeforeCleanup = null;
        try {
            using var deadline = Deadline(cancellationToken);
            await environment.RequireOriginalProfileAsync(deadline.Token);
            selection = await activation.AcquireExistingExclusionAsync(profileId, deadline.Token);
            await RequireSameEnvironmentAsync(observed, deadline.Token);
            source = await environment.AcquireSourceExclusionAsync(observed.Source, deadline.Token)
                ?? throw new InvalidOperationException("Legacy installation source returned no owned exclusion.");
            sql = await ProjectDeletionRecoveryTransaction.OpenAsync(options, transactions, IsolationLevel.ReadCommitted, deadline.Token);
            await DatasetLifecycleStore.RequireActiveForDeletionRecoveryAsync(sql.Context, observed.Activation.Record.Plan.Binding, deadline.Token);
            string[] keys = participants.SelectMany(item => item.PreparationScopeKeys.Select(key => key.Value))
                .Append(ProjectMutationScopeKeys.ForProject(original.Project.ProjectId)).Append(ProjectMutationScopeKeys.Hierarchy)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            Require(await SerializableMutationScope.TryAcquireRelationalScopeLocksAsync(sql.Context, keys, deadline.Token),
                ProjectDeletionRecoveryRefusal.ExclusionBusy);
            current = await ProjectDeletionLegacySnapshot.ReadAsync(sql.Context, transactions, original, observed.Source,
                observed.Activation, environment.ProfileIdentity, participants, ProjectDeletionRecoveryReadMode.Exclusive, deadline.Token);
            Require(current.Digest == expectedPlan, ProjectDeletionRecoveryRefusal.PlanChanged);
            if (current.InstalledRow is null) {
                await StageAsync(sql.Context, current, deadline.Token);
                using (transactions.Enter(sql.Context)) {
                    foreach (var item in current.Participants) {
                        await participants.Single(owner => owner.Id == item.State.Obligation.Participant)
                            .BindLegacyAsync(original, item, deadline.Token);
                    }
                }
                await sql.CommitAsync(deadline.Token);
            }
        } catch (Exception primary) {
            failureBeforeCleanup = primary;
        }
        var closure = await ReleaseAsync(failureBeforeCleanup, sql, source, selection);
        bool diagnosticsUnavailable = closure.Failure is not null && !Log(original, closure.Failure);
        if (sql?.CommitAcknowledged == true) {
            return new ProjectDeletionRecoveryResult.InstalledAwaitingRelease(original, expectedPlan, closure.CleanupFailed, diagnosticsUnavailable);
        }
        if (current?.InstalledRow is not null && current.Digest == expectedPlan) {
            return Installed(current, closure.CleanupFailed, diagnosticsUnavailable);
        }
        if (sql?.CommitAttempted == true) {
            try {
                var recovered = await ObserveAsync(original, CancellationToken.None);
                if (recovered.MatchesInstalled(observed)) {
                    return Installed(recovered, closure.CleanupFailed, diagnosticsUnavailable);
                }
            } catch (Exception observationFailure) {
                diagnosticsUnavailable |= !Log(original, observationFailure);
            }
            return new ProjectDeletionRecoveryResult.Unconfirmed(original.Completion, closure.CleanupFailed, diagnosticsUnavailable);
        }
        return new ProjectDeletionRecoveryResult.Refused(original.Completion, closure.CleanupFailed
            ? ProjectDeletionRecoveryRefusal.ProbeReleaseUnconfirmed : Reason(closure.Failure!), diagnosticsUnavailable);
    }

    private async Task<ProjectDeletionLegacySnapshot> ObserveAsync(ProjectDeletionPreparationContext original, CancellationToken cancellationToken) {
        Require(original.Completion.DatabaseProfileId == profileId, ProjectDeletionRecoveryRefusal.BindingChanged);
        using var deadline = Deadline(cancellationToken);
        await environment.RequireOriginalProfileAsync(deadline.Token);
        var source = sourceFactory() ?? throw new InvalidOperationException("Legacy installation requires an existing canonical organization catalog.");
        Require(source.DatabaseProfileId == profileId, ProjectDeletionRecoveryRefusal.BindingChanged);
        await environment.RequireOriginalSourceAsync(source, deadline.Token);
        var selected = await activation.ObserveActiveBindingAsync(profileId, deadline.Token);
        Require(selected.Record.Plan.Binding.Namespace == original.Completion.Namespace, ProjectDeletionRecoveryRefusal.BindingChanged);
        ProjectDeletionRecoveryTransaction? sql = null;
        ProjectDeletionLegacySnapshot? snapshot = null;
        Exception? failure = null;
        try {
            sql = await ProjectDeletionRecoveryTransaction.OpenAsync(options, transactions, IsolationLevel.RepeatableRead, deadline.Token);
            snapshot = await ProjectDeletionLegacySnapshot.ReadAsync(sql.Context, transactions, original, source, selected,
                environment.ProfileIdentity, participants, ProjectDeletionRecoveryReadMode.Observe, deadline.Token);
        } catch (Exception primary) {
            failure = primary;
        }
        ThrowAfterRelease(await ReleaseAsync(failure, sql));
        await RequireSameEnvironmentAsync(snapshot!, deadline.Token);
        return snapshot!;
    }

    private async Task StageAsync(ProjectsDbContext context, ProjectDeletionLegacySnapshot snapshot, CancellationToken cancellationToken) {
        var now = clock.GetUtcNow();
        Require(now.Offset == TimeSpan.Zero && now > DateTimeOffset.MinValue && now < DateTimeOffset.MaxValue,
            ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
        var original = snapshot.Original;
        byte[] recovery = ProjectDeletionRecoveryCodec.Write(snapshot.Evidence(participants));
        context.Add(new ProjectDeletionCompletionRecord {
            NamespaceId = original.Completion.Namespace.Value, CompletionId = original.Completion.Id.Value,
            DatabaseProfileId = profileId, ProjectId = original.Project.ProjectId, LifetimeId = original.Project.LifetimeId,
            SchemaVersion = ProjectDeletionCompletionProtocol.SchemaVersion, OriginKind = ProjectDeletionCompletionOrigin.LegacyInstallation,
            CreatedAtUtc = now, ReleaseState = ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease,
            ReferencePayload = ProjectDeletionCompletionCodec.Write(snapshot.Completion), RecoveryPayload = recovery,
            RecoveryDigest = SHA256.HashData(recovery)
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireSameEnvironmentAsync(ProjectDeletionLegacySnapshot original, CancellationToken cancellationToken) {
        await environment.RequireOriginalProfileAsync(cancellationToken);
        await environment.RequireOriginalSourceAsync(original.Source, cancellationToken);
        Require(sourceFactory() == original.Source &&
            await activation.ObserveActiveBindingAsync(profileId, cancellationToken) == original.Activation,
            ProjectDeletionRecoveryRefusal.BindingChanged);
    }

    private static ProjectDeletionRecoveryResult Installed(ProjectDeletionLegacySnapshot snapshot, bool cleanupUnconfirmed, bool diagnosticsUnavailable)
        => snapshot.InstalledRow!.ReleasedAtUtc is { } released
            ? new ProjectDeletionRecoveryResult.Released(snapshot.Original.Completion, released, cleanupUnconfirmed, diagnosticsUnavailable)
            : new ProjectDeletionRecoveryResult.InstalledAwaitingRelease(snapshot.Original, snapshot.Digest, cleanupUnconfirmed, diagnosticsUnavailable);

    private bool Log(ProjectDeletionPreparationContext original, Exception failure) {
        try {
            logger.LogError("Legacy deletion installation {CompletionId}, project {ProjectId}, profile {ProfileId}, dataset {NamespaceId} failed; type {FailureType}.",
                original.Completion.Id.Value, original.Project.ProjectId, profileId, original.Completion.Namespace.Value, failure.GetType().FullName);
            return true;
        } catch {
            return false;
        }
    }
}
