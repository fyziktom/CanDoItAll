# SB06 Process dispatch heuristic refactor

## Status

Ready for implementation.  
Critical foundation: **Yes**

## Objective

Reduce heuristic fragility in process dispatch and governed rule handling before adding more process families.

## Covered Inputs

R11; source evidence P1-03.

## Prerequisites

SB01-SB05 completed. Behavior gates must exist before splitting services.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`

## Deliverables

- `IRequiredToolResolver` for typed step/tool requirements.
- `IBrowserProofRequirementResolver` with explicit proof flags and target runtime identity.
- `IArtifactRequirementMatcher` for expected artifact matching and lineage checks.
- `IStepCompletionPolicy` for branch/completion decisions.
- `IDispatchDecisionEngine` that returns a typed decision record and diagnostics.
- Golden tests for common software-delivery, review, validation, cleanup, and project-structure writeback steps.

## Dependency Impact

This subbundle affects downstream proof and must be treated as a dependency exactly as modeled in `bundle://plan/01-phase-plan.md`. If this subbundle fails, all downstream subbundles that depend on its runtime behavior or proof contract must be reopened.

## Validation Depth

Critical subbundle validation requires semantic adequacy proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, changed-file hashes, and command/browser transcripts where applicable.

## Implementation Steps

1. Freeze current behavior with characterization tests before refactoring.
2. Extract one service at a time; preserve behavior under tests.
3. Move text/regex signals behind named predicates and typed contract flags.
4. Add false-positive and false-negative tests for browser proof, screenshot storage, cleanup, JavaScript-only context, .NET validation, project-structure writeback, and durable artifact writes.
5. Update telemetry/logging to emit the typed dispatch decision and its evidence.

## Scope Exceptions

None planned. If implementation discovers a legacy compatibility exception, record it in this file and in `traceability/` before continuing.

## Do Not Do

Do not refactor before SB04/SB05 proves the production path. Do not replace heuristics with larger untested heuristics. Do not hardcode the five scenario names.

## Acceptance Checklist

- [ ] Source references were reopened before editing.
- [ ] Implementation is the smallest correct change set for this subbundle.
- [ ] Failing-first proof was captured for behavior-changing critical work.
- [ ] Passing proof was captured after implementation.
- [ ] Anti-stub audit was run.
- [ ] Raw notes owned by this subbundle were closed or explicitly blocked.
- [ ] Downstream dependency impact was reviewed before moving on.

## Proof Required

Characterization test before extraction, passing tests after extraction, complexity/size inventory, no scenario-key scan, one downstream E2E smoke after refactor.

## Browser Validation Logging

Use SB04/SB08 downstream browser proof after dispatch refactor; no separate browser UI required unless UI changes occur.

## Progression Gate

SB09 cannot start until a downstream E2E smoke passes after this refactor.

## Suggested Agent Prompt

You are implementing `SB06 Process dispatch heuristic refactor` in `fyziktom/CanDoItAll` on branch `development`. Read this subbundle README, the root README, `plan/01-phase-plan.md`, `traceability/`, and all exact source references before editing. Implement only this subbundle. Do not close it without the required semantic proof, transcripts, changed-file hashes, anti-stub audit, and raw-note closure update.
