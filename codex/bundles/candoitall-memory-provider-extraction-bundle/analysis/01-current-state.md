# Current State Analysis

## Current Cognitive Memory module shape

The current `src/Modules/CanDoItAll.Modules.CognitiveMemory` project is a combined UI module, domain model, persistence model, service layer, projection/RAG integration, MAF integration, ingestion system, review UI, scoring system, temporal replay system, and operational runtime. It is not a thin host wrapper.

The 2026-07-05 live re-entry confirmed that the module still exists in the main repo and still owns native engine, UI, persistence, MAF context contribution, workflow executors, projection adapters, and source ingestion code. The earlier ZIP-based inventory remains directionally accurate, but `analysis/04-live-repo-reentry-alignment.md` is now the current source-truth addendum for MAF and native repo state.

Approximate current module inventory from the uploaded source snapshot:

| Area | Files | Approx LOC | Extraction implication |
| --- | ---: | ---: | --- |
| `Advanced` | 22 | 8511 | Contains MAF contributor/executors, professor, curator, self-regulation, distributed compute, and accepted-use feedback. Must move behind native provider/service boundary or generic abstractions. |
| `Common` | 4 | 622 | Contains current shared contracts. Some concepts can seed generic protocol, but names are native-specific. |
| `Foundation` | 4 | 1086 | Native records and validators. Must move to native service DB. |
| `Ingestion` | 4 | 1279 | Current ingestion is tied to native model and host DB. Must be split into generic source gateway plus native provider ingestion. |
| `Projection` | 2 | 945 | RAG/Qdrant projection coupling must become optional native provider projection. |
| `Quality` | 17 | 7363 | Native engine logic. Should not remain in main CanDoItAll generic module. |
| `Recall` | 15 | 3933 | Native recall engine. It should be exposed through generic protocol, not through MAF direct dependency. |
| `Pages` | 23 | 6003 | Current rich UI must be converted into provider-specific RCL or external native service UI surface. |
| `ReviewUi` | 8 | 2575 | Native review workflow UI. Should remain provider-specific. |
| Other folders | 46 | 12221 | Scoring, signals, taxonomy, temporal replay, workspace attention, procedures, settings, operations. Mostly native provider engine/service ownership. |

## Current hard dependency seams

- `src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj` directly references `CanDoItAll.Modules.CognitiveMemory` and Qdrant/SemanticCompletion driver projects.
- `src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` calls `AddConfiguredQdrantRagDriver` and `AddCognitiveMemoryModule` in the base runtime module registration path.
- `src/App/CanDoItAll.Composition/ModuleAssemblies.cs` includes `CognitiveMemoryModuleAssemblyMarker`, causing the main application model assembly discovery path to include native memory entity configurations.
- `src/Modules/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` registers native services directly into the main host service collection, including MAF context contributor and workflow executors.
- `src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` implements `IAgentContextContributor` and several `IWorkflowExecutor` classes directly with `ICognitiveMemory*` interfaces.
- Current native memory services use `IDbContextFactory<AppDbContext>` heavily, which means native memory records are in the main application persistence model today.
- Current API endpoints under `src/App/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs` expose native Cognitive Memory endpoints directly from the main web app.

## Current MAF and source-snapshot re-entry findings

- Current MAF context contribution is defined by `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentContextContributionContracts.cs` and bridged into MAF by `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentContextContributionProvider.cs`.
- Current runtime tools use `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs` and `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs`.
- Current workflow executors use `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/WorkflowExecutorContracts.cs` and descriptor models in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`.
- Current provider profile/capability concepts exist under `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers` and `repo://src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderCapabilityContracts.cs`; memory provider identity must not conflict with these or rely on string tags when a typed purpose/kind is needed.
- Current memory source snapshot contracts already exist in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`, with Workbench and Workflow providers implemented and Process currently unavailable through `UnavailableProcessRuntimeEvidenceSourceProvider`.
- The separate `C:\repositories\CanDoItAll.CognitiveMemory` repository exists but is unscaffolded at re-entry. SB24 must create the native project structure instead of merely verifying it.

## Current tests and expected regression surfaces

The current test suite contains memory-focused unit, integration, component, and Playwright tests. This is useful because the migration can preserve behavior through tests, but the tests currently reference native `CognitiveMemory*` types and the main AppDbContext model. They need to be split into:

- generic memory contract/runtime tests in the main repo;
- native Cognitive Memory engine/persistence tests in the native repo;
- adapter/compatibility tests proving the old behavior can be reached through the generic provider protocol;
- dependency guards proving MAF and base composition do not reference native classes.

## Current implementation hazard

A direct folder move would fail the architecture objective. The current module is entangled with AppDbContext, main host composition, Qdrant provider setup, MAF context contribution, workflow executors, existing source snapshot contracts, and native UI pages. The migration must be staged through generic contracts and adapter boundaries before the physical native service extraction.
