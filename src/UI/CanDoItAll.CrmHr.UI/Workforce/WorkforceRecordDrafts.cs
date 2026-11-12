using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Workforce;

public enum WorkforceSkillField { Skill, Proficiency, Years, Certification, Validated, Notes }
public enum WorkforceDefinitionField { Name, Category, Description, Active }
public enum WorkforceCapacityField { Kind, Dates, Percentage, Notes }

public sealed class WorkforceRecordDrafts {
    private PartySkillEditorModel? skillReviewLocal;
    private PartySkillEditorModel? skillUndo;
    private long skillHints;
    private long comparedHint;
    private long comparedInput;
    public WorkforceSkillReview? SkillReview { get; private set; }
    public bool SkillOutdated { get; private set; }
    public bool SkillMissing { get; private set; }
    public bool SkillReviewStale => comparedHint != skillHints;
    public bool CanApplySkill => SkillReview?.CanApply == true && !SkillReviewStale && Skill.CanSave && !SkillMissing &&
        comparedInput == Skill.InputRevision && skillReviewLocal is not null && SameSkill(skillReviewLocal, Skill.Draft);
    public bool CanUndoSkill => skillUndo is not null && Skill.CanSave && SkillReview is null && !SkillMissing;
    public bool CanSaveSkill => Skill.CanSave && !SkillOutdated && !SkillMissing && SkillReview is null;
    public RecordDraftState<PartySkillEditorModel, WorkforceSkillField> Skill { get; } = new(model => model.Snapshot(), SameSkill, CopySkill);
    public RecordDraftState<SkillDefinitionEditorModel, WorkforceDefinitionField> Definition { get; } = new(model => model.Snapshot(), SameDefinition, CopyDefinition);
    public RecordDraftState<CapacityBlockEditorModel, WorkforceCapacityField> Capacity { get; } = new(model => model.Snapshot(), SameCapacity, CopyCapacity);
    public bool IsDirty => Skill.IsDirty || SkillReview is not null || Definition.IsDirty || Capacity.IsDirty;
    public bool IsUnknown => Skill.IsUnknown || Definition.IsUnknown || Capacity.IsUnknown;

    public WorkforceRecordDrafts() => Open(Guid.Empty);

    public void Open(Guid partyId) {
        Skill.Open(NewSkill(partyId));
        Definition.Open(new());
        Capacity.Open(NewCapacity(partyId));
        SkillReview = null;
        skillUndo = skillReviewLocal = null;
        SkillOutdated = SkillMissing = false;
        skillHints = comparedHint = comparedInput = 0;
    }

    public void SelectSkill(Guid skillId, PartySkillObservation? observed) {
        if (observed is not null && (observed.PartyId != Skill.Draft.PartyId || observed.SkillId != skillId)) {
            throw new InvalidOperationException("The selected skill observation identifies another worker or skill.");
        }
        var untouched = !Skill.IsDirty && !Skill.IsSaving;
        if (untouched && observed is not null) {
            CopySkill(Skill.Draft, From(observed));
        }
        Skill.Draft.SkillId = skillId;
        Skill.Draft.Id = observed?.Id;
        Skill.Draft.Original = observed;
        Skill.Input(new(WorkforceSkillField.Skill));
        if (untouched && observed is not null) {
            Skill.Rebase(From(observed));
        }
        SkillReview = null;
        skillUndo = skillReviewLocal = null;
        SkillOutdated = SkillMissing = false;
    }

    public void HintSkills() {
        skillHints++;
        SkillOutdated = Skill.Draft.SkillId != Guid.Empty;
    }

    public void ObserveSkills(IReadOnlyList<PartySkillItemModel> rows) {
        var current = rows.SingleOrDefault(row => row.SkillId == Skill.Draft.SkillId)?.Original;
        SkillMissing = Skill.Draft.Id.HasValue && current?.Id != Skill.Draft.Id;
        SkillOutdated = SkillMissing || current != Skill.Draft.Original;
    }

