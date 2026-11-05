using CanDoItAll.AgentFramework.Models;
using CanDoItAll.CrmHr.UI.Recruiting;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class RecruitmentRecordDraftsTests {
    [Fact]
    public void Support_review_applies_only_to_the_local_draft_and_retains_choices_after_a_third_hint() {
        var workspace = Workspace();
        var drafts = Open(workspace);
        var local = Guid.NewGuid();
        var remote = Guid.NewGuid();
        drafts.Support.Draft.ManagerPartyId = local;
        var current = RecruitmentRecordDrafts.FromSupport(workspace.SupportAssignments);
        current.ManagerPartyId = remote;
        current.SetOriginal(new([new(Guid.NewGuid(), remote, 1)]));
        var comparison = drafts.SupportComparison;
        comparison.Hint();
        comparison.Compare(current, comparison.HintRevision);
        Assert.False(comparison.CanApply);
        comparison.Review!.Manager.UseLocal();
        comparison.Hint();
        Assert.False(comparison.CanApply);
        comparison.Compare(current, comparison.HintRevision);
        Assert.Equal(local, comparison.Review!.Manager.Result);

        Assert.True(comparison.Apply());

        Assert.Equal(local, drafts.Support.Draft.ManagerPartyId);
        Assert.Equal(remote, current.ManagerPartyId);
        Assert.Equal(current.Original!.Rows, drafts.Support.Draft.Original!.Rows);
        Assert.True(drafts.Support.IsDirty);
        Assert.Null(drafts.Support.Accepted);
        comparison.Undo();
        Assert.Equal(local, drafts.Support.Draft.ManagerPartyId);
        Assert.True(comparison.IsOutdated);
    }

    [Fact]
    public void Conversion_review_adopts_one_preflight_but_cannot_apply_after_pending_or_later_input() {
        var workspace = Workspace();
        var drafts = Open(workspace);
        drafts.Conversion.Draft.JobTitle = "Local title";
        var current = workspace.Conversion.Snapshot();
        current.Location = "Peer location";
        current.Original = current.Original! with { PartyUpdatedAtUtc = current.Original.PartyUpdatedAtUtc.AddSeconds(1) };
        var comparison = drafts.ConversionComparison;
        comparison.Compare(current, comparison.HintRevision);
        Assert.True(comparison.CanApply);
        drafts.Conversion.Input(new(RecruitmentConversionField.Capacity, true));
        Assert.False(comparison.Apply());
        drafts.Conversion.Input(new(RecruitmentConversionField.Capacity));
        Assert.False(comparison.Apply());
        comparison.Compare(current, comparison.HintRevision);

        Assert.True(comparison.Apply());

        Assert.Equal("Local title", drafts.Conversion.Draft.JobTitle);
        Assert.Equal("Peer location", drafts.Conversion.Draft.Location);
        Assert.Equal(current.Original, drafts.Conversion.Draft.Original);
        Assert.Null(drafts.Conversion.Accepted);
    }

    [Fact]
    public void A_hint_arriving_after_a_clean_context_read_started_requires_another_explicit_pull() {
        var workspace = Workspace();
        var drafts = Open(workspace);
        var readHint = drafts.SupportComparison.HintRevision;
        var remote = Guid.NewGuid();
        var current = workspace with { SupportAssignments = workspace.SupportAssignments with { ManagerPartyId = remote } };
        drafts.HintSupport();

        drafts.Observe(current, explicitPull: true, supportReadHint: readHint);

        Assert.Null(drafts.Support.Draft.ManagerPartyId);
        Assert.True(drafts.SupportOutdated);
        drafts.Observe(current, explicitPull: true);
        Assert.Equal(remote, drafts.Support.Draft.ManagerPartyId);
        Assert.False(drafts.SupportOutdated);
    }
    [Fact]
    public void A_created_interview_keeps_later_input_as_another_create_and_retains_pending_time_input() {
        var workspace = Workspace();
        var drafts = Open(workspace);
        var model = drafts.Interview.Draft;
        model.Feedback = "Submitted feedback";
        var submission = drafts.Interview.Begin()!;
        model.Feedback = "Later feedback";
        drafts.Interview.Input(new(RecruitmentInterviewField.Schedule, true));
        var accepted = submission.Model.Snapshot();
        accepted.Id = Guid.NewGuid();

        drafts.AcceptInterview(submission, new(accepted.Id.Value, accepted));
        drafts.Interview.Complete(submission);

        Assert.Same(model, drafts.Interview.Draft);
        Assert.Equal("Later feedback", model.Feedback);
        Assert.Null(model.Id);
        Assert.Equal("Submitted feedback", drafts.Interview.Accepted!.Feedback);
        Assert.True(drafts.Interview.HasPendingInput);
        Assert.False(drafts.Interview.CanSave);
    }

    [Fact]
    public void A_created_task_without_later_input_resets_in_place_and_keeps_the_accepted_row_separate() {
        var drafts = Open(Workspace());
        var model = drafts.Task.Draft;
        model.Title = "Submitted task";
        var submission = drafts.Task.Begin()!;
        var accepted = submission.Model.Snapshot();
        accepted.Id = Guid.NewGuid();

        drafts.AcceptTask(submission, new(accepted.Id.Value, accepted));
        drafts.Task.Complete(submission);

        Assert.Same(model, drafts.Task.Draft);
        Assert.Empty(model.Title);
        Assert.Null(model.Id);
        Assert.Equal("Submitted task", drafts.Task.Accepted!.Title);
        Assert.False(drafts.Task.IsDirty);
    }

    [Fact]
    public void Support_acceptance_keeps_later_direct_input_without_claiming_that_it_was_accepted() {
        var workspace = Workspace();
        var drafts = Open(workspace);
        var manager = Guid.NewGuid();
        var laterBuddy = Guid.NewGuid();
        drafts.Support.Draft.ManagerPartyId = manager;
        var submission = drafts.BeginSupport()!;
        drafts.Support.Draft.BuddyPartyId = laterBuddy;
        var accepted = submission.Model.Snapshot();
        accepted.SetOriginal(new([new(Guid.NewGuid(), manager, 1)]));

        drafts.AcceptSupport(submission, accepted);
        drafts.Support.Complete(submission);

        Assert.Null(drafts.Support.Accepted!.BuddyPartyId);
        Assert.Equal(laterBuddy, drafts.Support.Draft.BuddyPartyId);
        Assert.True(drafts.Support.IsDirty);
        Assert.Equal(accepted.Original!.Rows, drafts.Support.Draft.Original!.Rows);
    }

    [Fact]
    public void Conversion_acceptance_keeps_later_values_and_a_peer_hint_that_arrived_during_the_write() {
        var workspace = Workspace();
        var drafts = Open(workspace);
        var model = drafts.Conversion.Draft;
        model.JobTitle = "Submitted title";
        var submission = drafts.BeginConversion()!;
        model.CapacityHoursPerWeek = 80m;
        drafts.HintConversion();
        var receipt = ConversionReceipt(workspace);

        drafts.AcceptConversion(submission, receipt);
        drafts.Conversion.Complete(submission);

        Assert.Same(model, drafts.Conversion.Draft);
        Assert.Equal("Canonical accepted title", model.JobTitle);
        Assert.Equal(80m, model.CapacityHoursPerWeek);
        Assert.Equal(40m, drafts.Conversion.Accepted!.CapacityHoursPerWeek);
        Assert.Equal(receipt.Original, model.Original);
        Assert.True(drafts.Conversion.IsDirty);
        Assert.True(drafts.ConversionOutdated);
        Assert.Null(drafts.BeginConversion());
    }

    [Fact]
    public void An_unknown_conversion_keeps_submission_and_later_input_after_manual_context_refresh() {
        var workspace = Workspace();
        var drafts = Open(workspace);
        drafts.Conversion.Draft.Notes = "Submitted notes";
        var submission = drafts.BeginConversion()!;
        drafts.Conversion.Draft.Notes = "Later notes";
        drafts.Conversion.Unknown(submission);
        drafts.Conversion.Complete(submission);
        var receipt = ConversionReceipt(workspace);
        var current = workspace with {
            Application = receipt.Application,
            HasWorkforceProfile = true,
            Conversion = new() { ApplicationId = receipt.ApplicationId, Original = receipt.Original, JobTitle = receipt.Workforce.JobTitle }
        };

        drafts.Observe(current, explicitPull: true);

        Assert.True(drafts.IsUnknown);
        Assert.Equal("Submitted notes", drafts.Conversion.UnknownSubmission!.Model.Notes);
        Assert.Equal("Later notes", drafts.Conversion.Draft.Notes);
        Assert.Null(drafts.BeginConversion());
    }

    [Fact]
    public void A_clean_conversion_waits_for_explicit_pull_and_keeps_its_model() {
        var workspace = Workspace();
        var drafts = Open(workspace);
        var model = drafts.Conversion.Draft;
        var current = workspace.Conversion.Snapshot();
        current.JobTitle = "Peer title";
        current.Original = current.Original! with { Application = current.Original.Application with { DesiredRole = "Peer role" } };
        var next = workspace with { Conversion = current };
        drafts.Observe(next, explicitPull: false);
        Assert.NotEqual("Peer title", model.JobTitle);
        Assert.True(drafts.ConversionOutdated);

        drafts.Observe(next, explicitPull: true);

        Assert.Same(model, drafts.Conversion.Draft);
        Assert.Equal("Peer title", model.JobTitle);
        Assert.False(drafts.ConversionOutdated);
        Assert.False(drafts.Conversion.IsDirty);
    }

    private static RecruitmentRecordDrafts Open(RecruitmentWorkspaceModel workspace) {
        var drafts = new RecruitmentRecordDrafts();
        drafts.Open(workspace);
        return drafts;
    }
    private static RecruitmentWorkspaceModel Workspace() {
        var id = Guid.NewGuid();
        var party = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var original = new RecruitmentApplicationObservation(id, party, null, null, null, "Role", "", RecruitmentStage.Offer, null, RecruitmentDecision.Approved, "", at);
        var application = new RecruitmentApplicationEditorModel {
            Id = id, PartyId = party, DesiredRole = "Role", Stage = RecruitmentStage.Offer, Decision = RecruitmentDecision.Approved, Original = original
        };
        return new(application, true, "Candidate", "", "", "", PartyType.Person, null, AiResourceBindingStatus.Unbound, "", "", false,
            [], [], [], new(party, null, "", null, "", null, ""), new() {
                ApplicationId = id, Original = new(original, new(party, PartyType.Person, null, null), PartyLifecycleStatus.Candidate, at)
            });
    }
    private static RecruitmentConversionReceipt ConversionReceipt(RecruitmentWorkspaceModel workspace) {
        var application = workspace.Application.Snapshot();
        application.Stage = RecruitmentStage.Hired;
        application.Original = application.Original! with { Stage = RecruitmentStage.Hired, UpdatedAtUtc = application.Original.UpdatedAtUtc.AddSeconds(1) };
        var profile = new WorkforceProfileEditorModel {
            Id = Guid.NewGuid(), PartyId = application.PartyId!.Value, JobTitle = "Canonical accepted title", CapacityHoursPerWeek = 40m
        };
        profile.Original = new(profile.PartyId, PartyType.Person, profile.Id, WorkforceProfileValues.From(profile));
        return new(application.Id!.Value, profile.PartyId, application, profile,
            new(application.Original, profile.Original, PartyLifecycleStatus.Active, application.Original.UpdatedAtUtc));
    }
}
