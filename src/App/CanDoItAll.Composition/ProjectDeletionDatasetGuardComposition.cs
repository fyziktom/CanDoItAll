using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Composition;

internal static class ProjectDeletionDatasetGuardComposition {
    internal static ProjectDeletionDatasetGuard Create(ResolvedDatabaseProfile profile, CoordinatedDatabaseTransaction transactions) {
        var projects = new DbContextOptionsBuilder<ProjectsDbContext>();
        var agents = new DbContextOptionsBuilder<AgentProjectAccessDbContext>();
        var workbench = new DbContextOptionsBuilder<WorkbenchDbContext>();
        AppDbContextOptionsConfigurator.Configure(projects, profile);
        AppDbContextOptionsConfigurator.Configure(agents, profile);
        AppDbContextOptionsConfigurator.Configure(workbench, profile);
        return new(profile.Profile.Id, projects.Options, transactions,
            [new AgentProjectDeletionPendingInventory(agents.Options, transactions),
             new ProjectWorkbenchDeletionPendingInventory(workbench.Options, transactions)]);
    }
}
