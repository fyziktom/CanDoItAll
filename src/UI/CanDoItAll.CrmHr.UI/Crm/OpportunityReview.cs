using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Crm;

public sealed class OpportunityReview {
    public OpportunityReview(CrmOpportunityEditorModel original, CrmOpportunityEditorModel local, CrmOpportunityEditorModel current, bool forceWholeCollection = false) {
        Original = original.Snapshot();
        Current = current.Snapshot();
        Title = TextMerge.Compare(original.Title, local.Title, current.Title).Field;
        StageDecision = new(OpportunityValues.StageDecision(original), OpportunityValues.StageDecision(local), OpportunityValues.StageDecision(current));
        RelationshipStage = TextMerge.Compare(original.RelationshipStage, local.RelationshipStage, current.RelationshipStage).Field;
        Source = new(original.OpportunitySource, local.OpportunitySource, current.OpportunitySource);
        Owner = new(original.OwnerPartyId, local.OwnerPartyId, current.OwnerPartyId);
        DeliveryUnit = new(original.DeliveryUnitPartyId, local.DeliveryUnitPartyId, current.DeliveryUnitPartyId);
        CommercialValue = new(OpportunityValues.CommercialValue(original), OpportunityValues.CommercialValue(local), OpportunityValues.CommercialValue(current));
        Probability = new(original.ProbabilityPercent, local.ProbabilityPercent, current.ProbabilityPercent);
        ExpectedClose = new(original.ExpectedCloseOn, local.ExpectedCloseOn, current.ExpectedCloseOn);
        Competitor = TextMerge.Compare(original.CompetitorName, local.CompetitorName, current.CompetitorName).Field;
        PartnerContribution = TextMerge.Compare(original.PartnerContributionSummary, local.PartnerContributionSummary, current.PartnerContributionSummary).Field;
        Summary = TextMerge.Compare(original.Summary, local.Summary, current.Summary).Field;
        Notes = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
        Project = new(OpportunityValues.Project(original), OpportunityValues.Project(local), OpportunityValues.Project(current));
        if (OpportunityValues.ProjectWasReplaced(original, current)) {
            Project.RequireChoice();
        }
        var localRows = local.Snapshot().Parties;
        var rows = PartyRowComparison.Compare(Original.Parties, localRows, Current.Parties, row => row.Id, OpportunityValues.RowEquality, out var whole);
        WholeCollection = whole ?? (forceWholeCollection ? new(Original.Parties, localRows, Current.Parties, OpportunityValues.RowsEquality) : null);
        Rows = WholeCollection is null ? rows : [];
    }
    public CrmOpportunityEditorModel Original { get; }
    public CrmOpportunityEditorModel Current { get; }
    public MergeField<string> Title { get; }
    public MergeField<OpportunityStageDecision> StageDecision { get; }
    public MergeField<string> RelationshipStage { get; }
    public MergeField<OpportunitySource> Source { get; }
    public MergeField<Guid> Owner { get; }
    public MergeField<Guid?> DeliveryUnit { get; }
    public MergeField<OpportunityCommercialValue> CommercialValue { get; }
    public MergeField<int> Probability { get; }
    public MergeField<DateOnly?> ExpectedClose { get; }
    public MergeField<string> Competitor { get; }
    public MergeField<string> PartnerContribution { get; }
    public MergeField<string> Summary { get; }
    public MergeField<string> Notes { get; }
    public MergeField<OpportunityProjectReference> Project { get; }
    public IReadOnlyList<KeyedMergeRow<Guid, CrmOpportunityPartyLinkEditorModel>> Rows { get; }
    public MergeField<IReadOnlyList<CrmOpportunityPartyLinkEditorModel>>? WholeCollection { get; }
    public bool IsResolved => Title.IsResolved && StageDecision.IsResolved && RelationshipStage.IsResolved && Source.IsResolved && Owner.IsResolved && DeliveryUnit.IsResolved && CommercialValue.IsResolved && Probability.IsResolved && ExpectedClose.IsResolved && Competitor.IsResolved && PartnerContribution.IsResolved && Summary.IsResolved && Notes.IsResolved && Project.IsResolved &&
        (WholeCollection?.IsResolved ?? Rows.All(row => row.Field.IsResolved));
    public bool CanApply => IsResolved && GroupError is null;
    public string? GroupError {
        get {
            if (!IsResolved) {
                return null;
            }
            var result = Candidate();
            if (OpportunityValues.ProjectWasReplaced(Original, Current) && result.LinkedProjectId == Current.LinkedProjectId &&
                result.LinkedProjectAdmission != Current.LinkedProjectAdmission) {
                return "The earlier project instance cannot receive this write. Choose the current instance or select another project separately.";
            }
            if (result.Parties.Any(row => Original.Parties.Any(before => before.Id == row.Id) && !Current.Parties.Any(saved => saved.Id == row.Id))) {
                return "A linked-party row was removed. Keep its current absence and add any new link separately.";
            }
            if (result.Stage == Current.Stage && !string.IsNullOrWhiteSpace(result.StageNotes)) {
                return "The current saved record already has this stage. Choose its current stage decision to avoid replaying a stage note.";
            }
            return Validate(result);
        }
    }

