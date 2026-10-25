using CanDoItAll.Infrastructure.Persistence;
using System.Data.Common;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectDefinitionOperationTests {
    [Theory]
    [InlineData(AdmissionRecovery.Available)]
    [InlineData(AdmissionRecovery.ReadRevoked)]
    [InlineData(AdmissionRecovery.LookupUnavailable)]
    public async Task Initial_receipt_lookup_failure_cannot_reject_an_already_accepted_operation_without_current_recovery_evidence(AdmissionRecovery recovery) {
        var lookupFailure = new InitialReceiptLookupFailure();
        await using var app = await TestApplication.CreateAsync(WithInterceptors(lookupFailure));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var access = new ReceiptAccess();
        var invocation = Invocation(access);
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();
        var request = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(await NewOperationAsync(services, invocation), Version, Creation());
        var original = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.CreateAsync(request, invocation));
        access.Allowed = recovery != AdmissionRecovery.ReadRevoked;
        lookupFailure.FailuresRemaining = recovery == AdmissionRecovery.LookupUnavailable ? 2 : 1;

        var outcome = await writer.CreateAsync(request, invocation);

        if (recovery == AdmissionRecovery.ReadRevoked) {
            var rejected = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected>(outcome);
            Assert.Contains(rejected.Errors, error => error.Code == ProjectErrorCodes.DefinitionReceiptUnavailable);
        } else if (recovery == AdmissionRecovery.LookupUnavailable) {
            var unconfirmed = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Unconfirmed>(outcome);
            Assert.Equal(request.Operation, unconfirmed.Operation);
            Assert.Equal(ProjectDefinitionRecoveryReason.CommitUnconfirmed, unconfirmed.Reason);
        } else {
            var accepted = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(outcome);
            Assert.True(accepted.Replayed);
            Assert.Equal(Encoded(original.Result), Encoded(accepted.Result));
        }
        Assert.Equal(0, lookupFailure.FailuresRemaining);
        Assert.Equal(recovery == AdmissionRecovery.LookupUnavailable ? 2 : 1, lookupFailure.FailuresObserved);
        var current = await CurrentAsync(services, original.Result.Acknowledgement.Project.ProjectId);
        Assert.Equal(original.Result.Acknowledgement.Definition, current.Expectation);
        await using var database = await DatabaseAsync(services);
        Assert.Single(await database.Set<Project>().ToArrayAsync());
        var stored = Assert.Single(await database.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
        Assert.Equal(Encoded(original.Result), stored.AcceptedPayload);
    }

    [Theory]
    [InlineData(AdmissionRecovery.Available)]
    [InlineData(AdmissionRecovery.ReadRevoked)]
    [InlineData(AdmissionRecovery.LookupUnavailable)]
    public async Task Duplicate_that_missed_acceptance_before_epoch_closure_recovers_exact_receipt_under_current_read_permission(AdmissionRecovery recovery) {
        var delayedObservation = new DelayedOriginalReceiptObservation(recovery == AdmissionRecovery.LookupUnavailable);
        await using var app = await TestApplication.CreateAsync(WithInterceptors(delayedObservation));
        await using var originalScope = app.Services.CreateAsyncScope();
        await using var duplicateScope = app.Services.CreateAsyncScope();
        var services = originalScope.ServiceProvider;
        var access = new ReceiptAccess();
        var invocation = Invocation(access);
        var request = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(await NewOperationAsync(services, invocation), Version, Creation());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        delayedObservation.Armed = true;
        var duplicate = duplicateScope.ServiceProvider.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(request, invocation, deadline.Token);
        ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted original;
        ProjectDefinitionOutcome<ProjectEditorAcknowledgement> recovered;
        try {
            await delayedObservation.Observed.Task.WaitAsync(deadline.Token);
            original = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(
                await services.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(request, invocation, deadline.Token));
            await using var database = await DatabaseAsync(services);
            Assert.Equal(1, await database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Infrastructure_FiniteOperationEpochs" SET "State" = {(int)FiniteOperationEpochState.Closed}, "ClosedAtUtc" = clock_timestamp()
                WHERE "NamespaceId" = {request.Operation.Namespace.Value} AND "PoolId" = {ProjectFiniteOperationPool.Pool.Value}
                    AND "Epoch" = {request.Operation.Epoch.Value}
                """, deadline.Token));
            access.Allowed = recovery != AdmissionRecovery.ReadRevoked;
        } finally {
            delayedObservation.Release.TrySetResult();
            recovered = await duplicate;
        }

        if (recovery == AdmissionRecovery.ReadRevoked) {
            var rejected = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected>(recovered);
            Assert.Contains(rejected.Errors, error => error.Code == ProjectErrorCodes.DefinitionReceiptUnavailable);
        } else if (recovery == AdmissionRecovery.LookupUnavailable) {
            var unconfirmed = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Unconfirmed>(recovered);
            Assert.Equal(request.Operation, unconfirmed.Operation);
            Assert.Equal(ProjectDefinitionRecoveryReason.CommitUnconfirmed, unconfirmed.Reason);
            Assert.True(delayedObservation.RecoveryBlocked);
        } else {
            var accepted = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(recovered);
            Assert.True(accepted.Replayed);
            Assert.Equal(Encoded(original.Result), Encoded(accepted.Result));
        }
        Assert.Equal(recovery == AdmissionRecovery.LookupUnavailable ? 1 : 2, delayedObservation.ObservationCount);
        Assert.True(delayedObservation.ClosedEpochWasRead);
        var current = await CurrentAsync(services, original.Result.Acknowledgement.Project.ProjectId);
        Assert.Equal(original.Result.Acknowledgement.Definition, current.Expectation);
        await using var verification = await DatabaseAsync(services);
        Assert.Single(await verification.Set<Project>().ToArrayAsync(deadline.Token));
        var stored = Assert.Single(await verification.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync(deadline.Token));
        Assert.Equal(Encoded(original.Result), stored.AcceptedPayload);
    }

    public enum AdmissionRecovery { Available, ReadRevoked, LookupUnavailable }

    private sealed class InitialReceiptLookupFailure : DbCommandInterceptor {
        public int FailuresRemaining { get; set; }
        public int FailuresObserved { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (FailuresRemaining > 0 && IsReceiptObservation(command)) {
                FailuresRemaining--;
                FailuresObserved++;
                throw new TimeoutException("Owned initial or recovery receipt observation outage for an accepted operation.");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DelayedOriginalReceiptObservation(bool failRecovery) : DbCommandInterceptor {
        private Guid? delayedContext;
        public bool Armed { get; set; }
        public int ObservationCount { get; private set; }
        public bool ClosedEpochWasRead { get; private set; }
        public bool RecoveryBlocked { get; private set; }
        public TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (failRecovery && Release.Task.IsCompleted && IsReceiptObservation(command)) {
                RecoveryBlocked = true;
                throw new TimeoutException("Owned receipt observation outage after epoch admission refusal.");
            }
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (IsReceiptObservation(command)) {
                if (Armed) {
                    Armed = false;
                    delayedContext = eventData.Context!.ContextId.InstanceId;
                    ObservationCount++;
                    Observed.TrySetResult();
                    await Release.Task.WaitAsync(cancellationToken);
                } else if (Release.Task.IsCompleted) {
                    ObservationCount++;
                }
            }
            if (Release.Task.IsCompleted && command.Transaction is not null &&
                command.CommandText.Contains("JOIN \"Infrastructure_FiniteOperationEpochs\" AS e", StringComparison.Ordinal) &&
                !IsReceiptObservation(command) &&
                eventData.Context!.ContextId.InstanceId != delayedContext) {
                ClosedEpochWasRead = true;
            }
            return result;
        }
    }

    private static bool IsReceiptObservation(DbCommand command)
        => command.CommandText.Contains("LEFT JOIN \"Projects_DefinitionReceipts\" AS r", StringComparison.Ordinal);
}
