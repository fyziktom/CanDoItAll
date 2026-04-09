# 20 Implemented Architecture Hardening And Form Componentization

## Status

- `Completed`

## Objective

- Repair the executed process-management implementation so the module stops depending on oversized files, inline-only editors, and weak architecture-note placeholders before more UX complexity is added.

## Covered Inputs

- `REQ-003`
- `REQ-005`
- `REQ-007`
- `REQ-009`
- `REQ-014`
- `REQ-019`
- `REQ-021`
- Review `02-implementation-coverage-audit.md`

## Prerequisites

- `19-post-implementation-bundle-phase04-generation`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\02-architecture\IMPORTANT ADDITIONAL NOTES.md`
- `C:\repositories\CanDoItAll\process-management-bundle\reviews\02-implementation-coverage-audit.md`
- `C:\repositories\CanDoItAll\process-management-bundle\01-workbooks\01-process-management-execution-grade.xlsx`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`

## Deliverables

- Reusable process editor components instead of inline-only role, step, assignment, artifact, and governance forms.
- A smaller process workspace orchestration surface with extracted tab-specific components.
- A smaller process-service architecture with clearer collaborator boundaries for definition lifecycle, runtime orchestration, artifacts/journal, analytics, and exchange.
- Typed enterprise extension seams for the highest-risk architecture-note concerns that are currently only absent or represented as summary strings.
- Regression-safe proof that refactoring preserved current working behavior.

## Dependency Impact

- Canvas parity work in the next phase depends on reusable forms existing first.
- If this subbundle is skipped, the process canvas will either duplicate form logic or keep coupling new work to the current oversized files.

## Validation Depth

- `Architecture-critical`

## Implementation Steps

1. Split `ProcessWorkspace` into focused process-editor components that can be hosted both inline and inside canvas floating windows.
2. Extract process form components for definition/governance, roles, steps, assignment resolution, and artifact capture.
3. Reduce `ProcessesService` responsibility spread by introducing smaller internal collaborators or partial slices with explicit ownership boundaries.
4. Replace the highest-risk architecture-note blind spots with typed seams where the current code still relies on loose summary-only fields.
5. Keep existing routes, build behavior, seed flows, and current integration coverage green while refactoring.

## Scope Exceptions

- Full right-click, toolbox, selection-window, and double-click canvas parity belongs to phase 06 after the reusable forms exist.

## Do Not Do

- Do not introduce a second canonical process store.
- Do not copy existing inline editor markup into multiple new files without real reuse.
- Do not hide missing architecture-note concepts behind unstructured JSON blobs or prompt-only behavior.
- Do not keep `ProcessWorkspace` and `ProcessesService` effectively monolithic after claiming this subbundle is closed.

## Acceptance Checklist

- Process editor forms exist as reusable components under `src\CanDoItAll.Modules.Processes\Components`.
- `ProcessWorkspace.razor`, `ProcessWorkspace.razor.cs`, and `ProcessesService.cs` are materially smaller and clearer than the audited versions.
- The architecture-note gaps reopened by the audit have explicit typed seams, not only prose placeholders.
- Current routes, seed flows, and runtime behaviors still pass build and targeted regression validation.

## Proof Required

- Updated file-size evidence recorded in the execution report or repair bundle notes.
- Build proof for `CanDoItAll.Modules.Processes` and the web host.
- Targeted regression tests for process definition save/publish, runtime start/transition, and seed flows.
- Browser smoke on `/processes` and `/projects/{id}/processes` after component extraction.

## Browser Validation Logging

- Route:
  `/processes`
- Route:
  `/projects/{id}/processes`
- Viewport:
  `1920x1080`
- Evidence:
  screenshots proving the refactored forms still render compactly and without regressions

## Progression Gate

- Phase 06 may not start until reusable form components exist, the oversized files are reduced, and the refactor passes both regression tests and browser smoke.

## Suggested Agent Prompt

```text
Refactor only the executed process-management architecture slice. Split ProcessWorkspace and ProcessesService into smaller, clearer units, extract reusable process editor forms that can later live in canvas floating windows, add typed seams for the highest-risk architecture-note gaps, and keep all current behavior green before closing.
```

