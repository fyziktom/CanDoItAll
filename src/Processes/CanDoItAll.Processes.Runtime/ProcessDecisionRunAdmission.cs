using System.Collections.Immutable;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

[JsonConverter(typeof(ProcessDecisionRunAdmissionJsonConverter))]
public sealed record ProcessDecisionRunAdmission(ProcessRunId RunId, DecisionBudgetId BudgetId, Guid DatabaseProfileId,
    Guid WorkspaceNamespaceId, Guid WorkspaceHeadRevision, ProcessDefinitionVersionId DefinitionVersionId,
    DecisionDataPolicy WorkspacePolicy, DecisionDataPolicyOverride? DefinitionOverride,
    DecisionDataPolicyOverride? AuthorizedInvocationOverride, DecisionDataPolicyCapabilities OwnerCapabilities,
    ImmutableArray<DecisionDataPolicyBudget> EnclosingBudgets, DecisionDataPolicy Policy) {
    public const int SchemaVersion = 1;
    public const int MaximumUtf8Bytes = ProcessDecisionDataPolicyAdmission.MaximumUtf8Bytes;

    public ProcessDecisionFundingReference Funding => new(RunId, BudgetId);

    public void Validate() {
        if (RunId.Value == Guid.Empty || BudgetId.Value == Guid.Empty || DatabaseProfileId == Guid.Empty ||
            WorkspaceNamespaceId == Guid.Empty || WorkspaceHeadRevision == Guid.Empty || DefinitionVersionId.Value == Guid.Empty ||
            WorkspacePolicy is null || OwnerCapabilities is null || Policy is null || EnclosingBudgets.IsDefault ||
            EnclosingBudgets.Length > ProcessDecisionDataPolicyAdmission.MaximumEnclosingPolicies || EnclosingBudgets.Any(item => item is null)) {
            throw new InvalidDataException("The Process root budget requires its original runtime identity and bounded policy provenance.");
        }
        foreach (var preference in new[] { DefinitionOverride, AuthorizedInvocationOverride }) {
            if (preference is not null) {
                _ = DecisionDataPolicyOverrideCodec.Serialize(preference);
            }
        }
        var resolved = DecisionDataPolicyResolver.Resolve(WorkspacePolicy, OwnerCapabilities, definition: DefinitionOverride,
            authorizedInvocation: AuthorizedInvocationOverride, enclosingBudgets: EnclosingBudgets);
        if (resolved != Policy) {
            throw new InvalidDataException("The root policy differs from its original preferences and limits.");
        }
    }

    public void RequireRoot(ProcessRuntimeStateSnapshot state, ProcessDefinitionVersionId definitionVersionId) {
        Validate();
        if (RunId != state.RunId || state.RunId != state.RootRunId || DefinitionVersionId != definitionVersionId ||
                state.ProjectAdmission is { } project && project.DatabaseProfileId != DatabaseProfileId) {
            throw new InvalidDataException("A root decision budget must retain its real root run, definition and database profile.");
        }
    }

    public void RequireChild(ProcessDecisionDataPolicyAdmission child) {
        Validate();
        child.Validate();
        if (child.Funding != Funding || child.WorkspaceNamespaceId != WorkspaceNamespaceId ||
            child.WorkspaceHeadRevision != WorkspaceHeadRevision || child.WorkspacePolicy != WorkspacePolicy ||
            !child.EnclosingBudgets.Contains(Policy.Budget)) {
            throw new InvalidDataException("Funded Process work must retain the original root policy and its enclosing budget.");
        }
        if (Policy.ReadBasisMode == ReadBasisMode.RunSnapshot && (child.Policy.ReadBasisMode != ReadBasisMode.RunSnapshot ||
            child.Policy.PublicationFreshness != Policy.PublicationFreshness || child.Policy.OnBasisChange != Policy.OnBasisChange)) {
            throw new InvalidDataException("Shared run material requires the root owner's original read, publication and change policy.");
        }
    }
}
