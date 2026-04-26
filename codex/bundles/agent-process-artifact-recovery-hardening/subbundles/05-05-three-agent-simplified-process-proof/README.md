# 05 Three Agent Simplified Process Proof

## Status

- `Completed`

## Objective

Prove artifact output and handoff behavior with a smaller three-agent process before returning to the full rich software-delivery process.

## Covered Notes

- User asked for a less complicated process with roughly three agents.
- The goal is mainly to test artifact outputs and handoffs.

## Prerequisites

- Subbundle 04 mock failure matrix is green.
- Required artifact and retry routing contracts are stable.

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AgentFrameworkAuditProofTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AgentFrameworkAuditProofTests.Seeding.cs`

## Scope

- Create or seed a simplified three-agent process for:
  1. Scope or architecture brief.
  2. Implementation with artifacts and validation.
  3. Review/QA consuming upstream artifacts.
- Prove the process handles required artifacts and artifact inputs.
- Use deterministic mock agents.

## Dependency Impact

- Final closure proof for this bundle.

## Validation Depth

- Integration test for the simplified process run.
- Browser proof only if the process workspace UI state or operator flow is part of the shipped change.
- Screenshot review for UI-visible proof.

## Implementation Steps

1. Define the smallest process that tests artifact output and consumption.
2. Seed deterministic mock assignments.
3. Run the simplified process through service-level proof first.
4. If UI state changed or route proof is needed, add a focused Playwright scenario.
5. Update execution report with browser analytics and raw-note closure.

## Scope Exceptions

- Do not run the full multi-team software-delivery process as the primary closure proof.
- A later full real-agent smoke may be useful, but it is outside this bundle's required deterministic closure.

## Do Not Do

- Do not create another large process template.
- Do not depend on real external LLM providers.
- Do not skip artifact assertions.

## Acceptance Checklist

- Three-agent process completes deterministic artifact handoff.
- Implementation step produces required artifacts including rollout/checklist content.
- Review/QA step consumes upstream artifact inputs.
- Missing artifact negative path is still covered by earlier phases.

## Proof Required

- Integration test result.
- Playwright/browser result if UI-visible behavior changes.
- Execution report and raw-note closure updated.

## Browser Validation Logging

- If run through UI, record route, desktop viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.
- Also review screenshots for text readability, clipping, spacing, hierarchy, and consistency with the existing Process Workspace.

## Progression Gate

- Bundle may close only after the simplified process proof passes and all raw notes are closed or explicitly scoped.
- Stop and reopen earlier subbundles if the three-agent proof only passes by bypassing artifact expectations or retry classification.

## Suggested Agent Prompt

```text
Implement subbundle 05 only after subbundles 01-04 are green. Add a deterministic simplified three-agent process proof that validates required artifacts and handoff behavior without running the full rich software-delivery process.
```
