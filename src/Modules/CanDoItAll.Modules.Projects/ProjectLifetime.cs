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
}

internal sealed class ProjectRetirementRecordConfiguration : IEntityTypeConfiguration<ProjectRetirementRecord> {
    public void Configure(EntityTypeBuilder<ProjectRetirementRecord> builder) {
        builder.ToTable("Projects_ProjectRetirements");
        builder.HasKey(record => record.LifetimeId);
        builder.HasIndex(record => new { record.ProjectId, record.RetiredAtUtc });
    }
}

public sealed class ProjectWriteAdmissionService(
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

    public async Task RequireForMutationAsync(ProjectWriteAdmission admission, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admission);
        await using var dbContext = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new ProjectsDbContext(options), cancellationToken);
        await RequireAsync(dbContext, admission, cancellationToken);
    }

    internal async Task RequireAsync(ProjectsDbContext dbContext, ProjectWriteAdmission admission, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(admission);
        if (admission.DatabaseProfileId != canonicalDatabase.Profile.Profile.Id) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
        var active = await dbContext.Set<Project>().AsNoTracking().AnyAsync(project =>
            project.Id == admission.ProjectId && project.LifetimeId == admission.LifetimeId &&
            !dbContext.Set<ProjectRetirementRecord>().Any(retirement => retirement.LifetimeId == project.LifetimeId), cancellationToken);
        if (!active) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
    }
}
