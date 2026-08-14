# SB13 — Final stable gate, CI matrix, and release decision

Status: **Locked**  
Proof tier: **Governed**  
Depends on: **SB12**

## Outcome

Run expensive repository-wide evidence exactly once at the final head and decide whether merge and later UI-isolation work are unlocked.

## Owned requirements

- `RQ-001` — Synchronize simple-chats with the latest development baseline before hardening proof.
- `RQ-002` — Replace stale or commitless proof with evidence tied to the actual implementation head and classify the prior 19 failures.
- `RQ-031` — Keep implementation portable and prove affected behavior on Linux plus the final Windows/Linux/macOS CI matrix.
- `RQ-034` — Run the expensive stable solution gate and CI matrix once, only at the immutable final head.
- `RQ-035` — Use filtered affected-scope tests throughout; forbid repeated full Unit/Integration/Solution suites before the final gate.

## Scope

- Confirm a clean worktree and immutable final commit.
- Run one restore, one Release solution build, and one stable filtered solution test with one dependency mode.
- Run documentation validation and pending-model-change check.
- Push the exact validated commit and run the Windows/Linux/macOS CI matrix once.
- Classify any failure against synchronized development without repeatedly rerunning the whole suite.
- Issue FINAL Ready or Blocked and close both the original and hardening bundle traceability.

## Explicit non-goals

- No implementation fixes without reopening the owning subbundle.
- No unfiltered suite.
- No Playwright/LiveProcess/LongRunning/Quarantined lanes.

## Current-source entry points

- `docs/testing.md`
- `plan/06-final-release-gate.md`
- `reviews/FINAL-RELEASE-DECISION.md`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Run expensive repository-wide evidence exactly once at the final head and decide whether merge and later UI-isolation work are unlocked.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Release gate only; any source change reopens its owner and invalidates final evidence.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Exactly one stable filtered solution test at the final commit.
- Exactly one final CI matrix run for the pushed validated commit.

Critical database/lifecycle claims require real PostgreSQL proof; mocks alone are supporting evidence.

## Partial Class Policy

No new production partial file may be the final boundary. A temporary extraction partial is allowed only
with a named deletion step inside this same subbundle and proof that it is removed before closure.

## Architecture Proof Required

- before/after owner and dependency evidence;
- direct test of the new owner;
- negative test that fails against the previous shallow implementation;
- source assertion that superseded behavior is no longer reachable;
- no cycle and no forbidden dependency;
- actual commands and commit SHA in the proof manifest.

## Validation budget

Follow `test-budget.json` and `plan/04-test-budget-and-gates.md`. During this work unit:

- no solution-wide test command;
- no unfiltered Unit or Integration project;
- no Playwright/LiveProcess/LongRunning/Quarantined gate;
- at most the declared focused command budget;
- do not rerun an unchanged failed command without a concrete fix or diagnostic reason.

## Acceptance checklist

- [ ] The final Release solution build passes at the exact recorded commit.
- [ ] The repository stable filtered test gate passes at the exact recorded commit.
- [ ] Documentation and pending-model-change checks pass.
- [ ] Windows, Linux, and macOS CI jobs pass for the same commit.
- [ ] No broad suite was rerun after an unchanged failure merely to seek a different result.
- [ ] FINAL explicitly states whether UI/component-isolation work is unlocked.

## Reopen triggers

- any source/migration/project/test/API change after final gate
- CI validates another commit
- the stable gate remains red

## Progression decision

Close `reviews/FINAL-RELEASE-DECISION.md`. Do not unlock UI work unless FINAL is Ready.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
