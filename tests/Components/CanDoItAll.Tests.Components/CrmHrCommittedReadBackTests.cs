using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// What the four editing hosts do when the owner committed and the read-back that follows it does not come back.
//
// A commit is knowledge: the write happened exactly once, the identity the owner assigned stays bound to the editor,
// the host reports a secondary warning instead of a failed write, and nothing is retried. Reconciling afterwards is a
// read: the operator reloads and sees the canonical record, and no second write reaches the owner. A write the owner
// refused is the opposite: nothing was persisted, the draft keeps every field for correction, and a stale version is a
// refusal rather than a success or an unknown outcome.
//
// The hosts run against the real services and PostgreSQL through the component harness. The owner keeps its real
// persistence; only the named read the host issues after the commit is made to fail, and every write is counted.
public sealed class CrmHrCommittedReadBackTests
{
    [Fact]
    public async Task The_crm_account_reports_a_failed_read_back_as_a_warning_writes_once_and_reconciles_by_reading()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var notifications = harness.Context.Services.GetRequiredService<NotificationService>();
        var accountId = await CreateAccountAsync(harness, "Read back warning");
        var cut = RenderAccount(harness, accountId);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var draft = view.ProfileEditor;
        draft.CommercialNotes = "committed while the read-back failed";
        notifications.Messages.Clear();

        control.FailReadIn(nameof(CrmService.GetAccountWorkspaceAsync), HostReloads.Crm);
        await cut.InvokeAsync(view.SaveAccountProfileAsync);

        Assert.Equal(1, control.Interceptions);
        // Exactly one write reached the owner, and it is the one the operator dispatched.
        Assert.Equal(1, control.Commits);
        Assert.Equal("committed while the read-back failed", (await crm.GetAccountWorkspaceAsync(accountId))!.Profile.CommercialNotes);
        // The failure is a secondary warning about the view, never a failed write and never a success.
        var warning = Assert.Single(notifications.Messages);
        Assert.Equal(NotificationSeverity.Warning, warning.Severity);
        Assert.Equal("Refresh failed", warning.Summary);
        Assert.Contains("kept by its owner", warning.Detail, StringComparison.Ordinal);
        // The editor still holds the record it committed; it never became a new unsaved account.
        Assert.Same(draft, view.ProfileEditor);
        Assert.Equal(accountId, view.ProfileEditor.AccountPartyId);
        Assert.Equal(accountId, view.SelectedAccount?.AccountPartyId);

        // Reconciling is a read: the operator reloads the same record and no second write reaches the owner.
        notifications.Messages.Clear();
        await cut.InvokeAsync(() => view.SelectAccountAsync(accountId));

