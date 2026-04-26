# Assumptions And Risks

## Assumptions

- The supplied DB is representative of the current failure and should be queried read-only.
- The current rich `software-delivery` template remains valuable; this bundle should harden it rather than deleting the migration/rollout checklist.
- A no-DB app still needs rollout/rollback notes, but the artifact wording and prompt must make that explicit.
- Deterministic mock/runtime tests are the right first validation layer before another real-agent process run.

## Critical Path Risks

- If the single-agent proof still fails, later three-agent or whole-process proof is noise.
- If required artifacts are only checked by title text in the final response, real agents can still miss durable files or produce empty sections.
- If retry routing cannot distinguish current-step from upstream missing artifacts, the system will keep retrying the wrong agent.
- If the mock runtime only covers happy paths, regressions will reappear only in expensive real-agent runs.

## Validation Risks

- Real provider behavior is nondeterministic, so final proof must rely on deterministic mocks plus a small optional real-agent smoke, not the full rich process as the first gate.
- UI proof can show missing artifacts correctly while the backend still retries the wrong owner; backend tests must lead.
- Existing repo-wide build blockers may still prevent full-solution validation and must be reported separately from this bundle.

## Reopen Triggers

- Any focused test shows a missing required artifact can complete a step.
- A current-step artifact omission causes upstream rerun routing.
- An upstream input artifact omission causes repeated retries of the downstream step.
- Mock agents cannot reproduce at least one observed failure mode.
- The three-agent proof passes only by bypassing artifact expectations.
