using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Resources;

public sealed class ProjectResource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid Revision { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid? ProjectLifetimeId { get; set; }

    public Guid? OwnerPartyId { get; set; }

    public Guid? MaintainerPartyId { get; set; }

    public ResourceKind? ResourceKind { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ConnectorPluginKey { get; set; } = string.Empty;

    public string ConfigSchemaVersion { get; set; } = string.Empty;

    public string LocationOrIdentifier { get; set; } = string.Empty;

    public string ConfigJson { get; set; } = "{}";

    public string LinkedSecretIdsJson { get; set; } = "[]";

    public ResourceValidationStatus ValidationStatus { get; set; } = ResourceValidationStatus.Unknown;

    public ResourceSensitivity Sensitivity { get; set; } = ResourceSensitivity.Normal;

    public bool SupportsPreview { get; set; }

    public bool SupportsIndexing { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectResourceConfiguration : IEntityTypeConfiguration<ProjectResource>
{
    public void Configure(EntityTypeBuilder<ProjectResource> builder)
    {
        builder.ToTable("Resources_ProjectResources", table => table.HasCheckConstraint(
            "CK_Resources_ProjectResources_Revision", "\"Revision\" <> '00000000-0000-0000-0000-000000000000'::uuid"));
        builder.HasKey(resource => resource.Id);
        builder.Property(resource => resource.Revision).IsConcurrencyToken().ValueGeneratedNever();
        builder.HasIndex(resource => new { resource.ProjectId, resource.ProjectLifetimeId, resource.ConnectorPluginKey });
        builder.Property(resource => resource.Name).HasMaxLength(200).IsRequired();
        builder.Property(resource => resource.Description).HasColumnType("TEXT");
        builder.Property(resource => resource.ConnectorPluginKey).HasMaxLength(160).IsRequired();
        builder.Property(resource => resource.ConfigSchemaVersion).HasMaxLength(40).IsRequired();
        builder.Property(resource => resource.LocationOrIdentifier).HasMaxLength(1000).IsRequired();
        builder.Property(resource => resource.ConfigJson).HasColumnType("TEXT");
        builder.Property(resource => resource.LinkedSecretIdsJson).HasColumnType("TEXT");
    }
}

public sealed class ResourcesService(
    IDbContextFactory<ResourcesDbContext> dbContextFactory,
    Projects.IProjectRecordQueryService projectQueries,
    ResourceConnectorPluginRegistry resourceConnectorPluginRegistry,
    DbContextOptions<ResourcesDbContext> contextOptions,
    CoordinatedDatabaseTransaction coordinatedTransaction,
    ProjectWriteAdmissionService writeAdmissions,
    ResourceMetadataWriter metadata,
    ResourceMetadataPreparation preparation)
{
    public async Task<IReadOnlyList<ResourceProjectionFact>> ListProjectProjectionFactsAsync(
        Guid projectId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var admission = await writeAdmissions.CaptureAsync(projectId, cancellationToken);
        return admission is null ? [] : await LoadProjectProjectionFactsAsync(dbContext, projectId, admission.LifetimeId, cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceProjectionFact>> ListProjectProjectionFactsForMutationAsync(
        Guid projectId, CancellationToken cancellationToken = default) {
        await using var dbContext = await coordinatedTransaction.CreateEnlistedAsync(contextOptions,
            static options => new ResourcesDbContext(options), cancellationToken);
        var admission = await writeAdmissions.CaptureForMutationAsync(projectId, cancellationToken);
        return admission is null ? [] : await LoadProjectProjectionFactsAsync(dbContext, projectId, admission.LifetimeId, cancellationToken);
    }

    private async Task<IReadOnlyList<ResourceProjectionFact>> LoadProjectProjectionFactsAsync(
        ResourcesDbContext dbContext, Guid projectId, Guid lifetimeId, CancellationToken cancellationToken) {
        var resources = await dbContext.Set<ProjectResource>().AsNoTracking()
            .Where(resource => resource.ProjectId == projectId && resource.ProjectLifetimeId == lifetimeId)
            .OrderBy(resource => resource.Name)
            .ToListAsync(cancellationToken);
        return resources.Select(resource => {
            var connector = resourceConnectorPluginRegistry.Resolve(resource);
            return new ResourceProjectionFact(resource.Id, connector.ResolveWorkbenchObjectType(resource),
                connector.ResolveWorkbenchObjectSubtype(resource), resource.Name, resource.LocationOrIdentifier,
                resource.ValidationStatus, resource.Description, resource.CreatedAtUtc);
        }).ToArray();
    }

    public async Task<ResourceProjectionScopeFact?> ReadProjectionScopeAsync(
        Guid resourceId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resource = await dbContext.Set<ProjectResource>().AsNoTracking()
            .Where(item => item.Id == resourceId)
            .Select(item => new ProjectResource {
                Id = item.Id,
                ProjectId = item.ProjectId,
                ProjectLifetimeId = item.ProjectLifetimeId,
                ResourceKind = item.ResourceKind,
                ConnectorPluginKey = item.ConnectorPluginKey,
                ConfigSchemaVersion = item.ConfigSchemaVersion
            }).FirstOrDefaultAsync(cancellationToken);
        if (resource is null) {
            return null;
        }
        var current = resource.ProjectId == Guid.Empty ? null : await writeAdmissions.CaptureAsync(resource.ProjectId, cancellationToken);
        if (current is null || current.LifetimeId != resource.ProjectLifetimeId) {
            return null;
        }
        var connector = resourceConnectorPluginRegistry.Resolve(resource);
        return new(resource.ProjectId, connector.ResolveWorkbenchObjectType(resource),
            connector.ResolveWorkbenchObjectSubtype(resource));
    }

    public IReadOnlyList<ConnectorPluginManifest> ListConnectorManifests()
    {
        return resourceConnectorPluginRegistry.ListManifests();
    }

    public string BuildLocationPreview(ResourceEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            var connectorPlugin = resourceConnectorPluginRegistry.Resolve(ResolveRequestedConnectorPluginKey(model));
            return connectorPlugin.BuildLocation(model);
        }
        catch
        {
            return model.LocationOrIdentifier?.Trim() ?? string.Empty;
        }
    }

    public async Task<IReadOnlyList<ResourceSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resources = await dbContext.Set<ProjectResource>()
            .AsNoTracking()
            .OrderBy(resource => resource.Name)
            .Select(resource => new ProjectResource {
                Id = resource.Id,
                ProjectId = resource.ProjectId,
                ProjectLifetimeId = resource.ProjectLifetimeId,
                ResourceKind = resource.ResourceKind,
                ConnectorPluginKey = resource.ConnectorPluginKey,
                ConfigSchemaVersion = resource.ConfigSchemaVersion,
                Name = resource.Name,
                LocationOrIdentifier = resource.LocationOrIdentifier,
                ValidationStatus = resource.ValidationStatus,
                Sensitivity = resource.Sensitivity
            })
            .ToListAsync(cancellationToken);

        var projectIds = resources.Select(resource => resource.ProjectId)
            .Where(id => id != Guid.Empty).Distinct().ToArray();
        var projects = (await projectQueries.GetManyAsync(projectIds, cancellationToken))
            .ToDictionary(project => project.Id);

        return resources.Select(resource => new ResourceSummary(
                resource.Id,
                resource.ProjectId,
                resource.ProjectLifetimeId is { } lifetimeId && projects.TryGetValue(resource.ProjectId, out var project) &&
                    project.LifetimeId == lifetimeId ? project.Name : "Unknown project",
                resource.ResourceKind,
                resourceConnectorPluginRegistry.TryResolve(resource, out var connector) ? connector!.Manifest.PluginKey : resource.ConnectorPluginKey,
                connector?.Manifest.DisplayName ?? "Unavailable connector",
                resource.Name,
                resource.LocationOrIdentifier,
                resource.ValidationStatus,
                resource.Sensitivity) { ProjectLifetimeId = resource.ProjectLifetimeId })
            .ToList();
    }

    public Task<ResourceMetadataContext> ReadContextAsync(CancellationToken cancellationToken = default)
        => metadata.ReadContextAsync(cancellationToken);

    public Task<ResourceMetadataReadResult> ReadAsync(Guid id, CancellationToken cancellationToken = default)
        => metadata.ReadAsync(id, cancellationToken);

    public ResourceEditorModel CreateEditor(ResourceMetadataSnapshot snapshot) => preparation.CreateEditor(snapshot);

    public Task<ResourceMetadataSaveOutcome> SaveAsync(ReplaceResourceMetadataCommand command, CancellationToken cancellationToken = default)
        => metadata.ReplaceAsync(command, cancellationToken);

    public Task<ResourceMetadataDeleteOutcome> DeleteAsync(DeleteResourceMetadataCommand command, CancellationToken cancellationToken = default)
        => metadata.DeleteAsync(command, cancellationToken);
    private static string ResolveRequestedConnectorPluginKey(ResourceEditorModel model)
    {
        var connectorPluginKey = model.ConnectorPluginKey?.Trim();
        if (string.IsNullOrWhiteSpace(connectorPluginKey))
        {
            throw new InvalidOperationException("Select a connector plugin before building the resource configuration.");
        }
        return connectorPluginKey;
    }

}