    public void CompareSkill(IReadOnlyList<PartySkillItemModel> rows, long observedHint) {
        var draft = Skill.Draft;
        if (draft.SkillId == Guid.Empty) {
            return;
        }
        var current = rows.SingleOrDefault(row => row.SkillId == draft.SkillId)?.Original;
        if (current is null || draft.Id.HasValue && draft.Id != current.Id) {
            SkillMissing = true;
            SkillOutdated = true;
            return;
        }
        var before = draft.Original is { } original ? From(original) : NewSkill(draft.PartyId);
        var review = new WorkforceSkillReview(before, draft, From(current));
        if (SkillReview is { } previous) {
            review.PreserveChoices(previous);
        }
        SkillReview = review;
        skillReviewLocal = draft.Snapshot();
        comparedHint = observedHint;
        comparedInput = Skill.InputRevision;
    }

    public long BeginSkillReview() => skillHints;

    public bool ApplySkill() {
        if (!CanApplySkill) {
            return false;
        }
        skillUndo = Skill.Draft.Snapshot();
        var candidate = SkillReview!.Candidate();
        Skill.Rebase(SkillReview.Current);
        CopySkill(Skill.Draft, candidate);
        Skill.Input(new(WorkforceSkillField.Notes));
        SkillOutdated = false;
        CancelSkillReview();
        return true;
    }

    public void CancelSkillReview() {
        SkillReview = null;
        skillReviewLocal = null;
    }

    public void UndoSkill() {
        if (!CanUndoSkill) {
            return;
        }
        var current = Skill.Draft.Original;
        CopySkill(Skill.Draft, skillUndo!);
        Skill.Draft.Id = current?.Id;
        Skill.Draft.Original = current;
        Skill.Input(new(WorkforceSkillField.Notes));
        skillUndo = null;
    }

    private static PartySkillEditorModel From(PartySkillObservation row) => new() {
        Id = row.Id, PartyId = row.PartyId, SkillId = row.SkillId, Original = row, Proficiency = row.Proficiency,
        YearsExperience = row.YearsExperience, CertificationStatus = row.CertificationStatus, LastValidatedOn = row.LastValidatedOn, Notes = row.Notes
    };

    public void AcceptSkill(RecordSubmission<PartySkillEditorModel, WorkforceSkillField> submission, WorkforceSaveReceipt<PartySkillEditorModel> receipt) {
        if (receipt.Id == Guid.Empty || receipt.Id != receipt.Accepted.Id || receipt.Accepted.PartyId != submission.Model.PartyId ||
            receipt.Accepted.SkillId != submission.Model.SkillId || receipt.Accepted.Original?.Id != receipt.Id) {
            throw new InvalidOperationException("The skill receipt does not identify its submitted worker and accepted row.");
        }
        if (submission.Model.Id is null && !Skill.HasLaterInput(submission)) {
            Skill.ResetAcceptedCreate(submission, receipt.Accepted, NewSkill(submission.Model.PartyId));
            return;
        }
        skillUndo = null;
        Skill.Accept(submission, receipt.Accepted, () => {
            var target = Skill.Draft;
            var saved = receipt.Accepted;
            if (Skill.Changed(submission, WorkforceSkillField.Skill) || target.SkillId != submission.Model.SkillId) {
                return;
            }
            target.Id = saved.Id;
            target.Original = saved.Original;
            if (!Skill.Changed(submission, WorkforceSkillField.Proficiency) && target.Proficiency == submission.Model.Proficiency) {
                target.Proficiency = saved.Proficiency;
            }
            if (!Skill.Changed(submission, WorkforceSkillField.Years) && target.YearsExperience == submission.Model.YearsExperience) {
                target.YearsExperience = saved.YearsExperience;
            }
            if (!Skill.Changed(submission, WorkforceSkillField.Certification) && target.CertificationStatus == submission.Model.CertificationStatus) {
                target.CertificationStatus = saved.CertificationStatus;
            }
            if (!Skill.Changed(submission, WorkforceSkillField.Validated) && target.LastValidatedOn == submission.Model.LastValidatedOn) {
                target.LastValidatedOn = saved.LastValidatedOn;
            }
            if (!Skill.Changed(submission, WorkforceSkillField.Notes) && target.Notes == submission.Model.Notes) {
                target.Notes = saved.Notes;
            }
        });
    }

