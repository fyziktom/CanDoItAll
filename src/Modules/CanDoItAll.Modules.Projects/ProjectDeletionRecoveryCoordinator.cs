using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static CanDoItAll.Modules.Projects.ProjectDeletionRecoveryResources;
using static CanDoItAll.Modules.Projects.ProjectDeletionRecoverySnapshot;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDeletionRecoveryCoordinator {
    private readonly Guid profileId;
    private readonly DbContextOptions<ProjectsDbContext> options;
    private readonly CoordinatedDatabaseTransaction transactions;
    private readonly DatasetActivationService activation;
    private readonly IProjectDeletionRecoveryEnvironment environment;
    private readonly IReadOnlyDictionary<ProjectDeletionParticipantId, IProjectDeletionRecoveryParticipant> participants;
    private readonly TimeProvider clock;
    private readonly ILogger<ProjectDeletionRecoveryCoordinator> logger;

    public ProjectDeletionRecoveryCoordinator(ResolvedDatabaseProfile profile, DbContextOptions<ProjectsDbContext> options,
        CoordinatedDatabaseTransaction transactions, DatasetActivationService activation,
        IProjectDeletionRecoveryEnvironment environment, IReadOnlyCollection<IProjectDeletionRecoveryParticipant> participants,
        TimeProvider clock, ILogger<ProjectDeletionRecoveryCoordinator> logger) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(participants);
        if (profile.Profile.Id == Guid.Empty || profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql ||
            participants.Count > ProjectDeletionCompletionBinding.MaximumParticipants) {
            throw new ArgumentException("Recovery requires an explicit PostgreSQL profile and bounded participant registry.");
        }
        var registered = participants.ToArray();
        if (registered.Length != participants.Count || registered.Any(value => value is null || value.ProtocolVersion <= 0 ||
            string.IsNullOrWhiteSpace(value.Id.Value) || value.PreparationScopeKeys.Count > 16 ||
            value.PreparationScopeKeys.Any(key => string.IsNullOrWhiteSpace(key.Value) || key.Value.Length > 256)) ||
            registered.Select(value => value.Id).Distinct().Count() != registered.Length) {
            throw new ArgumentException("Recovery requires exact compiled participant registrations.");
        }
        profileId = profile.Profile.Id;
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        this.activation = activation ?? throw new ArgumentNullException(nameof(activation));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.participants = registered.ToDictionary(value => value.Id);
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProjectDeletionRecoveryResult> InspectAsync(ProjectDeletionCompletionAddress address,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(address);
        try {
            var observed = await ObserveAsync(address, cancellationToken);
            return new ProjectDeletionRecoveryResult.Observed(observed.Snapshot.Plan);
        } catch (Exception failure) {
            return Refused(address, failure);
        }
    }

    public async Task<ProjectDeletionRecoveryResult> ReconcileAsync(ProjectDeletionCompletionAddress address,
        ProjectDeletionRecoveryDigest expectedPlan, ProjectDeletionRecoveryPrecondition precondition,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(expectedPlan);
        if (precondition != ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped) {
            throw new ArgumentException("Offline release requires every writer host to remain stopped through terminal cleanup.", nameof(precondition));
        }
        ReleasedProbe released;
        try {
            var observed = await ObserveAsync(address, cancellationToken);
            if (observed.Snapshot.Row.ReleasedAtUtc is { } alreadyReleased) {
                return new ProjectDeletionRecoveryResult.Released(address, alreadyReleased);
            }
            Require(observed.Snapshot.Digest == expectedPlan, ProjectDeletionRecoveryRefusal.PlanChanged);
            released = await ProbeAsync(observed, cancellationToken);
            if (released.Observed.Snapshot.Row.ReleasedAtUtc is { } releasedDuringProbe) {
                return new ProjectDeletionRecoveryResult.Released(address, releasedDuringProbe);
            }
        } catch (Exception failure) {
            return Refused(address, failure);
        }
        return await MarkAsync(released, cancellationToken);
    }

    private async Task<ObservedCompletion> ObserveAsync(ProjectDeletionCompletionAddress address, CancellationToken cancellationToken) {
        Require(address.DatabaseProfileId == profileId, ProjectDeletionRecoveryRefusal.BindingChanged);
        using var deadline = Deadline(cancellationToken);
        await environment.RequireOriginalProfileAsync(deadline.Token);
        var before = await activation.ObserveActiveBindingAsync(profileId, deadline.Token);
        Require(before.Record.Plan.Binding.Namespace == address.Namespace, ProjectDeletionRecoveryRefusal.BindingChanged);
        ProjectDeletionRecoveryTransaction? sql = null;
        ProjectDeletionRecoverySnapshot? snapshot = null;
        Exception? failure = null;
        try {
            sql = await ProjectDeletionRecoveryTransaction.OpenAsync(options, transactions, IsolationLevel.RepeatableRead, deadline.Token);
            snapshot = await ProjectDeletionRecoverySnapshot.ReadAsync(sql.Context, transactions, address, before,
                environment.ProfileIdentity, participants, ProjectDeletionRecoveryReadMode.Observe, deadline.Token);
        } catch (Exception primary) {
            failure = primary;
        }
        ThrowAfterRelease(await ReleaseAsync(failure, sql));
        await RequireSameEnvironmentAsync(before, snapshot!.Evidence.Source, deadline.Token);
        return new(snapshot, before);
    }

    private async Task<ReleasedProbe> ProbeAsync(ObservedCompletion expected, CancellationToken cancellationToken) {
        using var deadline = Deadline(cancellationToken);
        IAsyncDisposable? activationLease = null;
        IAsyncDisposable? source = null;
        ProjectDeletionRecoveryTransaction? sql = null;
        ProjectDeletionRecoverySnapshot? snapshot = null;
        Exception? failure = null;
        try {
            await environment.RequireOriginalProfileAsync(deadline.Token);
            activationLease = await activation.AcquireExistingExclusionAsync(profileId, deadline.Token);
            Require(await activation.ObserveActiveBindingAsync(profileId, deadline.Token) == expected.Activation,
                ProjectDeletionRecoveryRefusal.BindingChanged);
            source = await environment.AcquireSourceExclusionAsync(expected.Snapshot.Evidence.Source, deadline.Token)
                ?? throw new InvalidOperationException("The original source exclusion adapter returned no owned lease.");
            sql = await ProjectDeletionRecoveryTransaction.OpenAsync(options, transactions, IsolationLevel.ReadCommitted, deadline.Token);
            await DatasetLifecycleStore.RequireActiveForDeletionRecoveryAsync(sql.Context, expected.Activation.Record.Plan.Binding, deadline.Token);
            Require(await SerializableMutationScope.TryAcquireRelationalScopeLocksAsync(sql.Context,
                expected.Snapshot.ExclusionKeys(participants), deadline.Token), ProjectDeletionRecoveryRefusal.ExclusionBusy);
            snapshot = await ProjectDeletionRecoverySnapshot.ReadAsync(sql.Context, transactions, expected.Snapshot.Completion.Original.Completion,
                expected.Activation, environment.ProfileIdentity, participants, ProjectDeletionRecoveryReadMode.Exclusive, deadline.Token);
            Require(snapshot.MatchesReleaseIdentity(expected.Snapshot) &&
                (snapshot.Row.ReleasedAtUtc is not null || snapshot.Digest == expected.Snapshot.Digest), ProjectDeletionRecoveryRefusal.PlanChanged);
        } catch (Exception primary) {
            failure = primary;
        }
        ThrowAfterRelease(await ReleaseAsync(failure, sql, source, activationLease));
        cancellationToken.ThrowIfCancellationRequested();
        return new(new(snapshot!, expected.Activation));
    }

    private async Task<ProjectDeletionRecoveryResult> MarkAsync(ReleasedProbe released, CancellationToken cancellationToken) {
        var observed = released.Observed;
        var snapshot = observed.Snapshot;
        var address = snapshot.Completion.Original.Completion;
        ProjectDeletionRecoveryTransaction? sql = null;
        Exception? failure = null;
        DateTimeOffset now = default;
        bool compareMiss = false;
        try {
            using var deadline = Deadline(cancellationToken);
            await RequireSameEnvironmentAsync(observed.Activation, snapshot.Evidence.Source, deadline.Token);
            sql = await ProjectDeletionRecoveryTransaction.OpenAsync(options, transactions, IsolationLevel.ReadCommitted, deadline.Token);
            await DatasetLifecycleStore.RequireActiveForDeletionHandoffAsync(sql.Context, observed.Activation.Record.Plan.Binding, deadline.Token);
            await RequireInstalledProtocolAsync(sql.Context, address, deadline.Token);
            now = clock.GetUtcNow();
            Require(now.Offset == TimeSpan.Zero && now > DateTimeOffset.MinValue && now < DateTimeOffset.MaxValue,
                ProjectDeletionRecoveryRefusal.UnsupportedEvidence);
            now = new(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
            int changed = await ProjectDeletionReleasePersistence.MarkAsync(sql.Context, snapshot.Evidence, snapshot.Row.CreatedAtUtc!.Value,
                snapshot.Row.ReferencePayload!, snapshot.Row.RecoveryPayload!, snapshot.Row.RecoveryDigest!, now, deadline.Token);
            compareMiss = changed == 0;
            Require(changed == 1, ProjectDeletionRecoveryRefusal.PlanChanged);
            await sql.CommitAsync(deadline.Token);
        } catch (Exception primary) {
            failure = primary;
        }
        var closure = await ReleaseAsync(failure, sql);
        bool diagnosticsUnavailable = closure.Failure is not null && !Log(address, closure.Failure);
        if (sql?.CommitAcknowledged == true) {
            return new ProjectDeletionRecoveryResult.Released(address, now, closure.CleanupFailed, diagnosticsUnavailable);
        }
        if (sql?.CommitAttempted == true || compareMiss) {
            try {
                if (await ObserveReleaseAsync(observed) is { } committed) {
                    return new ProjectDeletionRecoveryResult.Released(address, committed, closure.CleanupFailed, diagnosticsUnavailable);
                }
            } catch (Exception readFailure) {
                diagnosticsUnavailable |= !Log(address, readFailure);
            }
            return sql?.CommitAttempted == true
                ? new ProjectDeletionRecoveryResult.Unconfirmed(address, closure.CleanupFailed, diagnosticsUnavailable)
                : new ProjectDeletionRecoveryResult.Refused(address, ProjectDeletionRecoveryRefusal.PlanChanged, diagnosticsUnavailable);
        }
        return new ProjectDeletionRecoveryResult.Refused(address, closure.CleanupFailed
            ? ProjectDeletionRecoveryRefusal.ProbeReleaseUnconfirmed : Reason(closure.Failure!), diagnosticsUnavailable);
    }

    private async Task<DateTimeOffset?> ObserveReleaseAsync(ObservedCompletion expected) {
        using var deadline = Deadline(CancellationToken.None);
        await RequireSameEnvironmentAsync(expected.Activation, expected.Snapshot.Evidence.Source, deadline.Token);
        ProjectDeletionRecoveryTransaction? sql = null;
        Exception? failure = null;
        DateTimeOffset? released = null;
        try {
            sql = await ProjectDeletionRecoveryTransaction.OpenAsync(options, transactions, IsolationLevel.RepeatableRead, deadline.Token);
            var row = await ProjectDeletionCompletionReader.ReadForRecoveryAsync(sql.Context, expected.Snapshot.Completion.Original, deadline.Token);
            if (row is not null && row.Matches(expected.Activation.Record.Plan.Binding) &&
                row.Interpret(expected.Snapshot.Completion) is ProjectDeletionCompletionObservation.Released found &&
                expected.Snapshot.MatchesReleaseIdentity(row)) {
                released = found.ReleasedAtUtc;
            }
        } catch (Exception primary) {
            failure = primary;
        }
        ThrowAfterRelease(await ReleaseAsync(failure, sql));
        await RequireSameEnvironmentAsync(expected.Activation, expected.Snapshot.Evidence.Source, deadline.Token);
        return released;
    }

    private async Task RequireSameEnvironmentAsync(DatasetActivationFileObservation original,
        ProjectDeletionSourceRecovery source, CancellationToken cancellationToken) {
        await environment.RequireOriginalProfileAsync(cancellationToken);
        await environment.RequireOriginalSourceAsync(source, cancellationToken);
        Require(await activation.ObserveActiveBindingAsync(profileId, cancellationToken) == original,
            ProjectDeletionRecoveryRefusal.BindingChanged);
    }

    private ProjectDeletionRecoveryResult.Refused Refused(ProjectDeletionCompletionAddress address, Exception failure)
        => new(address, Reason(failure), !Log(address, failure));

    private bool Log(ProjectDeletionCompletionAddress address, Exception failure) {
        try {
            logger.LogError("Offline deletion recovery {CompletionId} in profile {ProfileId}, dataset {NamespaceId} failed; type {FailureType}.",
                address.Id.Value, address.DatabaseProfileId, address.Namespace.Value, failure.GetType().FullName);
            return true;
        } catch {
            return false;
        }
    }

    private sealed record ObservedCompletion(ProjectDeletionRecoverySnapshot Snapshot, DatasetActivationFileObservation Activation);
    private sealed record ReleasedProbe(ObservedCompletion Observed);
}
