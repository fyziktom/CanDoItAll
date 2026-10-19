using System.Collections.Immutable;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

[JsonConverter(typeof(ProcessDecisionDataPolicyAdmissionJsonConverter))]
public sealed record ProcessDecisionDataPolicyAdmission(
    Guid WorkspaceNamespaceId,
    Guid WorkspaceHeadRevision,
    ProcessDefinitionVersionId DefinitionVersionId,
    ProcessStepInstanceId StepInstanceId,
    DecisionDataPolicy WorkspacePolicy,
    DecisionDataPolicyOverride? DefinitionOverride,
    DecisionDataPolicyOverride? StepOverride,
    DecisionDataPolicyOverride? AuthorizedInvocationOverride,
    DecisionDataPolicyCapabilities OwnerCapabilities,
    ImmutableArray<DecisionDataPolicyBudget> EnclosingBudgets,
    DecisionDataPolicy Policy) {
    public const int LegacySchemaVersion = 1;
    public const int SchemaVersion = 2;
    public const int MaximumUtf8Bytes = 32_768;
    public const int MaximumEnclosingPolicies = 32;

    public ProcessDecisionExecutionContract? ExecutionContract { get; init; }

    public void Validate() {
        if (WorkspaceNamespaceId == Guid.Empty || WorkspaceHeadRevision == Guid.Empty ||
            DefinitionVersionId.Value == Guid.Empty || StepInstanceId.Value == Guid.Empty ||
            WorkspacePolicy is null || OwnerCapabilities is null || Policy is null ||
            EnclosingBudgets.IsDefault || EnclosingBudgets.Length > MaximumEnclosingPolicies ||
            EnclosingBudgets.Any(budget => budget is null)) {
            throw new InvalidDataException("The Process decision policy requires its original identity and bounded provenance.");
        }
        ExecutionContract?.Validate();
        foreach (var preference in new[] { DefinitionOverride, StepOverride, AuthorizedInvocationOverride }) {
            if (preference is not null) {
                _ = DecisionDataPolicyOverrideCodec.Serialize(preference);
            }
        }
        var resolved = DecisionDataPolicyResolver.Resolve(WorkspacePolicy, OwnerCapabilities,
            definition: DefinitionOverride, step: StepOverride, authorizedInvocation: AuthorizedInvocationOverride,
            enclosingBudgets: EnclosingBudgets);
        if (resolved != Policy) {
            throw new InvalidDataException("The Process decision policy differs from its retained source preferences and limits.");
        }
    }

    public void RequireBinding(ProcessDefinitionVersionId definitionVersionId, ProcessStepInstanceId stepInstanceId) {
        Validate();
        if (DefinitionVersionId != definitionVersionId || StepInstanceId != stepInstanceId) {
            throw new InvalidDataException("The Process decision policy belongs to another definition version or logical step.");
        }
    }

    public void RequireExecutionContract(ProcessDecisionExecutionContract supported) {
        ArgumentNullException.ThrowIfNull(supported);
        supported.Validate();
        Validate();
        if (ExecutionContract != supported) {
            throw new InvalidDataException("The Process decision requires its exact admitted execution contract, version and executor owner.");
        }
    }
}
