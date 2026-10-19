using System.Buffers.Binary;
using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionPublicationFence(DbContextOptions<ProcessPersistenceDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IProcessDecisionPublicationFence {
    public async Task RequireAsync(ProcessDecisionChildAdmission original, ProcessDecisionPublicationKind kind,
        CancellationToken cancellationToken = default) {
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
        var key = RootLockKey(original.Address.RootRunId.Value);
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
    }

    private static bool AllowsRuntime(ProcessDecisionChildRead current, ProcessDecisionPublicationKind kind)
        => current.RootStatus != ProcessRuntimeStatus.CancelRequested && !ProcessRuntimeTerminalStates.IsRunTerminal(current.RootStatus) && (kind switch {
        ProcessDecisionPublicationKind.Dispatch => current.RunStatus == ProcessRuntimeStatus.Active &&
            current.StepStatus is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running && current.ActiveClaimToken is not null && current.HasLiveClaim,
        ProcessDecisionPublicationKind.ManagerReview =>
            current.RunStatus is ProcessRuntimeStatus.Active or ProcessRuntimeStatus.Waiting or ProcessRuntimeStatus.Blocked or ProcessRuntimeStatus.WaitingForUser &&
            current.StepStatus is ProcessRuntimeStepStatus.Waiting or ProcessRuntimeStepStatus.WaitingApproval or ProcessRuntimeStepStatus.Blocked &&
            current.ActiveClaimToken is null && !current.HasLiveClaim,
        _ => false
    });

    private static long RootLockKey(Guid rootRunId) {
        Span<byte> bytes = stackalloc byte[16];
        rootRunId.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]) ^ BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]);
    }

    private static ProcessExecutionAuthorityMismatchException Mismatch(string message) => new(message);
}
