using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal sealed record ProcessDecisionDispatchSelection(ProcessDecisionChildMaterialBinding Original,
    ProcessDecisionExecutionContract SupportedExecution, ProcessDecisionContentContract? SupportedContent, TimeProvider Clock) {
    internal void Validate(ProcessExecutionClaimEvidence evidence) {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(Original);
        ArgumentNullException.ThrowIfNull(SupportedExecution);
        Original.Validate();
        SupportedExecution.Validate();
        SupportedContent?.Validate();
        var address = Original.Child.Address;
        if (Original.ExecutionContract != SupportedExecution || SupportedExecution.Executor != ProcessDecisionChildOwner.Agent ||
            evidence.ExecutionRunId != address.ChildId.Value || evidence.RunId != address.RunId || evidence.StepInstanceId != address.StepInstanceId) {
            throw new ProcessExecutionAuthorityMismatchException("The selected Agent execution differs from its exact admitted child or registered contract.");
        }
    }

    internal string Fingerprint(string originalSource, ProcessDecisionDataPolicyAdmission policy)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Domain = "process-decision-agent-source/v1", OriginalSource = originalSource,
            OriginalChild = ProcessDecisionChildMaterialBindingCodec.Serialize(Original),
            OriginalPolicy = ProcessDecisionDataPolicyAdmissionCodec.Serialize(policy)
        })))).ToLowerInvariant();
}

internal static class EfProcessDecisionDispatchBinding {
    internal static async Task<(bool Allowed, DateTimeOffset ObservedAt)> RequireAsync(ProcessPersistenceDbContext context, ProcessExecutionClaimEvidence evidence,
        ProcessRunId rootRunId, ProcessAssignmentSource originalAssignment, ProcessRuntimeStepAssignment assignment,
        ProcessDecisionDispatchSelection selection, CancellationToken cancellationToken) {
        if (originalAssignment.ReadinessHash != assignment.ReadinessHash || originalAssignment.DecisionDataPolicy is null || assignment.DecisionDataPolicy is null ||
            ProcessDecisionDataPolicyAdmissionCodec.Serialize(originalAssignment.DecisionDataPolicy) !=
                ProcessDecisionDataPolicyAdmissionCodec.Serialize(assignment.DecisionDataPolicy)) {
            throw Mismatch("The Process assignment changed its originally admitted readiness or decision policy.");
        }
        var child = await RequireOriginalAsync(context, evidence, rootRunId, originalAssignment, selection, cancellationToken).ConfigureAwait(false);
        var observedAt = selection.Clock.GetUtcNow();
        return (evidence.ExecutionMayDispatch && child.Observation.IsCurrent && child.Observation.Generation.Phase == ProcessDecisionPhase.Active &&
            child.Observation.Closure is null && child.Observation.Settlement is null && !child.BudgetOverrun && selection.Original.Child.Grant.WorkTicks > 0 &&
            child.RootStatus != ProcessRuntimeStatus.CancelRequested && !ProcessRuntimeTerminalStates.IsRunTerminal(child.RootStatus) &&
            child.RunStatus == ProcessRuntimeStatus.Active && child.StepStatus is (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running) &&
            child.ActiveClaimToken == evidence.DispatchClaimToken && child.MatchingClaimExpiresAtUtc > observedAt, observedAt);
    }

    internal static async Task<ProcessDecisionChildRead> RequireOriginalAsync(ProcessPersistenceDbContext context, ProcessExecutionClaimEvidence evidence,
        ProcessRunId rootRunId, ProcessAssignmentSource originalAssignment, ProcessDecisionDispatchSelection selection,
        CancellationToken cancellationToken) {
        var original = selection.Original;
        var address = original.Child.Address;
        var policy = originalAssignment.DecisionDataPolicy
            ?? throw Mismatch("The original Process assignment has no decision policy.");
        policy.RequireExecutionContract(selection.SupportedExecution);
        if (!string.Equals(originalAssignment.ExecutorKind, ProcessLaunchExecutorKinds.Agent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(originalAssignment.ExecutorId, out var originalAgent) || originalAgent != evidence.ExecutorAgentId ||
            string.IsNullOrWhiteSpace(originalAssignment.ReadinessHash)) {
            throw Mismatch("The selected child differs from its originally admitted Agent or readiness contract.");
        }
        var expectedOwner = policy.SharedRunScopeId is null ? address.RunId : address.RootRunId;
        if (rootRunId != address.RootRunId || policy.StepInstanceId != address.StepInstanceId ||
            policy.WorkspaceNamespaceId != original.Scope.WorkspaceNamespaceId || policy.Policy.ReadBasisMode != original.Scope.ReadBasisMode ||
            original.Scope.OwnerRunId != expectedOwner || policy.SharedRunScopeId is { } shared && shared != original.Scope.Id ||
            policy.ContentContract != selection.SupportedContent || policy.MaterialProtocol is null) {
            throw Mismatch("The selected child differs from its original Process material scope or registered content contract.");
        }
        var child = await EfProcessDecisionChildQuery.ReadCoreAsync(context, address, cancellationToken).ConfigureAwait(false)
            ?? throw Mismatch("The exact selected Process child is unavailable.");
        if (child.Observation.Admission != original.Child) {
            throw Mismatch("The selected Process child or its original grant changed.");
        }
        await RequireMaterialStampAsync(context, selection, policy, cancellationToken).ConfigureAwait(false);
        return child;
    }

    private static async Task RequireMaterialStampAsync(ProcessPersistenceDbContext context, ProcessDecisionDispatchSelection selection,
        ProcessDecisionDataPolicyAdmission policy, CancellationToken cancellationToken) {
        var original = selection.Original;
        var reference = original.Material.Reference;
        var row = await (from material in context.DecisionMaterials.AsNoTracking()
            join scope in context.DecisionMaterialScopes.AsNoTracking() on material.ScopeId equals scope.ScopeId
            where material.MaterialId == reference.MaterialId.Value && material.ScopeId == reference.ScopeId.Value && material.Generation == reference.Generation
            select new { Scope = scope, material.Fingerprint }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null) {
            throw Mismatch("The exact selected Process material is unavailable.");
        }
        var saved = row.Scope;
        ProcessDecisionMaterialScope scopeValue = new(new(saved.ScopeId), saved.DatabaseProfileId, saved.WorkspaceNamespaceId,
            new(saved.RootRunId), new(saved.OwnerRunId), saved.StepInstanceId is { } step ? new(step) : null);
        if (scopeValue != original.Scope || row.Fingerprint != original.Material.Fingerprint || saved.MaterialProtocol != policy.MaterialProtocol ||
            saved.ContractId != (selection.SupportedContent?.SchemaId ?? selection.SupportedExecution.ContractId) ||
            saved.ContractVersion != (selection.SupportedContent?.Version ?? selection.SupportedExecution.Version) ||
            saved.Executor != (selection.SupportedContent is null ? (ProcessDecisionChildOwner?)selection.SupportedExecution.Executor : null)) {
            throw Mismatch("The selected Process material stamp, scope or protocol contract changed.");
        }
    }

    private static ProcessExecutionAuthorityMismatchException Mismatch(string message) => new(message);
}
