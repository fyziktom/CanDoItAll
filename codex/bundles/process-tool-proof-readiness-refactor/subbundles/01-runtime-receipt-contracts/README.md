# 01-runtime-receipt-contracts

## Status

- `Completed`

## Objective

- Create the typed process-side contract and runtime receipt gate that distinguish allowed tools from required current-run proof.

## Success Criteria

- Process steps can declare required runtime tool and MCP receipts.
- Governed execution metadata carries the effective contract to MAF.
- Step finalization rejects or escalates a success outcome when required current-run receipts are missing.

## Covered Inputs

- R1 Typed Step Capability And Proof Contract.
- R3 Runtime Metadata And MAF Boundary.
- R4 Outcome Receipt Gate.
- Run `6f0d229f` evidence where QA recheck attached tools but produced no browser/image receipts.

## Prerequisites

- Bundle preparation validated.
- Existing process capability scope, execution metadata, and runtime receipt files reviewed before editing.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs`
- `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeStepAssignmentStore.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Metadata.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeCapabilityScopeModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.RuntimeToolReceipts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Capabilities/ProcessAllowedOperationsCapabilityPolicyCompiler.cs`
- `bundle://architecture/01-target-solution.md`

## Deliverables

- Strongly typed step proof contract and receipt requirement records.
- Contract compiler or normalizer for existing `ProcessCapabilityScope` plus new receipt requirements.
- Metadata adapter that carries required receipt data into governed AgentFramework execution.
- Receipt gate service that compares required receipts against recorded runtime/MCP tool receipts.
- Backward-compatible handling for existing assignments with empty scope.

## Dependency Impact

- `02-hr-capability-readiness` depends on this contract to evaluate selected agents and tool access.
- `03-manager-fallback-drivers` depends on receipt-gate diagnostics.
- `04-template-process-e2e` depends on contract persistence and metadata translation.

## Validation Depth

- Critical foundation.
- Unit and integration proof is required before downstream subbundles start.

## Implementation Steps

1. Add or evolve process contract records in the contracts layer for required capabilities, suppressed capabilities, and required receipts.
2. Add a process application service that compiles template, assignment, and driver data into an immutable effective step contract.
3. Extend assignment persistence and metadata building without breaking existing empty-scope assignments.
4. Extend MAF metadata models only with generic required-receipt data.
5. Implement a receipt gate that checks current-run tool receipts before final outcome acceptance.
6. Add focused unit tests for allow, deny, suppress, require, and missing-receipt cases.
7. Add integration tests around metadata translation and finalization rejection.

## Scope Exceptions

- Do not migrate process templates in this phase.
- Do not add HR dialog UI in this phase.
- Do not implement manager fallback strategy changes in this phase.

## Do Not Do

- Do not add software-delivery, QA, UI-design, or project-management prompt text to MAF workspace plugins.
- Do not infer required proof from unstructured prose.
- Do not accept stale upstream artifacts as current-run receipts.
- Do not rebuild runtime tool or MCP catalogs per attempt when existing planners can be reused.

## Acceptance Checklist

- A step can require `browser_take_screenshot` and `workspace_analyze_image` receipts as typed data.
- An outcome with missing required receipts is blocked or routed with a typed diagnostic.
- Existing process runs with empty scopes continue to run under existing behavior unless templates opt in.
- MAF changes remain generic and process-agnostic.
- Tests cover missing receipt, stale receipt, and matching current-run receipt cases.

## Proof Required

- `dotnet test` for the process contract/compiler test project or equivalent targeted test filter.
- `dotnet test` for AgentFramework metadata/receipt gate tests.
- Source proof showing no new process-template dependency from MAF projects.
- Negative test transcript where a claimed successful outcome without required receipts is rejected.

## Browser Validation Logging

- N/A for browser UI during this subbundle because it is runtime contract infrastructure.
- If implementation touches process UI projection, capture the affected route and screenshot in `reviews/01-execution-report.md`.

## Progression Gate

- Downstream work may start only after receipt requirements are typed, persisted or compiled, carried through metadata, and enforced by tests.

## C# Architecture Impact

- Adds a small contract/compiler/gate surface; avoids adding more policy branches to large MAF execution classes.

## Boundary Ownership

- Contracts live in process contracts/application layers; MAF owns only generic metadata and receipt evaluation hooks.

## Dependency Direction

- Process modules adapt process contracts to MAF models. MAF must not reference process templates or process modules.

## Pattern Decision

- Use compiler plus immutable value objects for contract construction and a policy evaluator for receipt gating.

## Testability Contract

- Compiler and gate must be testable with in-memory fixtures and no launched process, browser, or MCP server.

## Partial Class Policy

- Do not create new partial-class shards for new policy. Extract focused services instead.

## Architecture Proof Required

- Include dependency proof that no MAF project references process template projects.
- Include source proof that receipt enforcement is not prompt-only.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Build the typed process step capability/proof contract and required receipt gate, keep MAF generic, add focused tests for missing and satisfied current-run receipts, update the execution report, and stop if metadata or receipt enforcement cannot be proven.
```
