# SB00 — Baseline sync and proof reconciliation

Status: **Ready**  
Proof tier: **Governed**  
Depends on: **None**

## Outcome

Synchronize the feature branch with current development, replace stale provenance, and classify the existing red stable-gate evidence without rerunning the whole suite.

## Owned requirements

- `RQ-001` — Synchronize simple-chats with the latest development baseline before hardening proof.
- `RQ-002` — Replace stale or commitless proof with evidence tied to the actual implementation head and classify the prior 19 failures.
- `RQ-034` — Run the expensive stable solution gate and CI matrix once, only at the immutable final head.
- `RQ-035` — Use filtered affected-scope tests throughout; forbid repeated full Unit/Integration/Solution suites before the final gate.

## Scope

- Merge or rebase the latest development commit into simple-chats in a clean worktree.
- Record feature head, development head, merge base, dependency mode, host, database, and skill provenance.
- Extract the 19 prior failing tests from committed TRX/log evidence and rerun only those exact tests on synchronized development and feature heads.
- Classify every prior failure as baseline, branch-induced, environment-sensitive, obsolete after synchronization, or unresolved.
- Update the original Simple LLM Chats bundle closure records so they reference the actual implementation commit.

## Explicit non-goals

- No product-code fixes except conflict resolution.
- No full stable suite.
- No streaming implementation.

## Current-source entry points

- `codex/bundles/Simple-Llm-Chats-Backend-Api/EXECUTION-PROGRESS.md`
- `codex/bundles/Simple-Llm-Chats-Backend-Api/reviews/FINAL-MERGE-DECISION.md`
- `codex/bundles/Simple-Llm-Chats-Backend-Api/subbundles/SB11-final-regression-and-release-gate/SESSION-HANDOFF.md`
- `docs/testing.md`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Synchronize the feature branch with current development, replace stale provenance, and classify the existing red stable-gate evidence without rerunning the whole suite.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Evidence reconciliation and branch synchronization; no runtime pattern change.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Run exact previously failing fully-qualified tests only on synchronized development and feature heads.
- Run bundle validators and architecture inventory; do not run solution-wide dotnet test.

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

- [ ] The feature branch contains the latest development commit or an explicitly recorded equivalent merge result.
- [ ] The actual implementation head and proof head are identical and recorded.
- [ ] Every one of the 19 prior failures has a reproducible classification or is explicitly obsolete with evidence.
- [ ] No branch-induced or unresolved prior failure is deferred beyond CP0.
- [ ] No solution-wide test suite was rerun during this subbundle.

## Reopen triggers

- development advances across affected files
- later evidence contradicts the failure classification

## Progression decision

Close `reviews/CP0-BASELINE-AND-PROOF.md`. Unlock SB01 only when CP0 is Ready.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
