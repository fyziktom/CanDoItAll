# SB07 Project Boundary And DI Hardening

## Status

- `Ready after SB03, SB05, and SB06`

## Objective

Audit and harden dependency direction, DI registration, service-locator usage, lifetime correctness, and project boundaries after the major extractions.

## Success Criteria

- Extracted services resolve through DI with scope validation.
- Core behavior no longer uses `IServiceProvider` as a service locator.
- Any project-reference change has before/after proof and no cycles.
- Architecture guard tests prevent partial/runtime growth regressions.

## Covered Inputs

- R09, R10, R11, R12.

## Prerequisites

- SB03, SB05, and SB06 closure.
- All extracted owner tests are passing.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeServiceCollectionExtensions.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`

## Deliverables

- Updated DI registration for extracted services.
- Dependency graph audit.
- Service-locator source assertions.
- Scope/lifetime validation smoke.
- Project-boundary decision record if new projects are needed.

## Dependency Impact

- SB08 final proof depends on this gate.
- Without DI/dependency hardening, isolated types may exist but production can still bypass them.

## Validation Depth

- Critical closure.

## Implementation Steps

1. Inspect all changed constructors and service registrations.
2. Remove or justify `IServiceProvider` dependencies in core behavior.
3. Run dependency graph audit and read affected `.csproj` files.
4. Add DI composition tests with scope validation.
5. Add source assertions for forbidden service-locator and partial-class patterns.
6. Record CodeAnalytics dependency/cycle proof.

## Scope Exceptions

- It is acceptable for composition roots and narrow SDK factories to use `IServiceProvider` when explicitly justified.

## Do Not Do

- Do not move code to `SharedKernel` or `Common` to avoid cycles.
- Do not add a new project only for file organization.
- Do not make interfaces for trivial single-use types unless tests or DI require them.

## C# Architecture Impact

Turns extracted classes into production-wired architecture, not isolated dead code.

## Boundary Ownership

Composition roots register. Runtime services own behavior. Contracts stay SDK-free where introduced.

## Dependency Direction

Must match `architecture/02-csharp-dependency-direction.md`.

## Pattern Decision

Composition-root extension methods and typed factories only where justified.

## Testability Contract

Tests verify DI resolves extracted services and production runtime uses them.

## Partial Class Policy

Guard tests block new runtime/composer partials.

## Architecture Proof Required

- `.csproj` before/after table.
- CodeAnalytics dependency/cycle result.
- DI registration smoke.
- Source assertions for service locator and partials.

## Acceptance Checklist

- [ ] DI registration matches architecture map.
- [ ] Scope validation passes.
- [ ] Dependency graph is acyclic.
- [ ] No broad manager/helper classes introduced.

## Proof Required

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- build and DI test transcript.
- dependency/cycle proof.
- source assertion transcript.

## Browser Validation Logging

- N/A. Backend composition only.

## Progression Gate

- SB08 may start only after dependency direction and DI proof pass.

## Suggested Agent Prompt

```text
Execute SB07 only. Harden DI and dependency direction after extraction, add guard tests, and prove production wiring uses the extracted services.
```
