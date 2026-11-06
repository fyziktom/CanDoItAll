using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.CrmHr.UI.Crm;

public enum OpportunityField { Title, StageDecision, RelationshipStage, Source, Owner, DeliveryUnit, CommercialValue, Probability, ExpectedClose, Competitor, PartnerContribution, Summary, Notes, Project, Parties }
public readonly record struct OpportunityInput(OpportunityField Field, bool Pending = false);
public sealed record OpportunityStageDecision(OpportunityStage Stage, string LostReason, string StageNotes);
public sealed record OpportunityCommercialValue(string CurrencyCode, decimal? Amount);
public sealed record OpportunityProjectReference(Guid? ProjectId, ProjectWriteAdmission? Admission);

public static class OpportunityValues {
    public static OpportunityStageDecision StageDecision(CrmOpportunityEditorModel model) => new(model.Stage, model.LostReason, model.StageNotes);
    public static OpportunityCommercialValue CommercialValue(CrmOpportunityEditorModel model) => new(model.CurrencyCode, model.Amount);
    public static OpportunityProjectReference Project(CrmOpportunityEditorModel model) => new(model.LinkedProjectId, model.LinkedProjectAdmission);
    public static bool ProjectWasReplaced(CrmOpportunityEditorModel original, CrmOpportunityEditorModel current) =>
        original.LinkedProjectId is not null && original.LinkedProjectId == current.LinkedProjectId && original.LinkedProjectAdmission != current.LinkedProjectAdmission;
    public static readonly IEqualityComparer<CrmOpportunityPartyLinkEditorModel> RowEquality = EqualityComparer<CrmOpportunityPartyLinkEditorModel>.Create(
        (a, b) => a is not null && b is not null && (a.Id, a.PartyId, a.Role) == (b.Id, b.PartyId, b.Role),
        row => HashCode.Combine(row.Id, row.PartyId, row.Role));
    public static bool SameRows(IReadOnlyList<CrmOpportunityPartyLinkEditorModel> a, IReadOnlyList<CrmOpportunityPartyLinkEditorModel> b) =>
        PartyEditorValues.SameRows(a, b, row => row.Id, RowEquality);
    public static readonly IEqualityComparer<IReadOnlyList<CrmOpportunityPartyLinkEditorModel>> RowsEquality =
        EqualityComparer<IReadOnlyList<CrmOpportunityPartyLinkEditorModel>>.Create((a, b) => ReferenceEquals(a, b) || a is not null && b is not null && SameRows(a, b), rows => rows.Count);
    public static bool Same(CrmOpportunityEditorModel a, CrmOpportunityEditorModel b) =>
        a.AccountPartyId == b.AccountPartyId &&
        a.Title == b.Title &&
        StageDecision(a) == StageDecision(b) &&
        a.RelationshipStage == b.RelationshipStage &&
        a.OpportunitySource == b.OpportunitySource &&
        a.OwnerPartyId == b.OwnerPartyId &&
        a.DeliveryUnitPartyId == b.DeliveryUnitPartyId &&
        CommercialValue(a) == CommercialValue(b) &&
        a.ProbabilityPercent == b.ProbabilityPercent &&
        a.ExpectedCloseOn == b.ExpectedCloseOn &&
        a.CompetitorName == b.CompetitorName &&
        a.PartnerContributionSummary == b.PartnerContributionSummary &&
        a.Summary == b.Summary &&
        a.Notes == b.Notes &&
        Project(a) == Project(b) && SameRows(a.Parties, b.Parties);

    public static void EnsureKeys(CrmOpportunityEditorModel model) {
        foreach (var row in model.Parties) {
            row.Id ??= Guid.NewGuid();
        }
    }

