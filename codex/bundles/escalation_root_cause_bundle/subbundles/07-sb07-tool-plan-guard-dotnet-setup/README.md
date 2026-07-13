# SB07 - Tool Plan Guard For .NET Setup

## Status

- `Completed`
- Critical foundation: yes

## Objective

Add a typed tool-plan guard for deterministic .NET solution setup so scaffold/wire/readback work cannot be declared complete without exact required receipts, resolved paths, side-effect manifests, and idempotency checks. This phase guards the existing plan; it does not yet replace it with a runtime-owned executor.

## Covered Inputs

- GPTPro runtime-owned .NET setup plan and tool preflight findings.
- REQ-011, REQ-013, REQ-014, REQ-015, REQ-017, REQ-018, REQ-020.
- Incident missing `workspace_pwsh_run_script` helper receipt.

## Prerequisites

- SB01 resolved script refs complete.
- SB02 required receipt aggregation complete.
- Current .NET setup template references refreshed.

## Exact Source References

- `bundle://codex/07-runtime-owned-dotnet-solution-setup-plan.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeToolPreflightService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/steps/create-dotnet-project.md`
- `repo://Templates/Processes/processes/dotnet-solution-setup/steps/add-test-project.md`
- `repo://Templates/Capabilities/tools.json`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeToolPreflightServiceTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/DotNetProcessLaunchVariableContributorTests.cs`

## Deliverables

- Typed .NET setup tool-plan model for create project, add test project, wire solution, and readback verification.
- Guard checks for tool name, arguments, resolved script ref, product root, target paths, idempotency, and side-effect manifest.
- Required receipt plan that treats `workspace_dotnet_new` as scaffold proof and `workspace_pwsh_run_script` as solution membership wiring proof.
- Preflight failures before agent execution when tool-critical details are invalid.
- Tests proving scaffold receipts alone are insufficient.

## Dependency Impact

- SB08 uses the guard model when defining template schema execution contracts.
- SB10 uses guard metadata for capability-aware assignment.
- SB11 builds runtime-owned executor behavior on top of this proven plan.
- SB12 validates the incident repair path.

## Validation Depth

- Critical foundation with unit tests, template fixture tests, and negative shallow-pass tests.
- Semantic proof must show the guard catches the exact missing helper class before false completion.

## Implementation Steps

1. Inventory the current .NET setup launch variables, scripts, side-effect manifests, and template requirements.
2. Define typed plan records for scaffold, wire solution, add test project, and readback operations.
3. Validate resolved script refs and reject unresolved placeholders.
4. Validate required tools and exact argument/path shape.
5. Validate side-effect manifest presence and expected mutations.
6. Validate idempotency: existing project folder means do not rerun scaffold blindly; run/verify the missing wire step.
7. Extend preflight service beyond tool-name availability.
8. Add tests where `workspace_dotnet_new` exists but `workspace_pwsh_run_script` is absent.
9. Add tests for invalid script path, wrong product root, wrong tool scope, and missing manifest.
10. Keep executor implementation for SB11; this phase only guards and validates the plan.

## Do Not Do

- Do not implement the runtime-owned executor in this phase.
- Do not treat `workspace_dotnet_new` as proof that the project is in the solution.
- Do not rely on prompt instructions as the guard.
- Do not allow unresolved script refs to pass preflight.

## Acceptance Checklist

- [x] .NET setup plan has typed create/wire/readback operations.
- [x] Missing helper receipt fails guard or completion gates.
- [x] Scaffold-only proof is rejected.
- [x] Guard runs before agent execution where possible.
- [x] Idempotent repair guidance is encoded in typed plan metadata.
- [x] Tests cover wrong path/scope/manifest.

## Proof Required

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- Failing-first scaffold-only false completion test.
- Passing preflight and plan validation tests.
- Source assertions for exact tool/path/scope checks.
- Production Behavior Artifact Matrix if new tool plan records are introduced.

## Browser Validation Logging

- `N/A`; no browser surface is changed.

## Progression Gate

- SB08, SB10, and SB11 may proceed only after deterministic .NET setup has a typed guard and missing helper receipt remains a hard failure.

## C# Architecture Impact

Introduces typed deterministic tool-plan contracts and richer preflight behavior.

## Boundary Ownership

Preflight belongs in process runtime/integration; Workbench only supplies .NET setup context.

## Dependency Direction

Tool-plan contracts must not make Workbench a dependency of runtime or template validation.

## Pattern Decision

Use PSR-007: command records for deterministic tool actions plus guard service.

## Testability Contract

Guard tests must use plain plan records and fake receipts, not live `dotnet` execution.

## Partial Class Policy

No adapter partial expansion unless required to feed receipts into the guard.

## Architecture Proof Required

- Explain contract placement.
- Confirm no runtime dependency on Workbench implementation.

## Suggested Agent Prompt

```text
Execute SB07 only. Add a typed tool-plan guard for .NET solution setup and exact preflight checks. Prove workspace_dotnet_new without workspace_pwsh_run_script cannot pass solution setup completion. Do not build the runtime-owned executor yet.
```
