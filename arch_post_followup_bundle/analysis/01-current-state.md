# Current-state analysis

## Summary

The current Process module is materially better than in earlier rounds, but the architecture is still not fully closed.

## Strong improvements that are already real

### Canonical dependency core is much healthier now
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs:1140-1148` proves that core step entity/editor/runtime types no longer expose the old dependency mirror properties.
- `src/CanDoItAll.Modules.Processes/ProcessDependencyCompatibilityBridge.cs:5-75` and `:259-287` show that old single-dependency fallback now lives at the import/export boundary rather than inside the core step types.

### Schema and lifecycle hardening is real
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs:23-35` adds `NextVersionNumber`, `ConcurrencyToken`, and a same-definition FK for `ActivePublishedVersionId`.
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs:56-64` enforces one draft and one published version per definition plus version-number uniqueness.
- `tests/CanDoItAll.Tests.Integration/ProcessSchemaIntegrationTests.cs:160-242` covers key lifecycle/schema cases.

### Durable outbox is real
- `src/CanDoItAll.Modules.Processes/ProcessOutbox.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs:16-60`

## Still-open issues

### Graph legality
- `ProcessesService.Support.cs:11-72` validates references but not cycles.
- `ProcessesService.Runtime.cs:124-130` still seeds the first step when no roots exist.
- `ProcessCanvasRecompositionService.cs:401-403` still appends unresolved nodes after the topological pass.

### Runtime singularity
- `ProcessRuntimeEntityConfigurations.cs:55-58` has no unique `(ProcessRunId, StepDefinitionId)` constraint.
- `ProcessRuntimeEntityConfigurations.cs:85` has only a non-unique runtime-assignment index.
- `ProcessesService.Runtime.cs:243-244` and `Runtime.Operations.cs:20-25` assume singular rows anyway.

### Workspace quiescence
- `ProcessWorkspace.DefinitionCrud.cs:38-52` already drains pending canvas persistence for save.
- `ProcessWorkspace.DefinitionCrud.cs:55-127` still does not do the same for publish/delete/export.
- `ProcessWorkspace.Canvas.Persistence.cs:5-54` shows the pending autosave really writes the whole editor through `ProcessesService.SaveAsync(editor)`.

### Published-only concurrency hole
- `ProcessesService.cs:47-63` returns an editor without `DefinitionConcurrencyToken` when no working version exists.
- `ProcessesService.Persistence.cs:48-49` only rejects stale save when the expected token exists.
- `ProcessesService.Support.cs:79-80` makes the token optional in practice.

### Query cohesion
- `ProcessWorkspaceRunDetailsLoader.cs:5-20` still does many sequential service calls.
- `ProcessWorkspace.razor.cs:176-232` still stitches the workspace from many sequential operations.

### Template helper isolation
- `ProcessTemplateCatalogService.cs:110-154`
- `ProcessTemplateLibraryService.cs:63-112`
- `ProcessTemplateProjectionService.cs:73-101`

These still repeat role/artifact mapping logic in slightly different shapes.

## Risk posture

The remaining risks are no longer “core model dual representation” or “no durable side effects”. The remaining risks are narrower but still meaningful enough that another clean closure claim would be premature.
