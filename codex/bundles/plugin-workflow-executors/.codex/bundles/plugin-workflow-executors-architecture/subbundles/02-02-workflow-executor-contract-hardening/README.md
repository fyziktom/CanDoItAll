# Workflow Executor Contract Hardening

## Status

- `Completed`

## Objective

- Harden executor descriptor/provenance/availability/policy/schema metadata for plugin ownership.

## Success Criteria

- Workflow executor descriptors expose or can be associated with source/provenance, availability, plugin ownership, version, and settings schema metadata.
- Current built-in executors remain backward compatible.
- Unimplemented/planned/unavailable executors cannot be treated as runnable without explicit validation/runtime handling.
- Duplicate executor id behavior remains deterministic and tested.

## Covered Inputs

- `R002`
- `R013`
- `R015`
- `R016`
- `R029`
- `R032`
- `F001`
- `F002`
- `F003`
- `F014`
- `F015`

## Prerequisites

- `SB01`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutorDescriptors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutorServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`

## Deliverables

- Descriptor metadata extension or adjacent metadata record.
- Availability model for implemented/planned/disabled/unavailable/incompatible executors.
- Validator and invoker behavior for unavailable executors.
- Updated built-in descriptor factory and APIs.
- Focused unit tests for descriptor compatibility, duplicate ids, and availability.

## Dependency Impact

- Later plugin module, workflow bridge, settings UI, and shop work depend on this foundation. Weak proof here causes duplication, secret leakage, or unstable plugin boundaries later.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Decide whether to evolve WorkflowExecutorDescriptor directly or add an adjacent source/availability descriptor while preserving serialized compatibility.
2. Add source/provenance metadata: built-in, bundled plugin, local package, remote package, plus plugin id/version fields when applicable.
3. Add executor availability semantics and update built-in/planned descriptors.
4. Update WorkflowDefinitionValidator to reject unavailable/unimplemented executor nodes unless a documented planning-only mode exists.
5. Update WorkflowExecutorInvoker to fail with a clear unavailable-executor error before invoking.
6. Update /api/workflows/executor-catalog DTO behavior if needed while preserving existing clients.
7. Add tests for existing built-ins, planned executors, duplicate ids, missing implementations, and JSON compatibility.

## Scope Exceptions

- Plugin module is still out of scope.
- Settings schema validation can be introduced as metadata but deeper validator implementation belongs to SB03.

## Do Not Do

- Do not break saved workflows using current executor ids.
- Do not remove planned descriptors without a deliberate replacement.
- Do not add plugin catalog persistence.
- Do not hard-code plugin-specific ids.

## Acceptance Checklist

- [x] Workflow executor descriptors expose or can be associated with source/provenance, availability, plugin ownership, version, and settings schema metadata.
- [x] Current built-in executors remain backward compatible.
- [x] Unimplemented/planned/unavailable executors cannot be treated as runnable without explicit validation/runtime handling.
- [x] Duplicate executor id behavior remains deterministic and tested.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "WorkflowExecutor"`
- `dotnet build src\CanDoItAll.AgentFramework.Models\CanDoItAll.AgentFramework.Models.csproj`
- `dotnet build src\CanDoItAll.AgentFramework.Core\CanDoItAll.AgentFramework.Core.csproj`

## Browser Validation Logging

- N/A unless API/client-visible executor catalog fields require UI review.

## Progression Gate

- Passed only when executor metadata can represent plugin-owned and unavailable executors without changing current workflow behavior.

## Suggested Agent Prompt

```text
Implement SB02 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
