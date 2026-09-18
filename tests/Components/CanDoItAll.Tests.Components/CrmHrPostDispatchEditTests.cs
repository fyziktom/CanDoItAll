using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Text typed after a save was dispatched belongs to the operator, not to the read-back that follows the commit.
//
// Each test submits a value, holds the owner read the host issues after the commit, types a different value into the
// same editor while the write is in flight, releases the read and then checks both truths: persistence holds what was
// submitted, and the live draft holds what was typed afterwards, in the same instance (and therefore the same
// EditContext) with the identity the owner assigned. A second explicit save then persists the later value exactly
// once, as an update of the record the first save created.
//
// The hosts run against the real services and PostgreSQL through the component harness. Only the read-back is
// intercepted; every write reaches the owner unchanged.
public sealed class CrmHrPostDispatchEditTests
{
    [Fact]
    public async Task The_crm_profile_keeps_what_was_typed_after_the_dispatch_and_the_next_save_updates_the_same_record()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var accountId = await CreateAccountAsync(harness, "Post dispatch profile");
        var cut = RenderAccount(harness, accountId);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var draft = view.ProfileEditor;
        cut.WaitForElement("[data-testid='crmhr-account-commercial-notes']").Change("submitted while the operator kept typing");

        // The owner write is held open while the operator types the next sentence into the same field.
        control.HoldWriteIn(nameof(CrmService.SaveAccountProfileAsync));
        var save = cut.InvokeAsync(view.SaveAccountProfileAsync);
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        cut.WaitForElement("[data-testid='crmhr-account-commercial-notes']").Change("typed after the save was dispatched");
        control.Release();
        await save.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1, control.Interceptions);
        Assert.Equal(1, control.Commits);
        var committed = await crm.GetAccountWorkspaceAsync(accountId);
        Assert.Equal("submitted while the operator kept typing", committed!.Profile.CommercialNotes);
        var committedProfileId = committed.Profile.Id;
        Assert.NotNull(committedProfileId);
        // The draft, its instance and its EditContext survive, and every field nobody touched after the dispatch takes
        // what the owner accepted, the profile identity it created included.
        Assert.Same(draft, view.ProfileEditor);
        Assert.Equal("typed after the save was dispatched", view.ProfileEditor.CommercialNotes);
        Assert.Equal(committedProfileId, view.ProfileEditor.Id);
        Assert.Equal(
            "typed after the save was dispatched",
            cut.WaitForElement("[data-testid='crmhr-account-commercial-notes']").GetAttribute("value"));

        await cut.InvokeAsync(view.SaveAccountProfileAsync);

        var persisted = await crm.GetAccountWorkspaceAsync(accountId);
        Assert.Equal("typed after the save was dispatched", persisted!.Profile.CommercialNotes);
        // The same profile record, updated once: the read-back never turned the committed profile into a new one.
        Assert.Equal(committedProfileId, persisted.Profile.Id);
    }

    [Fact]
    public async Task A_connection_row_created_by_its_own_commit_adopts_the_owner_identity_while_a_row_started_after_the_dispatch_survives()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var accountId = await CreateAccountAsync(harness, "Post dispatch connections");
        var firstContactId = await CreatePartyAsync(parties, $"Connection first {Suffix()}", PartyType.Person, PartyRoleKind.CustomerContact);
        var secondContactId = await CreatePartyAsync(parties, $"Connection second {Suffix()}", PartyType.Person, PartyRoleKind.CustomerContact);
        var cut = RenderAccount(harness, accountId);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        await cut.InvokeAsync(view.AddConnectedRecord);
        var rows = view.ConnectedRecordEditors;
        rows[0].RelatedPartyId = firstContactId;
        rows[0].Notes = "submitted connection";

        control.HoldWriteIn(nameof(CrmService.SaveConnectedRecordsAsync));
        var save = cut.InvokeAsync(view.SaveConnectedRecordsAsync);
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        rows[0].Notes = "typed after the save was dispatched";
        await cut.InvokeAsync(view.AddConnectedRecord);
        rows[^1].RelatedPartyId = secondContactId;
        rows[^1].Notes = "started after the save";
        control.Release();
        await save.WaitAsync(TimeSpan.FromSeconds(30));

        var committed = await crm.GetAccountWorkspaceAsync(accountId);
        var committedConnection = Assert.Single(committed!.ConnectedRecords);
        Assert.Equal("submitted connection", committedConnection.Notes);
        Assert.Same(rows, view.ConnectedRecordEditors);
        Assert.Equal(2, view.ConnectedRecordEditors.Count);
        // The row this commit created carries the connection identity the owner assigned and the note typed after the
        // dispatch; the row started afterwards is the operator's next record and the owner has never seen it.
        Assert.Equal(committedConnection.Id, view.ConnectedRecordEditors[0].Id);
        Assert.Equal("typed after the save was dispatched", view.ConnectedRecordEditors[0].Notes);
        Assert.Null(view.ConnectedRecordEditors[1].Id);
        Assert.Equal("started after the save", view.ConnectedRecordEditors[1].Notes);

        await cut.InvokeAsync(view.SaveConnectedRecordsAsync);

        var persisted = await crm.GetAccountWorkspaceAsync(accountId);
        Assert.Equal(2, persisted!.ConnectedRecords.Count);
        // The first row was updated, not deleted and recreated, so its identity and its project links survived.
        var updated = Assert.Single(persisted.ConnectedRecords, item => item.Id == committedConnection.Id);
        Assert.Equal("typed after the save was dispatched", updated.Notes);
        Assert.Contains(persisted.ConnectedRecords, item => item.RelatedPartyId == secondContactId);
    }

    [Fact]
    public async Task The_directory_party_keeps_what_was_typed_after_the_dispatch_and_stays_the_record_the_owner_created()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var partyId = await CreatePartyAsync(parties, $"Post dispatch party {Suffix()}", PartyType.Person, PartyRoleKind.CustomerContact);
        navigation.NavigateTo($"/crm-hr/directory?partyId={partyId:D}");
        var cut = harness.Context.Render<CrmHrDirectoryPage>();
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(partyId, view.Editor.Id));
        var draft = view.Editor;
        draft.Summary = "submitted summary";

        control.HoldWriteIn(nameof(PartyDirectoryService.SavePartyAsync));
        var save = cut.InvokeAsync(view.SaveAsync);
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        draft.Summary = "typed after the save was dispatched";
        control.Release();
        await save.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1, control.Interceptions);
        Assert.True(control.Commits > 0);
        var committed = await parties.GetPartyAsync(partyId);
        Assert.Equal("submitted summary", committed!.Summary);
        Assert.Same(draft, view.Editor);
        Assert.Equal(partyId, view.Editor.Id);
        Assert.Equal("typed after the save was dispatched", view.Editor.Summary);

        await cut.InvokeAsync(view.SaveAsync);

        var persisted = await parties.GetPartyAsync(partyId);
        Assert.Equal("typed after the save was dispatched", persisted!.Summary);
        // One record throughout: the read-back never turned the saved party back into a new draft.
        Assert.Equal(partyId, persisted.Id);
    }

    [Fact]
    public async Task A_workforce_skill_editor_starts_over_after_its_commit_but_never_clears_the_next_skill_typed_into_it()
    {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hr = harness.Context.Services.GetRequiredService<HrService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var partyId = await CreateWorkforcePersonAsync(parties, hr, $"Post dispatch workforce {Suffix()}");
        var firstSkill = await CreateSkillAsync(hr, $"First skill {Suffix()}");
        var secondSkill = await CreateSkillAsync(hr, $"Second skill {Suffix()}");
        navigation.NavigateTo($"/crm-hr/workforce?partyId={partyId:D}");
        var cut = harness.Context.Render<CrmHrWorkforcePage>();
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal("Post dispatch title", view.ProfileEditor.JobTitle));
        var skillDraft = view.SkillEditor;
        skillDraft.SkillId = firstSkill;
        skillDraft.YearsExperience = 3;

        // The create editor decides whether it starts over as soon as its owner returns, so the operator types while
        // the write itself is in flight.
        control.HoldWriteIn(nameof(HrService.SavePartySkillAsync));
        var save = cut.InvokeAsync(view.SavePartySkillAsync);
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        skillDraft.SkillId = secondSkill;
        skillDraft.YearsExperience = 7;
        control.Release();
        await save.WaitAsync(TimeSpan.FromSeconds(30));

        var committed = await hr.GetWorkforceProfileWorkspaceAsync(partyId);
        Assert.Equal(firstSkill, Assert.Single(committed!.Skills).SkillId);
        Assert.Same(skillDraft, view.SkillEditor);
        Assert.Equal(secondSkill, view.SkillEditor.SkillId);
        Assert.Equal(7, view.SkillEditor.YearsExperience);

        // With nothing typed after the dispatch the create editor still starts over, as it always did.
        await cut.InvokeAsync(view.SavePartySkillAsync);

        var afterSecond = await hr.GetWorkforceProfileWorkspaceAsync(partyId);
        Assert.Equal(2, afterSecond!.Skills.Count);
        Assert.NotSame(skillDraft, view.SkillEditor);
        Assert.Equal(Guid.Empty, view.SkillEditor.SkillId);
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
            LifecycleStatus = PartyLifecycleStatus.Active,
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
            JobTitle = "Post dispatch title",
            Status = "Active",
            LastChangedBy = "component-tests"
        });
        Assert.True(profile.IsSuccess);
        return partyId;
    }

    private static async Task<Guid> CreateSkillAsync(HrService hr, string name)
    {
        var skill = await hr.SaveSkillDefinitionAsync(new SkillDefinitionEditorModel { Name = name, Category = "Testing" });
        Assert.True(skill.IsSuccess);
        return skill.Value;
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];
}
