# 01 Live Run Forensics And Single Agent Proof

## Status

- `Completed`

## Objective

Convert the supplied real-run failure into a precise, testable diagnosis and prove the implementation lane in isolation before any multi-agent process proof.

## Covered Notes

- Real process failed at Step 3 with missing migration/rollout artifact.
- Console showed repeated identical writes and missing validation tools.
- First test should be one agent implementing an application.

## Prerequisites

- DB path from `inputs/01-source-artifacts.md` is accessible read-only.
- Current repo source references exist.
- Previous process-run-with-agents-review changes are present.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ScenarioHarnessAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\programming-workspace-analyst.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Scope

- Add focused diagnostics/tests that isolate one implementation agent or one dispatch candidate.
- Capture whether the implementation prompt and runtime can complete source changes, validation, and required artifacts.
- Do not repair full retry routing yet.

## Dependency Impact

- Blocks all later phases.
- If this phase proves the agent cannot complete a single implementation job, phase 05 must not run.

## Validation Depth

- Read-only DB query proof in bundle notes.
- Focused integration test for single implementation job or dispatch prompt behavior.
- Narrow build/test for touched projects.

## Implementation Steps

1. Capture DB-derived failure classification in a test fixture or bundle evidence.
2. Identify the narrowest existing harness path for one implementation agent.
3. Add or update a focused test so one implementation job must produce build/test proof and every required artifact.
4. Record whether the failure is prompt, runtime, artifact projection, or retry orchestration.
5. Update execution report with pass/fail and downstream gate decision.

## Scope Exceptions

- Do not require real external LLM execution in this subbundle.
- Do not run the full software-delivery process.

## Do Not Do

- Do not change the rich software-delivery template yet.
- Do not add broad UI changes.
- Do not mark the issue solved from DB inspection alone.

## Acceptance Checklist

- Real-run failure is classified with DB and source evidence.
- One-agent proof exists or a failing focused test captures the gap.
- The proof distinguishes source/validation failure from artifact omission.
- Downstream phases have a concrete behavioral target.

## Proof Required

- Focused integration test result.
- Bundle execution report updated.
- Any known failure preserved as a named blocker if not fixed in this phase.

## Browser Validation Logging

- N/A unless UI-visible state is changed.

## Progression Gate

- Proceed to subbundle 02 only after the single-agent behavior is reproducible or proven green with explicit artifact obligations.
- Stop and repair this subbundle if the evidence cannot distinguish source/validation failure from artifact omission.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Use the supplied DB read-only and add focused proof for one implementation agent or one dispatch candidate. Do not run the full rich process. Classify the failure before making broader repairs.
```
