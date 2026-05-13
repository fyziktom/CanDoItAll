# SB03 Controlled Host Tool And Command Capability

## Status

- `Ready`

## Objective

- Add a generic, grant-aware host-tool capability that lets plugins invoke reviewed local recipes without receiving arbitrary shell, arbitrary PowerShell, raw command services, or inherited secrets.

## Success Criteria

- Plugins can request only typed, registered recipes.
- PowerShell and Docker recipe access require explicit grants.
- Host-command output, timeouts, cancellation, environment, receipts, and audit are bounded and testable.

## Covered Inputs

- `N006`: Docker plugin requires host access.
- `N008`: plugins must remain generic.
- `N009`: PowerShell/files must be under explicit user control.
- Requirements `R006` through `R011` and `R015`.

## Prerequisites

- SB02 grant evaluator and grant persistence are complete.
- Existing workspace command boundary and process host behavior are understood.
- Architecture decision confirms no plugin receives raw command service access.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandExecutionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandPlanBuilder.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandEnvironmentPolicy.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Process\LocalWorkspaceProcessHost.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandReceiptWriter.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\PluginWaveArchitectureGuardrailTests.cs

## Deliverables

- Generic plugin host-tool capability contract with strongly typed recipe ids and typed request/result models.
- Recipe registry and recipe policy layer that sits above existing workspace command execution without exposing it directly.
- Plugin-safe environment policy that excludes broad `OPENAI_`, `OPENAI_API_KEY`, and unrelated credentials by default.
- Docker recipe definitions for list containers, pull image, start container, and read logs as recipe infrastructure, not plugin-core semantics.
- PowerShell recipe boundary for reviewed scripts only, if needed by Docker or future recipes.
- Unit tests for denied recipe, missing grant, invalid args, environment filtering, timeout, cancellation, output caps, and receipt metadata.

## Dependency Impact

- SB06 cannot implement the Docker sample without this host-tool capability.
- SB07 observability depends on recipe receipts and audit metadata from this phase.
- SB05 workflow bridge will use host-tool denial behavior when plugin executors run.

## Validation Depth

- `Critical safety foundation`

## Implementation Steps

1. Define host-tool capability abstractions in a dependency-safe location.
2. Add recipe id and request/result types without magic string command routing.
3. Implement grant checks using SB02 evaluator before recipe execution.
4. Implement plugin-safe environment shaping distinct from general workspace command environment.
5. Wrap existing process execution through reviewed recipes only.
6. Add Docker recipe validators for allowed registries, image refs, names, log limits, timeouts, and forbidden options.
7. Add tests and architecture guardrails proving raw command service exposure is impossible.
8. Update execution report with host boundary and test proof.

## Scope Exceptions

- No full Docker sample plugin or workflow in this subbundle.
- No browser UI in this subbundle.
- No OS sandbox implementation.

## Do Not Do

- Do not expose arbitrary command strings, inline PowerShell, or arbitrary Docker CLI flags.
- Do not pass inherited OpenAI or unrelated credentials into plugin host processes.
- Do not claim `LocalWorkspaceProcessHost` is a sandbox.
- Do not make Docker a first-class core plugin runtime dependency.

## Acceptance Checklist

- Host-tool capability is generic and grant-aware.
- PowerShell and Docker recipes require explicit grants.
- Docker recipes reject dangerous defaults and invalid arguments.
- Output caps are applied before plugin result payload construction.
- Receipts include recipe id, risk class, boundary descriptor, env variable names only, truncation state, and artifact references.

## Proof Required

- Unit test command and result for recipe policy and environment filtering.
- Unit test command and result for Docker recipe validators.
- Guardrail test proving plugins cannot access raw command execution services.
- Execution report row updated with SB03 closure decision.

## Browser Validation Logging

- `N/A`: this subbundle has no browser-visible implementation.

## Progression Gate

- SB06 may not start until this subbundle proves Docker list, pull, start, and logs can only be invoked through granted, typed recipes.

## Suggested Agent Prompt

```text
Implement SB03 only.
Create the generic plugin host-tool capability and reviewed recipe layer. Keep Docker as recipe implementations and policy tests only. Do not build the Docker plugin or UI.
```