    public CrmOpportunityEditorModel Candidate() {
        var result = Current.Snapshot();
        result.Title = Chosen(Title);
        var stageDecision = Chosen(StageDecision);
        result.Stage = stageDecision.Stage;
        result.LostReason = stageDecision.LostReason;
        result.StageNotes = stageDecision.StageNotes;
        result.RelationshipStage = Chosen(RelationshipStage);
        result.OpportunitySource = Chosen(Source);
        result.OwnerPartyId = Chosen(Owner);
        result.DeliveryUnitPartyId = Chosen(DeliveryUnit);
        var commercialValue = Chosen(CommercialValue);
        result.CurrencyCode = commercialValue.CurrencyCode;
        result.Amount = commercialValue.Amount;
        result.ProbabilityPercent = Chosen(Probability);
        result.ExpectedCloseOn = Chosen(ExpectedClose);
        result.CompetitorName = Chosen(Competitor);
        result.PartnerContributionSummary = Chosen(PartnerContribution);
        result.Summary = Chosen(Summary);
        result.Notes = Chosen(Notes);
        var project = Chosen(Project);
        result.LinkedProjectId = project.ProjectId;
        result.LinkedProjectAdmission = project.Admission;
        var selected = WholeCollection is { } whole ? Chosen(whole) :
            Rows.Select(row => Chosen(row.Field)).Where(entry => entry.Exists).Select(entry => entry.Value!).ToList();
        result.Parties = selected.Select(row => row.Snapshot()).ToList();
        return result;
    }

    public static string? Validate(CrmOpportunityEditorModel model) {
        if (model.AccountPartyId == Guid.Empty || model.OwnerPartyId == Guid.Empty || string.IsNullOrWhiteSpace(model.Title)) {
            return "Choose an account and owner and enter an opportunity title.";
        }
        if (!Enum.IsDefined(model.Stage) || !Enum.IsDefined(model.OpportunitySource) || model.ProbabilityPercent is < 0 or > 100 ||
            model.CurrencyCode.Trim() is not { Length: 3 } code || !code.All(char.IsAsciiLetter)) {
            return "Choose a valid stage, source, probability and three-letter currency.";
        }
        if (model.Stage == OpportunityStage.Lost && string.IsNullOrWhiteSpace(model.LostReason)) {
            return "A lost stage needs its loss reason in the same decision.";
        }
        if (model.LinkedProjectId is Guid id && (model.LinkedProjectAdmission is not { } admission || admission.ProjectId != id)) {
            return "Select the linked project again to observe its lifetime.";
        }
        if (model.Parties.Any(row => row.Id is null || row.Id == Guid.Empty || row.PartyId == Guid.Empty || row.PartyId == model.AccountPartyId || !Enum.IsDefined(row.Role)) ||
            model.Parties.Select(row => row.Id).Distinct().Count() != model.Parties.Count ||
            model.Parties.Select(row => (row.PartyId, row.Role)).Distinct().Count() != model.Parties.Count) {
            return "Linked-party rows need distinct identities and party/role pairs.";
        }
        return null;
    }


