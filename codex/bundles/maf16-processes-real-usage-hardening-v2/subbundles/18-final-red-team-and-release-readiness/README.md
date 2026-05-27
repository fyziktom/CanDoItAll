# SB18: Final Red-Team And Release Readiness

## Status

- Completed

## Objective

Close final red-team, web-app, agent communication, and release-readiness proof before real testing.

## Covered Inputs

- RQ10: produce final runbook and release-readiness report.
- User note: assure web app can run and test simple agent communication.

## Prerequisites

- SB17 observability and runbook must be complete.

## Exact Source References

- repo://CanDoItAll.slnx
- bundle://scripts/validation-commands.md
- bundle://reviews/01-execution-report.md

## Deliverables

- Full build and focused tests.
- Red-team proof for stale artifact, unreadable content, empty content hash, wrong execution run, QA product mutation, A2A/handoff mismatch, workflow artifact mismatch, and operator decision substitution.
- Web-app startup/browser proof.
- Simple agent communication proof.
- Final release-readiness report listing deferred MAF 1.6 features.

## Dependency Impact

- This is the final closure gate for the bundle.

## Validation Depth

- Critical final proof must include completed-stage validator output and fake-proof resistance audit.

## Implementation Steps

- Run required validation commands.
- Start the web app and capture browser proof.
- Test simple communication with agents using configured live provider or approved local mock/scenario runtime.
- Run final red-team checklist and completed-stage validator.
- Update final proof and execution report.

## Do Not Do

- Do not close with residual-risk prose when a required proof artifact is missing.
- Do not treat provider-unavailable live agent communication as passed; record approved fallback proof and limitation.

## Acceptance Checklist

- Build and focused tests pass.
- Web app runs and renders a route.
- Simple agent communication proof is captured.
- Completed-stage bundle validator passes.

## Proof Required

- Final build/test transcripts.
- Browser proof and screenshot.
- Agent communication transcript.
- Red-team verifier artifact.
- Completed-stage validator transcript.

## Browser Validation Logging

- Record route, desktop viewport, Playwright actions, screenshot path, and result for web-app startup and agent/process smoke.

## Progression Gate

- Bundle can be marked complete only after SB18 closure proof and completed-stage validation pass.

## Suggested Agent Prompt

Run final validation, web-app startup proof, simple agent communication proof, red-team checks, and completed-stage bundle validation.

## Closure Proof

- bundle://proof/SB18/manifest.md
- bundle://proof/SB18/semantic-invariants.md

