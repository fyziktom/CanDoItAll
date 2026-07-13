# Source Artifacts

## Uploaded source ZIPs

- `CanDoItAll-development (2)(1).zip`: current CanDoItAll development snapshot used as current-state source truth for local file analysis.
- `candoitall-memory-architecture-design(1).zip`: previously generated architecture package used as the architectural baseline for this implementation bundle.

## Live re-entry source truth

- `C:\repositories\CanDoItAll`: live main repository inspected during the 2026-07-05 re-entry refresh.
- `C:\repositories\CanDoItAll.CognitiveMemory`: live native target repository inspected during the 2026-07-05 re-entry refresh. At re-entry it contains only `README.md`, so SB24 must scaffold or align the first native solution/projects.
- `bundle://inputs/04-live-reentry-request.md`: preserved current user request for this re-entry.
- `bundle://analysis/04-live-repo-reentry-alignment.md`: current source alignment and post-MAF-refactor correction note.

## Repository bundle convention reviewed

The CanDoItAll bundle convention was reviewed from the development branch under:

- `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-preparation/assets/templates/root-readme-template.md`
- `repo://codex/skills/bundles/candoitall-bundle-preparation/assets/templates/subbundle-readme-template.md`
- `repo://codex/skills/bundles/candoitall-bundle-preparation/references/subbundle-contract.md`
- `repo://codex/skills/bundles/candoitall-bundle-preparation/references/bundle-validation-rubric.md`
- `repo://codex/bundles/skill-tool-mcp-isolation-template-migration/README.md`
- `repo://codex/bundles/skill-tool-mcp-isolation-template-migration/plan/01-phase-plan.md`

## Current code surfaces reviewed

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/CanDoItAll.Modules.CognitiveMemory.csproj`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentContextContributionContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentContextContributionProvider.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs`
- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/WorkflowExecutorContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderDispatchModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderCapabilityContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeEvidenceSourceProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/UnavailableProcessRuntimeEvidenceSourceProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj`
- `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/App/CanDoItAll.Composition/ModuleAssemblies.cs`
- `repo://src/App/CanDoItAll.Web/Api/CognitiveMemoryApi.cs`
- `repo://src/App/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/CognitiveMemoryPersistenceModelTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/CognitiveMemoryReviewUiPlaywrightTests.cs`

## Previous architecture package imported as baseline

The previous package established the target split into a generic Memory Provider module and a separate native Cognitive Memory service. This implementation bundle preserves that architecture and adds execution order, dependency gates, checkpoint/refactoring subbundles, stricter transition strategy, and source-grounded removal inventory.
