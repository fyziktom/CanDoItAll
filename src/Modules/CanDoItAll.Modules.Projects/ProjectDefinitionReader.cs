using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDefinitionReader(
    IDbContextFactory<ProjectsDbContext> contexts,
    ProjectWriteAdmissionService admissions,
    DbContextOptions<ProjectsDbContext> options,
    CoordinatedDatabaseTransaction transactions) {
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

    // The caller holds lifecycle exclusion and every project gate; this read only enlists.
    public async Task<Result<ProjectDefinitionScalarSnapshot>> ReadScalarsEnlistedAsync(ProjectWriteAdmission expected,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.DatabaseProfileId != admissions.DatabaseProfileId) {
            return Refuse(ProjectErrorCodes.LifetimeChanged, "The original project belongs to another database profile.");
        }
        await using var context = await transactions.CreateEnlistedAsync(options,
            static value => new ProjectsDbContext(value), cancellationToken);
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Enlisted scalar capture requires PostgreSQL.");
        }
        var preflight = await context.Database.SqlQuery<ScalarPreflight>($"""
            SELECT p."LifetimeId", p."ConcurrencyToken", p."Status", p."TargetDateUtc",
                octet_length(p."Name") AS "NameBytes", octet_length(p."Description") AS "DescriptionBytes",
                octet_length(p."Objective") AS "ObjectiveBytes", octet_length(p."CurrentPhase") AS "CurrentPhaseBytes",
                EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" r WHERE r."LifetimeId" = p."LifetimeId") AS "Retired"
            FROM "Projects_Projects" p WHERE p."Id" = {expected.ProjectId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (preflight is null) {
            return Refuse(ProjectErrorCodes.NotFound, "The project no longer exists.");
        }
        if (preflight.LifetimeId != expected.LifetimeId || preflight.Retired) {
            return Refuse(ProjectErrorCodes.LifetimeChanged, "The original project lifetime is unavailable.");
        }
        int?[] lengths = [preflight.NameBytes, preflight.DescriptionBytes, preflight.ObjectiveBytes, preflight.CurrentPhaseBytes];
        if (preflight.ConcurrencyToken == Guid.Empty || !Enum.IsDefined((ProjectStatus)preflight.Status) ||
                preflight.TargetDateUtc is { Kind: not DateTimeKind.Utc } || lengths.Any(value => value is null or < 0)) {
            return Refuse(ProjectErrorCodes.DefinitionSourceInvalid, "The stored project scalar source is invalid.");
        }
        if (lengths.Any(value => value > ProjectDefinitionLimits.VersionOneMaximum.MaxFieldUtf8Bytes)) {
            return Refuse(ProjectErrorCodes.DefinitionLimitExceeded, "The stored project scalar source exceeds the capture limit.");
        }
        var scalars = await context.Set<Project>().AsNoTracking()
            .Where(row => row.Id == expected.ProjectId && row.LifetimeId == expected.LifetimeId &&
                row.ConcurrencyToken == preflight.ConcurrencyToken)
            .Select(row => new ProjectDefinitionScalars(row.Name, row.Description, row.Objective, row.Status,
                row.CurrentPhase, row.TargetDateUtc))
            .SingleOrDefaultAsync(cancellationToken);
        if (scalars is null) {
            throw new InvalidOperationException("The enlisted scalar source changed outside its required project gate.");
        }
        return Result<ProjectDefinitionScalarSnapshot>.Success(new(new(expected, preflight.ConcurrencyToken), scalars));
    }

    private static Result<ProjectDefinitionScalarSnapshot> Refuse(string code, string message)
        => Result<ProjectDefinitionScalarSnapshot>.Failure(Error.Failure(message, code));

    private sealed class ScalarPreflight {
        public Guid LifetimeId { get; set; }
        public Guid ConcurrencyToken { get; set; }
        public int Status { get; set; }
        public DateTime? TargetDateUtc { get; set; }
        public int? NameBytes { get; set; }
        public int? DescriptionBytes { get; set; }
        public int? ObjectiveBytes { get; set; }
        public int? CurrentPhaseBytes { get; set; }
        public bool Retired { get; set; }
    }
}
