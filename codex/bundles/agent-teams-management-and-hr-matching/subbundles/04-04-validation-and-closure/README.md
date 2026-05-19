# 04-validation-and-closure

## Status

- `Completed`

## Objective

- Verify the full bundle, capture browser proof, close raw notes one by one, and synchronize bundle status with implementation evidence.

## Covered Inputs

- `N001` through `N011` final closure.

## Prerequisites

- Subbundle 01 closure gate has passed.
- Subbundle 02 closure gate has passed or has an honest blocker.
- Subbundle 03 closure gate has passed or has an honest blocker.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\agent-teams-management-and-hr-matching\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\codex\bundles\agent-teams-management-and-hr-matching\traceability\01-requirement-traceability.md
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessLaunchPlanningIntegrationTests.cs

## Deliverables

- Targeted test results recorded.
- Browser validation analytics populated.
- Raw note closure table updated with Solved, Partially solved, or Not solved.
- Root README and subbundle statuses synchronized.
- Completed-stage bundle validator run recorded.

## Dependency Impact

- This phase is the final evidence gate. Weak closure leaves the user without confidence that the broad workflow was actually solved.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run targeted tests for AgentFramework teams, Agents UI, and process launch matching.
2. Run build or broader test command appropriate to changed projects.
3. Start or reuse local app host for browser validation.
4. Capture `/agents?tab=agents` tree and membership modal screenshots.
5. Capture process launch HR matching selected-team marker screenshot.
6. Update execution report, raw-note closure, subbundle statuses, and root validation summary.
7. Run prepared and completed validators.

## Scope Exceptions

- If browser proof is blocked by local host setup, record the blocker and keep closure partial.

## Do Not Do

- Do not mark raw notes solved without matching code and proof.
- Do not leave executed subbundles as `Ready` or `In progress`.
- Do not treat missing screenshots as harmless when UI changed.

## Acceptance Checklist

- Tests support domain, UI, and process matching behavior.
- Browser proof exists for both UI surfaces.
- Raw-note closure is populated.
- Final validators pass or a concrete blocker is documented.

## Proof Required

- Test command output.
- Browser screenshots and action notes.
- Bundle validator output for prepared and completed stages.

## Browser Validation Logging

- Routes: `/agents?tab=agents` and `/processes` or relevant project process route.
- Viewports: desktop large and narrower pass for impacted layouts.
- Actions: verify tree filtering, membership modal, HR matching team selector, and out-of-team candidate marker.
- Screenshots: all evidence paths listed in `reviews/01-execution-report.md`.
- Review questions: no clipping, no overlap, readable statuses, stable layout, expected data visible.

## Progression Gate

- Completed. Raw notes are audited, targeted tests and build passed, prepared validator passed, and browser proof was captured for the Agents team management UI. Process browser proof is documented as blocked by local SQLite host contention and is covered by integration/build evidence.

## Suggested Agent Prompt

```text
Run final validation and closure only. Verify tests, capture browser proof, audit every raw note, update all bundle status files, run validators, and leave unresolved items as explicit blockers or follow-up work rather than residual-risk prose.
```
