using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanPublisher {
    private readonly IDbContextFactory<WorkbenchDbContext> contexts;
    private readonly DatasetActivationService activation;
    private readonly ProjectWriteAdmissionService admissions;
    private readonly CoordinatedDatabaseTransaction transactions;
    private readonly FiniteOperationEpochStore epochs;
    private readonly FiniteOperationPoolRegistration pool;
    private readonly ProjectTaskPlanBasisCapture capture;
    private readonly ProjectTaskPlanReceiptReader receipts;
    private readonly DecisionDataPolicyBudget policyCeiling;
    private readonly ILogger<ProjectTaskPlanPublisher> logger;

    internal ProjectTaskPlanPublisher(IDbContextFactory<WorkbenchDbContext> contexts, DatasetActivationService activation,
        ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions, FiniteOperationEpochStore epochs,
        ProjectTaskPlanOperationOptions options, ProjectTaskPlanBasisCapture capture, ProjectTaskPlanReceiptReader receipts,
        DecisionDataPolicyBudget policyCeiling, ILogger<ProjectTaskPlanPublisher> logger) {
        ArgumentNullException.ThrowIfNull(policyCeiling);
        this.contexts = contexts;
        this.activation = activation;
        this.admissions = admissions;
        this.transactions = transactions;
        this.epochs = epochs;
        pool = WorkbenchTaskPlanFiniteOperations.Create(options);
        this.capture = capture;
        this.receipts = receipts;
        this.policyCeiling = policyCeiling;
        this.logger = logger;
    }

    internal async Task<ProjectTaskPlanWriteContext> GetWriteContextAsync(ProjectTaskPlanCaller caller, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(caller);
        var binding = await activation.ReadActiveBindingAsync(admissions.DatabaseProfileId, cancellationToken);
        var current = await epochs.ReadCurrentAsync(binding, pool, cancellationToken);
        if (current.AdmissionClosesAtUtc <= current.ObservedAtUtc) {
            throw new FiniteOperationAdmissionException(FiniteOperationAdmissionFailure.AdmissionDeadlineElapsed);
        }
        return new(binding.Namespace, current.Epoch, caller.Bind(binding.Namespace), current.AdmissionClosesAtUtc, current.ReplayUntilUtc);
    }

    internal async Task<ProjectTaskPlanOutcome> PublishAsync(ProjectTaskPlanCommand command,
        ProjectTaskPlanInvocation invocation, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(invocation.Caller);
        ArgumentNullException.ThrowIfNull(invocation.Authority);
        ArgumentNullException.ThrowIfNull(invocation.ReceiptAccess);
        var operation = command.Operation;
        string digest;
        try {
            digest = ProjectTaskPlanOperationCodec.Digest(command);
        } catch (Exception exception) when (exception is ArgumentException or ProjectTaskPlanBasisException) {
            return Reject(ProjectTaskPlanRefusal.InvalidInput);
        }
        var readInvocation = new ProjectTaskPlanReceiptInvocation(invocation.Caller, invocation.ReceiptAccess);
        var initial = await receipts.LookupAsync(operation, readInvocation, cancellationToken);
        if (initial.Status != ProjectTaskPlanLookupStatus.Open) {
            return Interpret(initial);
        }

        ProjectTaskPlanAccepted? accepted = null;
        var duplicate = false;
        var commitAttempted = false;
        var committed = false;
        Exception? failure = null;
        try {
            var binding = await activation.ReadActiveBindingAsync(admissions.DatabaseProfileId, cancellationToken);
            if (binding.Namespace != operation.Namespace || invocation.Caller.Bind(binding.Namespace) != operation.CallerBinding) {
                return Reject(binding.Namespace != operation.Namespace ? ProjectTaskPlanRefusal.DatasetChanged : ProjectTaskPlanRefusal.CallerChanged);
            }
            var target = command.Basis.Project.Expectation.Project;
            if (target.DatabaseProfileId != admissions.DatabaseProfileId) {
                return Reject(ProjectTaskPlanRefusal.InvalidInput);
            }
            await using var authority = await invocation.Authority.AcquireAsync(command.Source, target, cancellationToken);
            RequireActiveWork(command, authority);
            var expectedProjects = CaptureProjects(target, authority);
            await authority.RequireCurrentAsync(cancellationToken);
            await using var context = await contexts.CreateDbContextAsync(cancellationToken);
            if (!context.Database.IsNpgsql()) {
                throw new NotSupportedException("Task-plan publication requires PostgreSQL.");
            }
            transactions.RequireOwnerProfile(context);
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            using var participation = transactions.Enter(context);
            var epoch = await epochs.EnterAsync(context, binding, pool, operation.Epoch, cancellationToken);
            if (await ProjectTaskPlanOperationProtocol.ExistsLockedAsync(context, operation, invocation.Caller, cancellationToken)) {
                duplicate = true;
            } else {
                await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context,
                    expectedProjects.Select(project => ProjectMutationScopeKeys.ForProject(project.ProjectId)).ToArray(), cancellationToken);
                await epochs.RequireAdmissibleAsync(context, epoch, cancellationToken);
                await admissions.RequireManyForMutationAsync(expectedProjects, cancellationToken);
                var current = await capture.ReadEnlistedAsync(context, binding, target, cancellationToken);
                RequirePolicy(command, current.Basis.CapturedAtUtc, authority);
                if (!ProjectTaskPlanBasisCodec.MaterialEquals(command.Basis, current.Basis)) {
                    throw new PublicationRefused(ProjectTaskPlanRefusal.BasisChanged);
                }
                await authority.RequireEnlistedAsync(cancellationToken);
                var staged = ProjectTaskPlanTaskStaging.StageNewPlainTasks(context, target, command.Proposal, current, current.Basis.CapturedAtUtc);
                accepted = new(operation, command.Basis.Project.Expectation, command.Source, digest,
                    current.Basis.CapturedAtUtc, epoch.ReplayUntilUtc, staged.Tasks);
                context.Add(ProjectTaskPlanOperationProtocol.CreateReceipt(accepted, invocation.Caller));
                await context.SaveChangesAsync(cancellationToken);
                await authority.RequireEnlistedAsync(cancellationToken);
                await admissions.RequireManyForMutationAsync(expectedProjects, cancellationToken);
                await epochs.RequireAdmissibleAsync(context, epoch, cancellationToken);
                RequirePolicy(command, await FiniteOperationEpochStore.ReadTimeAsync(context, cancellationToken), authority);
                commitAttempted = true;
                await transaction.CommitAsync(cancellationToken);
                committed = true;
            }
        } catch (Exception exception) {
            failure = exception;
        }

        if (committed) {
            if (failure is not null) {
                LogFailure(failure);
            }
            return new ProjectTaskPlanOutcome.Accepted(accepted!, false, failure is not null);
        }
        if (duplicate) {
            if (failure is not null) {
                LogFailure(failure);
                return new ProjectTaskPlanOutcome.Unconfirmed(operation, ProjectTaskPlanRefusal.CleanupIncomplete);
            }
            try {
                return Interpret(await receipts.LookupAsync(operation, readInvocation, cancellationToken), observedDuplicate: true);
            } catch (OperationCanceledException) {
                return new ProjectTaskPlanOutcome.Unconfirmed(operation, ProjectTaskPlanRefusal.DetailUnavailable);
            }
        }
        if (commitAttempted) {
            if (failure is not null) {
                LogFailure(failure);
            }
            ProjectTaskPlanLookup recovered;
            try {
                recovered = await receipts.LookupAsync(operation, readInvocation, cancellationToken);
            } catch (OperationCanceledException) {
                return new ProjectTaskPlanOutcome.Unconfirmed(operation, ProjectTaskPlanRefusal.CommitUnconfirmed);
            }
            return recovered.Status == ProjectTaskPlanLookupStatus.Found && recovered.Accepted?.SemanticDigest == digest
                ? new ProjectTaskPlanOutcome.Accepted(recovered.Accepted, true, true)
                : new ProjectTaskPlanOutcome.Unconfirmed(operation, ProjectTaskPlanRefusal.CommitUnconfirmed);
        }
        return failure switch {
            PublicationRefused refused => Reject(refused.Reason),
            ProjectTaskPlanAuthorityException denied => Reject(denied.Reason),
            ProjectWriteAdmissionRejectedException => Reject(ProjectTaskPlanRefusal.SourceChanged),
            DatasetActivationMismatchException => Reject(ProjectTaskPlanRefusal.DatasetChanged),
            FiniteOperationAdmissionException => Reject(ProjectTaskPlanRefusal.EpochUnavailable),
            DecisionDataPolicyException => Reject(ProjectTaskPlanRefusal.PolicyChanged),
            ProjectTaskPlanBasisException => Reject(ProjectTaskPlanRefusal.InvalidInput),
            not null when SerializableMutationScope.IsConflict(failure) => Reject(ProjectTaskPlanRefusal.TransactionConflict),
            _ => Unavailable()
        };

        ProjectTaskPlanOutcome Reject(ProjectTaskPlanRefusal reason) => new ProjectTaskPlanOutcome.Rejected(operation, reason,
            reason == ProjectTaskPlanRefusal.BasisChanged && command.Policy.OnBasisChange == OnBasisChange.PauseForReview);

        ProjectTaskPlanOutcome Unavailable() {
            if (failure is not null) {
                LogFailure(failure);
            }
            return Reject(ProjectTaskPlanRefusal.Unavailable);
        }

        ProjectTaskPlanOutcome Interpret(ProjectTaskPlanLookup lookup, bool observedDuplicate = false) {
            if (lookup.Status == ProjectTaskPlanLookupStatus.Found && lookup.Accepted is { } found) {
                return found.SemanticDigest == digest
                    ? new ProjectTaskPlanOutcome.Accepted(found, true)
                    : Reject(ProjectTaskPlanRefusal.IdentityConflict);
            }
            if (observedDuplicate || lookup.Status == ProjectTaskPlanLookupStatus.DetailUnavailable) {
                return new ProjectTaskPlanOutcome.Unconfirmed(operation, ProjectTaskPlanRefusal.DetailUnavailable);
            }
            if (lookup.Status == ProjectTaskPlanLookupStatus.Unavailable) {
                return new ProjectTaskPlanOutcome.Unconfirmed(operation, ProjectTaskPlanRefusal.CommitUnconfirmed);
            }
            return Reject(lookup.Status switch {
                ProjectTaskPlanLookupStatus.DatasetChanged => ProjectTaskPlanRefusal.DatasetChanged,
                ProjectTaskPlanLookupStatus.CallerContextChanged => ProjectTaskPlanRefusal.CallerChanged,
                _ => ProjectTaskPlanRefusal.EpochUnavailable
            });
        }

        void LogFailure(Exception exception) => logger.LogWarning(
            "Task-plan operation {OperationId} ended with failure type {FailureType}; commit attempted {CommitAttempted}, acknowledged {Committed}.",
            operation.Id, exception.GetType().Name, commitAttempted, committed);
    }

    private static ProjectWriteAdmission[] CaptureProjects(ProjectWriteAdmission target, IProjectTaskPlanHeldAuthority authority) {
        ArgumentNullException.ThrowIfNull(authority);
        var additional = authority.AdditionalProjects;
        if (additional.IsDefault || additional.Length > 1) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
        }
        var expected = new Dictionary<Guid, ProjectWriteAdmission> { [target.ProjectId] = target };
        foreach (var project in additional) {
            if (project is null || project.DatabaseProfileId != target.DatabaseProfileId ||
                    expected.TryGetValue(project.ProjectId, out var existing) && existing != project) {
                throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
            }
            expected[project.ProjectId] = project;
        }
        return expected.Values.OrderBy(project => project.ProjectId).ToArray();
    }

    private void RequirePolicy(ProjectTaskPlanCommand command, DateTimeOffset now, IProjectTaskPlanHeldAuthority authority) {
        var activeWork = command.Source.Version == ProjectTaskPlanSource.ActiveWorkVersion;
        var capabilities = new DecisionDataPolicyCapabilities(true, true, false, true, true, activeWork,
            PublicationFreshness.RequireCurrent, policyCeiling);
        if (DecisionDataPolicyResolver.Resolve(command.Policy, capabilities) != command.Policy) {
            throw new PublicationRefused(ProjectTaskPlanRefusal.PolicyChanged);
        }
        if (command.Source.DecisionStartedAtUtc > now || command.Basis.CapturedAtUtc > now ||
                !activeWork && now - command.Source.DecisionStartedAtUtc >= command.Policy.Budget.MaximumWorkDuration ||
                now - command.Basis.CapturedAtUtc >= command.Policy.Budget.MaximumBasisAge) {
            throw new PublicationRefused(ProjectTaskPlanRefusal.BudgetElapsed);
        }
        RequireActiveWork(command, authority);
    }

    private static void RequireActiveWork(ProjectTaskPlanCommand command, IProjectTaskPlanHeldAuthority authority) {
        if (command.Source.Version != ProjectTaskPlanSource.ActiveWorkVersion) {
            return;
        }
        if (authority is not IProjectTaskPlanActiveWorkAuthority work) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
        }
        work.RequireActiveWork(command.Source, command.Policy);
    }

    private sealed class PublicationRefused(ProjectTaskPlanRefusal reason) : InvalidOperationException {
        internal ProjectTaskPlanRefusal Reason { get; } = reason;
    }
}
