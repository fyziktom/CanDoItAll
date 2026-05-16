# Phase Plan For Codex

## Phase 0: Patch Readiness Audit

1. Inspect the current `cognitive-memory-architecture-v2` bundle.
2. Run a normal contract consistency audit before adding new Self-Regulation contracts and enum values.
3. Confirm current file numbering and choose final new file numbers.
4. Do not begin implementation; this is an architecture patch.

## Phase 1: Add Self-Regulation Architecture Files

Add new architecture files for Cognitive Self-Regulation, Self-Model and Epistemic Identity, Calibration Health and Probing Training, and Professor Review and Escalation.

## Phase 2: Patch Contracts

Add `CognitiveMemory.SelfRegulationContracts.cs`. Update score geometry, neuro patch, and probing contracts.

## Phase 3: Patch Requirements And Acceptance Criteria

Add FR-055 through FR-061 and NFR-034 through NFR-038 or equivalent next available IDs.

## Phase 4: Patch Subbundles

Add subbundles 21-26 and update existing dependencies, especially `19-metamemory-abstention-calibration`.

## Phase 5: Patch Validation

Add tests for self-model scoping, known failure pattern matching, humility triggers, posture selection, calibration health, professor review governance, scalar-only rejection, and policy bypass negative cases.

## Phase 6: Architecture Closure Review

Verify no behavior-affecting scalar-only self-regulation, no self-regulation direct truth mutation, no professor review direct truth mutation, no access/redaction bypass, no prompt-persona self-model, and no anthropomorphic consciousness claims.
