# SB06 — Rewrite tests and remove shape coupling

**Status:** Blocked by SB05  
**Outcome:** The entire target test slice uses durable public behavior/boundary seams;
private reflection, numeric tab seeding, uninitialized services, and incidental source
shape are absent.

## Owned requirements

R-050–R-055 and completion proof for R-020–R-045.

## Prerequisites and reopen triggers

Checkpoints A–C accepted. Reopen earlier owners if tests reveal duplicate state or hidden
I/O; do not add test-only backdoors.

## Work

1. Complete migration of all 46 primary component cases.
2. Preserve behavior coverage; replace private-shape cases one-for-one.
3. Consolidate details setup into one shared test harness using
   `AgentEditorSession`, typed section, and fake controller.
4. Rewrite the adjacent Workflows case through public click/navigation behavior.
5. Add/finalize the prepared 18 direct seam unit cases.
6. Add durable forbidden-injection tests that check dependency categories, not exact
   counts/private members/source syntax.
7. Run temporary forbidden-pattern checks and exact discovery.
8. Remove obsolete helper types/usings made unnecessary by the public seams.

## C# Architecture Impact

Makes the new boundary enforceable without freezing its implementation shape.

## Boundary Ownership

Tests target state, intents, workflows, and user-visible results only.

## Dependency Direction

Test fakes implement the three public seams; they do not reconstruct the full runtime for
component rendering.

## Pattern Decision

PSR-06.

## Testability Contract

- primary component discovery remains 46 unless pre-approved and explained;
- route discovery remains 10;
- new seam discovery is the frozen SB02 count (prepared 18);
- rewritten Workflows case expected discovery 1;
- no target private reflection/uninitialized-service match.

## Partial Class Policy

No test asserts partial/file/private member counts.

## Architecture Proof Required

- exact list/execution transcripts;
- forbidden-pattern transcript;
- direct boundary tests;
- no test-only production API or subclass hook;
- Checkpoint D approval.

## Non-goals

No cleanup of unrelated test classes or global test infrastructure.

## Progression gate

Checkpoint D passes and all focused tests are green from refreshed assemblies.
