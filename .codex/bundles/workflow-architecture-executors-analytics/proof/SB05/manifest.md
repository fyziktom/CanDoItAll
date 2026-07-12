# SB05 Proof Manifest

- Subbundle ID: SB05
- Status: Completed
- Baseline commit: 5f9d13dc04362442073b4782d544fbb88429af55
- Owned requirements: WF-AN-01, WF-AN-02, workflow token/model/cost/duration analytics, executor/plugin usage propagation, and process rollup without double counting
- Semantic invariant contract: bundle://proof/SB05/semantic-invariants.md
- Browser validation: N/A for SB05; typed API/projection proof only. Presentation belongs to SB06/SB07.

## Evidence

- Failing-first transcript: bundle://proof/SB05/transcripts/closure.txt
- Passing transcript: bundle://proof/SB05/transcripts/closure.txt
- Anti-stub transcript: bundle://proof/SB05/transcripts/closure.txt
- Failing-first: bundle://proof/SB05/failing-first.txt
- Passing focused unit/process rollup: bundle://proof/SB05/passing-unit.txt
- Passing real PostgreSQL persistence: bundle://proof/SB05/passing-persistence.txt
- Passing API validation: bundle://proof/SB05/passing-api.txt
- Passing builds/model convergence: bundle://proof/SB05/passing-build.txt
- Idempotent migration SQL: bundle://proof/SB05/migration-script.txt
- Production source assertions/matrix: bundle://proof/SB05/source-assertions.txt
- Anti-stub/silent-fallback audit: bundle://proof/SB05/anti-stub.txt
- Architecture/dependency proof: bundle://proof/SB05/architecture-snapshot.txt

## Named Test Proof

- LlmInvokerPreservesEachProviderObservationWithoutCollapsingIdentityOrDimensions
- ExecutorUsageReachesCompilerProgressAndBackendResult
- PricingDistinguishesKnownFreeFromUnknownWhileRetainingAllObservedTokens
- AnalyticsTotalsAreIndependentOfRecentEightRunWindow
- DurationUsesExplicitTerminalTimestampAndInjectedTimeProvider
- ProviderMappingPreservesEveryFactAndSeparatesUsageFromPricingKnowledge
- SyntheticMappingUsesStableIdsAndRejectsCorruptDimensions
- InMemoryStoreIsIdempotentAndRejectsImmutableFactConflictsAtomically
- RuntimePersistsOneCorrelatedFactWhenProgressAndBackendReturnTheSameObservation
- RuntimePersistsFailureFactsBeforeRethrowingBackendFailure
- PostgreSqlPersistsImmutableUsageFactsAndExecutesDatabaseAggregates
- Runtime_usage_reader_counts_typed_process_workflow_fact_once_when_agent_telemetry_has_same_id
- Runtime_usage_reader_rejects_invalid_query_boundaries
- Runtime_usage_reader_rejects_same_id_immutable_dimension_drift
- Workflow_api_handler_rejects_invalid_explicit_analytics_recent_take

## Raw Note Closure

- Tokens/models/cost/time analytics: complete through canonical facts, DB aggregates, typed query service, API, and SB06 DTO consumption.
- New LLM/executor/plugin usage: compiler/progress/result/failure boundaries preserve canonical facts; ImageAnalyze is the usage-aware executor proof.
- One implementation: WorkflowUsageObservationFactory is the canonical provider/legacy mapping; executors and workflow runtime carry facts rather than recomputing usage analytics.
- Starts from project/scheduler/agent/process: launch Origin is persisted generically; typed ProcessAssignment origin is indexed and process analytics consumes it without legacy GUID downcasts.
- Process-style rollup: workflow facts merge by stable usage ID and reject any immutable drift rather than double counting or overwriting.

## Changed-File SHA-256

The table records final working-tree hashes. Files shared with other subbundles contain the combined in-scope workspace state; the SB05 source assertions identify the owned behavior.

