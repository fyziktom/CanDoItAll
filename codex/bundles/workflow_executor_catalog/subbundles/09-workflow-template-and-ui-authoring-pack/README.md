# 09-workflow-template-and-ui-authoring-pack

## Status

- Status: `Completed`

## Closure Notes

- Added the workflow executor catalog template pack with five workflow examples covering folder summary, diff report, HTTP/document ingestion, JSON task creation, and approval-gated HTTP action.
- Seeded supporting sample files and bumped the workflow seed version to `2026-05-workflow-executor-catalog-v2`.
- Updated workflow authoring UI to show executor availability, approval, deterministic preview, planned status, and template pack cards.
- Browser proof: `bundle://proof/SB09/browser/`
- Proof manifest: `bundle://proof/SB09/manifest.md`
- Semantic invariants: `bundle://proof/SB09/semantic-invariants.md`

## Objective

Expose the new executor capabilities to users through templates and workflow authoring UI without overwriting user-managed definitions.

## Covered Inputs

- RN02: Expanded executors and helper nodes must be visible as useful workflow building blocks.
- RN03: Local folder/file workflows must be practical from examples.
- RN04: Improve workflow authoring UX and template coverage.
- R10: Add workflow templates and UI catalog entries demonstrating local folder/file workflows.
- R11: Scenario harness must cover the new authoring pack.

## Prerequisites

- SB01 through SB08 closure gates passed.
- Descriptor catalog accurately reflects implemented versus planned executors.
- Managed seed behavior is understood before editing templates.

## Exact Source References

- `repo://Templates/Workflows/manifest.yaml`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowExecutorCanvasCatalog.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

## Scope

- Add templates for local folder summary to Markdown, file diff report, HTTP download and document extraction, JSON transform to project task creation, and approval-gated external action where backing executors exist.
- Update template seed version without overwriting user-managed definitions.
- Update workflow canvas executor catalog grouping, availability, approval requirement, and deterministic preview indicators.
- Add component tests for template visibility, catalog availability, and settings cards.
- Add browser proof for real authoring UI changes.

## Dependency Impact

- SB10 final scenario harness depends on templates and UI reflecting actual implemented capabilities.
- Weak UI labeling can make planned/unavailable executors look runnable.

## Validation Depth

- Component tests for workflow page/catalog/template visibility.
- Managed seed tests proving user-managed definitions are not overwritten.
- Browser proof on `agents/workflows` for changed catalog/template UI if rendered behavior changes.

## Implementation Steps

1. Update template manifest with examples backed by completed executors only.
2. Bump seed metadata according to existing managed seed conventions.
3. Update executor catalog UI using existing components and styles.
4. Add component tests and, if needed, browser proof with screenshots.
5. Record analytics row in the execution report.

## Do Not Do

- Do not seed examples that require unimplemented executors.
- Do not overwrite user-managed workflow definitions.
- Do not introduce a new UI component pattern when existing workflow canvas components fit.
- Do not make planned/unavailable executors appear runnable.

## Acceptance Checklist

- Users can create practical folder/file workflows from examples.
- Planned and unavailable executors are clearly marked and cannot be mistaken as runnable.
- Template seed updates preserve user-managed definitions.
- UI catalog grouping and labels match the implemented executor catalog.

## Proof Required

- Passing component/template test transcript.
- Browser validation artifact for changed authoring UI, including screenshot review.
- Changed-file hashes, source assertions, and anti-stub audit.
- Execution report Browser Validation Analytics row for SB09.

## Browser Validation Logging

- Required for workflow authoring UI changes. Record route `agents/workflows`, desktop and relevant narrow viewport, Playwright actions/assertions, screenshots, and pass/fail result.

## Progression Gate

- Continue to SB10 only after templates and authoring UI expose only real runnable capabilities and browser/component proof is recorded.

## Suggested Agent Prompt

Use SB09 to connect the implemented executor surface to user-facing templates and authoring UI. Respect managed seed ownership, use existing components, and prove the route in a browser if UI changes.
