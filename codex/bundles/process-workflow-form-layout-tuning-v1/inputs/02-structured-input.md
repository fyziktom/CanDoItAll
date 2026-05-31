# Structured Input

## Core Objective

- Improve the layout of Processes Steps setup forms and Workflows Editor forms by organizing long forms into tabs and compact field groups.

## Success Criteria

- Process step details are split into tabs instead of one long vertical form.
- Process role assignment, branch outcome, and artifact expectation editors are compact and scan well inside their parent tabs.
- Workflow editor inspector separates definition, node, executor, routes, and preview/validation forms.
- Existing save, publish, preview, validation, node, edge, executor, and process definition behavior remains intact.
- Browser screenshots show readable desktop and narrow layouts without incoherent overlap or avoidable horizontal overflow.

## Hard Constraints

- Use existing shared components and existing project CSS.
- Do not add special styling or a new visual language.
- Keep model bindings, event handlers, and test IDs stable unless a rename is required for the new structure.
- Avoid runtime, persistence, service, and domain model changes.

## Allowed Side Effects

- Razor layout changes in the affected process and workflow components.
- Small component-local state for selected inner tabs.
- Minimal component CSS only when existing layout classes cannot express the structure.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `bundle://inputs/01-source-artifacts.md`
- `bundle://evidence/imagegen-proposals/README.md`

## Input Coverage Signals

- `N001` and `N003` specifically target the Processes Steps long form.
- `N002` requires proposal artifacts before implementation direction is considered settled.
- `N004` requires the same layout correction in the Workflows Editor.
- `N005` constrains the fix to layout, spacing, and component use rather than special styling.

## Dependency And Sequencing Signals

- The proposal/inventory phase must finish before implementation so the layout groups are intentional.
- Processes and Workflows can be implemented independently after proposals exist.
- Final validation depends on both UI changes and cannot close without browser proof.

## Validation Expectations

- Prepared-stage bundle validation before product edits.
- Targeted builds after product edits.
- Browser proof for `/processes` and `/agents/workflows`.
- Source assertions proving tabs and shared components are used.
- Anti-stub audit proving no placeholder UI or `TODO`/`NotImplemented` production paths were introduced.

## Evidence Contract

- `bundle://evidence/imagegen-proposals/*.png`
- `bundle://proof/SB04/transcripts/processes-module-build.txt`
- `bundle://proof/SB04/transcripts/agentframework-module-build.txt`
- `bundle://proof/SB04/transcripts/source-assertions.txt`
- `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB04/transcripts/browser-proof.txt`
- `bundle://proof/SB04/browser/processes-steps-desktop.png`
- `bundle://proof/SB04/browser/processes-steps-narrow.png`
- `bundle://proof/SB04/browser/workflows-editor-desktop.png`
- `bundle://proof/SB04/browser/workflows-editor-narrow.png`

## UI Validation Strategy

- Open a large desktop viewport first, inspect field grouping, tab affordances, and spacing.
- Validate a narrow viewport after desktop is acceptable.
- Check that tab panels do not trap unrelated forms in one long stack.
- Check that repeated cards remain readable and that action buttons are still reachable.

## Browser Validation Analytics

- Each UI subbundle records route, viewport, actions, assertions, screenshot path, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- "Processes page in Steps tab" maps to `ProcessWorkspaceStepsTab.razor` and the child setup editor components.
- "Workflows page in Editor" maps to `/agents/workflows` and `WorkflowCanvasEditor.razor`.
- Layout-only changes are sufficient unless browser proof reveals a hidden behavior regression.

## Primary Risks

- Existing local data may not contain enough process/workflow records for full visual proof without seeding.
- The Workflows editor is large; careless refactoring could break node/executor/edge event handlers.
- Adding many tabs can hide required fields if the active-tab state is not simple and predictable.
