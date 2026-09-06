# Provider source and consumers

ProviderManagement: SharedProviderSourceVerification and SourceSyncService (Replace), SharedProviderManagementService.NormalizeAlias (authoritative write); persistence/registry/API remain intact.
Module services: SharedProviderRecovery; ProviderEditorRecovery; ProviderEditorOperations (Complete/Remove/controlled retry).
Module UI: Sharing panel, Sources dialog, SharedProviderRefreshButton; ProviderModelThinkingEditor forwards refresh; AgentProviderProfilesPanel and ProviderProfilesSession receive catalog/editor reconciliation; AgentDetailsDialog receives runtime-provider refresh.
Tests: new direct verification/lifecycle tests; SharedProviderRecoveryTests, SharedSourceRecoveryTests, PublicationPanel/OwnedEffects/Lifetime/SourceAndImport/Refresh tests, ProviderSharedReconciliation/Operations and actual ProviderRecoveryIntegration/SourceSync fixtures. No private reflection or shape-count assertions.
