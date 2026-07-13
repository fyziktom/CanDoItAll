# SB02 - Contracts And Boundary Seams

## Status

- Status: `Completed`

## Objective

Create narrow contract seams and dependency boundaries for later extractions.

## Covered Inputs

- User requirement for smaller testable responsibilities.
- C# architecture boundary rules.
- GPTPro requirement for typed runtime/tool-plan/receipt contracts.

## Prerequisites

- SB01 baseline complete.
- Current `.csproj` references inspected.

## Exact Source References

- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/Processes/CanDoItAll.Processes.Contracts`
- `repo://src/Processes/CanDoItAll.Processes.Runtime`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration`

## Dependency Impact

- Likely contract/interface additions.
- Any project reference change requires before/after proof and CodeAnalytics.

## Validation Depth

- Build.
- Direct contract tests.
- Unsupported-policy negative tests.
- Dependency/cycle proof.

## Do Not Do

- Do not create broad common projects.
- Do not introduce service locator.
- Do not move implementations into contract projects.

## Acceptance Checklist

- [ ] Contract locations justified.
- [ ] No cycles.
- [ ] No reversed references.
- [ ] Unsupported cases fail explicitly.

## Proof Required

- Proof manifest with changed `.csproj` files.
- Dependency proof.
- Test transcript.
- Source assertions.

## Browser Validation Logging

- Not applicable.

## Progression Gate

- SB03 through SB06 may start only after contract boundaries pass.

## Suggested Agent Prompt

Implement SB02 only. Add the smallest real contract seams needed for later extraction and prove dependency direction.

## Goal

Introduce the smallest correct contract seams needed to extract responsibilities without creating cycles or fake abstractions.

## Scope

- Decide exact project placement for completion gate contracts, receipt expectation contracts, runtime-owned step executor contracts, lifecycle fact extraction contracts, managed artifact seams, and subprocess state resolver contracts.
- Extend existing process driver abstractions where domain-specific behavior must plug in.
- Wire composition without moving behavior prematurely.

## Implementation Steps

1. Inspect affected `.csproj` references before editing.
2. Decide each contract location using `architecture/01-csharp-boundary-map.md`.
3. Add stable records to `Processes.Contracts` only when shared across runtime/templates/modules/drivers.
4. Add driver extension interfaces to `Processes.Drivers.Abstractions` for driver-owned behavior:
   - runtime-owned step executor policy,
   - domain receipt expectation resolver/classifier,
   - recovery advice provider extensions if needed,
   - tool lifecycle fact extractor registration if driver-owned.
5. Keep MAF-specific interfaces in `Modules.Processes` when they are not reusable outside MAF integration.
6. Add DI registration for no-op/generic implementations only where they are real policies and tested.
7. Add unit tests for contract validation and unsupported-policy behavior.
8. Run build and CodeAnalytics dependency/cycle check.

## C# Architecture Impact

This subbundle defines the compile-time boundaries that all later extractions depend on. It is a critical foundation phase.

## Boundary Ownership

Expected:

- `Processes.Contracts`: stable typed data.
- `Processes.Drivers.Abstractions`: driver extension interfaces.
- `Modules.Processes`: MAF-specific integration seams.
- `AgentFramework.Core`: only generic tool lifecycle extractor abstraction if receipt writer owns generic extraction.

## Dependency Direction

Allowed:

- `Modules.Processes -> Drivers.Abstractions`
- `Runtime -> Drivers.Abstractions`
- domain implementations -> `Drivers.Abstractions`

Forbidden:

- `Drivers.Abstractions -> Modules.Processes`
- `Runtime -> Modules.Processes`
- `Runtime -> concrete domain driver`
- `AgentFramework.Core -> Modules.Processes`

## Pattern Decision

Use Strategy/Factory via driver catalog for domain behavior. Use Adapter for MAF boundary. Do not add abstract factories unless multiple related implementation products must be created together.

## Testability Contract

Tests must prove:

- Unsupported driver/policy fails explicitly.
- Contracts are usable without constructing the adapter.
- DI can resolve generic/default policies without `BuildServiceProvider` during registration.

## Partial Class Policy

No new partials. If contracts are introduced but behavior remains in adapter pending later subbundles, proof must mark it as temporary and reference the dependent subbundle that removes it.

## Architecture Proof Required

- Before/after project reference list.
- CodeAnalytics dependency/cycle result.
- Contract source assertions: no implementation/module/UI references.
- Unit tests for contract validation and unsupported policy behavior.
- Proof that no new adapter partial files were added.
