# Executor contracts catalog and plugin architecture

## Status

- `Completed`

## Objective

- Add the typed workflow executor contract surface that all built-in executors and later plugins will use.

## Success Criteria

- Workflow definitions can persist a generic executor node with typed executor id, settings JSON, and execution policy.
- Core exposes descriptor, catalog, executor, and invoker contracts without depending on UI or concrete plugin loading.
- Descriptor metadata includes category, setup renderer key, settings schema, default settings, result shape, and default policy.
- Validation rejects unknown executor ids and invalid timeout/retry policy.

## Covered Inputs

- R01, R02, R03, R10, R15.

## Prerequisites

- Bundle prepared-stage validation passes.
- Source review confirms MAF function executors can call an invoker delegate.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\FunctionExecutor.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\Executor.cs`

## Deliverables

- New typed executor id and descriptor records in workflow models.
- New Core interfaces for executor catalog, executor implementations, and invocation.
- Built-in descriptor catalog entries for storage, project structure, HTTP, image, spreadsheet, and follow-up generic executors.
- Validator checks for executor id and policy shape.

## Dependency Impact

- Subbundles 02, 03, 04, and 05 depend on stable contracts. Weak descriptor shape here invalidates spreadsheet runtime wiring, UI setup, and provider tests.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add model records/enums for executor ids, categories, settings schema, descriptor metadata, and execution policy.
2. Extend `WorkflowNodeKind` with a generic executor kind and add init-only executor settings to `WorkflowNodeSettings`.
3. Add Core catalog/executor/invoker interfaces and a built-in catalog implementation.
4. Add descriptor entries for implemented and planned generic executors.
5. Extend validation for known executor id and execution policy range.
6. Add focused unit tests for serialization compatibility, catalog lookup, unknown executor validation, and invalid policy validation.

## Scope Exceptions

- Do not implement plugin assembly loading, remote plugin discovery, or plugin-provided Razor component loading in this subbundle.

## Do Not Do

- Do not add one workflow node kind per tool.
- Do not dispatch by raw string comparisons outside typed id/catalog code.
- Do not create UI fields in this phase.

## Acceptance Checklist

- `WorkflowExecutorId` is strongly typed.
- Existing workflow definitions can still be created without executor settings.
- Unknown executor ids fail validation.
- Catalog entries expose setup renderer keys for future UI/plugin integration.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter WorkflowExecutor`
- `Select-String` scan proving no ClosedXML concern was introduced in this subbundle.
- Update execution report gate row.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Subbundles 02, 03, 04, and 05 may start only when executor contracts compile and validation tests pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
