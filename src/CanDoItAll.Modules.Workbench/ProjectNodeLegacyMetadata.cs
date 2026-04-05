using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectNodeLegacyMetadata
{
    public static ProjectNodeReferenceSet ReadLegacyReferences(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return ProjectNodeReferenceSet.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            return new ProjectNodeReferenceSet
            {
                MeetingParticipantIds = ReadGuidList(root, "meeting", "participantIds"),
                RecordingMeetingNodeId = ReadGuid(root, "recording", "meetingNodeArtifactId"),
                RecordingTranscriptNodeId = ReadGuid(root, "recording", "transcriptNodeArtifactId"),
                TranscriptRecordingNodeId = ReadGuid(root, "transcript", "recordingNodeArtifactId"),
                TranscriptProviderProfileId = ReadGuid(root, "transcript", "lastProviderProfileId"),
                ParticipantParentNodeId = ReadGuid(root, "participant", "parentParticipantArtifactId"),
                WorkItemAssigneeNodeId = ReadGuid(root, "workItem", "assigneeParticipantArtifactId"),
                WorkItemRepositoryResourceId = ReadGuid(root, "workItem", "repositoryResourceId"),
                RepositoryResourceId = ReadGuid(root, "repository", "resourceId"),
                EnvironmentRepositoryResourceId = ReadGuid(root, "environment", "repositoryResourceId"),
                InfrastructureSecretReferenceId = ReadGuid(root, "infrastructure", "secretReferenceArtifactId"),
                InfrastructureStorageCatalogId = ReadGuid(root, "infrastructure", "storageCatalogId")
            };
        }
        catch (JsonException)
        {
            return ProjectNodeReferenceSet.Empty;
        }
    }

    public static IReadOnlyList<ProjectNodeMarker> ReadLegacyMarkers(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (!TryReadNested(document.RootElement, "markerSet", out var markerSetElement) ||
                !markerSetElement.TryGetProperty("markers", out var markersElement) ||
                markersElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var markers = new List<ProjectNodeMarker>();
            foreach (var markerElement in markersElement.EnumerateArray())
            {
                var marker = ProjectObjectMetadataSerializer.NormalizeMarker(
                    ReadString(markerElement, "icon"),
                    ReadString(markerElement, "tone"),
                    ReadString(markerElement, "label"));
                if (marker is not null)
                {
                    markers.Add(marker);
                }
            }

            return ProjectObjectMetadataSerializer.NormalizeMarkers(markers);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<Guid> ReadGuidList(JsonElement root, string sectionName, string propertyName)
    {
        if (!TryReadNested(root, sectionName, out var section) ||
            !section.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<Guid>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                Guid.TryParse(item.GetString(), out var parsed))
            {
                results.Add(parsed);
            }
        }

        return results;
    }

    private static Guid? ReadGuid(JsonElement root, string sectionName, string propertyName)
    {
        if (!TryReadNested(root, sectionName, out var section) ||
            !section.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return Guid.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool TryReadNested(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }
}
