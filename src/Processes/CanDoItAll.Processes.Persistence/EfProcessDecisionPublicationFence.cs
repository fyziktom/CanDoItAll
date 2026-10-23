using System.Data;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionPublicationFence(DbContextOptions<ProcessPersistenceDbContext> options,
    CoordinatedDatabaseTransaction transactions, TimeProvider clock) : IProcessDecisionPublicationFence {
    public Task RequireAsync(ProcessDecisionChildAdmission original, ProcessDecisionPublicationKind kind,
        CancellationToken cancellationToken = default)
        => RequireCoreAsync(original, kind, null, null, cancellationToken);

    public Task RequireSelectedAsync(ProcessDecisionChildMaterialBinding original, ProcessExecutionDispatchAuthority expectedSource,
        ProcessDecisionExecutionContract supportedExecution, ProcessDecisionContentContract? supportedContent,
        ProcessDecisionPublicationKind kind, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expectedSource);
        var selection = new ProcessDecisionDispatchSelection(original, supportedExecution, supportedContent, clock);
        selection.Validate(expectedSource.Evidence);
        if (expectedSource.RootRunId != original.Child.Address.RootRunId || expectedSource.SourceAuthority is null) {
            throw Mismatch("Selected Process publication requires its exact saved source authority and root.");
        }
        return RequireCoreAsync(original.Child, kind, selection, expectedSource, cancellationToken);
    }

    private async Task RequireCoreAsync(ProcessDecisionChildAdmission original, ProcessDecisionPublicationKind kind,
        ProcessDecisionDispatchSelection? selection, ProcessExecutionDispatchAuthority? expectedSource, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(original);
        original.Validate();
        if (!Enum.IsDefined(kind)) {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new ProcessPersistenceDbContext(configured),
            cancellationToken).ConfigureAwait(false);
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted) {
            throw new NotSupportedException("Process decision publication requires the effect owner's PostgreSQL ReadCommitted transaction.");
        }
        var key = ProcessRootAdvisoryKey.From(original.Address.RootRunId.Value);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken).ConfigureAwait(false);
        var current = await EfProcessDecisionChildQuery.ReadCoreAsync(context, original.Address, cancellationToken).ConfigureAwait(false)
            ?? throw Mismatch("The exact enrolled Process child no longer exists.");
        var observation = current.Observation;
        if (!observation.IsCurrent || observation.Admission != original || current.BudgetOverrun || observation.Settlement is not null ||
                original.Grant.WorkTicks == 0 || !AllowsRuntime(current, kind)) {
            throw Mismatch("The Process generation, child allocation or current runtime state no longer permits this publication.");
        }
        ProcessDecisionTransitions.RequirePublication(observation.Generation, original.Address.Reference,
            new(original.Address.Reference, original.Address.ChildId, original.Owner, original.AllocationId, observation.Closure), kind);
        if (selection is not null && expectedSource is not null) {
            await ProcessExecutionSourceRowLocks.AcquireAsync(context, expectedSource.RootRunId, expectedSource.Evidence, cancellationToken).ConfigureAwait(false);
            var result = await EfProcessExecutionAuthorityQuery.ReadCoreAsync(context, expectedSource.Evidence, clock.GetUtcNow(),
                cancellationToken, selection).ConfigureAwait(false);
            if (result.Snapshot is not { SourceAuthority: not null } source ||
                kind == ProcessDecisionPublicationKind.Dispatch && !source.ObservedCurrentDispatch ||
                !SameSource(expectedSource, source) || !AllowsRuntime(current, kind)) {
                throw Mismatch("The exact Process source, material, contract or claim no longer permits this selected publication.");
            }
        }
    }

    private static bool SameSource(ProcessExecutionDispatchAuthority expected, ProcessExecutionDispatchAuthority current)
        => JsonSerializer.Serialize(expected with { ObservedAtUtc = current.ObservedAtUtc, ObservedCurrentDispatch = current.ObservedCurrentDispatch }) ==
            JsonSerializer.Serialize(current);

    private bool AllowsRuntime(ProcessDecisionChildRead current, ProcessDecisionPublicationKind kind)
        => current.RootStatus != ProcessRuntimeStatus.CancelRequested && !ProcessRuntimeTerminalStates.IsRunTerminal(current.RootStatus) && (kind switch {
        ProcessDecisionPublicationKind.Dispatch => current.RunStatus == ProcessRuntimeStatus.Active &&
            current.StepStatus is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running && current.MatchingClaimExpiresAtUtc > clock.GetUtcNow(),
        ProcessDecisionPublicationKind.ManagerReview =>
            current.RunStatus is ProcessRuntimeStatus.Active or ProcessRuntimeStatus.Waiting or ProcessRuntimeStatus.Blocked or ProcessRuntimeStatus.WaitingForUser &&
            current.StepStatus is ProcessRuntimeStepStatus.Waiting or ProcessRuntimeStepStatus.WaitingApproval or ProcessRuntimeStepStatus.Blocked &&
            current.ActiveClaimToken is null && !current.HasLiveClaim,
        _ => false
    });


    private static ProcessExecutionAuthorityMismatchException Mismatch(string message) => new(message);
}
