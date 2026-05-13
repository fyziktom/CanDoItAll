# Policy Observability And Sanitization Foundation

## Status

- `Completed`

## Objective

- Add execution policy/audit/sanitization foundations for plugin executor calls.

## Success Criteria

- Plugin executor calls have an observability/audit plan tied to workflow run/node/plugin/connection ids.
- Settings and errors have redacted summaries.
- Payload and artifact capture policies are explicit.
- Tests verify secret values do not appear in plugin execution logs/errors.

## Covered Inputs

- `R020`
- `R021`
- `R024`
- `R035`
- `F002`
- `F006`
- `F009`
- `F014`

## Prerequisites

- `SB02,SB05,SB06`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecretRuntimeResolver.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`

## Deliverables

- Execution event model or adapter for plugin executor calls.
- Redaction helper for plugin/executor settings summaries.
- Output size/payload limit policy for plugin executors.
- Audit requirements for install/enable/disable/execute actions.
- Tests for redacted logging and payload limits.

## Dependency Impact

- Later plugin module, workflow bridge, settings UI, and shop work depend on this foundation. Weak proof here causes duplication, secret leakage, or unstable plugin boundaries later.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Identify existing workflow event sink/activity logging points.
2. Define plugin execution event fields: workflow id, run id, node id, executor id, plugin id, connection id, status, duration, redacted settings summary.
3. Add redaction helper for settings and secret-reference fields.
4. Define output payload limit and artifact capture behavior for plugin executors.
5. Add tests that intentionally include secret-looking values in settings/errors and assert redaction.
6. Document how plugin install/update/enable/disable events will be audited in SB10/SB15.

## Scope Exceptions

- Full observability dashboards are out of scope.
- Plugin module events can be modeled before plugin module exists.

## Do Not Do

- Do not log raw settings JSON when it may contain secret references or tokens.
- Do not let plugin executor exceptions bypass redaction.
- Do not store unlimited plugin output in workflow runtime messages.

## Acceptance Checklist

- [x] Plugin executor calls have an observability/audit plan tied to workflow run/node/plugin/connection ids.
- [x] Settings and errors have redacted summaries.
- [x] Payload and artifact capture policies are explicit.
- [x] Tests verify secret values do not appear in plugin execution logs/errors.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "Redaction|WorkflowEvent|PluginPolicy"`
- `dotnet build src\CanDoItAll.AgentFramework.Core\CanDoItAll.AgentFramework.Core.csproj`

## Browser Validation Logging

- N/A.

## Progression Gate

- Passed. `WorkflowExecutorInvoker` now emits redacted execution audit records through `IWorkflowExecutorExecutionObserver`, plugin executor output payloads are capped, and plugin failure/settings summaries are sanitized before reaching audit records or invocation exceptions.

## Suggested Agent Prompt

```text
Implement SB07 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
