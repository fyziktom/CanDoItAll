using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessAcceptedAssignmentGuard {
    internal static IQueryable<ProcessRuntimeStepAssignmentEntity> Query(ProcessPersistenceDbContext context)
        => context.RuntimeStepAssignments.AsNoTracking().Select(assignment => new ProcessRuntimeStepAssignmentEntity {
            RunId = assignment.RunId, PlanId = assignment.PlanId, StepInstanceId = assignment.StepInstanceId,
            StepKey = assignment.StepKey, ExecutorKind = assignment.ExecutorKind, ExecutorId = assignment.ExecutorId,
            ReadinessHash = assignment.ReadinessHash, CreatedAtUtc = assignment.CreatedAtUtc,
            AllowedOperations = assignment.AllowedOperations, OperationTargetScope = assignment.OperationTargetScope,
            LaunchVariablesJson = assignment.LaunchVariablesJson, CapabilityScopeJson = assignment.CapabilityScopeJson,
            DecisionDataPolicyJson = assignment.DecisionDataPolicyJson,
            WorkflowId = assignment.WorkflowId, WorkflowVersionId = assignment.WorkflowVersionId,
            WorkflowOutputMapping = assignment.WorkflowOutputMapping
        });

    internal static async Task RequireAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateEntity state,
        ProcessStepInstanceId stepId, ProcessDecisionExecutionContract contract, CancellationToken cancellationToken) {
        if (context.Database.IsNpgsql()) {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT 1 FROM process_runtime_step_assignments
                WHERE "RunId" = {state.RunId} AND "StepInstanceId" = {stepId.Value} FOR SHARE
                """, cancellationToken).ConfigureAwait(false);
        }
        var current = await Query(context).SingleOrDefaultAsync(item => item.RunId == state.RunId && item.StepInstanceId == stepId.Value,
            cancellationToken).ConfigureAwait(false) ?? throw Changed();
        var original = await EfProcessAcceptedSource.ReadProjectedAsync(context, state, stepId, cancellationToken).ConfigureAwait(false);
        if (!Same(original.Assignment, ProcessAssignmentSource.From(EfProcessRuntimeStepAssignmentStore.ToAssignment(current))) ||
                original.Assignment.DecisionDataPolicy?.ExecutionContract != contract) {
            throw Changed();
        }
    }

    internal static bool Same(ProcessAssignmentSource original, ProcessAssignmentSource current) {
        if (current.CreatedAtUtc != EfProcessExecutionAuthorityQuery.StoredTimestamp(original.CreatedAtUtc)) {
            return false;
        }
        ProcessAssignmentSource Normalize(ProcessAssignmentSource value) => value with {
            CreatedAtUtc = current.CreatedAtUtc,
            AllowedOperations = value.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            LaunchVariables = value.LaunchVariables.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Value)
        };
        return JsonSerializer.Serialize(Normalize(original)) == JsonSerializer.Serialize(Normalize(current));
    }

    private static ProcessExecutionAuthorityMismatchException Changed()
        => new("Selected preparation cannot replace the original accepted assignment.");
}
