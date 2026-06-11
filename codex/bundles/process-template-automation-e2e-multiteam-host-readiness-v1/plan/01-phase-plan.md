# Phase Plan

## Execution Order
1. SB01 Baseline and ratio guard.
2. SB02 Template catalog and multi-team inventory.
3. SB03 Blazor/.NET automation E2E.
4. SB04 Multi-team software-delivery automation E2E.
5. SB05 Business-analysis automation E2E.
6. SB06 Runtime-host readback on real runs.
7. SB07 Scheduler/workflow read-only job lifecycle.
8. SB08 Release matrix and red-team.

## Subbundle Dependency Map

```mermaid
graph TD
  SB01[SB01 Baseline and ratio guard]
  SB02[SB02 Template catalog and multi-team inventory]
  SB03[SB03 Blazor/.NET automation E2E]
  SB04[SB04 Multi-team software-delivery automation E2E]
  SB05[SB05 Business-analysis automation E2E]
  SB06[SB06 Runtime-host readback on real runs]
  SB07[SB07 Scheduler/workflow read-only job lifecycle]
  SB08[SB08 Release matrix and red-team]
  SB01 --> SB02 --> SB03 --> SB04 --> SB05 --> SB06 --> SB07 --> SB08
```

## Critical Subbundles
- SB01 through SB08 are critical.
- SB01 is the code-first and current-state foundation for every downstream proof.
- SB02 is the template identity foundation for representative E2E proof.
- SB03 through SB05 are production-path automation foundations.
- SB06 through SB07 are runtime-host and scheduler/workflow readiness foundations.
- SB08 is the final release and fake-proof resistance gate.

## Phase Gates
Each subbundle must record:
- source/test files changed,
- focused validation commands,
- semantic positive proof,
- adversarial negative proof,
- source scans for Core leakage and driver/runtime side effects,
- downstream progression decision.

## Final Validation Matrix
- Build.
- Full unit tests.
- Focused integration tests for templates, automation dispatch, scheduler/workflow verification jobs, runtime-host readback, and Core boundaries.
- Large desktop Playwright proof when UI/project-structure route is part of the subbundle.
- Optional live OpenAI process-run smoke only when opt-in variables are explicit.
- Code-first ratio gate.
