# Assumptions And Risks

## Working Assumptions
- The previous bundle report is historical context only; source and tests in this checkout are authoritative.
- Live OpenAI proof is opt-in and must be skipped honestly when explicit live environment variables are absent.
- PostgreSQL proof may require local database availability; if unavailable, the affected subbundle must be blocked rather than rewritten as in-memory proof.

## Critical Path Risks
- SB02 is a user-facing gate; API-only proof cannot unlock downstream confidence.
- SB03-SB05 depend on production-path automation dispatch, not manual transition tests.
- SB06-SB07 must preserve read-only runtime-host boundaries and cannot introduce execution-capable driver hooks.
- SB08 final closure is blocked if bundle/proof edits dominate source and test changes.

## Validation Risks
- Playwright routes may require seeded project and process data before the launch flow is visible.
- Focused integration tests may be slow or environment-sensitive because they exercise outbox dispatch, PostgreSQL, and process-mock runtimes.
- Final completed-stage validation requires artifact-backed proof manifests and semantic invariant contracts for every completed critical subbundle.

## Reopen Triggers
- Reopen SB02 if the UI route cannot start from project/project-structure context or cannot show run detail readback.
- Reopen SB03 if automation proof uses `SuppressAutomationDispatch = true` or lacks execution-run/artifact/finalizer readback.
- Reopen SB04 if `software-delivery` is not clearly the canonical multi-team representative or governance steps are skipped without proof.
- Reopen SB05 if business-analysis proof falls back to in-memory persistence or leaks software/.NET domain terms.
- Reopen SB06-SB07 if read-only verification introduces mutation permissions, direct driver hooks, or manager bypasses.
- Reopen SB08 if final source scans, browser proof, or code-first ratio contradict earlier closure.
