# SB07: Representative template regression matrix

## Status
- Status: `Completed`

## Objective
Run and stabilize the final representative process matrix.

## Covered Inputs
- REQ-007: Run final representative template regression matrix and classify manual contract tests separately from automation E2E.

## Prerequisites
- SB06 must be completed with process-owned scheduler/workflow proof.
- SB03 and SB04 browser/API classifications must be current.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright
- repo://codex/bundles/process-template-release-readiness-ui-live-host-closure-v1/reviews/01-execution-report.md

## Deliverables
- Build proof.
- Full unit test proof.
- Focused integration matrix proof.
- Large desktop Playwright UI proof for project/project-structure launch and any runtime-host UI path.
- Manual contract tests classified separately from automation E2E proof.

## Dependency Impact
- SB08 release decision depends directly on this matrix.
- Any matrix failure must reopen the owning subbundle rather than being summarized away.

## Validation Depth
- Standard build/test commands with transcripts.
- Focused integration test commands for representative templates.
- Large desktop Playwright transcript and screenshots for UI proof.

## Implementation Steps
1. Run Blazor app delivery automation E2E.
2. Run canonical multi-team/software-delivery automation E2E.
3. Run business-analysis PostgreSQL automation E2E or blocked classification.
4. Run project/project-structure UI launch proof.
5. Run runtime-host readback proof on real run/step.
6. Run scheduler/workflow trigger plus verification job proof.
7. Confirm existing manual-transition contract tests are classified separately.

## Do Not Do
- Do not count manual-transition contract tests as primary E2E proof.
- Do not count skipped live OpenAI tests as live proof.

## Acceptance Checklist
- Build passes.
- Full unit passes.
- Focused integration passes.
- Playwright large-desktop UI proof passes.
- Manual contract tests are not primary E2E proof.
- Live OpenAI classified honestly.

## Proof Required
- `bundle://proof/SB07/manifest.md`
- `bundle://proof/SB07/semantic-invariants.md`
- Build, unit, focused integration, and Playwright transcripts.
- Screenshot or trace paths for large desktop UI proof.

## Browser Validation Logging
- Record project/project-structure and runtime-host route coverage, `1900x1200` viewport, Playwright MCP evidence, screenshots, visual review result, and pass/fail in the execution report.

## Progression Gate
- SB08 may start only after the representative matrix passes or failed cells reopen their owning subbundle with explicit blocker status.

## Suggested Agent Prompt
Run and stabilize only the representative matrix for SB07, record proof artifacts and browser analytics, then run the closure gate before SB08.
