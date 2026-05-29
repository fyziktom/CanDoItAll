# 01-validator-catalog-and-runtime-guardrails

## Objective

Fix workflow validation so executor catalog/runnability/settings checks are always active in save/import/publish/test paths.

## Required work

1. Add a failing-first test showing that a workflow with an unknown executor ID can currently pass validation when `WorkflowDefinitionValidator` is registered without `IWorkflowExecutorCatalog`.
2. Fix DI registrations:
   - `AddAgentFrameworkCore`
   - `AddAgentFrameworkModule`
   - test host registrations
3. If direct injection causes cycles, split validation:
   - graph validator
   - runtime capability validator
4. Validate:
   - unknown executor ID fails,
   - planned executor ID fails,
   - unavailable plugin executor fails,
   - invalid executor settings fail,
   - unavailable backend fails.
5. Ensure both in-memory and persistent catalog services use the same validation semantics.

## Source references

- `WorkflowDefinitionValidator` optional catalog constructor and `executorCatalog is not null` checks.
- `AgentFrameworkServiceCollectionExtensions` and `AgentFrameworkModuleServiceCollectionExtensions` explicit `new WorkflowDefinitionValidator()` registrations.
- `BuiltInWorkflowExecutorDescriptors.Planned`.

## Acceptance checklist

- Unknown/planned/unavailable executors cannot be saved as active runnable workflow definitions.
- Tests fail before fix and pass after fix.
- No runtime-only failure is used as substitute for catalog validation.
