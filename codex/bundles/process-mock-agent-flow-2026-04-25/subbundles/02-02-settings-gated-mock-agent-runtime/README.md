# 02-settings-gated-mock-agent-runtime

## Status

- `Completed`

## Objective

- Add a disabled-by-default mock provider, role-specific mock agents, and deterministic runtime routing behind `AgentFramework:ProcessMockAgents:Enabled`.

## Covered Inputs

- R1 deterministic mock agents.
- R2 disabled by default.
- R3 no real LLM calls.
- R4 role-specific agents.
- R5 workspace artifacts.
- R10 typed constants/options.
- R11 predictable disabled failure.
- R12 settings gate proof.
- R13 runtime response proof.

## Prerequisites

- Subbundle 01 architecture seam is accepted.
- Prepared bundle validation has passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkWorkspaceFactory.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkCatalogWarmupService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkModuleServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ScenarioHarnessAgentRuntime.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ScenarioHarnessSupport.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\WorkspaceFileService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\appsettings.Development.json

## Deliverables

- `ProcessMockAgentOptions` bound from configuration.
- Mock provider and role-agent catalog seeding when enabled.
- Runtime decorator that handles only the mock provider and delegates everything else.
- Deterministic artifact-writing behavior through `IWorkspaceFileService`.
- Tests for disabled and enabled mock-agent behavior.

## Dependency Impact

- Subbundle 03 cannot build a process repair loop until the role agents exist and the runtime can produce deterministic outcomes.
- Weak settings proof risks exposing test-only agents to normal users.

## Validation Depth

- Critical runtime foundation.

## Implementation Steps

1. Add option constants and bind them in AgentFramework DI.
2. Add mock catalog metadata with stable provider base URL, model, role keys, and tags.
3. Seed the mock provider and role agents only when enabled, before AI directory synchronization.
4. Add an `IAgentRuntime` decorator that handles the mock provider and delegates all other providers.
5. Write deterministic role responses and artifacts through `IWorkspaceFileService`.
6. Add targeted integration tests for option gating and direct runtime behavior.

## Scope Exceptions

- Full process definition orchestration belongs to subbundle 03.

## Do Not Do

- Do not enable mock agents by default.
- Do not modify real provider adapters.
- Do not bypass workspace file auditing.

## Acceptance Checklist

- Disabled configuration does not seed mock provider or agents.
- Enabled configuration seeds multiple role-specific mock agents.
- Mock runtime does not call the inner runtime for `process-mock://agents`.
- Non-mock providers still delegate to the existing runtime chain.
- Artifacts are written under deterministic paths.

## Proof Required

- Targeted tests for catalog gating and mock runtime output.
- `dotnet test` filter or equivalent command recorded in the execution report.
- Diff review showing no process dispatcher special case.

## Browser Validation Logging

- N/A: backend runtime and integration-test subbundle.

## Progression Gate

- Subbundle 03 may start only after enabled mock agents are visible to the technical agent bridge and direct runtime output is deterministic.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Add the settings-gated mock provider, role agents, and deterministic runtime decorator. Preserve real provider execution and write artifacts through the workspace file service.
```
