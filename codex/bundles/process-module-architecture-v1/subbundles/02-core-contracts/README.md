# SB02 Core And Contracts

## Status

Planned.

## Objective

Create the new generic contracts, abstractions, and core process kernel with no EF, Razor, UI, or domain-specific dependencies.

## Covered Inputs

- REQ-001
- REQ-002
- REQ-003
- REQ-005
- REQ-006

## Prerequisites

- SB01 complete.

## Exact Source References

- `bundle://architecture/01-target-solution.md`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Contracts`

## Deliverables

- `CanDoItAll.Processes.Contracts`
- `CanDoItAll.Processes.Abstractions`
- `CanDoItAll.Processes.Core`
- Strong typed IDs, definition models, instance-plan models, artifact contracts, branch contracts, event contracts, and invariant rules.

## Dependency Impact

- Every later subbundle depends on this boundary.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add project skeletons.
2. Define strongly typed IDs and generic enums.
3. Define definition, instance plan, runtime state, event, artifact, branch, and strategy contracts.
4. Port only pure rules that still match the new model.
5. Add architecture tests proving forbidden dependencies are absent.

## Scope Exceptions

No dispatcher, EF persistence, UI, or concrete drivers in this subbundle.

## Do Not Do

- Do not add `.NET`, Blazor, Office, browser, or marketing terms to core.
- Do not model agent-specific recovery in core.
- Do not reference EF or Razor.

## Acceptance Checklist

- Core builds independently.
- Core tests cover graph invariants, artifact slot rules, branch route rules, and loop budget primitives.
- Forbidden dependency tests pass.

## Proof Required

- Unit test transcript.
- Architecture dependency test transcript.
- Semantic Adequacy Gate.
- `proof/SB02/manifest.md` with source assertions and anti-stub audit output.
- Production Behavior Artifact Matrix for any new event/state/record contract.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB04, SB05, SB06, SB07, and SB08 cannot proceed until generic boundaries are proven.

## Suggested Agent Prompt

Build only the generic core and contracts. Any domain term in core is a blocker, not a convenience.
