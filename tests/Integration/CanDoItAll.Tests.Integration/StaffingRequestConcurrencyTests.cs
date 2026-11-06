using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class StaffingRequestConcurrencyTests {
    [Fact]
    public async Task A_stale_staffing_request_cannot_replace_a_peers_saved_demand() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<HrService>();
        var admission = await CreateProjectAsync(scope.ServiceProvider);
        var created = await owner.SaveStaffingRequestAsync(NewRequest(admission));
        Assert.True(created.IsSuccess);
        var shown = Assert.Single((await owner.SearchStaffingRequestsAsync(new(admission.ProjectId))).Items);
        var peer = From(shown, admission);
        peer.Notes = "Accepted peer demand notes";
        peer.AllocationPercent = 75m;
        Assert.True((await owner.SaveStaffingRequestAsync(peer)).IsSuccess);
        var stale = From(shown, admission);
        stale.Notes = "Stale staffing replacement";

        var result = await owner.SaveStaffingRequestAsync(stale);

        Assert.True(result.IsFailure);
        var current = Assert.Single((await owner.SearchStaffingRequestsAsync(new(admission.ProjectId))).Items);
        Assert.Equal("Accepted peer demand notes", current.Notes);
        Assert.Equal(75m, current.AllocationPercent);
    }

    [Fact]
    public async Task An_explicit_missing_staffing_identity_cannot_create_a_replacement_row() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<HrService>();
        var admission = await CreateProjectAsync(scope.ServiceProvider);
        var draft = NewRequest(admission);
        draft.Id = Guid.NewGuid();

        var result = await owner.SaveStaffingRequestAsync(draft);

        Assert.True(result.IsFailure);
        Assert.Empty((await owner.SearchStaffingRequestsAsync(new(admission.ProjectId))).Items);
    }

    [Fact]
    public async Task Existing_staffing_replacement_requires_the_shown_row_original() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<HrService>();
        var admission = await CreateProjectAsync(scope.ServiceProvider);
        var created = await owner.SaveStaffingRequestAsync(NewRequest(admission));
        Assert.True(created.IsSuccess);
        var unobserved = NewRequest(admission);
        unobserved.Id = created.Value;
        unobserved.Notes = "Unobserved replacement";

        var result = await owner.SaveStaffingRequestAsync(unobserved);

        Assert.True(result.IsFailure);
        Assert.NotEqual(unobserved.Notes, Assert.Single((await owner.SearchStaffingRequestsAsync(new(admission.ProjectId))).Items).Notes);
    }

    [Fact]
    public async Task A_staffing_delete_requires_its_displayed_row_before_removing_current_demand() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<HrService>();
        var admission = await CreateProjectAsync(scope.ServiceProvider);
        var created = await owner.SaveStaffingRequestAsync(NewRequest(admission));
        Assert.True(created.IsSuccess);

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.DeleteStaffingRequestAsync(created.Value));

        Assert.Equal(created.Value, Assert.Single((await owner.SearchStaffingRequestsAsync(new(admission.ProjectId))).Items).Id);
    }

    internal static async Task<ProjectWriteAdmission> CreateProjectAsync(IServiceProvider services) {
        var created = await services.GetRequiredService<ProjectsService>().CreateWithAdmissionAsync(new() {
            Name = "Staffing concurrency project", Description = "Native demand proof", Objective = "Protect shown demand"
        });
        Assert.True(created.IsSuccess);
        return created.Value!;
    }

    internal static StaffingRequestEditorModel NewRequest(ProjectWriteAdmission admission) => new() {
        ProjectId = admission.ProjectId, ExpectedProjectAdmission = admission,
        Title = "Observed staffing demand", NeededRole = "Delivery engineer", AllocationPercent = 40m,
        StartDate = new(2026, 11, 2), EndDate = new(2026, 11, 30), Notes = "Original demand"
    };

    internal static StaffingRequestEditorModel From(StaffingRequestItemModel row, ProjectWriteAdmission admission) => new() {
        ExpectedProjectAdmission = admission, Original = row.Original,
        Id = row.Id, ProjectId = row.ProjectId, RequestedByPartyId = row.RequestedByPartyId,
        DeliveryUnitPartyId = row.DeliveryUnitPartyId, Title = row.Title, NeededRole = row.NeededRole,
        SkillIds = row.NeededSkills.Select(skill => skill.Id).ToList(), StartDate = row.StartDate, EndDate = row.EndDate,
        AllocationPercent = row.AllocationPercent, Status = row.Status, Notes = row.Notes
    };
}
