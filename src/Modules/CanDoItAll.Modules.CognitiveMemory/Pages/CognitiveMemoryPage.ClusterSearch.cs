namespace CanDoItAll.Modules.CognitiveMemory.Pages;

public partial class CognitiveMemoryPage
{
    internal string clusterSearchText = string.Empty;
    internal string clusterSearchKeyFamilyText = string.Empty;
    internal string clusterSearchReadinessText = string.Empty;
    internal string clusterSearchRiskText = string.Empty;

    internal string ClusterSearchBadgeText
        => PageInfo(CognitiveMemoryReviewUiCollectionKind.ClusterSearchResults).TotalCount.ToString();

    internal CognitiveMemoryClusterSearchFilter CreateClusterSearchFilter()
        => new(
            clusterSearchText.Trim(),
            ParseOptionalEnum<CognitiveMemoryQualityClusterKeyFamily>(clusterSearchKeyFamilyText),
            ParseOptionalEnum<CognitiveMemoryQualityClusterReadiness>(clusterSearchReadinessText),
            ParseOptionalEnum<CognitiveMemoryRiskLevel>(clusterSearchRiskText));

    internal async Task ApplyClusterSearchAsync()
    {
        if (isBusy)
        {
            return;
        }

        pageIndexes[CognitiveMemoryReviewUiCollectionKind.ClusterSearchResults] = 0;
        await RefreshAsync();
    }

    internal async Task ClearClusterSearchAsync()
    {
        if (isBusy)
        {
            return;
        }

        clusterSearchText = string.Empty;
        clusterSearchKeyFamilyText = string.Empty;
        clusterSearchReadinessText = string.Empty;
        clusterSearchRiskText = string.Empty;
        pageIndexes[CognitiveMemoryReviewUiCollectionKind.ClusterSearchResults] = 0;
        await RefreshAsync();
    }

    internal static TValue? ParseOptionalEnum<TValue>(string value)
        where TValue : struct, Enum
        => Enum.TryParse<TValue>(value, ignoreCase: false, out var parsed)
            ? parsed
            : null;
}
