# Phase Plan

## Phase 1 — Baseline and regression proof

Run restore/build/test baseline. Confirm the previous executor-catalog implementation is present and stable.

## Phase 2 — Office365 executor

Implement the new address-based unprocessed message download executor and add-only category mutation semantics.

## Phase 3 — Templates

Add two managed workflow templates and test loader/seed behavior.

## Phase 4 — Scheduler typed input contract

Add workflow input parameter descriptors and scheduler form model.

## Phase 5 — Scheduler UX providers

Add contact/email, project, and project-node option provider surfaces and UI picker behavior.

## Phase 6 — Polling semantics and idempotency

Make no-message success explicit and duplicate prevention robust.

## Phase 7 — Scheduler observability

Improve run history summaries, no-message status, retry, approval/preapproval behavior, and audit metadata.

## Phase 8 — Final proof

Run fake Graph end-to-end tests, scheduler component tests, workflow template tests, integration tests, and browser proof.
