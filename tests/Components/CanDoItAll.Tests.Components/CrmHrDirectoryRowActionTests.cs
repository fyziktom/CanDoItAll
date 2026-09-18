using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Row actions of the Directory editor through the production host and the real owner. Each row's controls render
// inside component child content, after the row loop has moved on; a Remove button therefore names the row it was
// rendered for, never the loop position. (Before the repair, Remove passed the row count and the host's RemoveAt threw
// out of range, failing the circuit.)
public sealed class CrmHrDirectoryRowActionTests
{
    [Fact]
    public async Task Removing_the_first_additional_role_removes_exactly_that_role_and_the_save_keeps_the_others()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var saved = await parties.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = $"Row action person {Guid.NewGuid():N}",
            LastChangedBy = "component-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel { RoleKind = PartyRoleKind.Employee, Title = "Primary employee", IsPrimary = true },
                new PartyRoleAssignmentEditorModel { RoleKind = PartyRoleKind.Stakeholder, Title = "First additional role" },
                new PartyRoleAssignmentEditorModel { RoleKind = PartyRoleKind.CustomerContact, Title = "Second additional role" }
            ]
        });
        Assert.True(saved.IsSuccess);
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/directory?partyId={saved.Value:D}");
        var cut = harness.Context.Render<CrmHrDirectoryPage>();
        // The owner decides the row order; the journey removes the row that shows the first additional role.
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid^='crmhr-party-extra-role-title-']").Count));
        var target = RowOf(cut, "crmhr-party-extra-role-title", "First additional role");
        var other = 1 - target;
        Assert.Equal("Second additional role", cut.Find($"[data-testid='crmhr-party-extra-role-title-{other}']").GetAttribute("value"));

        await cut.Find($"[data-testid='crmhr-party-extra-role-remove-{target}']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Second additional role", cut.Find("[data-testid='crmhr-party-extra-role-title-0']").GetAttribute("value"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-party-extra-role-title-1']"));
        });

        // The click completes with the host's save; the owner is read back afterwards, off the renderer's dispatcher.
        await cut.Find("[data-testid='crmhr-party-save-button']").ClickAsync(new MouseEventArgs());

        var persisted = await parties.GetPartyAsync(saved.Value);
        Assert.Equal(["Second additional role"], persisted!.Roles.Where(role => !role.IsPrimary).Select(role => role.Title).ToArray());
        Assert.Equal(PartyRoleKind.Employee, Assert.Single(persisted.Roles, role => role.IsPrimary).RoleKind);
    }

    [Fact]
    public async Task Removing_the_first_confidential_note_removes_exactly_that_note_and_the_save_keeps_the_other()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var saved = await parties.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = $"Row action note person {Guid.NewGuid():N}",
            LastChangedBy = "component-tests",
            IsSensitive = true,
            ConfidentialNotes =
            [
                new PartyConfidentialNoteEditorModel { Category = PartyConfidentialNoteCategories.HumanResources, NoteText = "Synthetic first note" },
                new PartyConfidentialNoteEditorModel { Category = PartyConfidentialNoteCategories.Compliance, NoteText = "Synthetic second note" }
            ]
        });
        Assert.True(saved.IsSuccess, string.Join(" ", saved.Errors.Select(error => error.Message)));
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/directory?partyId={saved.Value:D}");
        var cut = harness.Context.Render<CrmHrDirectoryPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-directory-tab-handling']")));
        cut.Find("[data-testid='crmhr-directory-tab-handling']").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='crmhr-confidential-note-item']").Count));
        var target = RowOf(cut, "crmhr-confidential-note-text", "Synthetic first note");

        await cut.Find($"[data-testid='crmhr-confidential-note-remove-{target}']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll("[data-testid='crmhr-confidential-note-item']"));
            Assert.Equal(PartyConfidentialNoteCategories.Compliance, cut.Find("[data-testid='crmhr-confidential-note-category-0']").GetAttribute("value"));
        });

        await cut.Find("[data-testid='crmhr-party-save-button']").ClickAsync(new MouseEventArgs());

        var persisted = await parties.GetPartyAsync(saved.Value);
        Assert.Equal(["Synthetic second note"], persisted!.ConfidentialNotes.Select(note => note.NoteText).ToArray());
    }

    // The row position whose numbered field shows the value: the numbered ids follow the rendered rows.
    private static int RowOf(IRenderedComponent<CrmHrDirectoryPage> cut, string fieldTestIdPrefix, string value)
    {
        for (var row = 0; row < 2; row++)
        {
            var field = cut.Find($"[data-testid='{fieldTestIdPrefix}-{row}']");
            if ((field.GetAttribute("value") ?? field.TextContent).Contains(value, StringComparison.Ordinal))
            {
                return row;
            }
        }

        throw new InvalidOperationException($"No rendered row shows '{value}'.");
    }
}
