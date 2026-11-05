using CanDoItAll.Editing;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.UI;

public sealed record ProcessComparisonField<T>(string Label, MergeField<T> Value, Action<ProcessDefinitionDraft, T> Apply);

public sealed class ProcessDefinitionComparison {
    public ProcessDefinitionComparison(ProcessDefinitionEditorDraftProjection original, ProcessDefinitionEditorDraftProjection local,
        ProcessDefinitionEditorProjection current) {
        if (original.DefinitionKey != local.DefinitionKey || local.DefinitionKey != current.DefinitionKey) {
            throw new InvalidOperationException("A definition comparison requires the same original owner.");
        }
        Local = local;
        Current = current;
        var before = From(original);
        var mine = From(local);
        var saved = new ProcessDefinitionDraft();
        saved.Discard(current);
        Text = [
            Field("Name", before.Name, mine.Name, saved.Name, (draft, value) => draft.Name = value),
            Field("Owner", before.OwnerName, mine.OwnerName, saved.OwnerName, (draft, value) => draft.OwnerName = value),
            Field("Customer", before.CustomerName, mine.CustomerName, saved.CustomerName, (draft, value) => draft.CustomerName = value),
            Field("Summary", before.Summary, mine.Summary, saved.Summary, (draft, value) => draft.Summary = value),
            Field("Value statement", before.ValueStatement, mine.ValueStatement, saved.ValueStatement, (draft, value) => draft.ValueStatement = value),
            Field("Manager override", before.ManagerOverrideSummary, mine.ManagerOverrideSummary, saved.ManagerOverrideSummary, (draft, value) => draft.ManagerOverrideSummary = value),
            Field("Governance notes", before.GovernanceNotes, mine.GovernanceNotes, saved.GovernanceNotes, (draft, value) => draft.GovernanceNotes = value),
            Field("Change summary", before.ChangeSummary, mine.ChangeSummary, saved.ChangeSummary, (draft, value) => draft.ChangeSummary = value),
            Field("Governance policy", before.GovernancePolicySummary, mine.GovernancePolicySummary, saved.GovernancePolicySummary, (draft, value) => draft.GovernancePolicySummary = value),
            Field("Interface contract", before.InterfaceContractSummary, mine.InterfaceContractSummary, saved.InterfaceContractSummary, (draft, value) => draft.InterfaceContractSummary = value),
            Field("Constitution rules", before.ConstitutionRuleSummary, mine.ConstitutionRuleSummary, saved.ConstitutionRuleSummary, (draft, value) => draft.ConstitutionRuleSummary = value),
            Field("Operating mode summary", before.OperatingModeSummary, mine.OperatingModeSummary, saved.OperatingModeSummary, (draft, value) => draft.OperatingModeSummary = value),
            Field("Simulation readiness", before.SimulationReadinessSummary, mine.SimulationReadinessSummary, saved.SimulationReadinessSummary, (draft, value) => draft.SimulationReadinessSummary = value)
        ];
        Criticality = Field("Criticality", before.Criticality, mine.Criticality, saved.Criticality, (draft, value) => draft.Criticality = value);
        Autonomy = Field("Autonomy", before.AutonomyLevel, mine.AutonomyLevel, saved.AutonomyLevel, (draft, value) => draft.AutonomyLevel = value);
        OperatingMode = Field("Operating mode", before.OperatingMode, mine.OperatingMode, saved.OperatingMode, (draft, value) => draft.OperatingMode = value);

        ProcessDefinitionDraft From(ProcessDefinitionEditorDraftProjection value) {
            var draft = new ProcessDefinitionDraft();
            draft.Discard(current with { Identity = value.Identity, Governance = value.Governance, Contracts = value.Contracts, Simulation = value.Simulation });
            return draft;
        }
    }

    public ProcessDefinitionEditorDraftProjection Local { get; }
    public ProcessDefinitionEditorProjection Current { get; }
    public IReadOnlyList<ProcessComparisonField<string>> Text { get; }
    public ProcessComparisonField<ProcessDefinitionCriticalityLevel> Criticality { get; }
    public ProcessComparisonField<ProcessDefinitionAutonomyLevel> Autonomy { get; }
    public ProcessComparisonField<ProcessDefinitionOperatingModeKind> OperatingMode { get; }
    public bool IsResolved => Text.All(item => item.Value.IsResolved) && Criticality.Value.IsResolved && Autonomy.Value.IsResolved && OperatingMode.Value.IsResolved;

    internal void ApplyTo(ProcessDefinitionDraft draft) {
        foreach (var field in Text) {
            field.Apply(draft, field.Value.Result);
        }
        Criticality.Apply(draft, Criticality.Value.Result);
        Autonomy.Apply(draft, Autonomy.Value.Result);
        OperatingMode.Apply(draft, OperatingMode.Value.Result);
    }

    private static ProcessComparisonField<T> Field<T>(string label, T original, T local, T current, Action<ProcessDefinitionDraft, T> apply)
        => new(label, new(original, local, current), apply);
}
