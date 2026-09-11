using System.Buffers.Binary;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessExecutionMutationGuard(
    DbContextOptions<ProcessPersistenceDbContext> options,
    CoordinatedDatabaseTransaction transactions,
    TimeProvider clock) : IProcessExecutionMutationGuard {
    public async Task RequireForMutationAsync(ProcessExecutionDispatchAuthority expected, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.RootRunId.Value == Guid.Empty || expected.SourceAuthority?.ProjectAdmission is null || expected.ProjectReference is null) {
            throw new ProcessExecutionAuthorityMismatchException("Native Process effects require the exact saved project launch authority.");
        }
        await RequireDispatchAsync(expected, cancellationToken);
    }

    public async Task RequireDispatchAsync(ProcessExecutionDispatchAuthority expected, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.RootRunId.Value == Guid.Empty) {
            throw new ProcessExecutionAuthorityMismatchException("Process dispatch requires its exact saved root identity.");
        }
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new ProcessPersistenceDbContext(configured), cancellationToken);
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Process native mutation fencing requires the PostgreSQL owner transaction.");
        }
        var key = RootLockKey(expected.RootRunId.Value);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
        var evidence = expected.Evidence;
        var state = await context.RuntimeStates.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_states WHERE "RunId" = {evidence.RunId.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (state is null || state.RootRunId != expected.RootRunId.Value) {
            throw new ProcessExecutionAuthorityMismatchException("The Process root binding no longer matches the admitted native effect.");
        }
        await context.RuntimeStepAssignments.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_step_assignments
            WHERE "RunId" = {evidence.RunId.Value} AND "StepInstanceId" = {evidence.StepInstanceId.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var result = await EfProcessExecutionAuthorityQuery.ReadCoreAsync(context, evidence, clock.GetUtcNow(), cancellationToken);
        if (result.Snapshot is not { ObservedCurrentDispatch: true } current || current.OwnerFingerprint != expected.OwnerFingerprint ||
                current.ProjectReference != expected.ProjectReference || current.RootRunId != expected.RootRunId ||
                current.SourceAuthority?.ProjectAdmission != expected.SourceAuthority?.ProjectAdmission) {
            throw new ProcessExecutionAuthorityMismatchException("The Process claim, readiness, source or lifetime no longer permits this native effect.");
        }
    }

    private static long RootLockKey(Guid rootRunId) {
        Span<byte> bytes = stackalloc byte[16];
        rootRunId.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]) ^ BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]);
    }
}
