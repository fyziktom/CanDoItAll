# SB04 — Agent details section and session boundary

**Status:** Blocked by SB03  
**Outcome:** Agent details has a stable typed section and explicit editor session/load
boundary; tests can render the real dialog without private state mutation.

## Owned requirements

R-023–R-024, R-034, partial R-035–R-036, R-043, R-052.

## Prerequisites and reopen triggers

Checkpoint B accepted. Reopen if section ownership cannot be reported to the page or
session duplicates stable existing models.

## Work

1. Add `AgentDetailsSection` and explicit section-order mapper for the current ten labels.
2. Add section parameter/callback and page-owned `AgentDetailsRequest`; default remains
   Identity. Do not expose raw index as semantic API.
3. Add `AgentEditorLoadRequest`, `AgentEditorSession`, and the
   `IAgentEditorController` contract/implementation with initial load behavior.
4. Add optional `InitialSession` so tests/scenarios bypass external load without
   subclassing or private reflection.
5. Move existing/new editor construction and initial agents/providers/capabilities/secrets
   load behind the controller. Preserve provider/secret partial errors.
6. Keep lazy project load and commands temporarily until SB05, but route all newly moved
   load behavior through the controller.
7. Create the shared details test harness and migrate representative section/load cases
   first, including numeric-index removal.

## C# Architecture Impact

Establishes the editor boundary without prematurely splitting ten section components.

## Boundary Ownership

Page owns details target/section outside the dialog; dialog owns current draft/rendering;
controller owns initial external load.

## Dependency Direction

Dialog -> editor controller/session; controller -> existing application services.

## Pattern Decision

PSR-04 and load portion of PSR-05.

## Testability Contract

Real dialog rendered with session + fake controller. No test subclass/private fields for
migrated cases.

## Partial Class Policy

No new partial or per-section code-behind split.

## Architecture Proof Required

- stable section mapping tests;
- initial session render proof;
- partial failure proof;
- first migrated tests have no reflection/uninitialized services;
- no label/order change.

## Non-goals

No command migration yet beyond what is required for coherent compilation; no routed page,
no section wrappers, no visual changes.

## Progression gate

Typed section/session is stable, representative tests use public seams, and load behavior
matches baseline.
