using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ResourcesOwnerPersistenceTests {
    [Fact]
    public async Task Runtime_model_preserves_resource_mapping_and_rejects_foreign_entities() {
        await using var application = await TestApplication.CreateAsync();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>()
            .CreateDbContextAsync();
        await using var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        var entity = Assert.Single(owner.GetService<IDesignTimeModel>().Model.GetEntityTypes());
        Assert.Equal(typeof(ProjectResource), entity.ClrType);
        var completeEntity = Assert.IsAssignableFrom<IEntityType>(
            schema.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProjectResource)));
        Assert.Equal(completeEntity.ToDebugString(MetadataDebugStringOptions.LongDefault),
            entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
    }

    [Fact]
    public async Task Legacy_configuration_and_owner_references_survive_restart_and_profile_isolation() {
        await using var environment = CanDoItAllTestEnvironment.Create("resources-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var projectId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var responsibleId = Guid.NewGuid();
        var maintainerId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        const string location = "https://example.invalid/owner-source";
        await using (var original = await TestApplication.CreateAsync(options)) {
            await using var schema = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
                .CreateDbContextAsync();
            var project = new Project { Id = projectId, Name = "Resource ownership project" };
            schema.Add(project);
            schema.Add(new ProjectResource {
                Id = resourceId,
                ProjectId = projectId,
                ProjectLifetimeId = project.LifetimeId,
                OwnerPartyId = responsibleId,
                MaintainerPartyId = maintainerId,
                ResourceKind = ResourceKind.WebLink,
                Name = "Legacy source",
                Description = "Saved configuration",
                LocationOrIdentifier = location,
                ConfigJson = JsonSerializer.Serialize(new WebLinkResourceConfig(location, "Historical title")),
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt,
                SupportsPreview = true,
                SupportsIndexing = true
            });
            await schema.SaveChangesAsync();
        }

        await using var restarted = await TestApplication.CreateAsync(options);
        await using var scope = restarted.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var editor = await service.GetAsync(resourceId);
        Assert.Equal(resourceId, editor.Id);
        Assert.Equal(responsibleId, editor.OwnerPartyId);
        Assert.Equal(maintainerId, editor.MaintainerPartyId);
        Assert.Equal(location, editor.LocationOrIdentifier);
        Assert.Equal("Resource ownership project", Assert.Single(await service.ListAsync()).ProjectName);
        editor.Name = "Edited through Resources";
        var saved = await service.SaveAsync(editor);
        Assert.True(saved.IsSuccess);
        Assert.Equal(resourceId, saved.Value);

        await using var owner = await restarted.Services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>()
            .CreateDbContextAsync();
        var record = await owner.Set<ProjectResource>().SingleAsync();
        Assert.Equal(resourceId, record.Id);
        Assert.Equal(projectId, record.ProjectId);
        Assert.Equal(responsibleId, record.OwnerPartyId);
        Assert.Equal(maintainerId, record.MaintainerPartyId);
        Assert.Equal(createdAt, record.CreatedAtUtc);
        Assert.Equal("Edited through Resources", record.Name);
        Assert.Equal(location, record.LocationOrIdentifier);
        Assert.Equal(new WebLinkResourceConfig(location, "Historical title"),
            JsonSerializer.Deserialize<WebLinkResourceConfig>(record.ConfigJson));
        Assert.True(record.SupportsPreview);
        Assert.True(record.SupportsIndexing);

        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = otherProfile
        });
        await using var otherScope = other.Services.CreateAsyncScope();
        var otherService = otherScope.ServiceProvider.GetRequiredService<ResourcesService>();
        Assert.Empty(await otherService.ListAsync());
        Assert.Null((await otherService.GetAsync(resourceId)).Id);
        Assert.Equal(resourceId, Assert.Single(await service.ListAsync()).Id);
    }

    [Fact]
    public async Task Project_owner_queries_preserve_source_limit_and_missing_promotion_target_denial() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectQueries = scope.ServiceProvider.GetRequiredService<ProjectRecordQueryService>();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => projectQueries.ListReferencesAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => projectQueries.ListReferencesAsync(
            ProjectRecordQueryLimits.MaximumReferenceCount + 1));

        var storageId = Guid.NewGuid();
        var config = new StorageObjectResourceConfig(
            ResourceFileSourceKey.ForStorage(storageId).Value,
            storageId,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            "source.txt",
            "source.txt",
            "text/plain",
            12);
        var writer = scope.ServiceProvider.GetRequiredService<IStorageObjectResourceWriter>();
        var missingProjectId = Guid.NewGuid();
        var missingAdmission = new ProjectWriteAdmission(scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>().DatabaseProfileId, missingProjectId, Guid.NewGuid());
        var denied = await Assert.ThrowsAsync<ResourcePromotionException>(() => writer.SaveAsync(
            new StorageObjectResourceWriteRequest(missingProjectId, "Missing project", ResourceSensitivity.Normal, config, missingAdmission)));
        Assert.Equal(ResourcePromotionFailureCode.TargetUnavailable, denied.Code);
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>()
            .CreateDbContextAsync();
        Assert.Empty(await owner.Set<ProjectResource>().ToArrayAsync());

        await using var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        schema.AddRange(Enumerable.Range(0, ResourceFileSourceCatalog.MaximumSourceCount + 1)
            .Select(index => new Project { Name = $"Bounded reference {index:D4}" }));
        await schema.SaveChangesAsync();
        var references = await projectQueries.ListReferencesAsync(2);
        Assert.Equal(["Bounded reference 0000", "Bounded reference 0001"], references.Select(item => item.Name));
        var catalog = scope.ServiceProvider.GetRequiredService<IResourceFileSourceCatalog>();
        var overflow = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.LoadAsync());
        Assert.Contains("more than 512 project sources", overflow.Message, StringComparison.Ordinal);
    }
}
