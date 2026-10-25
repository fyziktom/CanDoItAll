using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionChildMaterialRead(ProcessDecisionChildObservation Child, ProcessDecisionMaterial Material,
    ProcessRuntimeStepAssignment Assignment);

public sealed class ProcessDecisionChildMaterialReader(IProcessDecisionChildReader children,
    IProcessDecisionMaterialReader materials, IProcessRuntimeStepAssignmentStore assignments) {
    public async Task<ProcessDecisionChildMaterialRead> ReadOriginalAsync(ProcessDecisionChildMaterialBinding original,
        ProcessDecisionExecutionContract supportedExecution, ProcessDecisionContentContract? supportedContent,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(supportedExecution);
        original.Validate();
        supportedExecution.Validate();
        supportedContent?.Validate();
        if (original.ExecutionContract != supportedExecution) {
            throw new InvalidDataException("The original child requires a different registered execution contract.");
        }
        var address = original.Child.Address;
        var child = await children.ReadAsync(address, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original Process child is unavailable.");
        if (child.Admission != original.Child) {
            throw new InvalidDataException("The original Process child or grant changed.");
        }
        var assignment = await assignments.LoadAsync(address.RunId, address.StepInstanceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original Process assignment is unavailable.");
        var policy = assignment.DecisionDataPolicy
            ?? throw new InvalidDataException("The original Process assignment has no admitted decision policy.");
        policy.RequireExecutionContract(supportedExecution);
        var scope = original.Scope;
        var expectedOwner = policy.SharedRunScopeId is null ? address.RunId : address.RootRunId;
        var expectedExecutor = supportedExecution.Executor == ProcessDecisionChildOwner.Agent
            ? ProcessLaunchExecutorKinds.Agent : ProcessLaunchExecutorKinds.Workflow;
        if (assignment.RunId != address.RunId || assignment.StepInstanceId != address.StepInstanceId || policy.StepInstanceId != address.StepInstanceId ||
            !string.Equals(assignment.ExecutorKind, expectedExecutor, StringComparison.OrdinalIgnoreCase) ||
            policy.WorkspaceNamespaceId != scope.WorkspaceNamespaceId || policy.Policy.ReadBasisMode != scope.ReadBasisMode ||
            scope.OwnerRunId != expectedOwner || policy.SharedRunScopeId is { } shared && shared != scope.Id) {
            throw new InvalidDataException("The original Process assignment does not own this material scope.");
        }
        var material = await materials.ReadAsync(scope, original.Material.Reference, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original Process material is unavailable.");
        if (material.Scope != scope || material.Stamp != original.Material || material.Protocol != policy.MaterialProtocol ||
            material.ContentContract != policy.ContentContract || material.ContentContract != supportedContent ||
            material.ExecutionContract is { } legacy && legacy != supportedExecution) {
            throw new InvalidDataException("The original Process material differs from its admitted stamp or supported content contract.");
        }
        return new(child, material, assignment);
    }
}
