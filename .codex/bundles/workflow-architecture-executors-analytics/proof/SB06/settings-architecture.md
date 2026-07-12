# SB06 Executor Settings Architecture Proof

## Responsibility change

| Before | After |
|---|---|
| `WorkflowCanvasEditor` contained shadowed settings markup and mutation helpers for five built-in executor IDs. | The editor orchestrates descriptor selection, policy fields, and one `SettingsRendererHost` call for both inspector and modal. |
| Quick-create and edit paths could diverge in field handling. | `WorkflowExecutorConfigurationMapper` owns schema state read/write for both paths. |
| Image generation required editor-local provider branches and received generic GUID editing in the modal. | `WorkflowImageGenerationSettingsRenderer` is a cohesive trusted renderer used identically by both surfaces. |
| Custom renderer intent was metadata without a production contribution. | `WorkflowSettingsRendererSource` contributes the exact allow-listed built-in renderer contract through DI. |

## Boundary and pattern result

- Pattern: registry-backed Strategy for optional trusted renderers, with declarative schema rendering as the safe default.
- Dependency direction: the AgentFramework UI module depends on workflow descriptor/contracts and Workspace settings-renderer contracts; no runtime/plugin abstraction references Blazor.
- Composition: one `ISettingsRendererSource` registration; no service locator and no new project reference.
- Trust: key, owner, trust level, and schema version must all match before dynamic activation.
- Claim mode: `WorkflowExecutorSettingsPresentationMode.Schema` is the compatibility default; only `CustomRenderer` descriptors send a renderer claim to the host.
- Plugin validation: a custom-renderer executor must have a matching bundled/trusted renderer declaration plus the SettingsRenderer capability, and runtime projection rejects presentation-mode drift.
- Testability: the source and renderer instantiate directly under bUnit with a typed `IWorkflowComponentLibraryService` fake.

## Negative proof

- No executor-ID settings branches remain for StorageFile, HttpFetch, Spreadsheet, ProjectStructure, or ImageGeneration.
- No legacy generic `ReadExecutorSettings`/`UpdateExecutorSettings`/HTTP/spreadsheet mutation helper remains in the editor.
- Chat providers are absent from the image picker.
- Disabled image providers render as disabled options.
- A saved provider is not labeled unavailable while provider capabilities are still loading.
- Provider-load failures use fixed UI text and logs exclude exception/message payloads.
- The new renderer uses existing BaseLib layout/status/form wrappers and adds no page CSS or small/medium responsive work.
- Missing, incomplete, unregistered, trust-mismatched, owner-mismatched, and schema-version-mismatched renderer claims fail visibly; explicit schema mode alone receives the declarative fallback.
- Legacy plugin executor JSON defaults the new presentation mode to `Schema`.
- A `CustomRenderer` descriptor reaches the host even when its declarative schema has zero fields, so renderer activation and visible failure cannot be bypassed by an empty schema.
