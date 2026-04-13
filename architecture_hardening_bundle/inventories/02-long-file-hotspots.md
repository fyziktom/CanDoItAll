# Long-file hotspots

| Approx. lines | File | Why it is risky | Owning subbundle |
| ---: | --- | --- | --- |
| 1181 | `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor` | Large markup surface and UI-state concentration | `13-workspace-and-canvas-decomposition` |
| 850 | `src/CanDoItAll.Modules.Processes/ProcessTemplateLibraryService.cs` | Mixed library, preview, parsing, and summary responsibilities | `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation` |
| 829 | `src/CanDoItAll.Modules.Projects/ProjectModels.cs` | Project aggregate + duplicated slug helper | `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation`, `14-schema-hygiene-migrations-and-long-file-split` |
| 500 | `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs` | Parsing, projection, summary building, file loading | `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation` |
| 492 | `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs` | Broad workspace state/orchestration | `13-workspace-and-canvas-decomposition` |
| 483 | `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs` | UI persistence orchestration concentration | `13-workspace-and-canvas-decomposition` |
| 477 | `src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs` | Surface composition logic concentration | `13-workspace-and-canvas-decomposition` |
| 466 | `src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs` | Canvas catalog concentration | `13-workspace-and-canvas-decomposition` |
| 464 | `src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs` | Entity definitions, config, and canonicality issues in one file | `02-canonical-dependency-model-and-compatibility-boundary`, `14-schema-hygiene-migrations-and-long-file-split` |
| 461 | `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs` | Destructive save logic and slug/version helpers combined | `05-transaction-concurrency-and-conflict-hardening`, `06-differential-definition-graph-persistence` |
| 438 | `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs` | Publish lifecycle and clone behavior coupled | `08-publication-versioning-and-clone-engine-decomposition` |
| 435 | `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs` | Runtime orchestration hotspot | `09-runtime-state-machine-and-transition-policy-extraction` |
| 433 | `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Links.cs` | Canvas dependency/link orchestration concentration | `13-workspace-and-canvas-decomposition` |
| 416 | `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs` | Canonicality, normalization, and helper logic concentrated | `02-canonical-dependency-model-and-compatibility-boundary`, `03-side-effect-free-validation-and-editor-normalization-split` |
| 400 | `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Actions.cs` | Canvas mutation orchestration concentration | `13-workspace-and-canvas-decomposition` |
| 393 | `src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs` | Query logic concentration and in-memory shaping | `10-read-side-query-splitting-and-performance-hardening` |
| 390 | `src/CanDoItAll.Modules.Processes/ProcessTemplatePackModels.cs` | Dense model definitions and pack semantics | `14-schema-hygiene-migrations-and-long-file-split` |

## Interpretation rule

A large file is not automatically bad. It becomes a hotspot when:
- it mixes multiple responsibilities,
- it hides canonicality drift,
- it blocks targeted testing,
- it becomes the default place to add the next feature.

This bundle uses that stricter definition.
