namespace CanDoItAll.SharedKernel;

public static class LocalRuntimeHostedWorkerPolicy
{
    public const string LaneKindConfigurationKey = "CanDoItAllMcpLaneKind";

    public static bool AreBackgroundHostedWorkersEnabled(params string?[] laneKinds)
    {
        return !IsSuppressedBackgroundWorkerLane(ResolveLaneKind(laneKinds));
    }

    public static string? ResolveLaneKind(params string?[] laneKinds)
    {
        return FirstNonEmpty(laneKinds);
    }

    public static bool IsSuppressedBackgroundWorkerLane(string? laneKind)
    {
        return string.Equals(laneKind, "PublishedActive", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(laneKind, "PublishedCandidate", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
