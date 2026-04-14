# Detailed findings

## F001 — Dual dependency representation

Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs`

Impact:
- two effective sources of truth,
- repeated fallback logic,
- compatibility code leaks into runtime and reads,
- harder debugging and weaker invariants.

Owning subbundles:
- `02-canonical-dependency-model-and-compatibility-boundary`
- `03-side-effect-free-validation-and-editor-normalization-split`

## F002 — Validation mutates state

Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`

Impact:
- validation cannot be trusted as pure analysis,
- caller ordering becomes fragile,
- tests can pass or fail depending on hidden normalization side effects.

Owning subbundle:
- `03-side-effect-free-validation-and-editor-normalization-split`

## F003 — Destructive graph persistence

Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`

Impact:
- unstable child IDs,
- DB churn,
- harder auditability,
- more fragile references,
- higher risk during future merge or collaboration scenarios.

Owning subbundles:
- `05-transaction-concurrency-and-conflict-hardening`
- `06-differential-definition-graph-persistence`

## F004 — Missing optimistic concurrency

Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs`

Impact:
- lost updates,
- unsafe concurrent editing,
- unsafe concurrent transition processing,
- race-prone publish and draft generation.

Owning subbundle:
- `05-transaction-concurrency-and-conflict-hardening`

## F005 — Publication/version race windows

Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs`

Impact:
- slug collisions surface late,
- `Max + 1` version logic can race,
- clone and publish responsibilities are mixed.

Owning subbundle:
- `08-publication-versioning-and-clone-engine-decomposition`

## F006 — Runtime orchestration hotspot

Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`

Impact:
- oversized method,
- mixed concerns,
- weaker unit-test seam,
- higher chance of regression during policy changes.

Owning subbundle:
- `09-runtime-state-machine-and-transition-policy-extraction`

## F007 — Read-side small-data assumption

Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessesService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs`

Impact:
- increasing memory pressure,
- query inefficiency,
- weaker separation between query shape and domain mutation logic.

Owning subbundle:
- `10-read-side-query-splitting-and-performance-hardening`

## F008 — Cross-module duplication

Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateCatalogService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateLibraryService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs`
- `src/CanDoItAll.Modules.Factory/PromptLibraryPackLoader.cs`
- `src/CanDoItAll.Modules.Projects/ProjectModels.cs`

Impact:
- drift risk,
- repeated fixes,
- harder consistency review,
- more places to test the same parsing or transformation idea.

Owning subbundle:
- `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation`

## F009 — Workspace monolith risk

Observed in:
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Links.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Actions.cs`

Impact:
- high cognitive load,
- harder UI-state reasoning,
- bigger merge conflicts,
- weaker regression isolation.

Owning subbundle:
- `13-workspace-and-canvas-decomposition`

## F010 — Schema/configuration concentration and long-file sprawl

Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Projects/ProjectModels.cs`

Impact:
- model/config drift is easier to miss,
- relationship policy is harder to audit,
- migrations become harder to reason about.

Owning subbundle:
- `14-schema-hygiene-migrations-and-long-file-split`
