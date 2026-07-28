namespace CanDoItAll.Infrastructure.Persistence;

public static class PostgreSqlMigrationBaseline
{
    public const string CurrentMigrationId = "20260728161028_InitialPostgreSqlBaseline";

    public static IReadOnlySet<string> LegacyMigrationIds { get; } = new HashSet<string>(
        [
            "20260528182412_InitialPostgreSqlBaseline",
            "20260529111314_AddWorkflowCheckpoints",
            "20260529220032_AddSchedulerRunObservability",
            "20260603113251_DisableCognitiveMemoryByDefault",
            "20260610113813_AddProcessVerificationAuditRecords",
            "20260612173521_PersistSubprocessChildArtifactMapping",
            "20260612222259_AddWorkspaceCurrencySettings",
            "20260615235147_ProcessModuleArchitectureV3RuntimePersistence",
            "20260616144322_ProcessV3RuntimeTables",
            "20260616155920_ProcessRuntimeAssignmentOperationContracts",
            "20260616162335_ProcessRuntimeAssignmentLaunchVariables",
            "20260617131500_ProcessRuntimeEventGlobalSequenceIdentityRepair",
            "20260618103000_ProcessRuntimeAssignmentRoleIdentity",
            "20260621212712_RemoveUnusedValidationActivityAutomationModules",
            "20260705163628_GenericMemoryProviderRuntime",
            "20260706015654_RetireLegacyCognitiveMemoryMainDbModel",
            "20260707110549_IncludeCognitiveMemoryModuleModel",
            "20260707134848_ProcessRuntimeAssignmentCapabilityScope",
            "20260707195705_ProcessStrategyResultReceiptLineage",
            "20260707222506_ProcessRuntimeInputArtifactContracts",
            "20260708120721_ProcessRuntimeStepArtifactDescriptors",
            "20260712133000_DistributedMemoryWorkerPhaseLeases",
            "20260712133717_RetireNativeCognitiveMemoryModelMetadata",
            "20260712204230_AddWorkflowUsageAnalytics",
            "20260712210953_AddProcessWorkflowExecutorBinding",
            "20260712215655_AddWorkflowLaunchIdempotency",
            "20260716134013_AddProjectPlanQueryIndexes",
            "20260716171034_AddWorkforceRateUnitAndCurrency",
            "20260719120144_ConsolidatePromptGallery",
            "20260719133111_OptimizePromptGallerySearchAndWorkflowBindings",
            "20260719161437_AddPromptGalleryFavoritesAndPreferences",
            "20260719235310_AddWorkflowDefinitionWriteHeads",
            "20260723002019_OptimizeDashboardActivityIndexes",
            "20260724114400_ImproveCrmHrRecordSelectionAndRecognitionIntegrity",
            "20260724144440_OptimizeCrmHrHighCardinalityQueries",
            "20260724224501_AddProcessRunRecords",
            "20260725224031_AddProcessStrategyResultUserSafeSummary",
            "20260725233333_AddProcessBlockedRecoveryHistory",
            "20260726022532_AddWorkflowStableExternalIdentity",
            "20260726030623_AddWorkflowRunIdempotencyEvidence",
            "20260727103524_AddCrmAccountConnectionProjects",
            "20260727232724_AddProviderProfileConcurrencyToken"
        ],
        StringComparer.Ordinal);

    public static IReadOnlySet<string> CustomIndexNames { get; } = new HashSet<string>(
        [
            "IX_Workspace_ConnectorCommands_PendingClaimOrder",
            "IX_Prompts_PromptArtifacts_SearchText_Trgm",
            "IX_Prompts_PromptTags_NameKey_Trgm"
        ],
        StringComparer.Ordinal);

    public const string CreateCustomObjectsSql =
        """
        CREATE EXTENSION IF NOT EXISTS pg_trgm;

        CREATE INDEX IF NOT EXISTS "IX_Workspace_ConnectorCommands_PendingClaimOrder"
        ON "Workspace_ConnectorCommands" ((COALESCE("NextAttemptAtUtc", "CreatedAtUtc")), "CreatedAtUtc")
        INCLUDE ("Id", "ProjectId", "ConnectorPluginKey", "CommandKey", "LeaseExpiresAtUtc")
        WHERE "Status" = 0 AND "ApprovalState" <> 1;

        CREATE INDEX IF NOT EXISTS "IX_Prompts_PromptArtifacts_SearchText_Trgm"
        ON "Prompts_PromptArtifacts" USING GIN ("SearchText" gin_trgm_ops);

        CREATE INDEX IF NOT EXISTS "IX_Prompts_PromptTags_NameKey_Trgm"
        ON "Prompts_PromptTags" USING GIN ("NameKey" gin_trgm_ops);
        """;

    public const string DropCustomIndexesSql =
        """
        DROP INDEX IF EXISTS "IX_Prompts_PromptTags_NameKey_Trgm";
        DROP INDEX IF EXISTS "IX_Prompts_PromptArtifacts_SearchText_Trgm";
        DROP INDEX IF EXISTS "IX_Workspace_ConnectorCommands_PendingClaimOrder";
        """;
}
