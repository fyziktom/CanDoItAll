internal sealed class CognitiveMemoryEmbeddingBackedApproximateClusterCandidateProvider
{
    private readonly ICognitiveMemoryClusterSemanticSimilarityProvider _semanticSimilarityProvider;

    public CognitiveMemoryEmbeddingBackedApproximateClusterCandidateProvider(
        ICognitiveMemoryClusterSemanticSimilarityProvider semanticSimilarityProvider)
    {
        _semanticSimilarityProvider = semanticSimilarityProvider;
    }

    public IReadOnlyList<string> DiscoverCandidates(string text)
        => ExtractRareLexicalSignals(text);

    private static IReadOnlyList<string> ExtractRareLexicalSignals(string text)
        => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 6)
            .ToArray();
}

internal interface ICognitiveMemoryClusterSemanticSimilarityProvider
{
}
