using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrAuditTrailIntegrationTests
{
    [Fact]
    public async Task Non_public_contact_values_stay_out_of_search_and_picker_projections()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        const string privateEmail = "private-contact-493@example.test";
        const string publicPhone = "+1 555 01493";

        var partyResult = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Public directory identity",
            LastChangedBy = "integration-tests",
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Private email",
                    Value = privateEmail,
                    NormalizedValue = privateEmail,
                    IsPrimary = true,
                    IsPublic = false
                },
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Phone,
                    Label = "Public phone",
                    Value = publicPhone,
                    NormalizedValue = "+155501493",
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });
        Assert.True(partyResult.IsSuccess);

        Assert.Empty(await searchIndexService.SearchAsync(privateEmail));
        var option = await projectPartyBridge.GetPartyOptionAsync(partyResult.Value);
        Assert.NotNull(option);
        Assert.Equal(string.Empty, option.PrimaryEmail);
        Assert.Equal(publicPhone, option.PrimaryPhone);
    }

    [Fact]
    public async Task Sensitive_party_notes_and_workforce_records_stay_out_of_search()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var partyResult = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Aria Protected",
            Summary = "Sensitive workforce profile",
            Notes = "Operational staffing note",
            IsSensitive = true,
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = "aria.protected@example.test",
                    NormalizedValue = "aria.protected@example.test",
                    IsPrimary = true,
                    IsPublic = true
                }
            ],
            ConfidentialNotes =
            [
                new PartyConfidentialNoteEditorModel
                {
                    Category = PartyConfidentialNoteCategories.Compensation,
                    NoteText = "Private salary band 9",
                    CreatedBy = "integration-tests"
                }
            ]
        });
        Assert.True(partyResult.IsSuccess);

        var workforceResult = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = partyResult.Value,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "Confidential Operator",
            Discipline = "Operations",
            CapacityHoursPerWeek = 40m,
            Notes = "Sensitive workforce note",
            LastChangedBy = "integration-tests"
        });
        Assert.True(workforceResult.IsSuccess);

        var loadedParty = await partyDirectoryService.GetPartyAsync(partyResult.Value);
        Assert.NotNull(loadedParty);
        Assert.Equal("Operational staffing note", loadedParty.Notes);
        var confidentialNote = Assert.Single(loadedParty.ConfidentialNotes);
        Assert.Equal(PartyConfidentialNoteCategories.Compensation, confidentialNote.Category);
        Assert.Equal("Private salary band 9", confidentialNote.NoteText);

        Assert.Empty(await searchIndexService.SearchAsync("Aria Protected"));
        Assert.Empty(await searchIndexService.SearchAsync("Private salary band 9"));

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var confidentialNotes = await verificationContext.Set<PartyConfidentialNote>()
            .Where(item => item.PartyId == partyResult.Value)
            .ToListAsync();
        var searchDocuments = await verificationContext.Set<SearchDocument>()
            .Where(item => item.Route.Contains(partyResult.Value.ToString("D")))
            .ToListAsync();

        Assert.Single(confidentialNotes);
        Assert.Empty(searchDocuments);
    }

    [Fact]
    public async Task Archive_reactivate_and_workforce_updates_write_audit_history()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var createResult = await partyDirectoryService.SavePartyAsync(BuildPartyEditor("Mila Archive", null, PartyLifecycleStatus.Active));
        Assert.True(createResult.IsSuccess);

        var savedParty = await partyDirectoryService.GetPartyAsync(createResult.Value);
        Assert.NotNull(savedParty);

        var archiveResult = await partyDirectoryService.SavePartyAsync(BuildPartyEditor("Mila Archive", savedParty.Id, PartyLifecycleStatus.Archived));
        Assert.True(archiveResult.IsSuccess);

        savedParty = await partyDirectoryService.GetPartyAsync(createResult.Value);
        Assert.NotNull(savedParty);

        var reactivateResult = await partyDirectoryService.SavePartyAsync(BuildPartyEditor("Mila Archive", savedParty.Id, PartyLifecycleStatus.Active));
        Assert.True(reactivateResult.IsSuccess);

        var workforceResult = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = createResult.Value,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "Archive Proof Engineer",
            Discipline = "Platform",
            CapacityHoursPerWeek = 40m,
            Notes = "B12 audit proof",
            LastChangedBy = "integration-tests"
        });
        Assert.True(workforceResult.IsSuccess);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var auditEntries = (await verificationContext.Set<CrmHrAuditEntry>()
            .Where(item => item.EntityId == createResult.Value)
            .ToListAsync())
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();

        Assert.Contains(auditEntries, item => item.Action == "PartyCreated");
        Assert.Contains(auditEntries, item => item.Action == "PartyArchived");
        Assert.Contains(auditEntries, item => item.Action == "PartyReactivated");
        Assert.Contains(auditEntries, item => item.Action == "WorkforceProfileSaved");

        var activity = await partyDirectoryService.SearchPartyActivityAsync(
            new CrmActivityHistoryQuery(createResult.Value, PageSize: 20));
        Assert.Contains(activity.Items, item => item.Title.Contains("Archived party 'Mila Archive'.", StringComparison.Ordinal));
        Assert.Contains(activity.Items, item => item.Title.Contains("Reactivated party 'Mila Archive'.", StringComparison.Ordinal));
        Assert.Contains(activity.Items, item => item.Title.Contains("Saved workforce profile for 'Mila Archive'.", StringComparison.Ordinal));

        var workspace = await hrService.GetWorkforceWorkspaceAsync(createResult.Value);
        Assert.NotNull(workspace);
        Assert.Equal("integration-tests", workspace.LastChangedBy);
        Assert.True(workspace.UpdatedAtUtc > DateTimeOffset.MinValue);
    }

    private static PartyEditorModel BuildPartyEditor(string displayName, Guid? id, PartyLifecycleStatus status)
    {
        return new PartyEditorModel
        {
            Id = id,
            PartyType = PartyType.Person,
            LifecycleStatus = status,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            Notes = "Operational note",
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = $"{displayName.Replace(" ", ".", StringComparison.Ordinal).ToLowerInvariant()}@example.test",
                    NormalizedValue = $"{displayName.Replace(" ", ".", StringComparison.Ordinal).ToLowerInvariant()}@example.test",
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        };
    }
}
