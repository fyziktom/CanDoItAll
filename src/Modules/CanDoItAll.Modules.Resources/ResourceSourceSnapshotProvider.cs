using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MafMemorySourceKind = CanDoItAll.AgentFramework.Core.MemorySourceKind;

namespace CanDoItAll.Modules.Resources;

public sealed partial class ResourceSourceSnapshotProvider(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ResourceConnectorPluginRegistry resourceConnectorPluginRegistry) : IResourceSourceSnapshotProvider
{
    public async Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ResourceSourceSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ResourceId == Guid.Empty || request.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("Resource source requests must use null for catalog scope or non-empty resource/project ids.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<ProjectResource>().AsNoTracking();
        if (request.ResourceId.HasValue)
        {
            query = query.Where(resource => resource.Id == request.ResourceId.Value);
        }

        if (request.ProjectId.HasValue)
        {
            query = query.Where(resource => resource.ProjectId == request.ProjectId.Value);
        }

        var resources = await query.OrderBy(resource => resource.Name).ToListAsync(cancellationToken);
        var items = resources.Select(MapResource).ToArray();
        var scopeId = request.ResourceId ?? request.ProjectId ?? Guid.Empty;
        var page = MemorySourceSnapshotPage.Apply(
            items,
            request.Cursor,
            request.Take,
            MafMemorySourceKind.ResourceCatalog,
            scopeId,
            MemorySourceSnapshotProviderVersions.ResourceCatalog,
            out var nextCursor,
            out var hasMore);
        var snapshotHash = MemorySourceSnapshotHasher.Compute(page.Select(item => item.ContentHash).ToArray());
        return new MemorySourceSnapshot(
            new MemorySourceSnapshotManifest(
                MemorySourceSnapshotId.Create(MafMemorySourceKind.ResourceCatalog, scopeId, snapshotHash),
                MafMemorySourceKind.ResourceCatalog,
                scopeId,
                DateTimeOffset.UtcNow,
                items.Length,
                nextCursor,
                hasMore,
                hasMore ? MemorySourceSnapshotPageStatus.PageReturned : MemorySourceSnapshotPageStatus.EndOfSource,
                MemorySourceSnapshotHashScope.FullSnapshot,
                MemorySourceSnapshotProviderVersions.ResourceCatalog),
            page);
    }