| File | SHA-256 |
|---|---|
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowUsageModels.cs | fa78b5a14e9f3ca9d736bd6419bed323af7adb08074bafca997325c1f8bd62cc |
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowIdJsonConverters.cs | a50f3b4ad5509bc936a8395e773d39af35a7729df84c0d7180ce4963538bdf8a |
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs | e9c53d2cec7bc1322d4bbb09ca1e14656ee3632a9b8b18e5a9f84228ed6ea5b9 |
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs | d3f3b33337b13cd6e99cccb5dc62938fcc14b5cba45bf5c5c997224c1a2c11af |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowUsageAnalyticsContracts.cs | ca3c7be3ab9adeb03f4c2ce6f937cc64c23aa47b712197b68e38dabf5be4af43 |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowRuntimeContracts.cs | 47404a77827c6c768342e9eeca287ffa0f50a91724ffab30d85f81d49769fa0f |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowNodeExecutionProgress.cs | 924dc5d50a4f2f6dc7951f225bd7a07fdd7b76f1b278e529292bf2cfa66e785c |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/InMemoryWorkflowUsageObservationStore.cs | 9269a3418c9beb8ab56f4c3ad2af2a0bb3c7409304bafd33f00fbdc8a4462f89 |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowUsageAnalyticsStore.cs | 040257f65f3c88c6f0295c692e54117565e0cbfe2c1486a4b80882bcd5de5f6c |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowAnalyticsQueryService.cs | e7b84dc7bb4129e0e869b2bb6e781b9338bfa5dec49491514c7628f244eda869 |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeServiceCollectionExtensions.cs | 09bbd7b79dc85c5f5403018d882ae082c666e65f226b65e57b478981ff311e80 |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowLlmComponentInvoker.cs | 1d4c2b38f2f656842bb2820c53c4312dc129afb00fdf6b0fcd0113b2320d4f8a |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowCompiler.cs | 37cdc41d79021f7ce782d9743262ec7e15dcd5b5fc4f4a3fc392673b00758499 |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/WorkflowBackendProgressEventObserver.cs | bacc90439aa835dbee6e7f4d17074161fdff5c432d234152e27b133371aed873 |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafInProcessWorkflowExecutionBackend.cs | 9063708dbfab05d7a18cbbaa66bb3ebe761fef98232280429d4bf67a9e231e1b |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeManager.cs | 57de3c77896d68bb72919c4fe28c525547804b94c77f94d1cb1a2b905c37c8d6 |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/StoreBackedWorkflowNodeExecutionProgressObserver.cs | 265642d4e31d629b88762f22fb2a376cd28af0afb1fe64ea83350101815ce4d4 |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowRuntimeManagerRunLauncher.cs | baaa58c58e69abf341e1fc1b038a9bb99cb4773544e4c2ad4200bc9860526332 |
| repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowUsageObservationStore.cs | d7998fe182944f79ce3302a62ffd796c61c0f8781c54d16388bf0bdb10900e19 |
| repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs | 8d0a1dc20f667bf2efae387097f52fc1386d4e5a1c4e80b3363e17e407c4b20f |
| repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs | d9312a818850757d9e940941ee3ff5ef82ad1a188b9f42c4a6207fda6c0f7c48 |
| repo://src/Processes/CanDoItAll.Processes.Projections/ProcessExecutionObservationContracts.cs | 0dc9991fc09339f9e1623c8343059a861ebf390b9554e0af358b821c2f8740f7 |
| repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeUsageTelemetryReader.cs | feacaecd73fb5b0a63ecee70eb5a38db840ad086875e81bc8bcc02240a50ce74 |
| repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/WorkflowAwareProcessRuntimeUsageTelemetryReader.cs | 3d30f1aa09f17804a6e7acb2d60eb51bdac75c794b5f084c55798dedb70d143e |
| repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs | d045149b7974c7e12a1a2afeeb4e210e1d042f15b6d7c8369ba3255e2159735e |
| repo://src/App/CanDoItAll.Web/Api/WorkflowsApi.cs | 54c336e67db61c59330b234ef002683da8feb5f488124f8910dc2f7c55606c29 |
| repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260712204230_AddWorkflowUsageAnalytics.cs | 670c99da97b9a12d44279bd527f2befebb5dceb28beccae848af0da0f5127d09 |
| repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260712204230_AddWorkflowUsageAnalytics.Designer.cs | 8a0d572af4ca3f193e5ca46dabc4527420d336e2091f7f595b1f5fd2741b87e3 |
| repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs | 3c8d07865ec45c724f4c3c283eed7e7c66d957bd238223471e39ae5f3e859488 |
| repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowUsageAnalyticsRedGateTests.cs | b2e38ad2d9a27189a01af667ddc48a4e283c0d9993d5ed5516144af753e64919 |
| repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowUsageAnalyticsTests.cs | adb25a1688dd12a9cf144b8bd155dd4617b9e8acfee3c224e2b367703af9c22a |
| repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationMetadataTests.cs | 1c847f7603e1e58bb703a66396ea1127abc1c508311ac506ce6d58f7fccde74c |
| repo://tests/Integration/CanDoItAll.Tests.Integration/WorkflowUsagePersistenceIntegrationTests.cs | 506567e5256ab88d57d388276c727447bbb52810c88270ab3f01dfd35ab3319a |
| repo://tests/Integration/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs | 17fc65e6212049158d3f10ef6f41ca034b3febb025c180b996974f3d7e1defcc |

## Result

- SB05-CANONICAL-FACT is satisfied by immutable typed observations, exact one-to-one mapping, explicit unavailable/failure facts, and compatibility-only WorkflowUsageMetrics projection.
- SB05-CORRELATION-PERSISTENCE is satisfied by correlation before append, null-run rejection, NOT NULL schema, exact terminal/origin persistence, and typed process-origin indexes.
- SB05-IDEMPOTENT-AGGREGATION is satisfied by stable IDs, atomic conflicts, database-side complete totals, bounded recent rows only, and one-count process rollup.
- SB05-EXPLICIT-ANALYTICS is satisfied by TerminalAtUtc/TimeProvider duration, known-free versus unknown pricing, typed API query service, explicit 400 validation, and no event JSON projection.
