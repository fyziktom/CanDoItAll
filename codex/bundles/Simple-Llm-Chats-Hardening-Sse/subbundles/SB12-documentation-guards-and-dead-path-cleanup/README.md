# SB12 — Documentation, guards, and dead-path cleanup

Status: **Locked**  
Proof tier: **Behavioral**  
Depends on: **SB11**

## Outcome

Remove superseded paths, update authoritative documentation, and install guards that keep Simple Chats independent from agents and UI.

## Owned requirements

- `RQ-032` — Do not implement UI, shared-component refactoring, floating chat, or Project Structure context in this bundle.
- `RQ-033` — Preserve a clean future LlmChatDeployment boundary for enterprise chatbot channels without dormant deployment fields now.
- `RQ-035` — Use filtered affected-scope tests throughout; forbid repeated full Unit/Integration/Solution suites before the final gate.

## Scope

- Delete obsolete fake-UoW, post-commit evidence, synchronous request-owned execution, and duplicate SSE paths.
- Update module/API/testing/migration/architecture documentation.
- Add source guards for dependency direction, no UI/Razor, no agent/tools/skills/MCP coupling, server-owned origin, SSE reuse, and filtered-test policy.
- Record handoffs for later shared-component isolation, UI integration, Project Structure context, and enterprise deployment bundles.
- Refresh proof manifests and input closure at the actual implementation head.

## Explicit non-goals

- No UI refactor.
- No new feature behavior after CP2.
- No full stable gate.

## Current-source entry points

- `README.md`
- `docs/testing.md`
- `src/Modules/CanDoItAll.Modules.LlmChats/README.md`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/README.md`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Remove superseded paths, update authoritative documentation, and install guards that keep Simple Chats independent from agents and UI.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Documentation plus executable architecture guards; no new runtime layer.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Documentation validator.
- Bundle, traceability, test-policy, architecture, and SSE guards.
- Affected project build only when cleanup changes source.

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

- [ ] No production path uses the independent-context UoW or synchronous request-owned provider execution.
- [ ] No Razor, floating-chat, shared-component, Project Structure context, or UI integration was added.
- [ ] Executable guards enforce dependency direction and prevent agent/tool/skill/MCP leakage.
- [ ] Authoritative docs accurately describe asynchronous operation and SSE contracts.
- [ ] Future UI, context, and enterprise deployment bundles have explicit ownership handoffs.
- [ ] All proof and closure records reference the actual implementation head.

## Reopen triggers

- cleanup removes behavior required by focused tests
- docs contain conflicting source-of-truth claims
- guards miss actual dependency direction

## Progression decision

Unlock SB13 after this work unit passes, unless a checkpoint applies.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
