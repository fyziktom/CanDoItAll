# SB08 Release Decision

## Decision
- Status: Blocked for final closure and merge-readiness by the code-first ratio gate.

## Processes Restored
- User-facing project/project-structure launch and run-detail readback passed through SB02 Playwright proof and SB08 rerun.
- Representative Blazor, software-delivery, business-plan, runtime-host readback, scheduler-origin, and workflow-origin paths passed the focused SB08 integration matrix.
- Build and full unit tests passed.

## Still Blocked
- Final code-first ratio fails under the conservative `HEAD` baseline because no explicit bundle-start SHA was recorded in the prepared bundle.
- Tracked ratio: 1390 source/test changed lines versus 465 bundle changed lines; required minimum is 2325.
- Artifact-inclusive ratio also fails because generated proof files dominate the bundle directory.
- Live OpenAI provider proof was skipped because explicit opt-in variables and bounded model/budget settings were not present. This is not a process-restoration blocker because the live proof is optional, but it is not live-provider validation.

## Merge-Ready Conditions
- Rebase or reclassify the bundle against an explicit start SHA and rerun the final ratio.
- Reduce bundle/proof churn or add real source/test changes only if they are behaviorally justified.
- Keep the green build, unit, focused integration, and Playwright matrix green after any ratio repair.
