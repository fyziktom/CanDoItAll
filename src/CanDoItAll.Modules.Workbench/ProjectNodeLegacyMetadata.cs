using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectNodeLegacyMetadata
{
    public static ProjectNodeReferenceCollection ReadLegacyReferences(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return ProjectNodeReferenceCollection.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            return new ProjectNodeReferenceCollection
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
            return ProjectNodeReferenceCollection.Empty;
        }
    }

    public static string SanitizeLegacyReferenceMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return "{}";
        }

        try
        {
            if (JsonNode.Parse(metadataJson) is not JsonObject root)
            {
                return metadataJson;
            }

            var didChange = false;
            didChange |= RemoveNestedProperty(root, "meeting", "participantIds");
            didChange |= RemoveNestedProperty(root, "recording", "meetingNodeArtifactId");
            didChange |= RemoveNestedProperty(root, "recording", "transcriptNodeArtifactId");
            didChange |= RemoveNestedProperty(root, "transcript", "recordingNodeArtifactId");
            didChange |= RemoveNestedProperty(root, "transcript", "lastProviderProfileId");
            didChange |= RemoveNestedProperty(root, "participant", "parentParticipantArtifactId");
            didChange |= RemoveNestedProperty(root, "workItem", "assigneeParticipantArtifactId");
            didChange |= RemoveNestedProperty(root, "workItem", "repositoryResourceId");
            didChange |= RemoveNestedProperty(root, "repository", "resourceId");
            didChange |= RemoveNestedProperty(root, "environment", "repositoryResourceId");
            didChange |= RemoveNestedProperty(root, "infrastructure", "secretReferenceArtifactId");
            didChange |= RemoveNestedProperty(root, "infrastructure", "storageCatalogId");

            return didChange
                ? root.ToJsonString()
                : metadataJson;
        }
        catch (JsonException)
        {
            return metadataJson;
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

    private static bool RemoveNestedProperty(JsonObject root, string sectionName, string propertyName)
    {
        if (!TryGetNestedObject(root, sectionName, out var section))
        {
            return false;
        }

        var propertyToRemove = FindPropertyName(section, propertyName);
        if (propertyToRemove is null)
        {
            return false;
        }

        section.Remove(propertyToRemove);

        if (section.Count == 0)
        {
            var sectionToRemove = FindPropertyName(root, sectionName);
            if (sectionToRemove is not null)
            {
                root.Remove(sectionToRemove);
            }
        }

        return true;
    }

    private static bool TryGetNestedObject(JsonObject root, string propertyName, out JsonObject value)
    {
        var resolvedPropertyName = FindPropertyName(root, propertyName);
        if (resolvedPropertyName is not null &&
            root[resolvedPropertyName] is JsonObject objectValue)
        {
            value = objectValue;
            return true;
        }

        value = null!;
        return false;
    }

    private static string? FindPropertyName(JsonObject root, string propertyName)
    {
        foreach (var property in root)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Key;
            }
        }

        return null;
    }
}
