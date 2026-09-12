using CanDoItAll.Modules.Workbench;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrOwnerPersistenceTests {
    [Fact]
    public async Task Crm_owner_preserves_owned_mappings_without_foreign_project_or_provider_entities() {
        await using var application = await TestApplication.CreateAsync();
        await using var complete = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var entities = owner.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
        Assert.Equal(30, entities.Length);
        Assert.All(entities, entity => Assert.Equal(typeof(Party).Assembly, entity.ClrType.Assembly));
        foreach (var entity in entities) {
            var original = Assert.IsAssignableFrom<IEntityType>(complete.GetService<IDesignTimeModel>().Model.FindEntityType(entity.ClrType));
            Assert.Equal(original.GetTableName(), entity.GetTableName());
            Assert.Equal(original.GetProperties().Select(PropertyShape), entity.GetProperties().Select(PropertyShape));
            Assert.Equal(original.GetKeys().Select(key => string.Join(",", key.Properties.Select(property => property.Name))),
                entity.GetKeys().Select(key => string.Join(",", key.Properties.Select(property => property.Name))));
            Assert.Equal(original.GetIndexes().Select(index => (index.GetDatabaseName(), index.IsUnique, index.GetFilter())),
                entity.GetIndexes().Select(index => (index.GetDatabaseName(), index.IsUnique, index.GetFilter())));
            Assert.Equal(original.GetForeignKeys().Where(key => key.PrincipalEntityType.ClrType != typeof(Project)).Select(ForeignKeyShape),
                entity.GetForeignKeys().Select(ForeignKeyShape));
        }
        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => owner.Set<ProviderProfile>().ToQueryString());
    }

    [Fact]
    public async Task Assignment_pages_preserve_database_project_name_order_ties_and_missing_project_labels() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        await using var complete = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var party = new Party { PartyType = PartyType.Person, DisplayName = "Assignment owner" };
        var projects = new[] { "beta", "Alpha", "Alpha", "Žlutý", "Unknown project" }
            .Select(name => new Project { Name = name, Slug = Guid.NewGuid().ToString("N") }).ToArray();
        var lifetimes = projects.ToDictionary(project => project.Id, project => (Guid?)project.LifetimeId);
        var projectIds = projects.Select(project => project.Id).Append(Guid.NewGuid()).Append(Guid.Empty).ToArray();
        var kinds = Enum.GetValues<ProjectPartyAssignmentKind>();
        var assignments = projectIds.SelectMany(projectId => kinds.Select(kind => new ProjectPartyAssignment {
            PartyId = party.Id, ProjectId = projectId, ProjectLifetimeId = lifetimes.GetValueOrDefault(projectId),
            AssignmentKind = kind, Notes = "Retained assignment", NodeKey = Guid.NewGuid().ToString("N")
        })).ToArray();
        complete.Add(party);
        complete.AddRange(projects);
        complete.AddRange(assignments.Where(item => item.AssignmentKind != ProjectPartyAssignmentKind.WorkItemAssignee));
        complete.AddRange(assignments.Where(item => item.AssignmentKind == ProjectPartyAssignmentKind.WorkItemAssignee)
            .Select(item => new ProjectWorkAssignmentRecord {
                Id = item.Id, PartyId = item.PartyId, ProjectId = item.ProjectId, ProjectLifetimeId = item.ProjectLifetimeId,
                Notes = item.Notes, NodeKey = item.NodeKey
            }));
        await complete.SaveChangesAsync();
        var expectedAssignments = complete.Set<ProjectPartyAssignment>().AsNoTracking()
            .Select(item => new { item.Id, item.ProjectId, item.PartyId, AssignmentKind = item.AssignmentKind.ToString() })
            .Concat(complete.Set<ProjectWorkAssignmentRecord>().AsNoTracking()
                .Select(item => new { item.Id, item.ProjectId, item.PartyId, AssignmentKind = nameof(ProjectPartyAssignmentKind.WorkItemAssignee) }));
        var expectedQuery = from assignment in expectedAssignments
            where assignment.PartyId == party.Id
            join project in complete.Set<Project>() on assignment.ProjectId equals project.Id into related
            from project in related.DefaultIfEmpty()
            select new { assignment.Id, assignment.AssignmentKind, Name = project == null ? "Unknown project" : project.Name };
        var expected = await expectedQuery.OrderBy(item => item.Name).ThenBy(item => item.AssignmentKind).ThenBy(item => item.Id).ToArrayAsync();
        var service = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        const int pageSize = 4;
        var actual = new List<PartyProjectAssignmentItemModel>();
        for (var pageIndex = 0; pageIndex <= expected.Length / pageSize; pageIndex++) {
            var page = await service.SearchPartyProjectAssignmentsAsync(new(party.Id, pageIndex, pageSize));
            Assert.Equal(expected.Length, page.TotalCount);
            actual.AddRange(page.Items);
        }
        Assert.Equal(expected.Select(item => (item.Id, item.Name, item.AssignmentKind)),
            actual.Select(item => (item.Id, item.ProjectName, item.AssignmentKind.ToString())));
    }

    [Fact]
    public async Task Assignment_report_pages_more_than_4096_distinct_projects_without_materializing_a_name_catalog() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        await using var complete = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var party = new Party { PartyType = PartyType.Person, DisplayName = "Large assignment report" };
        var projects = Enumerable.Range(0, 4097).Select(index => new Project {
            Name = $"Report project {index:D4}", Slug = Guid.NewGuid().ToString("N")
        }).ToArray();
        var start = new DateTimeOffset(2031, 2, 3, 23, 40, 0, TimeSpan.FromHours(-4));
        complete.Add(party);
        complete.AddRange(projects);
        complete.AddRange(projects.Select(project => new ProjectPartyAssignment {
            PartyId = party.Id, ProjectId = project.Id, ProjectLifetimeId = project.LifetimeId, NodeKey = Guid.NewGuid().ToString("N"),
            StartsAtUtc = start.ToUniversalTime(), EndsAtUtc = start.AddDays(1).ToUniversalTime()
        }));
        await complete.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var page = await service.SearchPartyProjectAssignmentsAsync(new(party.Id, PageIndex: 409, PageSize: 10));
        Assert.Equal(4097, page.TotalCount);
        Assert.Equal(projects.Skip(4090).Select(project => project.Name), page.Items.Select(item => item.ProjectName));
        Assert.All(page.Items, item => {
            Assert.Equal(new DateOnly(2031, 2, 4), item.StartsOn);
            Assert.Equal(new DateOnly(2031, 2, 5), item.EndsOn);
        });
    }

    [Fact]
    public async Task Staffing_project_name_search_uses_the_same_database_case_and_literal_matching() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        await using var complete = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var project = new Project { Name = "Žlutý 50%_Istanbul", Slug = Guid.NewGuid().ToString("N") };
        var request = new StaffingRequest { ProjectId = project.Id, ProjectLifetimeId = project.LifetimeId, Title = "Quiet request", NeededRole = "Resource" };
        complete.AddRange(project, request);
        await complete.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<HrService>();
        foreach (var text in new[] { "žlutý", "50%_", "istanbul", "absent" }) {
            var search = text.ToUpperInvariant();
            var expected = await complete.Set<Project>().AnyAsync(item => item.Id == project.Id && item.Name.ToUpper().Contains(search));
            var page = await service.SearchStaffingRequestsAsync(new(project.Id, SearchText: text));
            Assert.Equal(expected ? 1 : 0, page.TotalCount);
            if (expected) {
                Assert.Equal(project.Name, Assert.Single(page.Items).ProjectName);
            }
        }
    }

    [Fact]
    public async Task Workforce_reads_preserve_empty_and_missing_project_references_with_existing_labels() {
        await using var application = await TestApplication.CreateAsync();
        var party = new Party { PartyType = PartyType.Person, DisplayName = "Legacy workforce" };
        var project = new Project { Name = "Current workforce project", Slug = Guid.NewGuid().ToString("N") };
        var projectIds = new[] { Guid.Empty, Guid.NewGuid(), project.Id };
        var date = new DateTimeOffset(2031, 4, 5, 0, 0, 0, TimeSpan.Zero);
        var assignments = projectIds.Select(projectId => new ProjectPartyAssignment {
            PartyId = party.Id, ProjectId = projectId, AssignmentKind = ProjectPartyAssignmentKind.TeamMember,
            AllocationPercent = 15m, StartsAtUtc = date, EndsAtUtc = date.AddDays(3), Notes = "Retained allocation"
        }).ToArray();
        var blocks = projectIds.Select(projectId => (Guid?)projectId).Append(null).Select(projectId => new CapacityBlock {
            PartyId = party.Id, RelatedProjectId = projectId, StartDateUtc = date, EndDateUtc = date.AddDays(1),
            Percentage = 10m, Notes = "Retained capacity"
        }).ToArray();
        await using (var seed = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            seed.AddRange(party, project);
            seed.AddRange(assignments);
            seed.AddRange(blocks);
            await seed.SaveChangesAsync();
        }

        for (var restart = 0; restart < 2; restart++) {
            await using var scope = application.Services.CreateAsyncScope();
            var workspace = Assert.IsType<WorkforceCapacityWorkspaceModel>(
                await scope.ServiceProvider.GetRequiredService<HrService>().GetWorkforceCapacityWorkspaceAsync(party.Id));
            Assert.Equal(assignments.Length, workspace.ProjectAllocations.Count);
            foreach (var assignment in assignments) {
                var item = Assert.Single(workspace.ProjectAllocations, item => item.AssignmentId == assignment.Id);
                Assert.Equal(assignment.ProjectId, item.ProjectId);
                Assert.Equal(assignment.ProjectId == project.Id ? project.Name : string.Empty, item.ProjectName);
                Assert.Equal(assignment.AllocationPercent, item.AllocationPercent);
                Assert.Equal(assignment.Notes, item.Notes);
            }
            Assert.Equal(blocks.Length, workspace.CapacityBlocks.Count);
            foreach (var block in blocks) {
                var item = Assert.Single(workspace.CapacityBlocks, item => item.Id == block.Id);
                Assert.Equal(block.RelatedProjectId, item.RelatedProjectId);
                Assert.Equal(block.RelatedProjectId == project.Id ? project.Name : string.Empty, item.RelatedProjectName);
                Assert.Equal(block.Notes, item.Notes);
            }
        }
    }

    [Fact]
    public async Task Workforce_displays_retained_allocations_but_only_current_lifetimes_affect_capacity() {
        await using var application = await TestApplication.CreateAsync();
        var party = new Party { PartyType = PartyType.Person, DisplayName = "Historical capacity" };
        var current = new Project { Name = "Current allocation", Slug = Guid.NewGuid().ToString("N") };
        var retired = new Project { Name = "Retained retired allocation", Slug = Guid.NewGuid().ToString("N") };
        var original = new Project { Name = "Original allocation", Slug = Guid.NewGuid().ToString("N") };
        var replacement = new Project { Id = original.Id, Name = "Replacement allocation", Slug = Guid.NewGuid().ToString("N") };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = new DateTimeOffset(today.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        ProjectPartyAssignment[] assignments = [
            Allocation(current.Id, current.LifetimeId, 20m, 20),
            Allocation(current.Id, null, 15m, 1),
            Allocation(retired.Id, retired.LifetimeId, 15m, 2),
            Allocation(original.Id, original.LifetimeId, 15m, 3),
            Allocation(Guid.NewGuid(), Guid.NewGuid(), 15m, 4),
            Allocation(Guid.Empty, null, 15m, 5)
        ];
        await using (var seed = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            seed.AddRange(party, current, retired, original, new WorkforceProfile {
                PartyId = party.Id, WorkforceKind = WorkforceKind.Employee, CapacityHoursPerWeek = 40m, Status = "Active"
            });
            await seed.SaveChangesAsync();
            seed.Remove(original);
            seed.AddRange(new ProjectRetirementRecord {
                ProjectId = original.Id, LifetimeId = original.LifetimeId, RetiredAtUtc = DateTimeOffset.UtcNow
            }, new ProjectRetirementRecord {
                ProjectId = retired.Id, LifetimeId = retired.LifetimeId, RetiredAtUtc = DateTimeOffset.UtcNow
            });
            await seed.SaveChangesAsync();
            seed.Add(replacement);
            seed.AddRange(assignments);
            await seed.SaveChangesAsync();
        }
        Assert.NotEqual(original.LifetimeId, replacement.LifetimeId);
        await AssertCapacityAsync(assignments, 20m, today.AddDays(20));
        await using (var owner = await application.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync()) {
            owner.Remove(await owner.Set<ProjectPartyAssignment>().SingleAsync(item => item.Id == assignments[0].Id));
            await owner.SaveChangesAsync();
        }
        await AssertCapacityAsync(assignments[1..], 0m, null);

        ProjectPartyAssignment Allocation(Guid projectId, Guid? lifetimeId, decimal percent, int endDays) => new() {
            PartyId = party.Id, ProjectId = projectId, ProjectLifetimeId = lifetimeId,
            AssignmentKind = ProjectPartyAssignmentKind.TeamMember, AllocationPercent = percent,
            StartsAtUtc = start, EndsAtUtc = start.AddDays(endDays + 1), Notes = "Retained capacity evidence"
        };

        async Task AssertCapacityAsync(IReadOnlyList<ProjectPartyAssignment> expected, decimal activePercent, DateOnly? nextAvailability) {
            await using var scope = application.Services.CreateAsyncScope();
            var hr = scope.ServiceProvider.GetRequiredService<HrService>();
            var workspace = Assert.IsType<WorkforceCapacityWorkspaceModel>(await hr.GetWorkforceCapacityWorkspaceAsync(party.Id));
            Assert.Equal(expected.Select(item => item.Id).Order(), workspace.ProjectAllocations.Select(item => item.AssignmentId).Order());
            Assert.Equal(current.Name, Assert.Single(workspace.ProjectAllocations, item => item.AssignmentId == assignments[1].Id).ProjectName);
            Assert.Equal(retired.Name, Assert.Single(workspace.ProjectAllocations, item => item.AssignmentId == assignments[2].Id).ProjectName);
            Assert.Empty(Assert.Single(workspace.ProjectAllocations, item => item.AssignmentId == assignments[3].Id).ProjectName);
            Assert.All(workspace.ProjectAllocations, item => Assert.True(item.IsActive));
            Assert.Equal(activePercent, workspace.CapacitySummary.ActiveAllocationPercent);
            Assert.Equal(100m - activePercent, workspace.CapacitySummary.AvailablePercent);
            Assert.Equal(nextAvailability, workspace.CapacitySummary.NextAvailabilityOn);
            var directory = Assert.Single(await hr.ListWorkforceDirectoryAsync(), item => item.PartyId == party.Id);
            Assert.Equal(100m - activePercent, directory.AvailablePercent);
            Assert.Equal(nextAvailability, directory.NextAvailabilityOn);
        }
    }

    [Fact]
    public async Task Recruiting_reads_preserve_empty_and_missing_project_references_with_existing_labels() {
        await using var application = await TestApplication.CreateAsync();
        var party = new Party { PartyType = PartyType.Person, DisplayName = "Legacy candidate" };
        var project = new Project { Name = "Current onboarding project", Slug = Guid.NewGuid().ToString("N") };
        var recruitment = new RecruitmentApplication { PartyId = party.Id, DesiredRole = "Engineer" };
        Guid?[] projectIds = [Guid.Empty, Guid.NewGuid(), project.Id, null];
        var tasks = projectIds.Select(projectId => new OnboardingTask {
            PartyId = party.Id, RelatedProjectId = projectId, Title = "Retained onboarding task", Notes = "Retained note"
        }).ToArray();
        await using (var seed = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            seed.AddRange(party, project, recruitment);
            seed.AddRange(tasks);
            await seed.SaveChangesAsync();
        }

        for (var restart = 0; restart < 2; restart++) {
            await using var scope = application.Services.CreateAsyncScope();
            var workspace = await scope.ServiceProvider.GetRequiredService<RecruitingService>()
                .GetRecruitmentWorkspaceAsync(recruitment.Id, party.Id);
            Assert.True(workspace.HasSelectedApplication);
            Assert.Equal(tasks.Length, workspace.LifecycleTasks.Count);
            foreach (var task in tasks) {
                var item = Assert.Single(workspace.LifecycleTasks, item => item.Id == task.Id);
                Assert.Equal(task.RelatedProjectId, item.RelatedProjectId);
                Assert.Equal(task.RelatedProjectId == project.Id ? project.Name : string.Empty, item.RelatedProjectName);
                Assert.Equal(task.Notes, item.Notes);
            }
        }
    }

    [Fact]
    public async Task Opportunity_missing_project_errors_keep_the_original_reference_diagnostic() {
        await using var application = await TestApplication.CreateAsync();
        var account = new Party { PartyType = PartyType.Organization, DisplayName = "Legacy account" };
        var owner = new Party { PartyType = PartyType.Person, DisplayName = "Opportunity owner" };
        var project = new Project { Name = "Current opportunity project", Slug = Guid.NewGuid().ToString("N") };
        var projectIds = new[] { Guid.Empty, Guid.NewGuid(), project.Id };
        var opportunities = projectIds.Select(projectId => new Opportunity {
            AccountPartyId = account.Id, OwnerPartyId = owner.Id, LinkedProjectId = projectId, Title = "Retained opportunity"
        }).ToArray();
        await using (var seed = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            seed.AddRange(account, owner, project);
            seed.AddRange(opportunities);
            await seed.SaveChangesAsync();
        }

        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CrmService>();
        foreach (var opportunity in opportunities.Where(item => item.LinkedProjectId != project.Id)) {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetOpportunityAsync(opportunity.Id));
            Assert.Equal($"Opportunity '{opportunity.Id}' references missing project '{opportunity.LinkedProjectId}'.", failure.Message);
        }
        var current = Assert.IsType<CrmOpportunityDetailModel>(await service.GetOpportunityAsync(opportunities[^1].Id));
        Assert.Equal(project.Id, current.LinkedProjectId);
        Assert.Equal(project.Name, current.LinkedProjectName);
    }

    private static object PropertyShape(IProperty property) => new {
        property.Name, property.ClrType, property.IsNullable, property.IsConcurrencyToken, property.ValueGenerated,
        ColumnType = property.GetColumnType(), Length = property.GetMaxLength(), Precision = property.GetPrecision(), Scale = property.GetScale(),
        DefaultValue = property.GetDefaultValue(), DefaultSql = property.GetDefaultValueSql(), Conversion = property.GetValueConverter()?.ProviderClrType
    };

    private static object ForeignKeyShape(IForeignKey key) => new {
        Principal = key.PrincipalEntityType.ClrType, Properties = string.Join(",", key.Properties.Select(property => property.Name)),
        key.DeleteBehavior, key.IsRequired, key.IsUnique
    };
}
