using CanDoItAll.AgentFramework.Rag.Driver.Abstractions;
using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Embeddings;
using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Semantics;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.CognitiveMemory;

public static class CognitiveMemoryModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCognitiveMemoryModule(this IServiceCollection services)
    {
        services.TryAddScoped<ICognitiveMemoryAccessPolicy, CognitiveMemoryDefaultAccessPolicy>();
        services.TryAddSingleton<ICognitiveMemoryRecordValidator, CognitiveMemoryRecordValidator>();
        services.TryAddSingleton<ICognitiveMemoryScoreSpaceRegistry, CognitiveMemoryScoreSpaceRegistry>();
        services.TryAddSingleton<ICognitiveMemoryScoreGeometryDriver, CognitiveMemoryScoreGeometryDriver>();
        services.TryAddScoped<ICognitiveMemoryMutationAuthority, CognitiveMemoryMutationAuthority>();
        services.TryAddScoped<ICognitiveMemorySourceIngestionService, CognitiveMemorySourceIngestionService>();
        services.TryAddSingleton<ICognitiveMemoryTaxonomyValidator, CognitiveMemoryTaxonomyValidator>();
        services.TryAddScoped<ICognitiveMemoryEmbeddingProvider>(provider =>
        {
            var embeddingGenerator = provider.GetService<IAgentTextEmbeddingGenerator>()
                ?? throw new InvalidOperationException("Cognitive memory embeddings require IAgentTextEmbeddingGenerator to be registered.");
            return new SemanticCompletionCognitiveMemoryEmbeddingProvider(
                embeddingGenerator,
                provider.GetRequiredService<IClock>());
        });
        services.TryAddScoped<ICognitiveMemorySemanticRanker>(provider =>
        {
            var ranker = provider.GetService<ISemanticTextRanker>()
                ?? throw new InvalidOperationException("Cognitive memory semantic ranking requires ISemanticTextRanker to be registered.");
            return new SemanticCompletionCognitiveMemoryRanker(
                ranker,
                provider.GetRequiredService<IClock>());
        });
        services.TryAddScoped(typeof(ICognitiveMemorySemanticClassifier<>), typeof(SemanticCompletionCognitiveMemoryClassifier<>));
        services.TryAddScoped<ICognitiveMemoryProjectionAdapter>(provider =>
        {
            var ragDriver = provider.GetService<IRagDriver>()
                ?? throw new InvalidOperationException("Cognitive memory vector projection requires IRagDriver to be registered.");
            return new RagCognitiveMemoryProjectionAdapter(ragDriver);
        });
        services.TryAddScoped<ICognitiveMemoryProjectionLifecycleService, CognitiveMemoryProjectionLifecycleService>();
        services.TryAddScoped<ICognitiveMemoryWorkspaceService, CognitiveMemoryWorkspaceService>();
        services.TryAddScoped<ICognitiveMemoryAttentionRouter, CognitiveMemoryAttentionRouter>();
        services.TryAddScoped<CognitiveMemorySignalLedger>();
        services.TryAddScoped<ICognitiveMemorySignalLedger>(provider => provider.GetRequiredService<CognitiveMemorySignalLedger>());
        services.TryAddScoped<ICognitiveMemoryPredictionErrorEngine>(provider => provider.GetRequiredService<CognitiveMemorySignalLedger>());
        services.TryAddScoped<ICognitiveMemoryRecallOrchestrator, CognitiveMemoryRecallOrchestrator>();
        services.TryAddScoped<ICognitiveMemoryConsolidationEngine, CognitiveMemoryConsolidationEngine>();
        services.TryAddScoped<CognitiveMemoryTemporalReplayService>();
        services.TryAddScoped<ICognitiveMemoryTemporalEpisodeService>(provider => provider.GetRequiredService<CognitiveMemoryTemporalReplayService>());
        services.TryAddScoped<ICognitiveMemoryReplayScheduler>(provider => provider.GetRequiredService<CognitiveMemoryTemporalReplayService>());
        services.TryAddScoped<CognitiveMemoryProcedureSkillService>();
        services.TryAddScoped<ICognitiveMemoryProcedureSkillMemoryService>(provider => provider.GetRequiredService<CognitiveMemoryProcedureSkillService>());
        services.TryAddScoped<ICognitiveMemorySimulationSandboxService>(provider => provider.GetRequiredService<CognitiveMemoryProcedureSkillService>());
        services.TryAddScoped<ICognitiveMemoryReviewUiService, CognitiveMemoryReviewUiService>();
        return services;
    }
}

public static class CognitiveMemoryModuleAssemblyMarker;
