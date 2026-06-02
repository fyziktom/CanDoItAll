# SB04 Real agent-driven multi-domain process E2E harness

## Status

Ready for implementation.  
Critical foundation: **Yes**

## Objective

Replace the old SB08 proof gap with real process automation proof for five domain-distinct app-generation scenarios.

## Covered Inputs

R08, R09; source evidence E09, E10, E11, E13.

## Prerequisites

SB01, SB02, and SB03 gates passed. A configured provider is available for real runs or this subbundle must remain blocked.

## Exact Source References

- `repo://codex/bundles/process-workflow-agent-hardening-v1/scripts/run_sb08_multidomain_e2e.ps1`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`

## Deliverables

- New E2E harness that seeds request packets but does not write app source code itself.
- Five scenario packets: Tetris Mini Game, Expense Tracker Lite, Plant Watering Planner, Study Kanban Flashcards, Recipe Pantry Planner.
- Automation dispatch active; no `suppressAutomationDispatch = true` in production proof transitions.
- Non-empty agent execution runs, tool receipts, usage observations, artifacts, and current-run lineage for each scenario.
- Browser validation against the generated app artifact for desktop and mobile.
- Genericity audit proving no scenario key branches in production code/templates/skills.

## Dependency Impact

This subbundle affects downstream proof and must be treated as a dependency exactly as modeled in `bundle://plan/01-phase-plan.md`. If this subbundle fails, all downstream subbundles that depend on its runtime behavior or proof contract must be reopened.

## Validation Depth

Critical subbundle validation requires semantic adequacy proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, changed-file hashes, and command/browser transcripts where applicable.

## Implementation Steps

1. Fork the old SB08 script into a new proof harness and remove all app-source generator functions from the production proof path.
2. Use API calls only to create project, upload request packet, start process run, poll automation, and approve pending tool requests when required.
3. Wait for terminal run state with timeouts and explicit failure reasons.
4. Resolve generated app root from process artifacts/tool receipts, not from harness-created paths.
5. Run build/browser proof on the generated app.
6. Write `agent-execution-runs.json` from actual execution-run API detail and fail if empty.
7. Write `usage-summary.json` from provider usage observations and fail or block if real provider usage is unavailable.
8. Keep the old harness only as `browser-fixture-regression`, not as process E2E closure proof.

## Scope Exceptions

None planned. If implementation discovers a legacy compatibility exception, record it in this file and in `traceability/` before continuing.

## Do Not Do

Do not generate scenario app code inside the E2E proof script. Do not manually complete process steps. Do not use `suppressAutomationDispatch=true` for claimed production E2E proof. Do not close this subbundle with a mock provider only.

## Acceptance Checklist

- [ ] Source references were reopened before editing.
- [ ] Implementation is the smallest correct change set for this subbundle.
- [ ] Failing-first proof was captured for behavior-changing critical work.
- [ ] Passing proof was captured after implementation.
- [ ] Anti-stub audit was run.
- [ ] Raw notes owned by this subbundle were closed or explicitly blocked.
- [ ] Downstream dependency impact was reviewed before moving on.

## Proof Required

Five current-run proof folders with process run detail, non-empty execution runs, tool receipts, usage observations, generated source root, build transcript, browser screenshots, console logs, cleanup receipt, and genericity audit.

## Browser Validation Logging

Required. Desktop and mobile screenshots for all five generated apps, plus console/network evidence where available.

## Progression Gate

SB05 proof-quality checker must reject the old SB08 proof and pass the new proof before this can be considered closed.

## Suggested Agent Prompt

You are implementing `SB04 Real agent-driven multi-domain process E2E harness` in `fyziktom/CanDoItAll` on branch `development`. Read this subbundle README, the root README, `plan/01-phase-plan.md`, `traceability/`, and all exact source references before editing. Implement only this subbundle. Do not close it without the required semantic proof, transcripts, changed-file hashes, anti-stub audit, and raw-note closure update.
