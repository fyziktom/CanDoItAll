using System.Text.Json;

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
}