    public OpportunityReview Refresh(CrmOpportunityEditorModel current, CrmOpportunityEditorModel draft) {
        var next = new OpportunityReview(Original, RefreshLocalDraft(draft), current, forceWholeCollection: WholeCollection is not null);
        Retain(Title, next.Title);
        Retain(StageDecision, next.StageDecision);
        Retain(RelationshipStage, next.RelationshipStage);
        Retain(Source, next.Source);
        Retain(Owner, next.Owner);
        Retain(DeliveryUnit, next.DeliveryUnit);
        Retain(CommercialValue, next.CommercialValue);
        Retain(Probability, next.Probability);
        Retain(ExpectedClose, next.ExpectedClose);
        Retain(Competitor, next.Competitor);
        Retain(PartnerContribution, next.PartnerContribution);
        Retain(Summary, next.Summary);
        Retain(Notes, next.Notes);
        Retain(Project, next.Project);
        if (WholeCollection is { } previousWhole && next.WholeCollection is { } whole) {
            Retain(previousWhole, whole, OpportunityValues.SameRows);
        } else {
            var previousRows = Rows.ToDictionary(row => row.Key);
            foreach (var row in next.Rows) {
                if (previousRows.TryGetValue(row.Key, out var previous)) {
                    Retain(previous.Field, row.Field, (a, b) => a.Exists == b.Exists &&
                        (!a.Exists || OpportunityValues.RowEquality.Equals(a.Value, b.Value)));
                }
            }
        }
        return next;
    }

    private CrmOpportunityEditorModel RefreshLocalDraft(CrmOpportunityEditorModel draft) {
        var local = draft.Snapshot();
        local.Title = PreviousChoice(Title, local.Title);
        var stageDecision = PreviousChoice(StageDecision, OpportunityValues.StageDecision(local));
        local.Stage = stageDecision.Stage;
        local.LostReason = stageDecision.LostReason;
        local.StageNotes = stageDecision.StageNotes;
        local.RelationshipStage = PreviousChoice(RelationshipStage, local.RelationshipStage);
        local.OpportunitySource = PreviousChoice(Source, local.OpportunitySource);
        local.OwnerPartyId = PreviousChoice(Owner, local.OwnerPartyId);
        local.DeliveryUnitPartyId = PreviousChoice(DeliveryUnit, local.DeliveryUnitPartyId);
        var commercialValue = PreviousChoice(CommercialValue, OpportunityValues.CommercialValue(local));
        local.CurrencyCode = commercialValue.CurrencyCode;
        local.Amount = commercialValue.Amount;
        local.ProbabilityPercent = PreviousChoice(Probability, local.ProbabilityPercent);
        local.ExpectedCloseOn = PreviousChoice(ExpectedClose, local.ExpectedCloseOn);
        local.CompetitorName = PreviousChoice(Competitor, local.CompetitorName);
        local.PartnerContributionSummary = PreviousChoice(PartnerContribution, local.PartnerContributionSummary);
        local.Summary = PreviousChoice(Summary, local.Summary);
        local.Notes = PreviousChoice(Notes, local.Notes);
        var project = PreviousChoice(Project, OpportunityValues.Project(local));
        local.LinkedProjectId = project.ProjectId;
        local.LinkedProjectAdmission = project.Admission;
        if (WholeCollection is { Choice: MergeChoice.Current or MergeChoice.Custom } whole) {
            local.Parties = whole.Result.Select(row => row.Snapshot()).ToList();
        } else if (WholeCollection is null) {
            var rows = local.Parties.ToDictionary(row => row.Id!.Value);
            foreach (var row in Rows.Where(row => row.Field.Choice is MergeChoice.Current or MergeChoice.Custom)) {
                var choice = row.Field.Result;
                if (choice.Exists) {
                    rows[row.Key] = choice.Value!.Snapshot();
                } else {
                    rows.Remove(row.Key);
                }
            }
            local.Parties = rows.Values.ToList();
        }
        return local;

        static T PreviousChoice<T>(MergeField<T> field, T draftValue) =>
            field.Choice is MergeChoice.Current or MergeChoice.Custom ? field.Result : draftValue;
    }

    private static void Retain<T>(MergeField<T> before, MergeField<T> after, Func<T, T, bool>? same = null) {
        if (before.Choice is MergeChoice.Automatic or MergeChoice.Unresolved) {
            return;
        }
        if (!(same ?? EqualityComparer<T>.Default.Equals)(before.Current, after.Current)) {
            after.RequireChoice();
        } else if (before.Choice == MergeChoice.Current) {
            after.UseCurrent();
        } else if (before.Choice == MergeChoice.Local) {
            after.UseLocal();
        } else {
            after.SetResult(before.Result);
        }
    }

    private static T Chosen<T>(MergeField<T> field) => field.IsResolved ? field.Result : field.Local;
}

