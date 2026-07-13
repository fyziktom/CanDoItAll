# SB16 DotNet Quality Repair Subprocess

## Status

- `In Progress`

## Objective

Replace monolithic software-delivery quality repair with a typed runtime-owned .NET subprocess that diagnoses exact failed evidence before mutation and uses independent validation plus one bounded bughunt repair before no-go escalation.

## Covered Inputs

- `bundle://inputs/04-persistent-repair-and-four-app-e2e-request.md`
- SB15 persistent retry result.

## Prerequisites

- SB15 progression gate passes.
- Existing subprocess bridge and template compatibility tests are green.

## Exact Source References

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/software-delivery/steps/quality-repair.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/Drivers/DotNet/DotNetSoftwareDeliverySubprocessContractProvider.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessDefinitionCatalogProjectionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessMafHardeningRegressionTests.cs`

## Deliverables

- New `dotnet-quality-repair` template with manager diagnosis, developer repair, independent QA, bughunt diagnosis, second repair, recheck, accepted handoff, and no-go packet.
- Runtime-owned `software-delivery/quality-repair` parent contract and .NET driver mapping.
- Generic instructions that work for UI and non-UI .NET products and never mention sample applications.

## Dependency Impact

- Templates depend only on existing process contracts and operations.
- Modules.Processes .NET driver depends on generic subprocess contracts; no reverse dependency is introduced.

## Validation Depth

- Critical template scanner, catalog projection, subprocess mapping/bridge, architecture, and focused process tests.

## Implementation Steps

1. Add failing template/contract projection tests for the runtime-owned parent and child graph.
2. Author the `dotnet-quality-repair` definition and step instructions with typed artifacts and finite branches.
3. Map `software-delivery/quality-repair` through the existing .NET subprocess contract provider.
4. Restrict parent operations to launch/observe and wire accepted/no-go child artifacts.
5. Run template compatibility, subprocess bridge, architecture, and regression tests.

## C# Architecture Impact

- One focused mapping method in the existing .NET subprocess contract provider.
- Process complexity belongs in declarative templates, not in runtime branches.

## Pattern Decision

- PSR-07.

## Do Not Do

- Do not teach generic runtime about QA, browser errors, Blazor, or repair roles.
- Do not let a product-mutating step accept its own change.
- Do not treat known failed proof as residual risk.
- Do not require browser proof for non-UI .NET products.

## Acceptance Checklist

- Parent launches/observes only.
- Exact failed evidence is a required diagnosis input.
- Every mutation is followed by independent current-execution proof.
- Same unresolved proof flows to bughunt, not blind retry.
- Human/no-go escalation occurs only after bounded diagnosis-guided re-repair.

## Proof Required

- `bundle://proof/SB16/manifest.md`
- `bundle://proof/SB16/semantic-invariants.md`
- Template scan, branch graph, focused tests, source assertions, changed-file hashes, and anti-stub audit.

## Browser Validation Logging

- Template tests do not require a browser. SB17 must record viewport, route, interaction, screenshot, snapshot, console, startup, and cleanup evidence for UI repair paths.

## Progression Gate

- SB17 may start only after templates seed/load on 5032 and all focused tests/build pass.

## Suggested Agent Prompt

Create a typed .NET quality-repair subprocess that separates diagnosis, mutation, independent validation, bughunt, bounded re-repair, and no-go evidence while leaving generic runtime untouched.
