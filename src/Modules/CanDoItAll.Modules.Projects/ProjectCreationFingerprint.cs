using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

internal static class ProjectCreationFingerprint {
    private const string CreationFingerprintDomain = "projects-owner-creation-v2";

    internal static async Task<string> ReadAsync(ProjectsDbContext context, ProjectWriteAdmission admission,
        CancellationToken cancellationToken) {
        var project = await context.Set<Project>().AsNoTracking().SingleAsync(row =>
            row.Id == admission.ProjectId && row.LifetimeId == admission.LifetimeId, cancellationToken);
        var phases = await context.Set<ProjectPhase>().AsNoTracking().Where(row => row.ProjectId == admission.ProjectId)
            .OrderBy(row => row.Id).ToArrayAsync(cancellationToken);
        var options = await context.Set<ProjectOptionSelection>().AsNoTracking().Where(row => row.ProjectId == admission.ProjectId)
            .OrderBy(row => row.Id).ToArrayAsync(cancellationToken);
        var hierarchy = await context.Set<ProjectHierarchyLink>().AsNoTracking().Where(row =>
            row.ChildProjectId == admission.ProjectId || row.ParentProjectId == admission.ProjectId)
            .OrderBy(row => row.Id).ToArrayAsync(cancellationToken);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = CreationFingerprintDomain,
            ProfileId = admission.DatabaseProfileId,
            Project = new {
                project.Id, project.LifetimeId, project.ConcurrencyToken, project.LegacyAgentAccessBindingEligible, project.Name, project.Slug,
                project.ExternalNamespace, project.ExternalKey,
                project.Description, project.Objective, project.Status, project.CurrentPhase, project.TargetDateUtc,
                project.CreatedAtUtc, project.UpdatedAtUtc
            },
            Phases = phases,
            Options = options,
            Hierarchy = hierarchy
        });
        return Convert.ToHexStringLower(SHA256.HashData(payload));
    }
}
