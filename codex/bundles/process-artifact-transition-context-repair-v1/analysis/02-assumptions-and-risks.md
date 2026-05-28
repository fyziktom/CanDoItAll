# Assumptions And Risks

## Working Assumptions

- The current failed run at `0a96e6f9-4a89-4422-b931-e782f1b26c94` is representative because it reproduces the same first-step artifact failure captured in the prior input bundle.
- Process-owned automation may carry internal validation context through `ProcessStepTransitionRequest` without exposing that context to public API callers.
- Existing manual transition tests represent the stale-artifact safety boundary that must stay intact.

## Critical Path Risks

- Accidentally treating all step transitions as automation would weaken manual governance.
- Fixing only direct agents would leave workflow, subprocess, and manager recovery artifacts vulnerable to the same second-pass validation failure.
- Restarting the running app on the same port could interrupt the user's test session.

## Validation Risks

- A test that calls only `ProcessCompletionArtifactValidator` would miss the double-validation bug.
- A test that manually seeds a satisfied read model without calling `TransitionStepAsync` would miss the actual failure.
- Template-only checks do not prove that process-owned transition validation works.

## Reopen Triggers

- Any focused integration test still reports `StaleOrWrongRun` for matching automation lineage.
- Manual stale-lineage completion starts passing.
- Blazor template governance no longer shows the generic Blazor WASM PWA live-run profile.
- The running web app stops responding during validation.

