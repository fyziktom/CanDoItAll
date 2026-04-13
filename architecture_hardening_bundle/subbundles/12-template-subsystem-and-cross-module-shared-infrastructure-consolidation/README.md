# Template subsystem and cross-module shared-infrastructure consolidation

## Status

- `Completed`

## Objective

- Remove the real duplicated helper hotspots in template and adjacent modules without creating a generic dumping ground or blurring domain ownership.

## Covered Inputs

- `U002` Check duplication across modules.
- `BRQ-012` Cross-module duplication reduction.
- `BRQ-022` Shared extraction discipline.
- `F008` Cross-module duplication.

## Prerequisites

- `11-architecture-review-gate-c` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackLoader.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateProjectionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateCatalogService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateLibraryService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\PromptLibraryPackLoader.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessTemplateProjectionServiceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessTemplateCatalogServiceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessTemplatePackLoaderTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessTemplateMermaidExporterTests.cs

## Deliverables

- Consolidated shared helper(s) for genuinely generic duplicated behavior such as file/json reading, enum parsing defaults, or slug generation where justified.
- A single process-template-domain helper for role snapshot summary generation if the semantics are the same across services.
- Updated tests proving the new shared helpers preserve prior behavior.

## Dependency Impact

- Schema hygiene and workspace cleanup should not have to keep repairing duplicated helpers afterward.
- Gate D will inspect whether the consolidation respected ownership boundaries.

## Validation Depth

- `High`

## Implementation Steps

1. Separate genuinely shared generic helper candidates from process-template-domain helper candidates.
2. Extract only the helpers whose semantics are actually shared and stable.
3. Update consuming services so the duplication is removed from active use.
4. Add or update tests that prove the shared behavior matches the previous outputs.

## Scope Exceptions

- Do not force every repeated helper into a global shared layer if the semantics are not truly generic.
- Prompt-gallery page-level duplication may remain local if extracting it would increase coupling more than it helps.

## Do Not Do

- Do not create a giant `Utils` class.
- Do not move process-template-domain logic into a generic shared layer merely because it appears more than once.
- Do not consolidate by hiding divergent semantics behind one ambiguous helper.

## Acceptance Checklist

- The main duplicated helper hotspots are reduced in active code.
- Ownership of extracted helpers is clearer, not blurrier.
- Role snapshot summary building is defined in one place if the semantics truly match.
- Tests prove parity for the extracted helpers.

## Proof Required

- MCP/template tests proving consolidated behavior.
- Any additional unit tests for extracted helpers.
- Execution-report note describing why each helper was placed where it was.

## Browser Validation Logging

- N/A unless template UI behavior changed visibly.

## Progression Gate

- The main duplicated helper hotspots are genuinely reduced, and the extraction respected ownership instead of creating a new dumping ground.

## Suggested Agent Prompt

```text
Implement only subbundle 12. Consolidate the real duplicated helper hotspots across Processes and adjacent modules, but keep ownership discipline strict. Extract only what is truly shared, prove parity with tests, and stop before UI decomposition or schema cleanup.
```
