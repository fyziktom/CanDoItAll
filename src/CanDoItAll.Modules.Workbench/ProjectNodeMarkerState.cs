using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectNodeMarkerState
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ProjectNodeMarker> Parse(string? markersJson)
    {
        if (string.IsNullOrWhiteSpace(markersJson))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<ProjectNodeMarker>>(markersJson, SerializerOptions);
            return ProjectObjectMetadataSerializer.NormalizeMarkers(parsed);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(IEnumerable<ProjectNodeMarker>? markers)
    {
        return JsonSerializer.Serialize(ProjectObjectMetadataSerializer.NormalizeMarkers(markers), SerializerOptions);
    }

    public static string NormalizeJson(string? markersJson)
    {
        return Serialize(Parse(markersJson));
    }

    public static ProjectNodeMarker? ResolvePrimary(string? markersJson)
    {
        return ProjectObjectMetadataSerializer.ResolvePrimaryMarker(Parse(markersJson));
    }

    public static string ResolveLegacyJson(
        string? markersJson,
        string? markerIcon,
        string? markerTone,
        string? markerLabel,
        string? metadataJson)
    {
        var parsedMarkers = Parse(markersJson);
        if (parsedMarkers.Count > 0)
        {
            return Serialize(parsedMarkers);
        }

        var legacyMetadataMarkers = ProjectNodeLegacyMetadata.ReadLegacyMarkers(metadataJson);
        if (legacyMetadataMarkers.Count > 0)
        {
            return Serialize(legacyMetadataMarkers);
        }

        var legacyPrimaryMarker = ProjectObjectMetadataSerializer.NormalizeMarker(markerIcon, markerTone, markerLabel);
        return legacyPrimaryMarker is null
            ? "[]"
            : Serialize([legacyPrimaryMarker]);
    }

    public static void HydrateLegacyFields(ProjectObjectRecord node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var primaryMarker = ResolvePrimary(node.MarkersJson);
        node.MarkerIcon = primaryMarker?.Icon ?? string.Empty;
        node.MarkerTone = primaryMarker?.Tone ?? string.Empty;
        node.MarkerLabel = primaryMarker?.Label ?? string.Empty;
    }

    public static async Task NormalizeAndHydrateAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<ProjectObjectRecord> nodes,
        CancellationToken cancellationToken = default)
    {
        if (nodes.Count == 0)
        {
            return;
        }

        var changed = false;
        foreach (var node in nodes)
        {
            var normalizedJson = ResolveLegacyJson(
                node.MarkersJson,
                node.MarkerIcon,
                node.MarkerTone,
                node.MarkerLabel,
                node.MetadataJson);
            if (!string.Equals(node.MarkersJson, normalizedJson, StringComparison.Ordinal))
            {
                node.MarkersJson = normalizedJson;
                changed = true;
            }

            HydrateLegacyFields(node);
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
