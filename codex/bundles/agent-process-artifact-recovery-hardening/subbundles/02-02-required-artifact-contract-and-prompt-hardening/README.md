# 02 Required Artifact Contract And Prompt Hardening

## Status

- `Completed`

## Objective

Make required artifacts, especially DB-free migration/rollout checklists, explicit enough that implementation agents produce durable artifacts after validation instead of relying on vague final summaries.

## Covered Notes

- Missing artifact was `Migration and rollout preparation checklist`.
- User questioned whether that artifact makes sense for an app with no DB.
- Strict governed completion must remain intact.

## Prerequisites

- Subbundle 01 completed or produced a focused failing test.
- Current prompt-generation source is understood.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\steps\implementation.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\programming-workspace-analyst.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Scope

- Harden prompt text and/or artifact contract generation.
- Ensure no-DB implementation steps still produce an explicit migration/rollout checklist.
- Add tests asserting required artifact instructions and artifact satisfaction behavior.

## Dependency Impact

- Unlocks mock failure matrix and three-agent process proof.
- Incorrect artifact semantics will make phase 05 meaningless.

## Validation Depth

- Prompt-generation tests.
- Artifact satisfaction/projection tests.
- Negative test proving missing required checklist blocks/fails.

## Implementation Steps

1. Add a prompt/test fixture for the implementation step with `Implementation change set` and `Migration and rollout preparation checklist`.
2. Ensure prompt says DB-free work must still write a checklist with `No data migration required` when true.
3. Ensure required artifact completion happens after successful validation, not before.
4. Ensure final response headings and durable files are both supported by projection as intended.
5. Update bundle proof.

## Scope Exceptions

- Do not remove the checklist from the software-delivery template in this phase.
- Do not create a separate DB-only process branch unless tests prove no prompt-level fix is viable.

## Do Not Do

- Do not silently auto-create placeholder artifacts.
- Do not let empty headings satisfy required artifact obligations.
- Do not weaken `IsRequired`.

## Acceptance Checklist

- Prompt text makes required artifacts unambiguous.
- DB-free work has an explicit valid checklist pattern.
- Missing required checklist still blocks/fails.
- Tests cover both present and missing checklist cases.

## Proof Required

- Focused integration/unit test result.
- Narrow build for touched projects.
- Execution report updated.

## Browser Validation Logging

- N/A unless UI text or process workspace rendering changes.

## Progression Gate

- Proceed to subbundle 04 only after required artifact contract tests are green.
- Stop and repair this subbundle if DB-free rollout checklist instructions remain ambiguous or empty artifacts can satisfy required obligations.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Harden required artifact prompt/contract behavior for DB-free migration rollout checklists and prove missing required artifacts still block completion.
```
