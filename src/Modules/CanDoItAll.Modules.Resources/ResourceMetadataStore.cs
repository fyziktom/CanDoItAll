using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Modules.Resources;

internal static class ResourceMetadataStore {
    internal static async Task<ResourceMetadataReadResult> ReadAsync(ResourcesDbContext owner,
        ResourceMetadataContext context, Guid id, ResourceMetadataPreparation preparation, CancellationToken cancellationToken) {
        var rows = await owner.Database.SqlQueryRaw<BoundedRow>(ReadSql,
            new NpgsqlParameter("resourceId", id),
            new NpgsqlParameter("scalarBytes", ResourceMetadataLimits.ScalarBytes),
            new NpgsqlParameter("descriptionBytes", ResourceMetadataLimits.DescriptionBytes),
            new NpgsqlParameter("configBytes", ResourceMetadataLimits.ConfigJsonBytes),
            new NpgsqlParameter("secretBytes", ResourceMetadataLimits.LinkedSecretIdsJsonBytes),
            new NpgsqlParameter("aggregateBytes", ResourceMetadataLimits.RawAggregateBytes)).ToListAsync(cancellationToken);
        var row = rows.Single();
        if (!row.Utf8) {
            throw new NotSupportedException("Resource metadata requires a UTF8 PostgreSQL database.");
        }
        if (row.Id is null) {
            return new ResourceMetadataReadResult.Missing(context, id);
        }
        var expected = Existing(context, row.Id.Value, row.Revision!.Value, row.ProjectId!.Value, row.ProjectLifetimeId);
        var lengths = new ResourceMetadataLengths(row.NameBytes, row.DescriptionBytes, row.ConnectorBytes,
            row.SchemaBytes, row.LocationBytes, row.ConfigBytes, row.SecretBytes);
        if (!row.FitsRaw) {
            return new ResourceMetadataReadResult.MetadataOnly(expected, lengths, row.HasNullPayload
                ? ResourceMetadataReadOnlyReason.InvalidMetadata : ResourceMetadataReadOnlyReason.TooLarge);
        }
        var values = new ResourceMetadataStoredValues((ResourceKind?)row.ResourceKind, row.OwnerPartyId, row.MaintainerPartyId,
            row.Name!, row.Description!, row.ConnectorPluginKey!, row.ConfigSchemaVersion!, row.LocationOrIdentifier!,
            row.ConfigJson!, row.LinkedSecretIdsJson!, (ResourceValidationStatus)row.ValidationStatus!.Value,
            (ResourceSensitivity)row.Sensitivity!.Value, row.SupportsPreview!.Value, row.SupportsIndexing!.Value);
        var readOnly = preparation.Inspect(values);
        if (readOnly is ResourceMetadataReadOnlyReason.UnsupportedSecretReferences or ResourceMetadataReadOnlyReason.InvalidConfiguration or
            ResourceMetadataReadOnlyReason.InvalidMetadata) {
            return new ResourceMetadataReadResult.MetadataOnly(expected, lengths, readOnly.Value);
        }
        try {
            var snapshot = new ResourceMetadataSnapshot(expected, values, row.CreatedAtUtc!.Value, row.UpdatedAtUtc!.Value);
            return new ResourceMetadataReadResult.Found(snapshot, readOnly);
        } catch (ArgumentException) {
            return new ResourceMetadataReadResult.MetadataOnly(expected, lengths, ResourceMetadataReadOnlyReason.TooLarge);
        }
    }

    internal static async Task<ResourceMetadataExpectation.Existing?> ReadHeaderAsync(ResourcesDbContext owner,
        ResourceMetadataContext context, Guid id, CancellationToken cancellationToken) {
        var row = await owner.Set<ProjectResource>().AsNoTracking().Where(resource => resource.Id == id)
            .Select(resource => new { resource.Id, resource.Revision, resource.ProjectId, resource.ProjectLifetimeId })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : Existing(context, row.Id, row.Revision, row.ProjectId, row.ProjectLifetimeId);
    }

    internal static ResourceMetadataExpectation.Existing Existing(ResourceMetadataContext context, Guid id,
        Guid revision, Guid projectId, Guid? lifetimeId) {
        if (id == Guid.Empty || revision == Guid.Empty || lifetimeId == Guid.Empty || projectId == Guid.Empty && lifetimeId is not null) {
            throw new InvalidDataException("The retained Resource identity, revision or project binding is invalid.");
        }
        ResourceProjectBinding project = lifetimeId is { } lifetime
            ? new ResourceProjectBinding.Bound(projectId, lifetime)
            : projectId == Guid.Empty ? new ResourceProjectBinding.RetainedUnbound() : new ResourceProjectBinding.RetainedProject(projectId);
        return new(context, id, new(revision), project);
    }

