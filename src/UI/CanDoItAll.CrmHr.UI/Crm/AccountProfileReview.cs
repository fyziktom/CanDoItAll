using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Crm;

public enum AccountProfileField { RelationshipStage, CommercialNotes, ConstraintNotes, TimingRiskNotes }

public sealed class AccountProfileReview(CrmAccountProfileEditorModel original, CrmAccountProfileEditorModel local,
    CrmAccountProfileEditorModel current) {
    public CrmAccountProfileEditorModel Current { get; } = current.Snapshot();
    public MergeField<CrmAccountRelationshipStage> Stage { get; } = new(original.RelationshipStage, local.RelationshipStage, current.RelationshipStage);
    public MergeField<string> Commercial { get; } = TextMerge.Compare(original.CommercialNotes, local.CommercialNotes, current.CommercialNotes).Field;
    public MergeField<string> Constraints { get; } = TextMerge.Compare(original.ConstraintNotes, local.ConstraintNotes, current.ConstraintNotes).Field;
    public MergeField<string> Timing { get; } = TextMerge.Compare(original.TimingRiskNotes, local.TimingRiskNotes, current.TimingRiskNotes).Field;
    public bool CanApply => Stage.IsResolved && Commercial.IsResolved && Constraints.IsResolved && Timing.IsResolved;

    public CrmAccountProfileEditorModel Candidate() => new() {
        Id = Current.Id, AccountPartyId = Current.AccountPartyId, Original = Current.Original, LastChangedBy = Current.LastChangedBy,
        RelationshipStage = Chosen(Stage), CommercialNotes = Chosen(Commercial), ConstraintNotes = Chosen(Constraints), TimingRiskNotes = Chosen(Timing)
    };

    public static bool SameValues(CrmAccountProfileEditorModel a, CrmAccountProfileEditorModel b) =>
        (a.RelationshipStage, a.CommercialNotes, a.ConstraintNotes, a.TimingRiskNotes) ==
        (b.RelationshipStage, b.CommercialNotes, b.ConstraintNotes, b.TimingRiskNotes);

    public static void Apply(CrmAccountProfileEditorModel draft, CrmAccountProfileEditorModel values) {
        draft.Id = values.Id;
        draft.AccountPartyId = values.AccountPartyId;
        draft.Original = values.Original;
        draft.LastChangedBy = values.LastChangedBy;
        draft.RelationshipStage = values.RelationshipStage;
        draft.CommercialNotes = values.CommercialNotes;
        draft.ConstraintNotes = values.ConstraintNotes;
        draft.TimingRiskNotes = values.TimingRiskNotes;
    }

    private static T Chosen<T>(MergeField<T> field) => field.IsResolved ? field.Result : field.Local;
}
