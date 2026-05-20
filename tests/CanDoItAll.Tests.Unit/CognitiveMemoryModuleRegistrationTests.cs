using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryModuleRegistrationTests
{
    [Fact]
    public async Task CognitiveMemoryModule_AllowsRelationalOnlyStartupWhenSemanticProvidersAreMissing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock());
        services.AddCognitiveMemoryModule();

        using var provider = services.BuildServiceProvider();

        var embeddingProvider = provider.GetRequiredService<ICognitiveMemoryEmbeddingProvider>();
        var semanticRanker = provider.GetRequiredService<ICognitiveMemorySemanticRanker>();
        var projectionAdapter = provider.GetRequiredService<ICognitiveMemoryProjectionAdapter>();
        var classifier = provider.GetRequiredService<ICognitiveMemorySemanticClassifier<RegistrationTestLabel>>();

        var embeddingError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await embeddingProvider.EmbedAsync(new CognitiveMemoryEmbeddingRequest(
                new CognitiveMemoryEmbeddingProfileId("missing"),
                "source text",
                new CognitiveMemoryProcessingBudget(1, 1024, TimeSpan.FromSeconds(5)))));
        var rankError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await semanticRanker.RankAsync(new CognitiveMemorySemanticRankRequest(
                "source text",
                new CognitiveMemoryPageRequest(take: 1),
                new CognitiveMemoryProcessingBudget(1, 1024, TimeSpan.FromSeconds(5)))));
        var projectionError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await projectionAdapter.EnsureCollectionAsync(new CognitiveMemoryProjectionCollectionRequest(
                new CognitiveMemoryProjectionCollectionName("missing"),
                VectorDimensions: 3)));
        var classifierError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await classifier.ClassifyAsync(new CognitiveMemorySemanticClassificationRequest(
                "source text",
                new CognitiveMemoryProcessingBudget(1, 1024, TimeSpan.FromSeconds(5)))));

        Assert.Contains("IAgentTextEmbeddingGenerator", embeddingError.Message);
        Assert.Contains("ISemanticTextRanker", rankError.Message);
        Assert.Contains("IRagDriver", projectionError.Message);
        Assert.Contains("ISemanticClassifier", classifierError.Message);
    }

    [Fact]
    public void CognitiveMemoryModule_RegistersQualityCollaboratorsAndVersionedOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock());
        services.AddCognitiveMemoryModule();

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<CognitiveMemoryQualityAlgorithmOptions>();
        Assert.Equal("quality-clustering-v3", options.Cluster.AlgorithmVersion.Value);
        Assert.IsAssignableFrom<ICognitiveMemoryClusterKeyExtractor>(provider.GetRequiredService<ICognitiveMemoryClusterKeyExtractor>());
        Assert.IsAssignableFrom<ICognitiveMemoryCandidatePairSelector>(provider.GetRequiredService<ICognitiveMemoryCandidatePairSelector>());
        Assert.IsAssignableFrom<ICognitiveMemoryDreamClaimSynthesizer>(provider.GetRequiredService<ICognitiveMemoryDreamClaimSynthesizer>());
        Assert.IsAssignableFrom<ICognitiveMemoryDreamModeClusterSelector>(provider.GetRequiredService<ICognitiveMemoryDreamModeClusterSelector>());
        Assert.IsAssignableFrom<ICognitiveMemoryDreamEntailmentValidator>(provider.GetRequiredService<ICognitiveMemoryDreamEntailmentValidator>());
        Assert.IsAssignableFrom<ICognitiveMemoryRecallBriefComposer>(provider.GetRequiredService<ICognitiveMemoryRecallBriefComposer>());
        Assert.IsAssignableFrom<ICognitiveMemoryProfessorTeachingExtractor>(provider.GetRequiredService<ICognitiveMemoryProfessorTeachingExtractor>());
    }

    private enum RegistrationTestLabel
    {
        Unknown = 0,
        Match = 1
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }
}
