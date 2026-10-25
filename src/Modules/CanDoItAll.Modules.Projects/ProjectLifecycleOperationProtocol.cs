using System.Security.Cryptography;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectLifecycleOperationProtocol(ICanonicalRuntimeDatabase database, DatasetActivationService activation,
    FiniteOperationEpochStore epochs, ProjectLifecycleReceiptReader reader, IOptions<ProjectDefinitionOperationOptions> options) {
    private FiniteOperationPoolRegistration Pool => ProjectFiniteOperationPool.Create(options.Value);

    internal TimeSpan MutationDeadline => Pool.Timing.MutationDeadline;

    public async Task<ProjectLifecycleWriteContext> GetWriteContextAsync(ProjectMutationCallerScope caller,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(caller);
        var profile = database.Profile;
        var generation = database.Generation;
        RequirePostgreSql(profile);
        var before = await activation.ObserveActiveBindingAsync(profile.Profile.Id, cancellationToken);
        RequireOriginal(profile, generation);
        var binding = before.Record.Plan.Binding;
        await epochs.RotateExpiredAsync(binding, Pool, cancellationToken);
        RequireOriginal(profile, generation);
        var current = await epochs.ReadCurrentAsync(binding, Pool, cancellationToken);
        RequireOriginal(profile, generation);
        var after = await activation.ObserveActiveBindingAsync(profile.Profile.Id, cancellationToken);
        RequireOriginal(profile, generation);
        if (after != before) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.ContextChanged);
        }
        if (current.AdmissionClosesAtUtc <= current.ObservedAtUtc) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.AdmissionClosed);
        }
        return new(profile.Profile.Id, binding.Namespace, caller.Bind(binding.Namespace), new(current.Epoch),
            current.AdmissionClosesAtUtc, current.ReplayUntilUtc);
    }

    internal async Task<ProjectLifecycleAttempt> PrepareAsync(ProjectLifecycleSubmission submission,
        ProjectLifecycleInvocation invocation, CancellationToken cancellationToken) {
        if (submission.Interpretation != ProjectLifecycleInterpretation.VersionOne) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.UnsupportedInterpretation);
        }
        var profile = database.Profile;
        var generation = database.Generation;
        RequirePostgreSql(profile);
        if (submission.Operation.DatabaseProfileId != profile.Profile.Id ||
                !invocation.Caller.Matches(submission.Operation.Namespace, submission.Operation.CallerBinding)) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.ContextChanged);
        }
        var digest = ProjectLifecycleOperationCodec.ComputeDigest(submission, invocation.Caller, invocation.EffectBinding);
        var original = await activation.ObserveActiveBindingAsync(profile.Profile.Id, cancellationToken);
        RequireOriginal(profile, generation);
        if (original.Record.Plan.Binding.Namespace != submission.Operation.Namespace) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.ContextChanged);
        }
        return new(submission, invocation, profile, generation, original, digest);
    }

    internal async Task<bool> TryReadAcceptedAsync(ProjectLifecycleAttempt attempt, CancellationToken cancellationToken) {
        RequireOriginal(attempt);
        var observed = await reader.ObserveAsync(attempt.Submission.Operation,
            new(attempt.Invocation.Caller, attempt.Invocation.ReceiptAccess), cancellationToken);
        RequireOriginal(attempt);
        var accepted = Interpret(attempt, observed.Lookup, observed.Evidence);
        if (accepted is null) {
            return false;
        }
        attempt.Accepted = accepted;
        return true;
    }

    internal async Task<bool> EnterBeforeOwnerAdmissionAsync(ProjectsDbContext context, ProjectLifecycleAttempt attempt,
        CancellationToken cancellationToken) {
        RequireOriginal(attempt);
        var row = await ProjectLifecycleReceiptReader.ReadAsync(context, attempt.Submission.Operation,
            attempt.Invocation.Caller, cancellationToken);
        if (row is null || !row.Matches(attempt.Binding)) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.ContextChanged);
        }
        var accepted = Interpret(attempt, row.Interpret(attempt.Submission.Operation), row);
        if (accepted is not null) {
            attempt.ReplayObserved = true;
            return true;
        }
        attempt.Epoch = await epochs.EnterAsync(context, attempt.Binding, Pool, attempt.Submission.Operation.Epoch.Value, cancellationToken);
        RequireOriginal(attempt);
        return false;
    }

    internal async ValueTask RequireCommitAsync(ProjectsDbContext context, ProjectLifecycleAttempt attempt,
        CancellationToken cancellationToken) {
        RequireOriginal(attempt);
        var active = await activation.ObserveActiveBindingAsync(attempt.Profile.Profile.Id, cancellationToken);
        RequireOriginal(attempt);
        if (active != attempt.Activation) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.ContextChanged);
        }
        await epochs.RequireAdmissibleAsync(context, attempt.Epoch
            ?? throw new InvalidOperationException("The original lifecycle epoch was not admitted."), cancellationToken);
        await RequireCurrentWriteAsync(attempt, cancellationToken);
        RequireOriginal(attempt);
    }

    internal static ValueTask RequireCurrentWriteAsync(ProjectLifecycleAttempt attempt, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (attempt.Invocation.Caller.Kind == ProjectMutationCallerKind.GovernedExecution) {
            if (attempt.Invocation.Authorization is null || attempt.Invocation.CurrentWrite is not null) {
                throw new InvalidOperationException("Governed lifecycle work requires the actual held source guard.");
            }
            return ValueTask.CompletedTask;
        }
        return RequireWriteAsync(attempt.Invocation.CurrentWrite
            ?? throw new InvalidOperationException("Operator lifecycle work requires its current write observer."), cancellationToken);
    }

    internal static ValueTask RequirePostReleaseWriteAsync(ProjectLifecycleAttempt attempt, CancellationToken cancellationToken)
        => RequireWriteAsync(attempt.Invocation.PostReleaseWrite, cancellationToken);

    private static async ValueTask RequireWriteAsync(IProjectLifecycleCurrentWriteAccess observer, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var decision = await observer.ObserveAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (decision != ProjectLifecycleWriteAccessDecision.Allowed) {
            throw new ProjectLifecycleRefusedException(decision switch {
                ProjectLifecycleWriteAccessDecision.CallerChanged => ProjectLifecycleRefusal.ContextChanged,
                ProjectLifecycleWriteAccessDecision.AuthorityChanged => ProjectLifecycleRefusal.AuthorityChanged,
                ProjectLifecycleWriteAccessDecision.Unavailable => ProjectLifecycleRefusal.AuthorityUnavailable,
                _ => ProjectLifecycleRefusal.WriteDenied
            });
        }
    }

    internal async Task StageAcceptedAsync(ProjectsDbContext context, ProjectLifecycleAttempt attempt,
        ProjectLifecycleAcknowledgement acknowledgement, CancellationToken cancellationToken) {
        var epoch = attempt.Epoch ?? throw new InvalidOperationException("The original lifecycle epoch was not admitted.");
        await epochs.RequireAdmissibleAsync(context, epoch, cancellationToken);
        var now = await FiniteOperationEpochStore.ReadTimeAsync(context, cancellationToken);
        var accepted = new ProjectLifecycleAccepted(attempt.Submission, ProjectLifecycleAccepted.VersionOne,
            now, epoch.ReplayUntilUtc, acknowledgement);
        var payload = ProjectLifecycleOperationCodec.EncodeAccepted(accepted);
        context.Add(new ProjectLifecycleReceiptRecord {
            NamespaceId = attempt.Submission.Operation.Namespace.Value,
            Epoch = attempt.Submission.Operation.Epoch.Value,
            CallerKind = attempt.Invocation.Caller.Kind,
            CallerKey = attempt.Invocation.Caller.Key.ToArray(),
            OperationId = attempt.Submission.Operation.Id,
            DatabaseProfileId = attempt.Profile.Profile.Id,
            SchemaVersion = attempt.Submission.Interpretation.Schema,
            NormalizationVersion = attempt.Submission.Interpretation.Normalization,
            ResultVersion = accepted.ResultVersion,
            CommandKind = attempt.Submission.Command.Kind,
            SemanticDigest = attempt.Digest.ToArray(),
            ProjectId = attempt.Target.ProjectId,
            LifetimeId = attempt.Target.LifetimeId,
            AcceptedAtUtc = now,
            AcceptedPayload = payload
        });
        await context.SaveChangesAsync(cancellationToken);
        attempt.Accepted = accepted;
    }

    internal void RequireOriginal(ProjectLifecycleAttempt attempt) => RequireOriginal(attempt.Profile, attempt.Generation);

    private void RequireOriginal(ResolvedDatabaseProfile profile, long generation) {
        if (database.Profile != profile || database.Generation != generation) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.ContextChanged);
        }
    }

    private static void RequirePostgreSql(ResolvedDatabaseProfile profile) {
        if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw new NotSupportedException("Durable Project lifecycle mutations require PostgreSQL.");
        }
    }

    private static ProjectLifecycleAccepted? Interpret(ProjectLifecycleAttempt attempt, ProjectLifecycleLookup lookup,
        ProjectLifecycleReceiptRow? evidence) {
        if (evidence?.ReceiptPresent == true && evidence.SemanticDigest is { Length: 32 } digest &&
                !CryptographicOperations.FixedTimeEquals(digest, attempt.Digest.ToArray())) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.IntentMismatch);
        }
        return lookup switch {
            ProjectLifecycleLookup.Found found when found.Accepted.Original == attempt.Submission => found.Accepted,
            ProjectLifecycleLookup.Found => throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.IntentMismatch),
            ProjectLifecycleLookup.NotObserved { State: ProjectOperationObservationState.Open } => null,
            ProjectLifecycleLookup.NotObserved or ProjectLifecycleLookup.ClosedAbsent
                => throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.AdmissionClosed),
            ProjectLifecycleLookup.Expired => throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.Expired),
            ProjectLifecycleLookup.UnknownFutureEpoch => throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.UnknownFutureEpoch),
            ProjectLifecycleLookup.ContextChanged => throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.ContextChanged),
            _ => throw new ProjectLifecycleObservationUnavailableException(ProjectLifecycleRecoveryReason.ReceiptUnavailable)
        };
    }
}