    public void AcceptDefinition(RecordSubmission<SkillDefinitionEditorModel, WorkforceDefinitionField> submission, WorkforceSaveReceipt<SkillDefinitionEditorModel> receipt) {
        if (receipt.Id != receipt.Accepted.Id || receipt.Accepted.Original?.Id != receipt.Id) {
            throw new InvalidOperationException("The definition receipt does not identify its accepted row.");
        }
        Definition.ResetAcceptedCreate(submission, receipt.Accepted, new());
    }

    public void AcceptCapacity(RecordSubmission<CapacityBlockEditorModel, WorkforceCapacityField> submission, WorkforceSaveReceipt<CapacityBlockEditorModel> receipt) {
        if (receipt.Id != receipt.Accepted.Id || receipt.Accepted.PartyId != submission.Model.PartyId || receipt.Accepted.Original?.Id != receipt.Id) {
            throw new InvalidOperationException("The capacity receipt does not identify its submitted worker and accepted row.");
        }
        Capacity.ResetAcceptedCreate(submission, receipt.Accepted, NewCapacity(submission.Model.PartyId));
    }

    public static PartySkillEditorModel NewSkill(Guid partyId) => new() { PartyId = partyId, LastValidatedOn = DateOnly.FromDateTime(DateTime.UtcNow) };
    public static CapacityBlockEditorModel NewCapacity(Guid partyId) => new() {
        PartyId = partyId, StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    private static bool SameSkill(PartySkillEditorModel a, PartySkillEditorModel b) =>
        (a.SkillId, a.Proficiency, a.YearsExperience, a.CertificationStatus, a.LastValidatedOn, a.Notes) ==
        (b.SkillId, b.Proficiency, b.YearsExperience, b.CertificationStatus, b.LastValidatedOn, b.Notes);

    private static bool SameDefinition(SkillDefinitionEditorModel a, SkillDefinitionEditorModel b) =>
        (a.Name, a.Category, a.Description, a.IsActive) == (b.Name, b.Category, b.Description, b.IsActive);

    private static bool SameCapacity(CapacityBlockEditorModel a, CapacityBlockEditorModel b) =>
        (a.BlockKind, a.StartDate, a.EndDate, a.Percentage, a.RelatedProjectId, a.Notes) ==
        (b.BlockKind, b.StartDate, b.EndDate, b.Percentage, b.RelatedProjectId, b.Notes);

    private static void CopySkill(PartySkillEditorModel target, PartySkillEditorModel source) {
        target.Id = source.Id;
        target.Original = source.Original;
        target.PartyId = source.PartyId;
        target.SkillId = source.SkillId;
        target.Proficiency = source.Proficiency;
        target.YearsExperience = source.YearsExperience;
        target.CertificationStatus = source.CertificationStatus;
        target.LastValidatedOn = source.LastValidatedOn;
        target.Notes = source.Notes;
    }

    private static void CopyDefinition(SkillDefinitionEditorModel target, SkillDefinitionEditorModel source) {
        target.Id = source.Id;
        target.Original = source.Original;
        target.Name = source.Name;
        target.Category = source.Category;
        target.Description = source.Description;
        target.IsActive = source.IsActive;
    }

    private static void CopyCapacity(CapacityBlockEditorModel target, CapacityBlockEditorModel source) {
        target.Id = source.Id;
        target.Original = source.Original;
        target.PartyId = source.PartyId;
        target.BlockKind = source.BlockKind;
        target.StartDate = source.StartDate;
        target.EndDate = source.EndDate;
        target.Percentage = source.Percentage;
        target.RelatedProjectId = source.RelatedProjectId;
        target.RelatedProjectAdmission = source.RelatedProjectAdmission;
        target.Notes = source.Notes;
    }
}
