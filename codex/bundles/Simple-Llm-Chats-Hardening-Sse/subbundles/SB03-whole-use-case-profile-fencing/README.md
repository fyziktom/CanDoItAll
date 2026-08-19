# SB03 — Whole-use-case profile fencing

Status: **Completed**
Proof tier: **Governed**  
Depends on: **SB02**

## Outcome

Fence every Simple Chat command and query from its first database read through its final commit/return against one database profile identity and generation.

## Owned requirements

- `RQ-011` — Fence every public use case from first read through final commit/return to one database profile identity and generation.
- `RQ-012` — A profile switch must prevent old-generation writes and produce deterministic retained evidence.

## Scope

- Introduce an operation-scoped LLM Chat profile/runtime scope factory.
- Capture profile ID, fingerprint, and generation before any repository/provider access.
- Create all DbContexts and provider leases through that captured scope.
- Link long-running execution cancellation to profile-switch notification.
- Assert captured identity immediately before every durable commit and before returning authoritative results.
- Remove scoped services that can survive a profile switch while retaining a stale DbContext/provider source.
- Test profile switches at deterministic barriers before read, after admission, during provider execution, before finalization, and before return.

## Explicit non-goals

- No user/tenant authorization model.
- No distributed worker yet.
- No UI profile-switch work.

## Current-source entry points

- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/DatabaseProfileLlmChatRuntimeLease.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/CanonicalLlmChatProviderResolver.cs`
- `src/Foundation/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Fence every Simple Chat command and query from its first database read through its final commit/return against one database profile identity and generation.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Explicit operation scope/lease; no captured current-profile state across operations and no ambient lookup after admission.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Focused unit tests for scope identity and invalidation.
- Focused integration tests that switch profile at deterministic barriers.

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

- [x] Every public LLM Chat application operation captures profile identity before its first read.
- [x] All repositories, provider resolution, transcript commands, and audit writes use the captured operation scope.
- [x] A profile switch prevents every subsequent old-generation durable commit.
- [x] A switch during provider execution yields deterministic non-success or RecoveryRequired with retained usage evidence.
- [x] No current-profile DbContext or provider lease is cached across operations.

## Reopen triggers

- a new service reads LLM Chat data outside the scope
- profile-switch semantics change
- streaming retains a lease after terminal closure

## Progression decision

Unlock SB04 after this work unit passes, unless a checkpoint applies.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
