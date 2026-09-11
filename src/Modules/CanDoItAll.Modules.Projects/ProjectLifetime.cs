using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectWriteAdmission {
    public ProjectWriteAdmission(Guid databaseProfileId, Guid projectId, Guid lifetimeId) {
        if (databaseProfileId == Guid.Empty || projectId == Guid.Empty || lifetimeId == Guid.Empty) {
            throw new ArgumentException("A project admission requires nonempty database profile, project and lifetime identifiers.");
        }
        DatabaseProfileId = databaseProfileId;
        ProjectId = projectId;
        LifetimeId = lifetimeId;
    }

    public Guid DatabaseProfileId { get; }
    public Guid ProjectId { get; }
    public Guid LifetimeId { get; }
}

public sealed class ProjectWriteAdmissionRejectedException(ProjectWriteAdmission admission)
    : InvalidOperationException($"Project '{admission.ProjectId:D}' in database profile '{admission.DatabaseProfileId:D}' no longer admits writes for lifetime '{admission.LifetimeId:D}'.") {
    public ProjectWriteAdmission Admission { get; } = admission;
}

public sealed class ProjectRetirementRecord {
    public Guid LifetimeId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTimeOffset RetiredAtUtc { get; set; }
    public RetainedEvidenceImport? ImportedHistory { get; set; }
}

internal sealed class ProjectRetirementRecordConfiguration : IEntityTypeConfiguration<ProjectRetirementRecord> {
    public void Configure(EntityTypeBuilder<ProjectRetirementRecord> builder) {
        builder.ToTable("Projects_ProjectRetirements");
        builder.HasKey(record => record.LifetimeId);
        builder.Property(record => record.ImportedHistory).HasRetainedEvidenceImportConversion();
        builder.HasIndex(record => new { record.ProjectId, record.RetiredAtUtc });
    }
}

public sealed partial class ProjectWriteAdmissionService(
    IDbContextFactory<ProjectsDbContext> dbContextFactory,
    DbContextOptions<ProjectsDbContext> contextOptions,
    CoordinatedDatabaseTransaction coordinatedTransaction,
    ICanonicalRuntimeDatabase canonicalDatabase) {
    public Guid DatabaseProfileId => canonicalDatabase.Profile.Profile.Id;

    internal ProjectWriteAdmission Capture(Project project) => new(canonicalDatabase.Profile.Profile.Id, project.Id, project.LifetimeId);

    public async Task<ProjectWriteAdmission?> CaptureAsync(Guid projectId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await CaptureAsync(dbContext, projectId, cancellationToken);
    }

    public async Task<ProjectWriteAdmission?> CaptureForMutationAsync(Guid projectId, CancellationToken cancellationToken = default) {
        await using var dbContext = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new ProjectsDbContext(options), cancellationToken);
        return await CaptureAsync(dbContext, projectId, cancellationToken);
    }

    private async Task<ProjectWriteAdmission?> CaptureAsync(ProjectsDbContext dbContext, Guid projectId, CancellationToken cancellationToken) {
        if (projectId == Guid.Empty) {
            throw new ArgumentException("A project is required.", nameof(projectId));
        }
        var lifetimeId = await dbContext.Set<Project>().AsNoTracking().Where(project => project.Id == projectId)
            .Select(project => (Guid?)project.LifetimeId).SingleOrDefaultAsync(cancellationToken);
        return lifetimeId.HasValue ? new(DatabaseProfileId, projectId, lifetimeId.Value) : null;
    }

    public async Task RequireCurrentAsync(ProjectWriteAdmission admission, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admission);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await RequireAsync(context, admission, cancellationToken);
    }

    public async Task RequireManyCurrentAsync(IReadOnlyCollection<ProjectWriteAdmission> admissions,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admissions);
        if (admissions.Count == 0) {
            return;
        }
        var expected = admissions.ToArray();
        foreach (var admission in expected) {
            ArgumentNullException.ThrowIfNull(admission);
            if (admission.DatabaseProfileId != DatabaseProfileId) {
                throw new ProjectWriteAdmissionRejectedException(admission);
            }
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var active = await ReadActiveLifetimesAsync(context,
            expected.Select(admission => admission.ProjectId).Distinct().ToArray(), cancellationToken);
        foreach (var admission in expected) {
            if (!active.TryGetValue(admission.ProjectId, out var lifetimeId) || lifetimeId != admission.LifetimeId) {
                throw new ProjectWriteAdmissionRejectedException(admission);
            }
        }
    }

    public async Task RequireForMutationAsync(ProjectWriteAdmission admission, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admission);
        await using var dbContext = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new ProjectsDbContext(options), cancellationToken);
        await RequireAsync(dbContext, admission, cancellationToken);
    }

    public async Task RequireManyForMutationAsync(IReadOnlyCollection<ProjectWriteAdmission> admissions,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admissions);
        if (admissions.Count == 0) {
            return;
        }
        var expected = admissions.ToDictionary(admission => admission.ProjectId);
        var foreign = expected.Values.FirstOrDefault(admission => admission.DatabaseProfileId != DatabaseProfileId);
        if (foreign is not null) {
            throw new ProjectWriteAdmissionRejectedException(foreign);
        }
        await using var context = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new ProjectsDbContext(options), cancellationToken);
        var active = await ReadActiveLifetimesAsync(context, expected.Keys.ToArray(), cancellationToken);
        foreach (var admission in expected.Values) {
            if (!active.TryGetValue(admission.ProjectId, out var project) || project != admission.LifetimeId) {
                throw new ProjectWriteAdmissionRejectedException(admission);
            }
        }
    }

    internal async Task RequireAsync(ProjectsDbContext dbContext, ProjectWriteAdmission admission, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(admission);
        if (admission.DatabaseProfileId != canonicalDatabase.Profile.Profile.Id) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
        var active = await ReadActiveLifetimesAsync(dbContext, [admission.ProjectId], cancellationToken);
        if (!active.TryGetValue(admission.ProjectId, out var lifetimeId) || lifetimeId != admission.LifetimeId) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
    }

    private static async Task<Dictionary<Guid, Guid>> ReadActiveLifetimesAsync(ProjectsDbContext context,
        Guid[] projectIds, CancellationToken cancellationToken) {
        IQueryable<Project> projects;
        if (context.Database.IsNpgsql()) {
            projects = context.Set<Project>().FromSqlInterpolated($"""
                SELECT * FROM "Projects_Projects"
                WHERE "Id" = ANY ({projectIds})
                ORDER BY "Id"
                FOR KEY SHARE
                """);
        } else if (context.Database.IsInMemory()) {
            projects = context.Set<Project>().Where(project => projectIds.Contains(project.Id));
        } else {
            throw new InvalidOperationException("Project admission supports PostgreSQL and explicit InMemory tests only.");
        }
        return await projects.AsNoTracking().Where(project =>
                !context.Set<ProjectRetirementRecord>().Any(retirement => retirement.LifetimeId == project.LifetimeId))
            .Select(project => new { project.Id, project.LifetimeId })
            .ToDictionaryAsync(project => project.Id, project => project.LifetimeId, cancellationToken);
    }
}
