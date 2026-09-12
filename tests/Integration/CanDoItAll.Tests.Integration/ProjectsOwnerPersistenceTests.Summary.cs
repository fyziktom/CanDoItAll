using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed partial class ProjectsOwnerPersistenceTests {
    [Fact]
    public async Task Point_summary_bounds_owner_reads_and_preserves_hierarchy_phases_and_current_crm_labels() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        Assert.Same(projects, services.GetRequiredService<IProjectSummaryQueryService>());
        async Task<Guid> CreateAsync(string name) {
            var result = await projects.SaveAsync(new ProjectEditorModel {
                Name = name, Status = ProjectStatus.OnHold, CurrentPhase = "Review"
            });
            Assert.True(result.IsSuccess);
            return result.Value;
        }
        var target = await CreateAsync("Bounded target");
        var parent = await CreateAsync("Bounded parent");
        var child = await CreateAsync("Bounded child");
        var unrelated = await CreateAsync("Unrelated portfolio");
        await using var owner = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        owner.AddRange(
            new ProjectPhase { ProjectId = target, Name = "First", OrderIndex = 0 },
            new ProjectPhase { ProjectId = target, Name = "Second", OrderIndex = 1 },
            new ProjectPhase { ProjectId = unrelated, Name = "Unrelated phase", OrderIndex = 0 },
            new ProjectHierarchyLink { ParentProjectId = parent, ChildProjectId = target },
            new ProjectHierarchyLink { ParentProjectId = target, ChildProjectId = child },
            new ProjectHierarchyLink { ParentProjectId = unrelated, ChildProjectId = child });
        await owner.SaveChangesAsync();
        var targetRow = await owner.Set<Project>().AsNoTracking().SingleAsync(item => item.Id == target);
        var admission = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(target));
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var bridge = services.GetRequiredService<IProjectPartyIntegrationBridge>();
        foreach (var (partyType, role, name) in new[] {
            (PartyType.Organization, ProjectPartyAssignmentRole.Customer, "Bounded customer"),
            (PartyType.OrganizationUnit, ProjectPartyAssignmentRole.DeliveryUnit, "Bounded delivery"),
            (PartyType.Person, ProjectPartyAssignmentRole.Manager, "Bounded owner")
        }) {
            var party = await directory.SavePartyAsync(new PartyEditorModel {
                PartyType = partyType, DisplayName = name, LifecycleStatus = PartyLifecycleStatus.Active,
                LastChangedBy = "summary-owner-test"
            });
            Assert.True(party.IsSuccess);
            var assignment = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest {
                ProjectId = target, ExpectedProjectAdmission = admission, PartyId = party.Value,
                Role = role, IsPrimary = true, Source = "summary-owner-test"
            });
            Assert.True(assignment.IsSuccess);
        }
        var listed = Assert.Single(await projects.ListAsync(), item => item.Id == target);
        var probe = new SummaryCommandProbe();
        var options = new DbContextOptionsBuilder<ProjectsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        options.AddInterceptors(probe);
        IProjectSummaryQueryService query = ActivatorUtilities.CreateInstance<ProjectsService>(services, new Factory(options.Options));
        var summary = Assert.IsType<ProjectSummary>(await query.GetSummaryAsync(target));
        Assert.Equal(target, summary.Id);
        Assert.Equal("Bounded target", summary.Name);
        Assert.Equal(ProjectStatus.OnHold, summary.Status);
        Assert.Equal("Review", summary.CurrentPhase);
        Assert.Equal(2, summary.PhaseCount);
        Assert.Equal(1, summary.ParentCount);
        Assert.Equal(1, summary.ChildCount);
        Assert.Equal(targetRow.UpdatedAtUtc, summary.UpdatedAtUtc);
        Assert.Equal("Bounded customer", summary.PrimaryCustomerName);
        Assert.Equal("Bounded delivery", summary.PrimaryDeliveryUnitName);
        Assert.Equal("Bounded owner", summary.PrimaryOwnerName);
        Assert.Equal(3, summary.RelatedParties!.Count);
        Assert.Equal(listed.RelatedPartySearchText, summary.RelatedPartySearchText);
        Assert.Equal(JsonSerializer.Serialize(listed), JsonSerializer.Serialize(summary));
        Assert.Equal(3, probe.Commands.Count);
        Assert.All(probe.Commands, command => {
            Assert.Contains("WHERE", command.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Equal([target], command.ProjectIds);
        });
        probe.Commands.Clear();
        var missing = Guid.NewGuid();
        Assert.Null(await query.GetSummaryAsync(missing));
        Assert.Equal([missing], Assert.Single(probe.Commands).ProjectIds);
        probe.Commands.Clear();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.GetSummaryAsync(target, cancellation.Token));
    }

    private sealed class SummaryCommandProbe : DbCommandInterceptor {
        public List<(string Sql, Guid[] ProjectIds)> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.CommandText, command.Parameters.Cast<DbParameter>()
                .Select(parameter => parameter.Value).OfType<Guid>().Distinct().ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
