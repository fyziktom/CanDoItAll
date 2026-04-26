# Agent Process Artifact Recovery Hardening

## Profile

- `feedback`

## Mission

Repair the real-agent process failure where a software-delivery run exhausted five implementation attempts, failed on repeated identical tool calls and missing validation tools, then surfaced `Missing required artifacts: Migration and rollout preparation checklist`.

The work must be isolated into small proof phases. Do not rerun the whole rich process as the primary validation loop. First prove one implementation agent can complete a narrow app-building step with all required artifacts, then harden artifact contracts, retry routing, mock coverage, and a smaller three-agent process.

## Immediate Finding

The missing artifact is not inherently wrong. The `software-delivery` implementation step requires both `Implementation change set` and `Migration and rollout preparation checklist`. For a calculator app with no database, the second artifact should still exist and explicitly say no database/data migration is needed while naming rollout, operational preconditions, rollback, and smoke validation.

The run did not fail only because the checklist was semantically inappropriate. The DB and logs show the agent repeatedly rewrote the same files, failed/omitted required build and test tool completion across attempts, and never produced the required checklist artifact before the process exhausted `5/5` attempts.

## Key Evidence

- Real DB: `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\bf40a76da44f4d0f858dc55f428483c8\db\candoitall.db`
- Failed process run: `8F1A0E9E-FC8A-405C-A370-57A1A560E9A3`
- Failed step run: `1F125B32-04B3-464F-A51C-563EF3DDBEEB`
- Failed step: `Implement feature, tests, and migration notes`
- Failed executor: `Programming Workspace Analyst`
- Missing expectation: `Migration and rollout preparation checklist`
- Relevant template: `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`

## Bundle Layout

- `inputs/` raw user feedback and DB forensics summary
- `analysis/` current-state diagnosis, assumptions, risks, and reopen triggers
- `requirements/` normalized requirements
- `architecture/` target behavior for artifact contracts and recovery routing
- `plan/` phase dependency map and gates
- `traceability/` note-by-note closure matrix
- `shared-prompts/` execution and QA prompts
- `subbundles/` isolated implementation phases
- `reviews/` readiness and execution reporting

## Execution Order

1. `subbundles/01-01-live-run-forensics-and-single-agent-proof`
2. `subbundles/02-02-required-artifact-contract-and-prompt-hardening`
3. `subbundles/03-03-retry-routing-and-upstream-artifact-recovery`
4. `subbundles/04-04-mock-agent-failure-matrix`
5. `subbundles/05-05-three-agent-simplified-process-proof`

## Validation Summary

- Bundle preparation status: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed by focused build/tests and bundle validator`
- Browser validation analytics: `Not required; implementation changed process runtime, mock runtime, and integration tests without UI surface changes`