    private MemorySourceItem MapResource(ProjectResource resource)
    {
        var connector = resourceConnectorPluginRegistry.Resolve(resource);
        var hasSensitivePayload = resource.Sensitivity != ResourceSensitivity.Normal ||
            HasLinkedSecret(resource.LinkedSecretIdsJson) ||
            ContainsSensitiveConfig(resource.ConfigJson);
        var safeLocation = RedactLocator(resource.LocationOrIdentifier);
        var content = BuildContent(
            ("Name", resource.Name),
            ("Connector", connector.Manifest.DisplayName),
            ("Location", safeLocation),
            ("Description", RedactText(resource.Description)),
            ("Validation", resource.ValidationStatus.ToString()),
            ("Sensitivity", resource.Sensitivity.ToString()),
            ("Supports preview", resource.SupportsPreview.ToString(CultureInfo.InvariantCulture)),
            ("Supports indexing", resource.SupportsIndexing.ToString(CultureInfo.InvariantCulture)),
            ("Linked secret count", CountLinkedSecrets(resource.LinkedSecretIdsJson).ToString(CultureInfo.InvariantCulture)));
        var itemId = MemorySourceItemId.Create(
            MafMemorySourceKind.ResourceCatalog,
            resource.Id,
            MemorySourceEntityKind.ResourceReference,
            $"resource:{resource.Id:D}");
        return new MemorySourceItem(
            itemId,
            MafMemorySourceKind.ResourceCatalog,
            MemorySourceEntityKind.ResourceReference,
            resource.Name,
            content,
            MemorySourceSnapshotHasher.Compute(
                resource.Id.ToString("D"),
                resource.ProjectId.ToString("D"),
                resource.Name,
                resource.Description,
                resource.LocationOrIdentifier,
                resource.ConfigJson,
                resource.LinkedSecretIdsJson,
                resource.Sensitivity.ToString()),
            resource.CreatedAtUtc,
            resource.UpdatedAtUtc,
            new MemorySourceProvenance(
                MafMemorySourceKind.ResourceCatalog,
                resource.Id,
                MemorySourceEntityKind.ResourceReference,
                $"resource:{resource.Id:D}",
                $"/resources?resourceId={resource.Id:D}"),
            MemorySourceSnapshotSecurity.CreatePermission(
                hasSensitivePayload,
                "Resource source snapshots expose metadata and safe locators only; config JSON and linked secret ids are not copied to providers.",
                "Source-grounded resource metadata for selected memory provider ingestion.",
                ResolveSensitivity(resource.Sensitivity)),
            Layout: null,
            Links: [],
            References:
            [
                new MemorySourceReference("resource", resource.Id.ToString("D"), 0),
                new MemorySourceReference("project", resource.ProjectId.ToString("D"), 1)
            ],
            BuildStorageReference(resource, safeLocation),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["connectorPluginKey"] = resource.ConnectorPluginKey,
                ["validationStatus"] = resource.ValidationStatus.ToString(),
                ["sensitivity"] = resource.Sensitivity.ToString(),
                ["linkedSecretCount"] = CountLinkedSecrets(resource.LinkedSecretIdsJson).ToString(CultureInfo.InvariantCulture)
            })
        {
            HashPolicy = MemorySourceSnapshotSecurity.CreateIntegrityHashPolicy(
                hasSensitivePayload,
                "Resource snapshot hashes may include raw config or linked secret ids and are for non-exportable integrity checks only.")
        };
    }

    private static MemorySourceStorageReference? BuildStorageReference(
        ProjectResource resource,
        string safeLocation)
    {
        if (string.IsNullOrWhiteSpace(resource.LocationOrIdentifier))
        {
            return null;
        }

        var locatorKind = Uri.TryCreate(resource.LocationOrIdentifier, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? "url"
                : "resource-locator";
        return new MemorySourceStorageReference(
            "resources",
            locatorKind,
            safeLocation,
            "application/octet-stream",
            resource.Name);
    }

    private static MemorySourceSensitivity ResolveSensitivity(ResourceSensitivity sensitivity)
        => sensitivity switch
        {
            ResourceSensitivity.Restricted => MemorySourceSensitivity.Sensitive,
            ResourceSensitivity.Sensitive => MemorySourceSensitivity.Confidential,
            _ => MemorySourceSensitivity.Internal
        };

    private static bool HasLinkedSecret(string json)
        => CountLinkedSecrets(json) > 0;

    private static int CountLinkedSecrets(string json)
    {
        try
        {
            var values = JsonSerializer.Deserialize<string[]>(json);
            return values?.Count(value => !string.IsNullOrWhiteSpace(value)) ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static bool ContainsSensitiveConfig(string json)
        => MemorySourceSnapshotSecurity.ContainsSensitiveInlineValue(json);

    private static string RedactLocator(string locator)
    {
        if (!Uri.TryCreate(locator, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Query))
        {
            return RedactText(locator);
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Join(
                "&",
                uri.Query
                    .TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(parameter =>
                    {
                        var parts = parameter.Split('=', 2);
                        return MemorySourceSnapshotSecurity.IsSensitiveQueryParameterName(parts[0])
                            ? $"{parts[0]}={MemorySourceSnapshotSecurity.RedactedValue}"
                            : parameter;
                    }))
        };
        return builder.Uri.ToString();
    }

    private static string RedactText(string value)
        => MemorySourceSnapshotSecurity.RedactSensitiveInlineValues(value);

    private static string BuildContent(params (string Label, string? Value)[] fields)
        => string.Join(
            Environment.NewLine,
            fields
                .Where(field => !string.IsNullOrWhiteSpace(field.Value))
                .Select(field => $"{field.Label}: {field.Value}"));
}