    public static void Apply(CrmOpportunityEditorModel target, CrmOpportunityEditorModel value) {
        target.Id = value.Id;
        target.ExpectedUpdatedAtUtc = value.ExpectedUpdatedAtUtc;
        target.AccountPartyId = value.AccountPartyId;
        target.LastChangedBy = value.LastChangedBy;
        target.Title = value.Title;
        target.RelationshipStage = value.RelationshipStage;
        target.OpportunitySource = value.OpportunitySource;
        target.OwnerPartyId = value.OwnerPartyId;
        target.DeliveryUnitPartyId = value.DeliveryUnitPartyId;
        target.ProbabilityPercent = value.ProbabilityPercent;
        target.ExpectedCloseOn = value.ExpectedCloseOn;
        target.CompetitorName = value.CompetitorName;
        target.PartnerContributionSummary = value.PartnerContributionSummary;
        target.Summary = value.Summary;
        target.Notes = value.Notes;
        target.Stage = value.Stage;
        target.LostReason = value.LostReason;
        target.StageNotes = value.StageNotes;
        target.CurrencyCode = value.CurrencyCode;
        target.Amount = value.Amount;
        target.LinkedProjectId = value.LinkedProjectId;
        target.LinkedProjectAdmission = value.LinkedProjectAdmission;
        var existing = target.Parties.Where(row => row.Id.HasValue).ToDictionary(row => row.Id!.Value);
        var adopted = value.Parties.Select(valueRow => {
            if (valueRow.Id is Guid id && existing.TryGetValue(id, out var row)) {
                row.PartyId = valueRow.PartyId;
                row.Role = valueRow.Role;
                return row;
            }
            return valueRow.Snapshot();
        }).ToList();
        target.Parties.Clear();
        target.Parties.AddRange(adopted);
    }

    public static CrmOpportunityEditorModel Rebase(CrmOpportunityEditorModel submitted, CrmOpportunityEditorModel local,
        CrmOpportunityEditorModel accepted, IReadOnlySet<OpportunityField> later) {
        var result = accepted.Snapshot();
        result.Title = Value(OpportunityField.Title, submitted.Title, local.Title, accepted.Title);
        var stageDecision = Value(OpportunityField.StageDecision, StageDecision(submitted), StageDecision(local), StageDecision(accepted));
        result.Stage = stageDecision.Stage;
        result.LostReason = stageDecision.LostReason;
        result.StageNotes = stageDecision.StageNotes;
        result.RelationshipStage = Value(OpportunityField.RelationshipStage, submitted.RelationshipStage, local.RelationshipStage, accepted.RelationshipStage);
        result.OpportunitySource = Value(OpportunityField.Source, submitted.OpportunitySource, local.OpportunitySource, accepted.OpportunitySource);
        result.OwnerPartyId = Value(OpportunityField.Owner, submitted.OwnerPartyId, local.OwnerPartyId, accepted.OwnerPartyId);
        result.DeliveryUnitPartyId = Value(OpportunityField.DeliveryUnit, submitted.DeliveryUnitPartyId, local.DeliveryUnitPartyId, accepted.DeliveryUnitPartyId);
        var commercialValue = Value(OpportunityField.CommercialValue, CommercialValue(submitted), CommercialValue(local), CommercialValue(accepted));
        result.CurrencyCode = commercialValue.CurrencyCode;
        result.Amount = commercialValue.Amount;
        result.ProbabilityPercent = Value(OpportunityField.Probability, submitted.ProbabilityPercent, local.ProbabilityPercent, accepted.ProbabilityPercent);
        result.ExpectedCloseOn = Value(OpportunityField.ExpectedClose, submitted.ExpectedCloseOn, local.ExpectedCloseOn, accepted.ExpectedCloseOn);
        result.CompetitorName = Value(OpportunityField.Competitor, submitted.CompetitorName, local.CompetitorName, accepted.CompetitorName);
        result.PartnerContributionSummary = Value(OpportunityField.PartnerContribution, submitted.PartnerContributionSummary, local.PartnerContributionSummary, accepted.PartnerContributionSummary);
        result.Summary = Value(OpportunityField.Summary, submitted.Summary, local.Summary, accepted.Summary);
        result.Notes = Value(OpportunityField.Notes, submitted.Notes, local.Notes, accepted.Notes);
        var project = Value(OpportunityField.Project, Project(submitted), Project(local), Project(accepted));
        result.LinkedProjectId = project.ProjectId;
        result.LinkedProjectAdmission = project.Admission;
        if (later.Contains(OpportunityField.Parties) || !SameRows(submitted.Parties, local.Parties)) {
            result.Parties = local.Parties.Select(row => row.Snapshot()).ToList();
        }
        return result;

        T Value<T>(OpportunityField field, T sent, T draft, T a) =>
            later.Contains(field) || !EqualityComparer<T>.Default.Equals(sent, draft) ? draft : a;
    }
}

