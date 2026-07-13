# Regression And UI Proof

## Status

- `Completed`

## Objective

- Prove the full refactor did not regress runtime behavior, browser-visible agent/workflow/process surfaces, or raw request closure.

## Covered Inputs

- N001-N010
- Requirements R01-R12

## Prerequisites

- SB01 through SB07 closure gates passed.
- All critical subbundle proof manifests and semantic invariants exist.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/AiAgentFlowTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/AgentCapabilitySetupFlowPlaywrightTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/WorkflowShellSmokeTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/ProcessShellSmokeTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`

## Deliverables

- Final build and test proof.
- Playwright browser proof with screenshots and explicit review.
- Raw-note closure audit.
- Final execution report and bundle validation.

## Dependency Impact

- This is the final closure gate. Any failure reopens the owning previous subbundle.

## Validation Depth

- `End-to-end regression and UI closure`

## Implementation Steps

1. Verify all previous proof manifests exist and cite real artifacts.
2. Run MAF and web builds.
3. Run focused MAF unit and integration tests.
4. Run Playwright UI tests for agent, capability, workflow, and process smoke surfaces.
5. Capture large-screen screenshots and review them against the workbook questions.
6. Run narrower viewport proof only if UI/layout files changed; otherwise document why it is not applicable.
7. Complete raw-note closure matrix.
8. Run final bundle validation.

## Scope Exceptions

- Do not add new feature work during final proof. Reopen the owning subbundle for fixes.

## Do Not Do

- Do not close from route-load-only browser proof.
- Do not attach screenshots without reviewing readability, errors, state, and layout.
- Do not ignore missing critical proof manifests.

## Acceptance Checklist

- MAF project build passes.
- Web project build passes.
- Focused unit tests pass.
- Focused integration tests pass.
- Playwright agent/capability/workflow/process proof passes.
- Screenshots are reviewed and recorded in `reviews/01-execution-report.md`.
- Raw notes N001-N010 are marked `Solved`, `Partially solved`, or `Not solved` with proof citations.
- Final validator passes.

## Proof Required

- `proof/SB08/manifest.md`
- `proof/SB08/semantic-invariants.md`
- Build transcripts.
- Unit and integration test transcripts.
- Playwright transcripts.
- Screenshots listed in `reviews/01-execution-report.md`.
- Browser analytics review.
- Raw-note closure audit.
- Changed-file hashes for all production and test changes.
- Anti-stub audit.
- Final bundle validator transcript.

## Browser Validation Logging

- Route `/agents`, large desktop viewport first: assert shell and tabs visible, capture `proof/SB08/screenshots/agents-shell-large.png`.
- Route `/agents?tab=agents`, large desktop viewport first: assert agent/chat runtime surface visible, capture `proof/SB08/screenshots/agents-chat-large.png`.
- Route `/agents?tab=capabilities&agentId={seed}`, large desktop viewport first: assert capability setup/runtime surface visible, capture `proof/SB08/screenshots/capability-setup-large.png`.
- Route `/agents/workflows`, large desktop viewport first: assert workflow shell visible, capture `proof/SB08/screenshots/workflows-large.png`.
- Process shell smoke route, large desktop viewport first: assert process runtime shell visible and no finalizer/runtime errors, capture `proof/SB08/screenshots/process-shell-large.png`.
- If UI files changed, repeat affected routes at a narrower viewport and record screenshots. If no UI files changed, record that narrower proof was not applicable and why.
- Review questions: no route error overlays, no console errors relevant to runtime, expected seeded data visible, no clipped runtime diagnostics, no broken capability/workflow/process panels.

## Progression Gate

- Bundle closes only after all proof is captured, raw notes are audited, and final validation passes. Otherwise reopen the owning subbundle.

## Suggested Agent Prompt

```text
Implement SB08 only. Do not add feature work. Verify previous proof, run builds/tests/Playwright, capture and review screenshots, complete raw-note closure, run final validation, and reopen the owning subbundle for any regression.
```
