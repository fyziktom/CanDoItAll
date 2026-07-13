# SB03 - Workflow Core Services Extraction

## Status

- `Completed`

## Objective

Move workflow core services out of catch-all AgentFramework Core into workflow-owned projects while preserving validation, catalog, routing, preview simulation, payload policy, failure formatting, process bridge, and descriptor-facing behavior.

## Success Criteria

- Workflow core services compile from workflow-owned projects and consume SB02 contracts/builders.
- Existing workflow validation, catalog queries, routing compilation, preview simulation, failure formatting, payload policy, and process bridge behavior are preserved.
- Validation and catalog failures produce typed, repairable diagnostics without relying on exception-string parsing as the primary contract.
- Host registration consumes the extracted services through explicit workflow registration, not ad hoc MAF/Core registration.
- Tests prove behavior parity for valid and invalid definitions.

## Covered Inputs

- R05, R06, R07, R10, R11, R13, R14, R15, R17.
- Architect note that MAF wrapper became too large.
- Architect note that logical blocks must close with hardening.

## Prerequisites

- SB01 accepted.
- SB02 completed with passing abstraction/builder proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Core\WorkflowDefinitionValidator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Core\WorkflowCatalogServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Core\WorkflowRoutingCompiler.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Core\WorkflowPreviewSimulationRenderer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Core\WorkflowPayloadPolicyService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Core\WorkflowFailureDisplayFormatter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Core\WorkflowProcessExecutorBridge.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`

## Deliverables

- `CanDoItAll.AgentFramework.Workflows.Core` project for workflow validators, catalog services, routing compiler, payload policies, preview renderer, typed diagnostic mapping/display helpers, and process bridge.
- Focused registration extension for workflow core services.
- Unit tests for validator, routing, catalog, payload policy, diagnostic mapping/display, and process bridge behavior.
- Migration notes for any services intentionally left behind.

## Dependency Impact

- SB05 hardening depends directly on this extraction. SB10 template loading and SB11 MAF adapter will consume these core services. Any accidental dependency on MAF, Blazor module types, plugin implementation types, or persistence details will block the base-up graph.

## Validation Depth

- `Critical foundation`
- Unit, service-composition, compatibility, and architecture-boundary tests.

## Implementation Steps

1. Move core workflow service classes into the workflow core project according to SB01 target ownership.
2. Keep constructor signatures explicit and strongly typed; avoid adding service locators or optional fallbacks.
3. Update DI registration to expose workflow core registration separately from global AgentFramework hosting.
4. Port or add tests that exercise existing validation and routing outcomes.
5. Add negative tests for malformed workflows, unknown executor references, invalid settings JSON, invalid routing, and unsafe retry policy diagnostics.
6. Run focused build/test commands for affected projects.
7. Update bundle execution report and proof manifests.

## Scope Exceptions

- Runtime manager, checkpoint stores, artifact stores, backend catalog, and external request runtime are SB04.
- Executor catalog, invoker, descriptors, observability, and executor helpers are SB06.

## Do Not Do

- Do not alter workflow validation semantics unless the change is recorded as an explicit bug fix with tests.
- Do not add silent recovery for missing executors or invalid graph edges.
- Do not collapse core services into a generic utility project.
- Do not keep `WorkflowFailureDisplayFormatter` string parsing as the only way to create a user-facing failure summary.

## Acceptance Checklist

- [x] Workflow core project compiles without MAF or UI references.
- [x] Validation and routing parity tests pass.
- [x] Failure diagnostics remain explicit, actionable, typed, and redacted.
- [x] Host registration references the workflow core extension.
- [x] No old workflow core classes remain duplicated in `AgentFramework.Core\Workflows`.

## Execution Notes

- Added `CanDoItAll.AgentFramework.Workflows.Core` for workflow validator, catalog services, routing compiler, preview renderer, payload policy, failure display helpers, diagnostic mapping, process bridge, and workflow core DI registration.
- Moved SB03 implementation files out of `CanDoItAll.AgentFramework.Core\Workflows` while preserving the `CanDoItAll.AgentFramework.Core` namespace to keep source churn contained during the base-up migration.
- Added `WorkflowCoreServiceCollectionExtensions` with shared workflow core registration and in-memory catalog registration.
- Updated Hosting and `CanDoItAll.Modules.AgentFramework` to call `AddWorkflowCoreServices()`; Hosting also calls `AddInMemoryWorkflowCatalogServices()`, while the module keeps its persistent catalog/run-store registrations.
- Added typed validation diagnostic mapping through `WorkflowFailureDiagnosticMapper`; validation failures preserve exact `InvalidOperationException` compatibility and attach typed diagnostics under the constant `WorkflowFailureDiagnosticMapper.ExceptionDataKey`.
- Left runtime manager, run/checkpoint/artifact/external-request stores, execution backend contracts, and executor contracts in their current projects for SB04 and SB06; moving those in SB03 would create avoidable cycles and exceed the subbundle boundary.
- Added focused unit coverage in `tests/CanDoItAll.Tests.Unit/WorkflowCoreExtractionTests.cs`.

## Proof Required

- `proof/SB03/manifest.md` with changed file hashes, build/test transcripts, and DI registration proof.
- `proof/SB03/semantic-invariants.md` covering validation parity, routing parity, typed failure diagnostics, redaction, repair hints, and no hidden fallback.
- Semantic Adequacy Gate proof with adversarial invalid graph cases, positive existing-template cases, and anti-stub audit.

## Browser Validation Logging

- `N/A`. This is backend service extraction.

## Progression Gate

- SB05 cannot begin until SB03 and SB04 are both complete. SB10 and SB11 cannot consume workflow core services until SB05 hardens this foundation.

## Suggested Agent Prompt

```text
Implement SB03 only. Extract workflow core services to the workflow-owned core project using SB02 contracts and builders. Preserve behavior, add parity and negative tests, update DI registration, and capture Semantic Adequacy Gate proof. Do not move runtime/store or executor implementation code.
```
