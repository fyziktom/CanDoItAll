using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectMarkerMutationMode
{
    ReplaceAll,
    Add,
    Remove,
    Toggle,
    ClearAll
}

internal static class ProjectWorkbenchObjectModeling
{
    internal static DateTimeOffset? ResolveEndUtc(DateTimeOffset? startUtc, DateTimeOffset? endUtc, int? durationSeconds)
    {
        if (endUtc.HasValue || !startUtc.HasValue)
        {
            return endUtc;
        }

        var effectiveDurationSeconds = durationSeconds.GetValueOrDefault(3600);
        return effectiveDurationSeconds > 0
            ? startUtc.Value.AddSeconds(effectiveDurationSeconds)
            : startUtc.Value.AddHours(1);
    }

    internal static int? NormalizeDurationSeconds(int? requestedDurationSeconds, DateTimeOffset? startUtc, DateTimeOffset? endUtc)
    {
        if (requestedDurationSeconds.HasValue)
        {
            return requestedDurationSeconds.Value > 0
                ? requestedDurationSeconds.Value
                : null;
        }

        if (!startUtc.HasValue || !endUtc.HasValue)
        {
            return null;
        }

        return CalculateDurationSeconds(startUtc.Value, endUtc.Value);
    }

    internal static int CalculateDurationSeconds(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var totalSeconds = Math.Abs((endUtc - startUtc).TotalSeconds);
        return Math.Max(1, (int)Math.Round(totalSeconds, MidpointRounding.AwayFromZero));
    }

    internal static string ResolveMetadataJson(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? metadataJson,
        string? fallbackMetadataJson,
        string? notes,
        SavedMediaDescriptor? media)
    {
        var effectiveMetadataJson = HasMeaningfulMetadata(metadataJson)
            ? metadataJson
            : HasMeaningfulMetadata(fallbackMetadataJson)
                ? fallbackMetadataJson
                : null;
        var metadata = ProjectNodeKindRegistry.NormalizeMetadata(
            objectType,
            objectSubtype,
            effectiveMetadataJson is null
                ? new ProjectObjectMetadataEnvelope()
                : ProjectObjectMetadataSerializer.Parse(effectiveMetadataJson),
            notes,
            media);

        ProjectObjectMetadataSerializer.Validate(objectType, objectSubtype ?? string.Empty, metadata);
        return ProjectObjectMetadataSerializer.Serialize(metadata);
    }

    internal static bool HasMeaningfulMetadata(string? metadataJson)
    {
        return !string.IsNullOrWhiteSpace(metadataJson) &&
            !string.Equals(metadataJson.Trim(), "{}", StringComparison.Ordinal);
    }

    internal static string NormalizeProgressMode(string? progressMode)
    {
        return (progressMode?.Trim() ?? string.Empty).ToLowerInvariant() switch
        {
            "complete" => "complete",
            "started" => "started",
            "progress" => "progress",
            "na" => "na",
            _ => "progress"
        };
    }

    internal static (string Mode, int Percent) ResolveStatusBackedProgress(string? status)
    {
        var normalizedStatus = status?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedStatus) ||
            normalizedStatus.Contains("n/a", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("not applicable", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("skip", StringComparison.OrdinalIgnoreCase))
        {
            return ("na", 0);
        }

        if (normalizedStatus.Contains("done", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("approved", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("used", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("final", StringComparison.OrdinalIgnoreCase))
        {
            return ("complete", 100);
        }

        if (normalizedStatus.Contains("review", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("testing", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("qa", StringComparison.OrdinalIgnoreCase))
        {
            return ("progress", 78);
        }

        if (normalizedStatus.Contains("active", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("in progress", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("running", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("blocked", StringComparison.OrdinalIgnoreCase))
        {
            return ("progress", 62);
        }

        if (normalizedStatus.Contains("planned", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("draft", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("queued", StringComparison.OrdinalIgnoreCase))
        {
            return ("progress", 28);
        }

        return ("progress", 48);
    }

    internal static IReadOnlyList<ProjectNodeMarker> AddMarker(
        IReadOnlyList<ProjectNodeMarker> existingMarkers,
        ProjectNodeMarker? marker)
    {
        if (marker is null)
        {
            return existingMarkers;
        }

        var updated = existingMarkers
            .Where(existing => !string.Equals(existing.Icon, marker.Icon, StringComparison.OrdinalIgnoreCase))
            .ToList();
        updated.Add(marker);
        return updated;
    }

    internal static IReadOnlyList<ProjectNodeMarker> ToggleMarker(
        IReadOnlyList<ProjectNodeMarker> existingMarkers,
        ProjectNodeMarker? marker)
    {
        if (marker is null)
        {
            return existingMarkers;
        }

        var hasMarker = existingMarkers.Any(existing => string.Equals(existing.Icon, marker.Icon, StringComparison.OrdinalIgnoreCase));
        if (hasMarker)
        {
            return existingMarkers
                .Where(existing => !string.Equals(existing.Icon, marker.Icon, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return AddMarker(existingMarkers, marker);
    }

    internal static IReadOnlyList<ProjectNodeMarker> RemoveMarker(
        IReadOnlyList<ProjectNodeMarker> existingMarkers,
        ProjectNodeMarker? marker)
    {
        if (marker is null)
        {
            return existingMarkers;
        }

        return existingMarkers
            .Where(existing => !string.Equals(existing.Icon, marker.Icon, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    internal static void ApplyPrimaryMarker(ProjectObjectRecord node, IReadOnlyList<ProjectNodeMarker> markers)
    {
        var primaryMarker = ProjectObjectMetadataSerializer.ResolvePrimaryMarker(markers);
        node.MarkerIcon = primaryMarker?.Icon ?? string.Empty;
        node.MarkerTone = primaryMarker?.Tone ?? string.Empty;
        node.MarkerLabel = primaryMarker?.Label ?? string.Empty;
    }
}
