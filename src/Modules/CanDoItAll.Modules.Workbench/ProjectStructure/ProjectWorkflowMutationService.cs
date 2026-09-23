using System.Data;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectWorkflowMutationAdmission(WorkflowStructureAuthority Authority,
    WorkflowProjectLifetime Target, WorkflowStructureAuthorityUse Use, WorkflowStructureOutputPlan? OutputPlan = null) {
    internal Func<CancellationToken, Task>? RequireContinuationAsync { get; init; }
}

public sealed class ProjectWorkflowMutationService(IWorkflowStructureSourceAuthorityPolicy sources,
    IWorkflowStructureOutputStore outputs, CoordinatedDatabaseTransaction transactions) {
    internal async Task<ProjectWorkflowMutationScope> BeginAsync(WorkbenchDbContext context, IReadOnlyCollection<string> keys,
        ProjectWorkflowMutationAdmission admission, IReadOnlyCollection<ProjectWriteAdmission>? expected,
        CancellationToken cancellationToken) {
        if (context.Database.CurrentTransaction is not null || expected is not { Count: 1 } ||
                expected.Single() != ProjectStructureWorkflowAuthorityService.ToProjectAdmission(admission.Target) ||
                !keys.Contains(ProjectStructureSerializableMutationScope.ForProject(admission.Target.ProjectId), StringComparer.Ordinal) ||
                admission.OutputPlan is { } plan && (plan.ProjectLifetime != admission.Target ||
                    plan.SourceAuthorityFingerprint != WorkflowStructureAuthorityFingerprint.Create(admission.Authority))) {
            throw new InvalidOperationException("The native Workflow mutation lost its exact original source, target or owner transaction boundary.");
        }
        var source = await sources.AcquireAsync(admission.Authority, admission.Use, admission.Target, cancellationToken);
        SerializableMutationScope? memoryMutation = null;
        IDbContextTransaction? transaction = null;
        try {
            if (context.Database.IsInMemory()) {
                memoryMutation = await SerializableMutationScope.BeginAsync(context, keys, cancellationToken);
            } else if (context.Database.IsNpgsql()) {
                transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            } else {
                throw new NotSupportedException("Workflow native mutations require PostgreSQL or explicit InMemory fixtures.");
            }
            using (transactions.Enter(context)) {
                await source.RequireForMutationAsync(cancellationToken);
                if (admission.OutputPlan is { } output) {
                    await outputs.RequireForMutationAsync(output, cancellationToken);
                }
                await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context, keys, cancellationToken);
                if (admission.RequireContinuationAsync is { } requireContinuation) {
                    await requireContinuation(cancellationToken);
                }
            }
            return new(context, transaction, memoryMutation, source, admission.OutputPlan, outputs, transactions, admission.RequireContinuationAsync);
        } catch {
            try {
                if (transaction is not null) {
                    await transaction.DisposeAsync();
                }
                if (memoryMutation is not null) {
                    await memoryMutation.DisposeAsync();
                }
            } finally {
                await source.DisposeAsync();
            }
            throw;
        }
    }
}

internal sealed class ProjectWorkflowMutationScope(WorkbenchDbContext context, IDbContextTransaction? transaction,
    SerializableMutationScope? memoryMutation, IWorkflowStructureSourceAuthorityLease source,
    WorkflowStructureOutputPlan? plan, IWorkflowStructureOutputStore outputs,
    CoordinatedDatabaseTransaction transactions, Func<CancellationToken, Task>? requireContinuation) : IAsyncDisposable {
    private bool committed;
    private int disposed;

    internal async Task CommitAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (committed) {
            throw new InvalidOperationException("The native Workflow transaction has already committed.");
        }
        using (transactions.Enter(context)) {
            await source.RequireForMutationAsync(cancellationToken);
            if (plan is not null) {
                await outputs.RequireForMutationAsync(plan, cancellationToken);
            }
            if (requireContinuation is not null) {
                await requireContinuation(cancellationToken);
            }
            if (transaction is not null) {
                await transaction.CommitAsync(cancellationToken);
            } else {
                await memoryMutation!.CommitAsync(cancellationToken);
            }
            committed = true;
        }
        await source.DisposeAsync();
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) != 0) {
            return;
        }
        try {
            if (transaction is not null) {
                await transaction.DisposeAsync();
            }
            if (memoryMutation is not null) {
                await memoryMutation.DisposeAsync();
            }
        } finally {
            await source.DisposeAsync();
        }
    }
}
