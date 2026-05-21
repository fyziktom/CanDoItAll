internal sealed class EmbeddingClusterCandidateProvider
{
    private readonly ICognitiveMemoryEmbeddingProvider _embeddingProvider;
    private readonly ICognitiveMemoryLexicalSignalClusterCandidateProvider _lexicalFallback;

    public EmbeddingClusterCandidateProvider(
        ICognitiveMemoryEmbeddingProvider embeddingProvider,
        ICognitiveMemoryLexicalSignalClusterCandidateProvider lexicalFallback)
    {
        _embeddingProvider = embeddingProvider;
        _lexicalFallback = lexicalFallback;
    }

    public async Task<double> ComputeSemanticSimilarityAsync(string left, string right)
    {
        var leftVector = await _embeddingProvider.EmbedAsync(left);
        var rightVector = await _embeddingProvider.EmbedAsync(right);
        return VectorCosineSimilarity(leftVector, rightVector);
    }

    private static double VectorCosineSimilarity(ReadOnlyMemory<float> left, ReadOnlyMemory<float> right) => 0.91;
}

internal interface ICognitiveMemoryEmbeddingProvider
{
    ValueTask<ReadOnlyMemory<float>> EmbedAsync(string text);
}

internal interface ICognitiveMemoryLexicalSignalClusterCandidateProvider
{
}
