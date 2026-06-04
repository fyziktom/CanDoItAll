# Normalized Requirements

## RQ-001: Preserve previous provider decoupling

- Requirement: MAF must not regain direct product-tool references to Processes/Projects/Workbench.
- Owning subbundle(s): SB01, SB04, SB07, SB12

## RQ-002: No premature Process Core split

- Requirement: Do not move broad runtime behavior or EF entities into a new core project.
- Owning subbundle(s): SB01, SB08, SB12

## RQ-003: Inventory process boundary

- Requirement: Produce source-backed inventory of dispatcher partials and AgentFramework usages.
- Owning subbundle(s): SB02

## RQ-004: Design execution seam

- Requirement: Define a small process automation execution client/facade and migration cutline.
- Owning subbundle(s): SB03

## RQ-005: Architecture guards before movement

- Requirement: Add/extend tests and scans before production movement starts.
- Owning subbundle(s): SB04

## RQ-006: Introduce execution facade

- Requirement: Add the process automation execution client/facade with parity behavior.
- Owning subbundle(s): SB05

## RQ-007: Move direct execution calls

- Requirement: Move direct ExecuteRun/GetDetail/adoption/recovery calls from dispatcher execution path behind facade.
- Owning subbundle(s): SB06

## RQ-008: Prove dispatcher coupling reduction

- Requirement: Add source scans proving direct execution calls have moved.
- Owning subbundle(s): SB07

## RQ-009: Minimal contracts foundation

- Requirement: Create only minimal stable process contracts/abstractions needed for future extraction.
- Owning subbundle(s): SB08

## RQ-010: Preserve receipt and required-tool semantics

- Requirement: Receipt projection, required-tool validation, and artifact lineage must not regress.
- Owning subbundle(s): SB09

## RQ-011: Refactor checkpoints

- Requirement: Run deeper refactor reviews after SB03, SB07, and SB10.
- Owning subbundle(s): SB04, SB07, SB10

## RQ-012: Runtime smoke

- Requirement: Run targeted unit, process-filtered integration, and full solution build proof.
- Owning subbundle(s): SB11, SB12

## RQ-013: Large-screen-only validation

- Requirement: Do not run small/medium/mobile browser validation; use large-screen only if UI is touched.
- Owning subbundle(s): All SBs

## RQ-014: Next-phase cutline

- Requirement: End with a precise cutline for actual Process Core extraction.
- Owning subbundle(s): SB12