    internal static void Stage(ResourcesDbContext owner, ResourceMetadataSnapshot snapshot, ResourceMetadataExpectation expected) {
        var values = snapshot.Values;
        var binding = (ResourceProjectBinding.Bound)snapshot.Expected.Project;
        var row = new ProjectResource {
            Id = snapshot.Expected.ResourceId,
            Revision = snapshot.Expected.Revision.Value,
            ProjectId = binding.ProjectId,
            ProjectLifetimeId = binding.LifetimeId,
            OwnerPartyId = values.OwnerPartyId,
            MaintainerPartyId = values.MaintainerPartyId,
            ResourceKind = values.LegacyResourceKind,
            Name = values.Name,
            Description = values.Description,
            ConnectorPluginKey = values.ConnectorPluginKey,
            ConfigSchemaVersion = values.ConfigSchemaVersion,
            LocationOrIdentifier = values.LocationOrIdentifier,
            ConfigJson = values.ConfigJson,
            LinkedSecretIdsJson = values.LinkedSecretIdsJson,
            ValidationStatus = values.ValidationStatus,
            Sensitivity = values.Sensitivity,
            SupportsPreview = values.SupportsPreview,
            SupportsIndexing = values.SupportsIndexing,
            CreatedAtUtc = snapshot.CreatedAtUtc,
            UpdatedAtUtc = snapshot.UpdatedAtUtc
        };
        if (expected is ResourceMetadataExpectation.Absent) {
            owner.Add(row);
        } else {
            owner.Attach(row);
            owner.Entry(row).State = EntityState.Modified;
            owner.Entry(row).Property(resource => resource.Revision).OriginalValue = ((ResourceMetadataExpectation.Existing)expected).Revision.Value;
        }
    }

    internal static void StageDelete(ResourcesDbContext owner, ResourceMetadataExpectation.Existing expected) {
        var row = new ProjectResource { Id = expected.ResourceId, Revision = expected.Revision.Value };
        owner.Attach(row);
        owner.Remove(row);
    }

    private sealed class BoundedRow {
        public bool Utf8 { get; set; }
        public Guid? Id { get; set; }
        public Guid? Revision { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? ProjectLifetimeId { get; set; }
        public Guid? OwnerPartyId { get; set; }
        public Guid? MaintainerPartyId { get; set; }
        public int? ResourceKind { get; set; }
        public int? ValidationStatus { get; set; }
        public int? Sensitivity { get; set; }
        public bool? SupportsPreview { get; set; }
        public bool? SupportsIndexing { get; set; }
        public DateTimeOffset? CreatedAtUtc { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }
        public long? NameBytes { get; set; }
        public long? DescriptionBytes { get; set; }
        public long? ConnectorBytes { get; set; }
        public long? SchemaBytes { get; set; }
        public long? LocationBytes { get; set; }
        public long? ConfigBytes { get; set; }
        public long? SecretBytes { get; set; }
        public bool FitsRaw { get; set; }
        public bool HasNullPayload { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ConnectorPluginKey { get; set; }
        public string? ConfigSchemaVersion { get; set; }
        public string? LocationOrIdentifier { get; set; }
        public string? ConfigJson { get; set; }
        public string? LinkedSecretIdsJson { get; set; }
    }

    private const string ReadSql = """
        WITH candidate AS (
            SELECT *, octet_length("Name")::bigint AS "NameBytes",
                octet_length("Description")::bigint AS "DescriptionBytes",
                octet_length("ConnectorPluginKey")::bigint AS "ConnectorBytes",
                octet_length("ConfigSchemaVersion")::bigint AS "SchemaBytes",
                octet_length("LocationOrIdentifier")::bigint AS "LocationBytes",
                octet_length("ConfigJson")::bigint AS "ConfigBytes",
                octet_length("LinkedSecretIdsJson")::bigint AS "SecretBytes"
            FROM "Resources_ProjectResources" WHERE "Id" = @resourceId
        ), bounded AS (
            SELECT *, COALESCE("NameBytes" <= @scalarBytes AND "DescriptionBytes" <= @descriptionBytes
                AND "ConnectorBytes" <= @scalarBytes AND "SchemaBytes" <= @scalarBytes
                AND "LocationBytes" <= @scalarBytes AND "ConfigBytes" <= @configBytes AND "SecretBytes" <= @secretBytes
                AND "NameBytes" + "DescriptionBytes" + "ConnectorBytes" + "SchemaBytes" + "LocationBytes" + "ConfigBytes" + "SecretBytes" <= @aggregateBytes,
                false) AS "FitsRaw",
                "Name" IS NULL OR "Description" IS NULL OR "ConnectorPluginKey" IS NULL OR "ConfigSchemaVersion" IS NULL
                OR "LocationOrIdentifier" IS NULL OR "ConfigJson" IS NULL OR "LinkedSecretIdsJson" IS NULL AS "HasNullPayload"
            FROM candidate
        )
        SELECT current_setting('server_encoding') = 'UTF8' AS "Utf8", "Id", "Revision", "ProjectId", "ProjectLifetimeId",
            "OwnerPartyId", "MaintainerPartyId", "ResourceKind", "ValidationStatus", "Sensitivity", "SupportsPreview", "SupportsIndexing",
            "CreatedAtUtc", "UpdatedAtUtc", "NameBytes", "DescriptionBytes", "ConnectorBytes", "SchemaBytes", "LocationBytes", "ConfigBytes", "SecretBytes",
            COALESCE("FitsRaw", false) AS "FitsRaw", COALESCE("HasNullPayload", false) AS "HasNullPayload",
            CASE WHEN "FitsRaw" THEN "Name" END AS "Name",
            CASE WHEN "FitsRaw" THEN "Description" END AS "Description",
            CASE WHEN "FitsRaw" THEN "ConnectorPluginKey" END AS "ConnectorPluginKey",
            CASE WHEN "FitsRaw" THEN "ConfigSchemaVersion" END AS "ConfigSchemaVersion",
            CASE WHEN "FitsRaw" THEN "LocationOrIdentifier" END AS "LocationOrIdentifier",
            CASE WHEN "FitsRaw" THEN "ConfigJson" END AS "ConfigJson",
            CASE WHEN "FitsRaw" THEN "LinkedSecretIdsJson" END AS "LinkedSecretIdsJson"
        FROM (SELECT 1) AS observation LEFT JOIN bounded ON true
        """;
}
