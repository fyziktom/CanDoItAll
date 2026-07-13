# Structured Input

## Desired End State

Workflows, workflow nodes, runtime services, builders/factories, templates, stores, and executor families are no longer hidden inside MAF, Core, or Blazor modules. They live in dedicated projects with explicit contracts, narrowly scoped implementations, testable composition, and plugin-compatible executor extension points. MAF consumes the workflow system through an adapter layer.

## Hard Requirements

- Workflow abstractions, builders, factories, core services, runtime services, stores, and executor contracts must be isolated into workflow-owned projects.
- Executor abstractions/helpers must be separated from executor implementations.
- Default executors must be grouped into logical implementation categories.
- Plugin-provided executors must remain first-class and must not lose grant, trust, source, OAuth/secret, host-command, side-effect, simulation, or deterministic test-mode behavior.
- New projects must be introduced and tested from base dependencies upward before runtime adoption.
- MAF reconnection must occur only after foundation, executor, plugin, template, and runtime hardening gates pass.
- UI, API, templates, project-structure workflow nodes, process/workflow regression paths, and tests must be updated after backend contracts are stable.
- Each logical block must close through a forced refactoring-hardening checkpoint.
- No silent fallback mechanisms may hide missing executor registrations, template errors, plugin grant failures, or runtime adapter failures.

## Assumptions

- Existing typed workflow model records in `CanDoItAll.AgentFramework.Models` may remain there unless execution proves they block clean project boundaries.
- Existing executor ids and template keys are compatibility contracts.
- MAF remains responsible only for Microsoft Agents workflow compilation/backend integration, not for default executor ownership.
- Plugin projects can be migrated gradually through compatibility adapter shims if package manifests and runtime registration need a transitional path.

## Primary Risk Themes

- Moving contracts without moving tests would create compile-time churn without architectural improvement.
- Moving executors too early would break plugin, template, and UI descriptor catalogs.
- Reconnecting MAF before hardening would preserve the current coupling under new project names.
- Plugin packages currently register `IWorkflowExecutor` implementations directly, so abstraction changes can break installed package compatibility.
- Runtime proof must distinguish Run Preview simulation from production side effects, especially for Gmail, Office365, and Docker.

## Validation Expectations

- Focused unit tests for every new project boundary.
- Project reference and namespace guard tests to prevent MAF/Core regressions.
- Descriptor parity tests for built-in and plugin executors.
- Template load and run-preview simulation tests for every moved executor family.
- Integration tests through API, runtime manager, plugin catalog, and process/workflow integration.
- Playwright proof for workflow shell and project-structure workflow nodes after UI adoption.
- Focused performance scans at hardening checkpoints.
