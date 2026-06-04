# 04-validation-and-closure

## Status

- `Completed`

## Objective

- Prove the product changes compile, render, and close every raw note with durable evidence.

## Success Criteria

- Targeted module builds pass or any unrelated warnings are explicitly separated.
- Browser proof exists for both affected routes at desktop and narrow widths.
- Source assertions prove tabbed layouts and shared-component usage.
- Anti-stub audit finds no placeholder implementation.
- Raw notes are closed as solved or explicitly marked partial with a follow-up.
- Completed-stage validator passes.

## Covered Inputs

- `N001`
- `N002`
- `N003`
- `N004`
- `N005`

## Prerequisites

- `subbundles/02-02-process-step-form-tabs` completed or honestly blocked.
- `subbundles/03-03-workflow-editor-form-tabs` completed or honestly blocked.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepRoleAssignmentEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.css`
- `repo://codex/bundles/process-workflow-form-layout-tuning-v1/reviews/01-execution-report.md`

## Deliverables

- Build transcripts under `bundle://proof/SB04/transcripts/`.
- Browser screenshots under `bundle://proof/SB04/browser/`.
- Source assertion and anti-stub audit transcripts.
- Updated execution report, raw-note closure, root validation summary, and completed-stage validator transcript.

## Dependency Impact

- This is the final closure phase. Weak proof leaves the bundle open.

## Validation Depth

- End-to-end UI closure for a layout-only bundle.

## Implementation Steps

1. Run targeted module builds.
2. Capture source assertions for process and workflow tab layouts.
3. Run anti-stub audit against changed production UI files.
4. Open the app in a browser and capture desktop plus narrow screenshots for both routes.
5. Update browser analytics and subbundle gate rows.
6. Close every raw note with proof.
7. Run completed-stage bundle validator and repair failures.

## Scope Exceptions

- If browser proof is blocked by local database/runtime setup, record the exact blocker and do not mark UI notes fully solved without alternative proof.

## Do Not Do

- Do not close from generated images alone.
- Do not hide missing browser proof as residual risk.

## Acceptance Checklist

- [x] Process module build captured.
- [x] AgentFramework module build captured.
- [x] Browser screenshots captured.
- [x] Raw notes closed.
- [x] Completed-stage validator captured.

## Proof Required

- `bundle://proof/SB04/transcripts/processes-module-build.txt`
- `bundle://proof/SB04/transcripts/agentframework-module-build.txt`
- `bundle://proof/SB04/transcripts/source-assertions.txt`
- `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB04/transcripts/browser-proof.txt`
- `bundle://proof/SB04/transcripts/validate-bundle-completed.txt`

## Browser Validation Logging

- Routes: `/processes` and `/agents/workflows`.
- Viewports: `1600x900` and `390x844`.
- Actions and assertions are inherited from `SB02` and `SB03`.
- Screenshots are recorded under `bundle://proof/SB04/browser/`.

## Progression Gate

- Bundle may close only after the execution report, validators, builds, browser analytics, and raw-note closure all support the same conclusion.

## Suggested Agent Prompt

```text
Close the bundle honestly. Capture command transcripts and browser artifacts, update all report rows, run the completed-stage validator, and reopen implementation if any proof is weak.
```
