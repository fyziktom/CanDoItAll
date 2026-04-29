# Agent Process Artifact Recovery Hardening

## Profile

- `feedback`

## Mission

Repair the real-agent process failure where a software-delivery run exhausted implementation attempts, failed on repeated identical tool calls and missing validation tools, then surfaced missing required artifacts or runtime proof gaps.

The work must be isolated into small proof phases. Do not rerun the whole rich process as the primary validation loop. First prove one implementation agent can complete a narrow delivery step with all required artifacts, then harden artifact contracts, retry routing, mock coverage, a smaller three-agent process, and the universal process-core boundary.

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
6. `subbundles/06-06-blazor-runtime-hosting-proof`
7. `subbundles/07-07-universal-process-core-guidance-extraction`

## Validation Summary

- Bundle preparation status: `Passed`
- Execution status: `Completed after 2026-04-29 universal process-core correction`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed by focused build/tests, source scans, and bundle validator`
- Browser validation analytics: `Not required for subbundle 07; no rendered UI route changed`

## 2026-04-26 Extension

- PostgreSQL run `33951fbf-9983-4a39-b440-fe0b371b4b32` failed in Delivery QA Observer at step `99413668-f019-4525-a584-a12846ea4b5c`.
- Root cause: QA helper used a relative `external-target/...` alias inside PowerShell, so `dotnet run --no-build` resolved the project under the managed workspace alias instead of the real `C:\programovani\dotnet\calculatorblazor\Calculator` path.
- Repair: managed OpenAI defaults now use `gpt-5-mini`; QA prompts and recovery directives now require native-path conversion inside helper scripts and browser click assertions for button-driven Blazor apps.
- Evidence report: `reviews/02-qa-observer-real-run-extension.md`.

## 2026-04-28 Extension

- CalcApp run failed at `/` with `Cannot find the fallback endpoint specified by route values: { page: /_Host, area: }`.
- Root cause: the implementation lane rewrote a modern net10 Blazor Web App host into legacy Blazor Server/Razor Pages hosting while build and engine tests still passed.
- Correction: this extension is retained as diagnostic history, but its process-core repair direction was wrong because it added sample and framework-specific rules to universal dispatch.

## 2026-04-29 Correction

- User feedback rejected calculator and .NET-specific hardcoding in process orchestration.
- Repair direction: process dispatch now stays domain-neutral; it enforces concrete deliverable proof, required tool receipts, validation after mutation, and explicit blockers without knowing about calculators or Blazor hosting.
- Domain-specific guidance is kept in agent instructions, task skills, reusable Blazor/.NET skills, and tool capabilities.
