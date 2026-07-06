# Modular Extraction Playbook

Use this playbook for large classes, partial-class clusters, runtime monoliths, and scattered tool/provider definitions.

## Phase 0: Inventory

Before editing, create a responsibility inventory:

- source file
- class or partial segment
- methods and nested types
- dependencies used
- responsibility category
- external SDK or infrastructure touchpoints
- likely target type/project
- existing tests
- missing tests
- risk level

## Phase 1: Characterization

Add or identify tests that lock down the behavior being moved. If the current behavior is wrong and will be intentionally changed, write failing-first tests for the desired behavior and note that they replace characterization.

## Phase 2: Extract contracts

Move stable interfaces, DTOs, options, and result records into an Abstractions or Contracts project. Keep contracts free of provider SDK types, EF types, UI types, and concrete runtime classes.

## Phase 3: Extract implementation

Create cohesive top-level implementation types. Use one public responsibility per type. Avoid creating a new generic manager that owns all old behavior.

## Phase 4: Add construction seam

Use an extension method, factory, builder, or catalog provider so the composition root can wire implementations without the old runtime knowing every concrete type.

## Phase 5: Wire caller through abstraction

The old caller should depend on the extracted abstraction or facade. It should delegate and stay thin. It should not contain fallback copies of the old logic.

## Phase 6: Delete or shrink old code

Remove moved logic from the old class. If a temporary bridge remains, mark it with a removal subbundle and test that behavior goes through the new path.

## Phase 7: Validate dependency direction

Run project reference checks and build. Reject cycles and implementation references from contracts/core.

## Phase 8: Prove extension seam

Add a small fake or sample provider/tool/driver in tests to prove the old runtime does not need another partial class edit for the next extension.

## Phase 9: Architecture review

Run the architecture review gate. Do not close the subbundle if the old large class still owns the responsibility.
