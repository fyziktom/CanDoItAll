# Test Inventory

## Current tests to preserve and split

| Current test group | Current examples | Future destination |
| --- | --- | --- |
| Native engine unit tests | `repo://tests/Unit/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs`, `repo://tests/Unit/CanDoItAll.Tests.Unit/CognitiveMemoryConsolidationEngineTests.cs` | Native service test suite after domain/application extraction. |
| Native persistence model tests | `repo://tests/Integration/CanDoItAll.Tests.Integration/CognitiveMemoryPersistenceModelTests.cs` and related model tests | Native persistence tests with `CognitiveMemoryDbContext`. |
| Current module registration tests | `repo://tests/Unit/CanDoItAll.Tests.Unit/CognitiveMemoryModuleRegistrationTests.cs` | Generic memory module registration tests plus native service registration tests. |
| Current UI component test | `repo://tests/Components/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs` | Native UI surface tests plus generic Memory UI tests. |
| Current Playwright review UI test | `repo://tests/Playwright/CanDoItAll.Tests.Playwright/CognitiveMemoryReviewUiPlaywrightTests.cs` | Native provider UI surface Playwright tests; generic UI smoke tests. |
| Current fakes | `repo://tests/Support/CanDoItAll.Tests.Support/CognitiveMemory/CognitiveMemoryFakes.cs` | Generic mock provider package and native service fakes. |
| Current MAF source snapshot tests if present or added | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`, `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs`, `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeEvidenceSourceProvider.cs` | Source Gateway contract tests and migration tests. |

## New tests required by this bundle

- Memory protocol envelope validation tests.
- Provider manifest and selection tests.
- Operation ledger and feedback lifecycle tests.
- Event dedupe and loop guard tests.
- Source Gateway adapter contract tests.
- MAF generic tool/executor shared handler tests.
- Architecture dependency guard tests.
- Startup without Qdrant/native memory integration test.
- Two-provider agent/workflow selection e2e test.
- Native remote provider driver contract test.
- Zero-provider tests for service registration, UI route, MAF tool exposure, workflow executor behavior, and context contributor skip/fail policy.
- MAF integration tests through current `IAgentRuntimeToolProvider`, `IWorkflowExecutor`, and `IAgentContextContributor` pipelines.
- Source snapshot compatibility tests proving existing Workbench/Workflow providers still work after Source Gateway rehome or wrapping.
