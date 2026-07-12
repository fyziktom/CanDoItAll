# Architecture Contracts And Composition Foundation

## Status

- `Completed`

## Objective

- Make workflow abstractions real and make one executor contribution set authoritative for catalog and invocation.

## Success Criteria

- Workflows.Core no longer references Workflows.Runtime for active contracts.
- Duplicate workflow service/runtime interfaces are removed and all active interfaces have production consumers.
- Catalog and invoker use identical contributions; planned entries remain visible but non-runnable.
- Standard and bundled plugin descriptor defaults/simulation cannot drift from implementations.

## Covered Inputs

- WF-ARCH-01, WF-ARCH-02, WF-PLUGIN-01, and the plugin-executor architecture note.
- CodeAnalytics findings about unused Workflows.Abstractions contracts and dual descriptor truth.

## Prerequisites

- Prepared-stage bundle validator passes.
- Baseline build/test status and dependency snapshot are captured before edits.

## Exact Source References

- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowServiceContracts.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowRuntimeContracts.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowServiceContracts.cs`
- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/WorkflowExecutorContracts.cs`
- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/WorkflowExecutorServiceCollectionExtensions.cs`
- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/WorkflowExecutorInvoker.cs`
- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins/PluginWorkflowExecutorRuntimeRegistration.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowFoundationHardeningCheckpointTests.cs`

## Deliverables

- Complete active workflow contracts in Abstractions and migrated implementations/composition.
- Immutable executor contribution/registration contract consumed by catalog and invoker.
- Migrated standard and plugin contributions with explicit duplicate/missing implementation validation.
- Replacement dependency/contribution tests; removal of shallow graph/partial-location assertions touched here.

## Dependency Impact

- SB02/SB03 rely on one contribution extension point.
- SB04/SB05 rely on active launch/runtime/query contracts pointing inward.
- SB06 relies on trustworthy descriptor schema/default/renderer metadata.

## Validation Depth

- `Critical foundation` with failing-first dependency and real-DI contribution parity proof.

## C# Architecture Impact

- Repairs a fake project boundary and removes an inverted Core-to-Runtime dependency.
- Consolidates two executor composition mechanisms without replacing the policy invoker.

## Boundary Ownership

- Workflows.Abstractions owns contracts; Workflows.Core/Runtime implement orchestration/runtime.
- WorkflowExecutors.Abstractions owns contribution shape; Core owns registry/catalog/invoker.

## Dependency Direction

- Enforce `Core -> Abstractions` and `Runtime -> Abstractions`; forbid `Core -> Runtime`.
- Plugin adapters depend on executor/plugin abstractions, not UI module implementation details.

## Pattern Decision

- Use PSR-01 Executor Contribution Registry. Do not use reflection scanning plus parallel descriptor sources or a keyed service locator.

## Testability Contract

- A static contribution/factory must prove descriptor identity, scoped resolution, duplicate detection, planned behavior, and implementation absence errors.
- Real bundled plugin DI tests must resolve real catalog entries rather than fabricated descriptors.

## Partial Class Policy

- No new partial classes. This subbundle changes contracts/composition only and must not grow runtime or UI partial clusters.

## Architecture Proof Required

- Project-reference diff, exact-symbol consumer evidence, no-cycle snapshot, and tests proving catalog/invoker identity.

## Implementation Steps

1. Add failing dependency and contribution-drift tests.
2. Complete/move active contracts to Workflows.Abstractions and migrate consumers one family at a time.
3. Remove duplicate interfaces and obsolete references after each build passes.
4. Add contribution contract/registry and migrate standard registrations.
5. Adapt bundled/plugin manifest registrations and add real catalog parity tests.
6. Run focused tests/build and capture semantic proof.

## Scope Exceptions

- Do not move workflow persistence out of Modules.AgentFramework yet; SB04/SB05 isolate it behind the repaired contracts first.

## Do Not Do

- Do not rename persisted executor IDs or silently normalize conflicting registrations.
- Do not add service-provider lookup inside executors/catalogs.
- Do not rewrite WorkflowExecutorInvoker policy behavior.

## Acceptance Checklist

- One active definition per workflow contract.
- Core-to-Runtime project reference removed where used only for contracts.
- Catalog and invoker enumerate the same runnable contribution IDs.
- Real Gmail/Office365/Docker defaults and simulation match catalog descriptors.
- Focused tests and affected project builds pass.

## Proof Required

- Failing-first transcript for inverted dependency or descriptor drift.
- Passing unit/integration transcript naming dependency and real-DI parity tests.
- Anti-stub `rg`/CodeAnalytics audit showing removed duplicate interfaces and no parallel descriptor truth.
- `bundle://proof/SB01/manifest.md` and `bundle://proof/SB01/semantic-invariants.md` during execution.

## Browser Validation Logging

- `N/A: no browser-visible behavior should change in SB01.`

## Progression Gate

- Passed. Dependency direction, real-DI contribution parity, the full build, focused tests, and the no-new-cycle snapshot are recorded in `bundle://proof/SB01/manifest.md` and `bundle://proof/SB01/semantic-invariants.md`.

## Suggested Agent Prompt

```text
Implement SB01 only. Repair active contract ownership and establish one executor contribution truth while preserving IDs, policy behavior, and scoped DI. Prove failure before the change, direct consumers after it, and stop if Core still depends on Runtime for contracts or catalog/invoker can drift.
```
