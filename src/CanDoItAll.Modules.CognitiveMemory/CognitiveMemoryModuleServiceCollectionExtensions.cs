using CanDoItAll.AgentFramework.Rag.Driver.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Embeddings;
using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Semantics;
using CanDoItAll.Infrastructure.ControlPlane;
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
        services.TryAddScoped<ICognitiveMemoryConsolidationCandidateApplicator, CognitiveMemoryConsolidationCandidateApplicator>();
        services.TryAddScoped<ICognitiveMemorySourceIngestionService, CognitiveMemorySourceIngestionService>();
        services.TryAddScoped<ICognitiveMemoryAutomationSettingsService, CognitiveMemoryAutomationSettingsService>();
        services.TryAddScoped<ICognitiveMemoryExternalSourceIngestionService, CognitiveMemoryExternalSourceIngestionService>();
        services.TryAddSingleton<ICognitiveMemoryTaxonomyValidator, CognitiveMemoryTaxonomyValidator>();
        services.TryAddScoped<ICognitiveMemoryEmbeddingProvider>(provider =>
        {
            var embeddingGenerator = provider.GetService<IAgentTextEmbeddingGenerator>();
            return embeddingGenerator is null
                ? new UnavailableCognitiveMemoryEmbeddingProvider()
                : new SemanticCompletionCognitiveMemoryEmbeddingProvider(
                    embeddingGenerator,
                    provider.GetRequiredService<IClock>());
        });
        services.TryAddScoped<ICognitiveMemorySemanticRanker>(provider =>
        {
            var ranker = provider.GetService<ISemanticTextRanker>();
            return ranker is null
                ? new UnavailableCognitiveMemorySemanticRanker()
                : new SemanticCompletionCognitiveMemoryRanker(
                    ranker,
                    provider.GetRequiredService<IClock>());
        });
        services.TryAddScoped(typeof(ICognitiveMemorySemanticClassifier<>), typeof(OptionalSemanticCompletionCognitiveMemoryClassifier<>));
        services.TryAddScoped<ICognitiveMemoryProjectionAdapter>(provider =>
        {
            var ragDriver = provider.GetService<IRagDriver>();
            return ragDriver is null
                ? new UnavailableCognitiveMemoryProjectionAdapter()
                : new RagCognitiveMemoryProjectionAdapter(ragDriver);
        });
        services.TryAddScoped<ICognitiveMemoryProjectionLifecycleService, CognitiveMemoryProjectionLifecycleService>();
        services.TryAddScoped<ICognitiveMemoryProjectionRebuildService, CognitiveMemoryProjectionRebuildService>();
        services.TryAddScoped<ICognitiveMemoryScheduledAutomationRunner, CognitiveMemoryScheduledAutomationRunner>();
        services.TryAddScoped<ICognitiveMemoryRetentionCleanupService, CognitiveMemoryRetentionCleanupService>();
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
        services.TryAddScoped<ICognitiveMemoryQualityDiagnosticsService, CognitiveMemoryQualityDiagnosticsService>();
        services.TryAddScoped<ICognitiveMemoryClusterPlanner, CognitiveMemoryClusterPlanner>();
        services.TryAddScoped<ICognitiveMemoryDreamValidator, CognitiveMemoryDreamValidator>();
        services.TryAddScoped<ICognitiveMemoryDreamConsolidationService, CognitiveMemoryDreamConsolidationService>();
        services.TryAddSingleton<ICognitiveMemoryAggregateConfidenceCalibrator, CognitiveMemoryAggregateConfidenceCalibrator>();
        services.TryAddScoped<ICognitiveMemoryAggregateMemoryApplicator, CognitiveMemoryAggregateMemoryApplicator>();
        services.TryAddScoped<ICognitiveMemoryRecallSynthesisService, CognitiveMemoryRecallSynthesisService>();
        services.TryAddScoped<ICognitiveMemoryReferenceResolver, CognitiveMemoryReferenceResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentContextContributor, CognitiveMemoryAgentContextContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDatabaseTransferHandler, CognitiveMemorySourceTruthDatabaseTransferHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, CognitiveMemoryRecallWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, CognitiveMemoryProbeWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, CognitiveMemoryLearningProposalWorkflowExecutor>());
        services.TryAddScoped<ICognitiveMemoryProbeService, CognitiveMemoryProbeService>();
        services.TryAddScoped<ICognitiveMemoryCuratorConversationService, CognitiveMemoryCuratorConversationService>();
        services.TryAddScoped<ICognitiveMemoryProfessorAnchorService, CognitiveMemoryProfessorAnchorService>();
        services.TryAddScoped<ICognitiveMemorySelfModelStore, CognitiveMemorySelfModelStore>();
        services.TryAddScoped<ICognitiveMemoryCalibrationHealthService, CognitiveMemoryCalibrationHealthService>();
        services.TryAddScoped<ICognitiveMemorySelfRegulationOrchestrator, CognitiveMemorySelfRegulationOrchestrator>();
        services.TryAddScoped<ICognitiveMemoryProfessorReviewService, CognitiveMemoryProfessorReviewService>();
        services.TryAddScoped<ICognitiveMemoryAnswerGateService, CognitiveMemoryAnswerGateService>();
        services.TryAddScoped<ICognitiveMemoryEpistemicDriveService, CognitiveMemoryEpistemicDriveService>();
        services.TryAddScoped<ICognitiveMemoryCrossProjectMemoryService, CognitiveMemoryCrossProjectMemoryService>();
        services.TryAddScoped<ICognitiveMemoryDistributedComputeCoordinator, CognitiveMemoryDistributedComputeCoordinator>();
        return services;
    }
}

public static class CognitiveMemoryModuleAssemblyMarker;
