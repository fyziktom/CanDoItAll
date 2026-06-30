using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkforceProfileIntegrationTests
{
    [Fact]
    public async Task SaveWorkforceProfileAsync_persists_reporting_line_and_role_alignment()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();

        var managerId = await CreatePartyAsync(
            partyDirectoryService,
            "Lena Manager",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            "lena.manager@example.test");
        var workerId = await CreatePartyAsync(
            partyDirectoryService,
            "Pavel Contractor",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.CustomerContact,
            "pavel.contractor@example.test");
        var unitId = await CreatePartyAsync(
            partyDirectoryService,
            "Delivery Platform",
            PartyType.OrganizationUnit,
            PartyLifecycleStatus.Active,
            PartyRoleKind.DeliveryUnit,
            "delivery.platform@example.test");

        var result = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = workerId,
            WorkforceKind = WorkforceKind.Contractor,
            EmployeeCode = "CTR-08",
            JobTitle = "Solution Architect",
            Discipline = "Architecture",
            Seniority = "Lead",
            HomeUnitPartyId = unitId,
            ManagerPartyId = managerId,
            StartDate = new DateOnly(2026, 4, 2),
            Location = "Prague",
            TimeZone = "Europe/Prague",
            InternalCostRate = 150m,
            ExternalBillingRate = 220m,
            CapacityHoursPerWeek = 32m,
            Status = "Active",
            Notes = "Available for shared delivery work.",
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess);

        var workspace = await hrService.GetWorkforceWorkspaceAsync(workerId);
        Assert.NotNull(workspace);
        Assert.Equal(WorkforceKind.Contractor, workspace.Profile.WorkforceKind);
        Assert.Equal("Solution Architect", workspace.Profile.JobTitle);
        Assert.Equal(unitId, workspace.Profile.HomeUnitPartyId);
        Assert.Equal(managerId, workspace.Profile.ManagerPartyId);
        Assert.Equal("Delivery Platform", workspace.HomeUnitName);
        Assert.Equal("Lena Manager", workspace.ManagerName);

        var directoryItems = await partyDirectoryService.ListDirectoryAsync();
        var worker = Assert.Single(directoryItems, item => item.Id == workerId);
        Assert.Contains(PartyRoleKind.Contractor, worker.Roles);
    }

    [Fact]
    public async Task SaveWorkforceProfileAsync_rejects_delivery_unit_kind_for_people()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();

        var workerId = await CreatePartyAsync(
            partyDirectoryService,
            "Jana Person",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            "jana.person@example.test");

        var result = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = workerId,
            WorkforceKind = WorkforceKind.DeliveryUnit,
            Status = "Active"
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "crmhr.workforce.person-delivery-unit");
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
