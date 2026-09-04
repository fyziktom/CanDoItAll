# SB05 — Agent editor command boundary

**Status:** Blocked by SB04  
**Outcome:** All external save/delete/reference/capability workflows leave
`AgentDetailsDialog`; only the editor controller plus justified host presentation remains.

## Owned requirements

R-034–R-038, R-042–R-045, related testability requirements.

## Prerequisites and reopen triggers

SB04 accepted. Reopen if controller becomes a service bag, error semantics collapse, or a
fourth interface appears necessary.

## Work

1. Complete `IAgentEditorController` workflows for lazy projects, provider/capability
   refresh, save canonicalization, delete, capability assignment, and verification.
2. Move external-root/project/workspace/image/provider normalization required for
   persistence out of Razor while retaining field-level draft presentation logic locally.
3. Refactor dialog calls to typed controller requests/results.
4. Remove direct injections of Workspace, provider administration, Projects, Secrets, and
   external-target registry from the dialog.
5. Preserve confirmation, notifications, DialogReference/Saved result semantics, managed
   deletion protection, partial failures, and retry behavior.
6. Directly test controller workflows without duplicating every field-level component
   assertion.
7. Migrate remaining details test classes to the shared public-seam harness.

## C# Architecture Impact

Completes the cohesive editor workflow boundary and removes cross-module/infrastructure
knowledge from Razor.

## Boundary Ownership

Dialog: draft/presentation/host confirmation. Controller: all external operations and
save canonicalization.

## Dependency Direction

Dialog -> `IAgentEditorController`; implementation -> existing services. No reverse UI
dependency.

## Pattern Decision

PSR-05. No per-service UI ports unless execution stops for an approved addendum.

## Testability Contract

Direct controller cases plus existing component behavior through fake controller.

## Partial Class Policy

No new partial. Responsibility leaves the current `.razor.cs` rather than being moved to
another partial.

## Architecture Proof Required

- forbidden dialog dependencies absent;
- old service calls absent;
- direct controller tests for save/delete/partial/lazy/capability workflows;
- behavior tests green;
- Checkpoint C approval.

## Non-goals

No section component split, provider workspace refactor, route migration, or project move.

## Progression gate

Checkpoint C passes and all details behavior tests are green through public seams.
