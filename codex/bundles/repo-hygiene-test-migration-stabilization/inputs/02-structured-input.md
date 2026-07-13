# Structured Input

## Core Objective

- Analyze and plan repairs for failing tests, migration checks, repository hygiene, and the `5032` runtime smoke path.

## Success Criteria

- A follow-up bundle exists with exact failure ownership, sequencing, and proof gates.
- Deterministic failing test evidence is captured.
- EF pending-model state is checked before any migration work is planned.
- `5032` rebuild/start/smoke validation is executed after preparation for the current workspace state.

## Hard Constraints

- Do not implement hygiene fixes during bundle preparation.
- Do not commit, revert, or discard unrelated changes.
- Do not generate a migration from the historical warning unless a current EF pending-model proof fails.
- Do not replace targeted source fixes with broad test suppression.

## Allowed Side Effects

- Create and validate this bundle.
- Capture evidence transcripts under this bundle.
- Build/start/smoke the current `5032` app instance after bundle preparation.

## Source Artifacts

- `inputs/00-original-request.md`
- `evidence/targeted-failing-tests.txt`
- `evidence/database-runtime-switch-test.txt`
- `evidence/ef-pending-model-check.txt`
- Historical `codex/bundles/filesystem-agent-tools/proof/full-unit-test.txt`

## Input Coverage Signals

- Tests may be obsolete after code changes.
- Migration failure may be real or order-dependent.
- Repository hygiene must be repaired, not bypassed.
- `5032` must be rebuilt and available for real user tests.

## Dependency And Sequencing Signals

- Repository hygiene failures block meaningful full-suite proof.
- Runtime launch/watch drift affects `5032` confidence.
- Process-template/branch-signal failures are semantically separate from repo hygiene.
- Database isolation should be evaluated after deterministic failures are separated.

## Validation Expectations

- Targeted failing-first tests before repairs.
- Passing targeted tests after each subbundle.
- EF pending-model check before any migration decision.
- Full unit-suite attempt after all subbundles.
- Fresh `5032` app smoke proof.

## Evidence Contract

- Command transcripts under `proof/SBxx/`.
- Changed-file hashes for completed critical subbundles.
- Source assertions for hygiene scanner, runtime launcher paths, branch-signal parser, and EF model registry state.
- Browser/API smoke evidence for `http://localhost:5032`.

## UI Validation Strategy

- Only SB05 is browser-visible. Use a real browser or HTTP smoke against `http://localhost:5032`; if the home route redirects, record the final URL and status.

## Browser Validation Analytics

- SB05 records route, viewport or HTTP probe, actions/assertions, screenshots if a browser is used, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- Some failures are obsolete tests after intentional layout/template changes.
- Some failures are real production regressions, especially branch-signal recovery and watch restore stale-reference behavior.
- EF model state is currently clean because `dotnet ef migrations has-pending-model-changes` reports no changes.

## Primary Risks

- Weakening hygiene tests can hide real repository pollution.
- Adding a migration for an isolation bug can create schema churn.
- Process tests can be made green while losing real branch-routing behavior if exact semantics are not preserved.
