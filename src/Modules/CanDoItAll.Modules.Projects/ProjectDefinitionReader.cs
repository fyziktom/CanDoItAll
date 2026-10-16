using System.Data;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDefinitionReader(
    IDbContextFactory<ProjectsDbContext> contexts,
    ProjectWriteAdmissionService admissions) {
    public async Task<Result<ProjectDefinitionSnapshot>> ReadAsync(Guid projectId,
        CancellationToken cancellationToken = default) {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        if (!context.Database.IsNpgsql() && !context.Database.IsInMemory()) {
            throw new InvalidOperationException("The project definition reader requires PostgreSQL or the test-only InMemory provider.");
        }
        await using var transaction = context.Database.IsNpgsql()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;
        var project = await context.Set<Project>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == projectId, cancellationToken);
        if (project is null) {
            return Result<ProjectDefinitionSnapshot>.Failure(Error.Failure("The project no longer exists.", ProjectErrorCodes.NotFound));
        }
        var phases = await context.Set<ProjectPhase>().AsNoTracking()
            .Where(row => row.ProjectId == projectId)
            .OrderBy(row => row.OrderIndex).ThenBy(row => row.Id)
            .Select(row => new ProjectDefinitionPhase(row.Id, row.Name, row.Goal, row.Status,
                row.StartDateUtc, row.EndDateUtc))
            .ToArrayAsync(cancellationToken);
        var options = await context.Set<ProjectOptionSelection>().AsNoTracking()
            .Where(row => row.ProjectId == projectId)
            .OrderBy(row => row.Category).ThenBy(row => row.Id)
            .Select(row => new ProjectDefinitionOption(row.Id, row.Category, row.OptionName, row.Notes))
            .ToArrayAsync(cancellationToken);
        var snapshot = new ProjectDefinitionSnapshot(
            new(admissions.Capture(project), project.ConcurrencyToken),
            new(project.Name, project.Description, project.Objective, project.Status, project.CurrentPhase, project.TargetDateUtc),
            project.ExternalNamespace is null ? null : new(project.ExternalNamespace, project.ExternalKey!),
            phases, options);
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken);
        }
        return Result<ProjectDefinitionSnapshot>.Success(snapshot);
    }
}