        cut.WaitForAssertion(() => Assert.Equal("committed while the read-back failed", view.ProfileEditor.CommercialNotes));
        Assert.Equal(1, control.Commits);
        Assert.DoesNotContain(notifications.Messages, message => message.Severity == NotificationSeverity.Error);
    }

    [Fact]
    public async Task The_directory_keeps_the_party_identity_its_commit_created_when_the_read_back_fails_and_the_next_save_updates_it()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var notifications = harness.Context.Services.GetRequiredService<NotificationService>();
        var cut = harness.Context.Render<CrmHrDirectoryPage>();
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        await cut.InvokeAsync(view.CreateNewAsync);
        var displayName = $"Read back created party {Suffix()}";
        view.Editor.DisplayName = displayName;
        view.Editor.Summary = "created while the read-back failed";
        notifications.Messages.Clear();

        control.FailReadIn(nameof(PartyDirectoryService.GetPartyAsync), HostReloads.Directory);
        await cut.InvokeAsync(view.SaveAsync);

        Assert.Equal(1, control.Interceptions);
        var created = Assert.Single(await parties.ListPartiesAsync(), item => item.DisplayName == displayName);
        // The created identity is bound to the editor although the read-back never returned: the form is not a new
        // draft, so the record is never created a second time.
        Assert.Equal(created.Id, view.Editor.Id);
        Assert.Contains(notifications.Messages, message =>
            message.Severity == NotificationSeverity.Warning && message.Summary == "Saved, refresh failed");
        Assert.DoesNotContain(notifications.Messages, message => message.Severity == NotificationSeverity.Error);

        view.Editor.Summary = "corrected after the failed refresh";
        await cut.InvokeAsync(view.SaveAsync);

        Assert.Single(await parties.ListPartiesAsync(), item => item.DisplayName == displayName);
        Assert.Equal("corrected after the failed refresh", (await parties.GetPartyAsync(created.Id))!.Summary);
    }

    [Fact]
    public async Task The_workforce_profile_reports_a_failed_read_back_as_a_warning_and_keeps_the_record_it_committed()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hr = harness.Context.Services.GetRequiredService<HrService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notifications = harness.Context.Services.GetRequiredService<NotificationService>();
        var partyId = await CreateWorkforcePersonAsync(parties, hr, $"Read back workforce {Suffix()}");
        navigation.NavigateTo($"/crm-hr/workforce?partyId={partyId:D}");
        var cut = harness.Context.Render<CrmHrWorkforcePage>();
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal("Read back title", view.ProfileEditor.JobTitle));
        var draft = view.ProfileEditor;
        draft.JobTitle = "committed while the read-back failed";
        notifications.Messages.Clear();

        control.FailReadIn(nameof(HrService.GetWorkforceProfileWorkspaceAsync), HostReloads.Workforce);
        await cut.InvokeAsync(view.SaveWorkforceProfileAsync);

        Assert.Equal(1, control.Interceptions);
        Assert.Equal(1, control.Commits);
        var committed = await hr.GetWorkforceProfileWorkspaceAsync(partyId);
        Assert.Equal("committed while the read-back failed", committed!.Profile.JobTitle);
        Assert.Contains(notifications.Messages, message => message.Severity == NotificationSeverity.Warning);
        Assert.DoesNotContain(notifications.Messages, message => message.Severity == NotificationSeverity.Error);
        // The editor still names the profile the owner holds, so the next save updates it instead of creating one.
        Assert.Same(draft, view.ProfileEditor);
        Assert.Equal(partyId, view.ProfileEditor.PartyId);

        await cut.InvokeAsync(view.SaveWorkforceProfileAsync);

        Assert.Equal(2, control.Commits);
        Assert.Equal(committed.Profile.Id, (await hr.GetWorkforceProfileWorkspaceAsync(partyId))!.Profile.Id);
    }

    [Fact]
    public async Task The_recruiting_application_reports_a_failed_read_back_as_a_warning_and_keeps_the_committed_application()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var recruiting = harness.Context.Services.GetRequiredService<RecruitingService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notifications = harness.Context.Services.GetRequiredService<NotificationService>();
        var candidateId = await CreatePartyAsync(parties, $"Read back candidate {Suffix()}", PartyType.Person, PartyRoleKind.Candidate);
        var application = await recruiting.SaveRecruitmentApplicationAsync(new RecruitmentApplicationEditorModel
        {
            PartyId = candidateId,
            DesiredRole = "Saved role",
            Stage = RecruitmentStage.Interviewing,
            Decision = RecruitmentDecision.Pending,
            LastChangedBy = "component-tests"
        });
        Assert.True(application.IsSuccess);
        navigation.NavigateTo($"/crm-hr/recruiting?applicationId={application.Value:D}");
        var cut = harness.Context.Render<CrmHrRecruitingPage>();
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal("Saved role", view.ApplicationEditor.DesiredRole));
        var draft = view.ApplicationEditor;
        draft.DesiredRole = "committed while the read-back failed";
        notifications.Messages.Clear();

        control.FailReadIn(nameof(RecruitingService.GetRecruitmentWorkspaceAsync), HostReloads.Recruiting);
        await cut.InvokeAsync(view.SaveApplicationAsync);

        Assert.Equal(1, control.Interceptions);
        Assert.Equal(1, control.Commits);
        var committed = await recruiting.GetRecruitmentWorkspaceAsync(application.Value);
        Assert.Equal("committed while the read-back failed", committed.Application.DesiredRole);
        Assert.Contains(notifications.Messages, message =>
            message.Severity == NotificationSeverity.Warning && message.Summary == "Saved, refresh failed");
        // The application identity survives the failed read-back, so the next save is an update of the same record.
        Assert.Same(draft, view.ApplicationEditor);
        Assert.Equal(application.Value, view.ApplicationEditor.Id);
    }

    [Fact]
    public async Task A_refused_connection_write_persists_nothing_and_keeps_the_draft_for_correction()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var notifications = harness.Context.Services.GetRequiredService<NotificationService>();
        var accountId = await CreateAccountAsync(harness, "Refused connection");
        var contactId = await CreatePartyAsync(parties, $"Refused contact {Suffix()}", PartyType.Person, PartyRoleKind.CustomerContact);
        var cut = RenderAccount(harness, accountId);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        await cut.InvokeAsync(view.AddConnectedRecord);
        await cut.InvokeAsync(view.AddConnectedRecord);
        var rows = view.ConnectedRecordEditors;
        // The same directory record and role twice: the owner refuses the whole submission.
        rows[0].RelatedPartyId = contactId;
        rows[0].Notes = "first row";
        rows[1].RelatedPartyId = contactId;
        rows[1].Notes = "second row";
        notifications.Messages.Clear();
        control.ResetCommits();

        await cut.InvokeAsync(view.SaveConnectedRecordsAsync);

        Assert.Equal(0, control.Commits);
        Assert.Empty((await crm.GetAccountWorkspaceAsync(accountId))!.ConnectedRecords);
        var refusal = Assert.Single(notifications.Messages);
        Assert.Equal(NotificationSeverity.Error, refusal.Severity);
        Assert.Contains("only be connected once", refusal.Detail, StringComparison.Ordinal);
        // Nothing was written, so the draft keeps both rows exactly as typed and the operator can correct one.
        Assert.Same(rows, view.ConnectedRecordEditors);
        Assert.Equal(2, view.ConnectedRecordEditors.Count);
        Assert.Equal("first row", view.ConnectedRecordEditors[0].Notes);
        Assert.Equal("second row", view.ConnectedRecordEditors[1].Notes);

        rows[1].Role = CrmAccountConnectionRole.Sponsor;
        await cut.InvokeAsync(view.SaveConnectedRecordsAsync);

        Assert.Equal(2, (await crm.GetAccountWorkspaceAsync(accountId))!.ConnectedRecords.Count);
    }

    [Fact]
    public async Task A_stale_opportunity_version_is_refused_and_never_reported_as_saved()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var notifications = harness.Context.Services.GetRequiredService<NotificationService>();
        var accountId = await CreateAccountAsync(harness, "Stale opportunity");
        var saved = await crm.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = $"Stale version opportunity {Suffix()}",
            Stage = OpportunityStage.Qualified,
            OpportunitySource = OpportunitySource.Direct,
            OwnerPartyId = await CreatePartyAsync(
                harness.Context.Services.GetRequiredService<PartyDirectoryService>(),
                $"Stale opportunity owner {Suffix()}",
                PartyType.Person,
                PartyRoleKind.AccountManager),
            CurrencyCode = "USD",
            Amount = 1000m,
            ProbabilityPercent = 20,
            LastChangedBy = "component-tests"
        });
        Assert.True(saved.IsSuccess, string.Join(" ", saved.Errors.Select(error => error.Message)));
        var opportunity = await crm.GetOpportunityAsync(saved.Value);
        var cut = RenderAccount(harness, accountId);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        notifications.Messages.Clear();
        var submission = new CrmOpportunityEditorModel
        {
            Id = saved.Value,
            AccountPartyId = accountId,
            Title = opportunity!.Title,
            Stage = OpportunityStage.Proposal,
            OpportunitySource = OpportunitySource.Direct,
            CurrencyCode = "USD",
            Amount = 2000m,
            ProbabilityPercent = 40,
            LastChangedBy = "component-tests",
            // The record was changed by somebody else after this editor read it.
            ExpectedUpdatedAtUtc = opportunity.UpdatedAtUtc.AddMinutes(-5)
        };
        control.ResetCommits();

        await cut.InvokeAsync(() => view.SaveOpportunityAsync(submission));

        // A concurrency conflict is a refusal: nothing is persisted and nothing claims the change was kept.
        Assert.Equal(0, control.Commits);
        var unchanged = await crm.GetOpportunityAsync(saved.Value);
        Assert.Equal(opportunity.UpdatedAtUtc, unchanged!.UpdatedAtUtc);
        Assert.Equal(OpportunityStage.Qualified, unchanged.Stage);
        Assert.DoesNotContain(notifications.Messages, message => message.Severity == NotificationSeverity.Success);
    }

    [Fact]
    public async Task Selecting_another_account_while_the_read_back_is_in_flight_never_claims_the_commit_was_undone()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var notifications = harness.Context.Services.GetRequiredService<NotificationService>();
        var committedAccountId = await CreateAccountAsync(harness, "Transition committed");
        var otherAccountId = await CreateAccountAsync(harness, "Transition other");
        var cut = RenderAccount(harness, committedAccountId);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        view.ProfileEditor.CommercialNotes = "committed before the operator moved on";
        notifications.Messages.Clear();

        control.HoldReadIn(nameof(CrmService.GetAccountWorkspaceAsync), HostReloads.Crm);
        var save = cut.InvokeAsync(view.SaveAccountProfileAsync);
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        var selection = cut.InvokeAsync(() => view.SelectAccountAsync(otherAccountId));
        control.Release();
        await save.WaitAsync(TimeSpan.FromSeconds(30));
        await selection.WaitAsync(TimeSpan.FromSeconds(30));

        // The commit stands and is never repeated; the retired read-back only stops presenting a record that is no
        // longer on screen, and it claims nothing about the write.
        Assert.Equal(1, control.Commits);
        Assert.Equal(
            "committed before the operator moved on",
            (await crm.GetAccountWorkspaceAsync(committedAccountId))!.Profile.CommercialNotes);
        cut.WaitForAssertion(() => Assert.Equal(otherAccountId, view.SelectedAccount?.AccountPartyId));
        Assert.DoesNotContain(notifications.Messages, message => message.Severity == NotificationSeverity.Error);
    }

    [Fact]
    public async Task An_unrelated_editor_draft_survives_a_failed_read_back_of_the_editor_that_committed()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var accountId = await CreateAccountAsync(harness, "Unrelated draft");
        var cut = RenderAccount(harness, accountId);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var interactionDraft = view.InteractionEditor;
        interactionDraft.Subject = "an interaction the operator is still writing";
        view.ProfileEditor.CommercialNotes = "committed with a failing read-back";

        control.FailReadIn(nameof(CrmService.GetAccountWorkspaceAsync), HostReloads.Crm);
        await cut.InvokeAsync(view.SaveAccountProfileAsync);

        Assert.Equal(1, control.Commits);
        // The read-back failed, so no editor was reconciled at all: the untouched interaction draft is still there.
        Assert.Same(interactionDraft, view.InteractionEditor);
        Assert.Equal("an interaction the operator is still writing", view.InteractionEditor.Subject);
    }

    // The host methods that issue the read-back after a commit. Naming them keeps a load still in flight from taking
    // an interception meant for the read-back; a host that renames one fails its test on the interception count.
    private static class HostReloads
    {
        public const string Crm = "ReloadSelectedAccountAsync";
        public const string Directory = "LoadDirectorySnapshotAsync";
        public const string Workforce = "LoadWorkforceSnapshotAsync";
        public const string Recruiting = "LoadAsync";
    }

    private static IRenderedComponent<CrmHrCrmPage> RenderAccount(ComponentTestHarness harness, Guid accountId)
    {
        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/crm-hr/crm?accountId={accountId:D}");
        var cut = harness.Context.Render<CrmHrCrmPage>();
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(accountId, view.SelectedAccount?.AccountPartyId));
        return cut;
    }

    private static async Task<Guid> CreateAccountAsync(ComponentTestHarness harness, string label)
    {
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        return await CreatePartyAsync(parties, $"{label} {Suffix()}", PartyType.Organization, PartyRoleKind.Customer);
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService parties,
        string displayName,
        PartyType partyType,
        PartyRoleKind roleKind)
    {
        var result = await parties.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = partyType == PartyType.Person && roleKind == PartyRoleKind.Candidate
                ? PartyLifecycleStatus.Candidate
                : PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests",
            Roles = [new PartyRoleAssignmentEditorModel { RoleKind = roleKind, Title = roleKind.ToString(), IsPrimary = true }]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreateWorkforcePersonAsync(
        PartyDirectoryService parties,
        HrService hr,
        string displayName)
    {
        var partyId = await CreatePartyAsync(parties, displayName, PartyType.Person, PartyRoleKind.Employee);
        var profile = await hr.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = partyId,
            WorkforceKind = WorkforceKind.Employee,
            JobTitle = "Read back title",
            Status = "Active",
            LastChangedBy = "component-tests"
        });
        Assert.True(profile.IsSuccess);
        return partyId;
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];
}
