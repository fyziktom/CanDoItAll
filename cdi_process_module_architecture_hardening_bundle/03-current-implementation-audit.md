# Current implementation audit

## Scope of the audit

The audit focused on the new `Processes` module, but also inspected adjacent modules and bundle examples to evaluate:
- canonicality,
- architecture and maintainability,
- testability,
- duplicated infrastructure,
- DB conflict risk,
- long-file and monolith risk,
- query and performance shape.

## Primary code areas reviewed

### Processes module
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace*`
- `src/CanDoItAll.Modules.Processes/ProcessTemplate*`

### Adjacent modules and infrastructure
- `src/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/CanDoItAll.Modules.Factory/PromptLibraryPackLoader.cs`
- `src/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor`

### Tests
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessImportMetadataIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessDeletionIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/SqliteWriteCoordinationIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs`
- `tests/CanDoItAll.Mcp.Processes.Tests/*`

## Main conclusions

### 1. Dependency semantics are not fully canonical

`ProcessStepDefinition` stores both legacy dependency pointers and explicit dependency rows. Helper code repeatedly falls back between these shapes. That means dependency semantics are not governed by one source of truth.

### 2. Validation is not pure

`ValidateDefinitionEditor` currently triggers normalization, which means validation changes data as a side effect. That is a correctness and testability smell.

### 3. Save logic is destructive

The definition save path removes and recreates child collections instead of applying a diff. Stable child identity does not survive ordinary edits.

### 4. Critical mutation flows are under-protected

There are no observed optimistic concurrency tokens in the process aggregate roots or runtime aggregates. SQLite coordination is helpful for write contention, but it is not a substitute for aggregate-level lost-update protection.

### 5. Publication and versioning are coupled and race-prone

Publish contains both state progression and clone logic. Slug uniqueness and next-version selection are based on pre-check patterns that can lose races.

### 6. Runtime logic is too concentrated

`TransitionStepAsync` is doing too much orchestration in one method. That reduces unit-testability and makes future policy changes risky.

### 7. Query logic is too broad

Listing and analytics still lean on broad loads and in-memory aggregation patterns. That is acceptable for small data, but it will not scale gracefully.

### 8. Template and helper duplication is real

JSON loading, enum parsing, role snapshot-summary construction, and slug-building logic exist in multiple places. These are now maintenance hotspots.

### 9. The workspace is already larger than it should be

`ProcessWorkspace` and some supporting canvas files are large enough that further feature growth without decomposition would likely create a harder-to-manage UI core.

## Audit limitation

This was a static review only. Build, runtime, migration generation, and browser proof were not executed in this environment, so every execution gate in this bundle still requires target-machine proof.
