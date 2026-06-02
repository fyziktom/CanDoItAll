# SB08 UI and observability hardening for blockers and usage

## Status

Ready for implementation.  
Critical foundation: **No**

## Objective

Make strict contract blockers, tool-policy denials, unknown usage, estimated usage, and known actual cost understandable in the UI and diagnostics.

## Covered Inputs

R13.

## Prerequisites

SB01-SB05 data shapes stable.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`

## Deliverables

- Run-detail panels for contract blocked/missing/invalid state.
- Usage display that distinguishes known actual cost, estimated cost, unknown usage, missing usage, and zero cost.
- Tool deny reason display with operation requirement and target scope.
- Workflow executor status badges for side effect, idempotency, preview/commit, and unknown provider usage.
- Browser screenshots for desktop and mobile.

## Dependency Impact

This subbundle affects downstream proof and must be treated as a dependency exactly as modeled in `bundle://plan/01-phase-plan.md`. If this subbundle fails, all downstream subbundles that depend on its runtime behavior or proof contract must be reopened.

## Validation Depth

Critical subbundle validation requires semantic adequacy proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, changed-file hashes, and command/browser transcripts where applicable.

## Implementation Steps

1. Add or update typed DTOs/read models for contract and usage states.
2. Ensure UI never displays `0.000000 USD` as precise actual cost when usage is unknown.
3. Add run-detail diagnostics for policy deny reasons and proof-quality status.
4. Validate process live dashboard and workflow editor in desktop and mobile viewports.
5. Record screenshots and browser console evidence.

## Scope Exceptions

None planned. If implementation discovers a legacy compatibility exception, record it in this file and in `traceability/` before continuing.

## Do Not Do

Do not use color-only status indicators. Do not hide unknown usage in collapsed details only. Do not present estimates as actuals.

## Acceptance Checklist

- [ ] Source references were reopened before editing.
- [ ] Implementation is the smallest correct change set for this subbundle.
- [ ] Failing-first proof was captured for behavior-changing critical work.
- [ ] Passing proof was captured after implementation.
- [ ] Anti-stub audit was run.
- [ ] Raw notes owned by this subbundle were closed or explicitly blocked.
- [ ] Downstream dependency impact was reviewed before moving on.

## Proof Required

Playwright/browser screenshots, console logs, UI state fixture for known/unknown/blocked runs, accessibility/readability review.

## Browser Validation Logging

Required for `/processes/live`, process run detail, and workflow executor UI if affected.

## Progression Gate

SB09 must confirm UI messages match runtime state and do not mislead about cost.

## Suggested Agent Prompt

You are implementing `SB08 UI and observability hardening for blockers and usage` in `fyziktom/CanDoItAll` on branch `development`. Read this subbundle README, the root README, `plan/01-phase-plan.md`, `traceability/`, and all exact source references before editing. Implement only this subbundle. Do not close it without the required semantic proof, transcripts, changed-file hashes, anti-stub audit, and raw-note closure update.
