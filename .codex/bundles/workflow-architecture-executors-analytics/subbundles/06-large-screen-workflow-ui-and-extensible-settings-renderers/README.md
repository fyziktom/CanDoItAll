# Large-Screen Workflow UI And Extensible Settings Renderers

## Status

- `Completed`

## Objective

- Make executor creation/editing schema-driven, trusted-renderer extensible, and analytics-complete on the existing large-screen workflow page.

## Success Criteria

- Every runnable standard/plugin contribution appears and can create/edit/save/reload without executor-ID settings switches.
- Descriptor renderer mode/key is preserved; trusted registration, schema version, and component contract are enforced.
- Missing claimed renderer and invalid settings JSON produce visible actionable diagnostics without overwriting raw settings.
- Workflow analytics panel shows complete duration, provider/model tokens, known cost, and unknown usage from SB05.
- UI uses existing BaseLib/CanvasLib and is validated only at large desktop size.

## Covered Inputs

- WF-UI-01 through WF-UI-03 and WF-AN-02.
- Plugin custom settings scheme, all-new-executor availability, and large-screen-only notes.

## Prerequisites

- SB03 runnable contribution set and SB05 analytics query/API gates pass.
- Retry components MCP before UI structure changes; if still unavailable, record exact failure and inspect existing component usage.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowExecutorCanvasCatalog.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/Connectors/SettingsRendererRegistry.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/SettingsRendererHost.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ConfigurationSchemaFallbackRenderer.razor`
- `repo://tests/Components/CanDoItAll.Tests.Components/WorkflowExecutorCanvasCatalogTests.cs`

## Deliverables

- `WorkflowExecutorNodeEditor` (or equivalently cohesive component) reused by inspector/dialog.
- Schema-to-canvas action factory and one result-returning configuration codec that preserves invalid raw JSON.
- Typed configuration option-source registry for secrets/providers/plugin connections.
- Explicit schema/application-renderer presentation mode and trusted renderer source registrations.
- Manifest/host validation for dangling key, trust, version, and required component parameters.
- Isolated workflow analytics panel/query consumption using BaseLib metrics/chart/table components.
- Component and maximized browser coverage for new built-in and plugin executors plus analytics.

## Dependency Impact

- SB07 depends on stable DOM/test IDs, component coverage, and screenshots.
- Plugin authors depend on explicit renderer/schema rules rather than hidden editor switches.

## Validation Depth

- `Critical UI foundation` with unit codec tests, component rendering, maximized browser interaction, console/network review, and screenshot inspection.

## C# Architecture Impact

- Extracts executor editing and analytics presentation from two page hotspots and activates a safe plugin UI extension boundary.

## Boundary Ownership

- Node editor owns UI orchestration; codec/action factory own schema mapping; option sources own dynamic choices; registry owns trusted renderer resolution; analytics panel owns presentation only.

## Dependency Direction

- UI consumes executor/analytics/settings contracts. Runtime/plugin abstractions do not reference Blazor. Trusted composition maps keys to concrete components.

## Pattern Decision

- Use PSR-05 Strategy Registry plus schema adapter. Reject manifest type activation and executor-ID switch branches.

## Testability Contract

- Pure codec/action tests cover every field/default and invalid JSON.
- Component tests inject descriptors/renderers/options/analytics and prove create/edit/save diagnostics.
- Browser test uses production DI/catalog and persisted reload.

## Partial Class Policy

- Do not add behavior to `WorkflowCanvasEditor.razor.cs` or `WorkflowsPage.razor.cs`. Extract cohesive components/services and reduce dead branches.

## Architecture Proof Required

- No executor-ID create/settings branch audit, renderer trust/version/key negative tests, real plugin schema round trip, and component ownership diff.

## Implementation Steps

1. Retry component-library discovery and record result.
2. Add failing codec/catalog/renderer/component tests for current hard-coded and inert behavior.
3. Extract schema codec/action factory and replace quick-create ID branches.
4. Extract/reuse executor node editor and remove unreachable specialized branches.
5. Implement trusted renderer/option-source registrations and explicit diagnostics.
6. Extract analytics panel and bind SB05 query/API projections.
7. Run component tests and maximized browser create/edit/save/reload/analytics flow; inspect screenshots.

## Scope Exceptions

- No small/medium/responsive CSS, viewport, touch, or layout pass. This explicit user constraint overrides generic narrower-width validation guidance.

## Do Not Do

- Do not introduce Radzen; it is absent in scoped projects.
- Do not add a new parallel component library or raw structural wrappers when BaseLib/CanvasLib components fit.
- Do not silently default invalid settings or silently ignore a claimed renderer.

## Acceptance Checklist

- New built-in/plugin descriptors need no editor ID switch.
- Custom renderer works with empty schema when explicitly selected and trusted.
- Untrusted/dangling/incompatible renderers fail visibly.
- Invalid JSON is preserved.
- Analytics values match API/query fixtures.
- 1600×1000 browser flow and screenshots pass.

## Proof Required

- Failing-first hard-coded-create/renderer/invalid-JSON/component transcripts.
- Passing unit/component transcript and production catalog browser assertions.
- Maximized screenshots for executor create/edit/reload and analytics provider/model/cost/duration views.
- Console/network error review and anti-stub `rg` audit for executor-ID branches/type activation.
- `bundle://proof/SB06/manifest.md` and `bundle://proof/SB06/semantic-invariants.md` during execution.

## Browser Validation Logging

- Route: `/agents/workflows` (non-artifact local context).
- Viewport: maximized 1600×1000 large-screen only.
- Actions: create document-to-Markdown, image/plugin nodes; inspect/edit settings; save/reload; run fixture; open Analytics.
- Assertions: catalog presence, correct fields/options, persisted values, renderer diagnostic behavior, provider/model/token/cost/duration/unknown values.
- Screenshots: `repo://workflow-executors-markdown.png`, `repo://workflow-custom-image-settings.png`, `repo://workflow-plugin-gmail-settings-fixed.png`, and `repo://workflow-analytics-desktop.png`.
- Review: no blocking clipping or overlap; the creation dialog is above floating canvas windows; custom image settings are immediately reachable in Node setup; analytics hierarchy and values are readable; current-session console errors are zero. See `bundle://proof/SB06/browser-validation.md`.

## Progression Gate

- Passed. Codec/component tests, trusted renderer negatives, real Gmail plugin schema rendering, analytics presentation, the 1600x1000 production browser flow, screenshot review, and zero current-session console errors are recorded in `bundle://proof/SB06/manifest.md` and `bundle://proof/SB06/browser-validation.md`.

## Suggested Agent Prompt

```text
Implement SB06 only. Retry component discovery, extract schema-driven executor editing and typed analytics presentation, enforce trusted renderer contracts, reuse BaseLib/CanvasLib, and prove the maximized desktop workflow without adding responsive scope.
```
