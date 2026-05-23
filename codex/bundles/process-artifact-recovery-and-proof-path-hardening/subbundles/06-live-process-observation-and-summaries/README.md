# SB06: Live Process Observation And Summaries

## Status

- Status: `Completed`
- Critical foundation: `Yes`

## Scope

- Run the process as a user would.
- Handle approvals and escalations.
- Keep large process data reviewable with summaries and evidence indices.

## Objective

Execute the process while recording enough compact evidence to understand progress and failures without loading all raw process records into context.

## Covered Inputs

- Follow-up request `03-live-blazor-delivery-request`
- `R012`

## Prerequisites

- SB05 seed and process link complete.
- Runtime host stable.

## Exact Source References

- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`

## Dependency Impact

- Live process records and proof files.
- No generated app edits by Codex.

## Validation Depth

- Launch-plan transcript.
- Run detail snapshots by phase.
- Approval/escalation decision transcript.
- Compact phase summaries and evidence index.

## Contract

- Launch process through API.
- Observe launch plans, assignments, escalations, approvals, step transitions, artifacts, and direct messages.
- Approve or reject escalations as user through API.
- Record UX observations through manager directives, process artifacts, or project-structure notes.
- After meaningful phases, create compact summaries and an evidence index so raw data can be inspected selectively.

## Implementation Steps

- Launch and observe the run through APIs.
- Approve/reject launch and operator approvals through APIs.
- Record UX observations through manager directives or process artifacts.
- Save compact summaries and evidence index under proof.
- Inspect raw records only when a summary points to a concrete issue.

## Do Not Do

- Do not silently auto-approve unknown escalations.
- Do not help agents build the app.
- Do not rely on cognitive memory summaries.

## Acceptance Checklist

- [x] Proof contains run ids, API transcripts, approvals/escalation decisions, and phase summaries.
- [x] UX observations are recorded in system data, not only in Codex notes.
- [x] Raw run records are referenced only where needed to explain a specific failure or proof point.

## Proof Required

- `bundle://proof/SB06/manifest.md`
- `bundle://proof/SB06/summaries/**`
- `bundle://proof/SB06/transcripts/live-run-observation.txt`

## Browser Validation Logging

- Browser validation is expected to be produced by process agents and independently checked in SB07.

## Progression Gate

- SB06 passes when the run completes or blocks with enough evidence to classify the reason.

## Suggested Agent Prompt

Use `bundle://shared-prompts/qa-prompt.md`.
