# 01-validator-catalog-and-runtime-guardrails

## Status

- Status: `Completed`

## Closure Notes

- Product core and module service registrations now construct `WorkflowDefinitionValidator` with `IWorkflowExecutorCatalog`.
- `WorkflowTemplatePackLoader` can validate templates against the catalog when created by DI.
- Added a product-path regression test proving `IWorkflowCatalogService.SaveDefinitionAsync` rejects `missing.executor`.
- Proof manifest: `bundle://proof/SB01/manifest.md`
- Semantic invariants: `bundle://proof/SB01/semantic-invariants.md`

## Objective

Fix workflow validation so executor catalog, runnability, backend availability, and settings schema checks are active in save, import, publish, and test paths.

## Covered Inputs

- RN01: Runtime/catalog correctness must be fixed before executor expansion.
- RN05: Keep current MAF alignment honest and avoid over-claiming unsupported backends.
- R1: Reject unknown, planned, disabled, unavailable, or schema-invalid executors at save/import/publish/test time.
- R12: Keep durable backend honesty.

## Prerequisites

- Prepared-stage bundle gate passes.
- No production executor expansion has started.
- Current service registrations are inspected before editing.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentFrameworkHostingServiceCollectionTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`

## Scope

- Add failing-first tests for unknown executor validation through product registrations.
- Wire `WorkflowDefinitionValidator` with `IWorkflowExecutorCatalog` in core hosting and module hosting.
- Keep direct construction only in tests that intentionally validate graph-only behavior.
- Split graph and runtime capability validation only if dependency cycles require it.
- Verify unknown, planned, unavailable plugin, invalid settings, and unavailable backend cases.

## Dependency Impact

- SB02-SB10 depend on this gate because expanded executors must not be persisted or test-run when unavailable.
- If this gate is weak, later executor and UI proof can falsely pass while production save/import paths remain unsafe.

## Validation Depth

- Failing-first unit or integration proof for the currently weak DI path.
- Passing unit tests for validator behavior with catalog injection.
- Passing integration/API proof for persistent and in-memory catalog services where practical.
- Critical semantic proof with artifact-backed manifest and downstream smoke before SB02.

## Implementation Steps

1. Add a failing-first test that builds the real hosting/module service provider and saves or validates a workflow with an unknown executor.
2. Replace explicit `new WorkflowDefinitionValidator()` registrations with DI-aware construction that injects `IWorkflowExecutorCatalog`.
3. Update test hosts and template loaders only where they participate in save/import/test behavior.
4. Add or update tests for planned executor, unavailable plugin executor, invalid settings schema, and unavailable backend.
5. Capture source assertions and proof transcripts under `bundle://proof/SB01/`.

## Do Not Do

- Do not hide unknown executor failures until runtime dispatch.
- Do not silently fallback to graph-only validation in product paths.
- Do not implement new executors in SB01.
- Do not weaken unavailable durable backend validation.

## Acceptance Checklist

- Unknown executor IDs cannot be saved, imported, published, or test-run as active runnable definitions.
- Planned built-in executor IDs fail validation while still appearing as planned in catalog surfaces.
- Unavailable plugin executor IDs fail validation with actionable messages.
- Invalid executor settings fail before runtime dispatch.
- In-memory and persistent workflow catalog services share the same validation semantics.

## Proof Required

- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- Failing-first transcript for the pre-fix DI/catalog gap.
- Passing transcript for targeted validator, hosting, and API/catalog tests.
- Changed-file SHA-256 hashes and source assertions.
- Anti-stub audit transcript covering validator and DI registration changes.

## Browser Validation Logging

- N/A unless this subbundle changes browser-visible workflow validation messages; if it does, add a row in `bundle://reviews/01-execution-report.md` with route, viewport, actions, screenshots, and result.

## Progression Gate

- Continue to SB02 only after product-path validation rejects unknown/planned/unavailable/schema-invalid executor definitions and the SB01 proof manifest exists.

## Suggested Agent Prompt

Use SB01 to make catalog-backed validation mandatory in product paths. Prove the old DI path failed, then wire the validator correctly and run focused tests before adding any executor surface.
