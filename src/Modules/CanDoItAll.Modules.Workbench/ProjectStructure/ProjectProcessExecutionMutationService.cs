using System.Data;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectProcessMutationAdmission {
    internal ProjectProcessMutationAdmission(ProcessExecutionDispatchAuthority dispatch) {
        Dispatch = dispatch;
    }

    public ProcessExecutionDispatchAuthority Dispatch { get; }
    public ProjectWriteAdmission ProjectAdmission => Dispatch.SourceAuthority?.ProjectAdmission is { } project
        ? new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)
        : throw new ProcessExecutionAuthorityMismatchException("The Process source has no original project lifetime.");
}

public sealed record ProjectProcessExecutionAccess(ProcessExecutionDispatchAuthority Dispatch, bool CanRead, bool CanWrite);

public sealed class ProjectProcessExecutionMutationService(
    IProcessExecutionDispatchAuthorityReader reader,
    IProcessExecutionMutationGuard processGuard,
    ProjectProcessLaunchAuthorityService authorities,
    CoordinatedDatabaseTransaction transactions) {
    public async Task<ProjectProcessExecutionAccess?> ReadAccessAsync(Guid executionRunId, CancellationToken cancellationToken = default) {
        var result = await reader.ReadAsync(executionRunId, cancellationToken);
        if (result.Snapshot is not { } dispatch) {
            return null;
        }
        var access = dispatch.SourceAuthority is { } authority ? await authorities.ObserveAsync(authority, cancellationToken) : new(true, false);
        return new(dispatch, access.ReadAllowed, access.DispatchAllowed && dispatch.ObservedCurrentDispatch);
    }

    public async Task<ProjectProcessMutationAdmission?> ObserveAsync(Guid executionRunId, CancellationToken cancellationToken = default) {
        var result = await ReadAccessAsync(executionRunId, cancellationToken);
        return result is { CanRead: true, Dispatch.SourceAuthority.ProjectAdmission: not null, Dispatch.ProjectReference: not null }
            ? new(result.Dispatch) : null;
    }

    internal async Task RequireMediaPreparationAsync(ProjectProcessMutationAdmission admission, Guid projectId, CancellationToken cancellationToken) {
        var current = await ReadAccessAsync(admission.Dispatch.Evidence.ExecutionRunId, cancellationToken);
        if (projectId != admission.ProjectAdmission.ProjectId || current is not { CanWrite: true } ||
                current.Dispatch.OwnerFingerprint != admission.Dispatch.OwnerFingerprint ||
                current.Dispatch.SourceAuthority?.CanCreateAssets != true ||
                !current.Dispatch.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase)) {
            throw new ProcessExecutionAuthorityMismatchException("The current Process source does not permit this asset preparation.");
        }
    }

    internal async Task<ProjectProcessNativeMutationScope> BeginAsync(WorkbenchDbContext context,
        IReadOnlyCollection<string> scopeKeys, ProjectProcessMutationAdmission admission,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(admission);
        if (!admission.Dispatch.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase)) {
            throw new ProcessExecutionAuthorityMismatchException("The admitted Process step does not permit native external effects.");
        }
        if (expectedAdmissions is not { Count: 1 } || expectedAdmissions.Single() != admission.ProjectAdmission ||
                !scopeKeys.Contains(ProjectStructureSerializableMutationScope.ForProject(admission.ProjectAdmission.ProjectId), StringComparer.Ordinal)) {
            throw new ProcessExecutionAuthorityMismatchException("A Process native mutation must retain its single original project lifetime.");
        }
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not null) {
            throw new NotSupportedException("A Process native mutation requires a new PostgreSQL Workbench owner transaction.");
        }
        var current = await ObserveAsync(admission.Dispatch.Evidence.ExecutionRunId, cancellationToken);
        if (current is null || !current.Dispatch.ObservedCurrentDispatch ||
                current.Dispatch.OwnerFingerprint != admission.Dispatch.OwnerFingerprint ||
                current.Dispatch.ProjectReference != admission.Dispatch.ProjectReference ||
                current.Dispatch.Evidence != admission.Dispatch.Evidence) {
            throw new ProcessExecutionAuthorityMismatchException("The saved Process execution no longer owns this native mutation.");
        }
        var source = admission.Dispatch.SourceAuthority!;
        var heldSource = await authorities.AcquireAsync(source, source, cancellationToken);
        IDbContextTransaction? transaction = null;
        try {
            transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            using var coordination = transactions.Enter(context);
            await processGuard.RequireForMutationAsync(admission.Dispatch, cancellationToken);
            await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context, scopeKeys, cancellationToken);
            await heldSource.RequireForMutationAsync(null, cancellationToken);
            return new(context, transaction, heldSource, admission.Dispatch, processGuard, transactions);
        } catch {
            if (transaction is not null) {
                await transaction.DisposeAsync();
            }
            await heldSource.DisposeAsync();
            throw;
        }
    }
}

internal sealed class ProjectProcessNativeMutationScope(
    WorkbenchDbContext context,
    IDbContextTransaction transaction,
    IProcessLaunchAuthorityLease source,
    ProcessExecutionDispatchAuthority dispatch,
    IProcessExecutionMutationGuard processGuard,
    CoordinatedDatabaseTransaction transactions) : IAsyncDisposable {
    private int disposed;
    private bool committed;

    internal async Task CommitAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (committed) {
            throw new InvalidOperationException("The Process native mutation has already committed.");
        }
        using (transactions.Enter(context)) {
            await processGuard.RequireForMutationAsync(dispatch, cancellationToken);
            await source.RequireForMutationAsync(null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        await source.DisposeAsync();
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) != 0) {
            return;
        }
        try {
            await transaction.DisposeAsync();
        } finally {
            await source.DisposeAsync();
        }
    }
}
