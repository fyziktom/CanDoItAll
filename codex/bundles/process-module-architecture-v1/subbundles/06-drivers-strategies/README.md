# SB06 Drivers And Strategies

## Status

Planned.

## Objective

Build the layered driver system and strategy factories that keep domain behavior outside the generic core/runtime.

## Covered Inputs

- REQ-006
- REQ-007
- REQ-008
- REQ-009

## Prerequisites

- SB02 complete.
- SB04 and SB05 interfaces available.

## Exact Source References

- `bundle://architecture/01-target-solution.md`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs`

## Deliverables

- New driver abstractions.
- Driver catalog.
- Driver stack selector.
- Strategy registry.
- Representative general software-development driver.
- Representative .NET driver.
- Representative verification driver migration.

## Dependency Impact

- Builder and runtime depend on drivers to supply strategies without core domain leaks.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Define driver descriptors and capability tags.
2. Define driver layers and selection rules.
3. Define strategy factory contracts.
4. Port verification drivers behind new descriptor model.
5. Add representative layered driver stack tests.
6. Add negative tests proving core has no domain vocabulary.

## Scope Exceptions

Do not build every domain driver in this subbundle. Prove the mechanism with representative drivers.

## Do Not Do

- Do not put domain enums in Process Core.
- Do not hardcode driver selection in dispatcher.
- Do not let drivers mutate runtime state directly.

## Acceptance Checklist

- A layered driver stack can be selected for a run.
- Drivers provide strategies through a registry.
- Domain-specific diagnostics remain behind driver contracts.
- Existing verification behavior is preserved where ported.

## Proof Required

- Driver selection tests.
- Strategy factory tests.
- Core boundary negative tests.
- Semantic Adequacy Gate.
- `proof/SB06/manifest.md`.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB07 requires driver-provided recovery and manager strategies.

## Suggested Agent Prompt

Implement drivers as domain extension packs. The core sees capabilities and strategies, not domain names.
