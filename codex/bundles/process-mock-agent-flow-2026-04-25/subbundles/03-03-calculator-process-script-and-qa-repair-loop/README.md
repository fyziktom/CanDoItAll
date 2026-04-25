# 03-calculator-process-script-and-qa-repair-loop

## Status

- `Completed`

## Objective

- Prove the deterministic mock agents can run a multi-step calculator delivery process where QA rejects the first developer output, a repair step runs, and QA approves the repaired output.

## Covered Inputs

- R5 workspace artifacts.
- R6 QA rejection.
- R7 QA approval after repair.
- R8 existing process transitions.
- R14 QA repair proof.

## Prerequisites

- Subbundle 02 has seeded role agents only when enabled.
- Mock runtime can return governed outcomes and write artifacts.
- Prepared bundle validation has passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeProgressionPlanner.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessStepTransitionGuard.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ScenarioHarnessSupport.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\TestApplication.cs

## Deliverables

- Deterministic calculator process flow fixture or test helper.
- QA first-pass rejection using branch outcome `repairs-required`.
- Repair developer output that corrects the calculator artifact.
- QA approval using branch outcome `approved`.
- Automated proof that process progression uses existing branch dependencies.

## Dependency Impact

- Subbundle 04 closure depends on this proof to show the original requested iteration and escalation case is covered.
- Future tuning of process execution core depends on deterministic reproductions of repair loops.

## Validation Depth

- Process-critical closure.

## Implementation Steps

1. Inspect existing process integration test helpers and choose the smallest fixture path.
2. Define or seed a calculator process with role assignments and branch-gated repair dependencies.
3. Execute the process using mock technical agents with the feature enabled.
4. Assert the QA first pass selects `repairs-required`.
5. Assert the repair step produces corrected artifacts.
6. Assert the QA recheck selects `approved` and downstream release work can proceed.

## Scope Exceptions

- UI controls for launching the mock process are out of scope for this first slice unless tests require a seed endpoint.

## Do Not Do

- Do not replace process branch logic with a hard-coded runtime loop.
- Do not make QA approval random or dependent on real LLM output.
- Do not expand to non-calculator scenarios in this subbundle.

## Acceptance Checklist

- The process contains multiple role-specific assignments.
- The first QA step returns work for repair.
- The repair step runs after the QA repair branch.
- The final QA step approves after repair.
- Required artifacts are visible to process execution artifact tracking.

## Proof Required

- Targeted process integration test output.
- Execution report updated with commands and results.
- Artifact paths or assertions recorded in the execution report.

## Browser Validation Logging

- N/A: backend process automation subbundle.

## Progression Gate

- Subbundle 04 may start only after the QA repair loop is proven or an explicit blocker is recorded with replacement proof.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Add deterministic calculator process-flow proof with QA rejection, repair, QA approval, and artifact assertions through the existing process automation path.
```
