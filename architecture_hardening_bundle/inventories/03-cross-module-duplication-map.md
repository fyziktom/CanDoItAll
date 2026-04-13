# Cross-module duplication map

| Duplicate concern | Current locations | Risk | Recommended owner | Owning subbundle |
| --- | --- | --- | --- | --- |
| Slug generation | `ProcessesService.Persistence.cs`, `ProjectModels.cs` | Inconsistent slug rules and repeated fixes | Shared text/slug helper or dedicated service in a neutral layer | `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation` |
| JSON file reader for pack-style files | `ProcessTemplatePackLoader.cs`, `ProcessTemplateProjectionService.cs`, `PromptLibraryPackLoader.cs`, optionally `PromptGalleryPage.razor` | Repeated file read + deserialize + error handling logic | Shared file/json helper in infrastructure or module-internal pack helper depending on semantics | `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation` |
| Enum text parsing with defaults | `ProcessTemplateProjectionService.cs`, `ProcessTemplateCatalogService.cs`, `ProcessTemplateLibraryService.cs`, `ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs` | Drift in defaults or parsing rules | Shared parser helper with explicit default behavior | `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation` |
| Role snapshot summary builder | `ProcessTemplateProjectionService.cs`, `ProcessTemplateCatalogService.cs`, `ProcessTemplateLibraryService.cs` | Divergent summary strings and review burden | Process-template-domain helper, not a global generic utility | `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation` |
| Dependency reconstruction fallback | `ProcessCanvasBranching.cs`, `ProcessesService.Support.cs`, `ProcessesService.Reads.cs`, `ProcessesService.Runtime.cs` | Canonicality drift across behaviors | Single compatibility adapter plus canonical reader | `02-canonical-dependency-model-and-compatibility-boundary` |
| Manual summary/count aggregation | `ProcessesService.cs`, `ProcessesService.Reads.cs` | Repeated broad-load patterns | Query services with shaped projections | `10-read-side-query-splitting-and-performance-hardening` |

## Anti-pattern warning

Do not solve duplication by creating:
- a giant `CommonHelpers` class,
- a generic `ProcessUtils` dumping ground,
- a shared helper that knows too much about multiple module domains.

The extraction location must match the semantic ownership of the duplicated code.
