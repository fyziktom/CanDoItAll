# 02-workspace-transfer-handlers

## Status

- `Completed`

## Objective

- Register the initial transfer items: ProjectStructure MCP token/settings, AI providers, AI agents, and process definitions.

## Covered Inputs

- ProjectStructure MCP token copy.
- Checkbox item list.
- Generic records/settings transfer.
- Secret-safe transfer.

## Prerequisites

- `01-01-transfer-foundation` closure gate must pass.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\ProjectStructure\ProjectStructureAgentAdministrationModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\ProjectStructure\ProjectStructureAgentAdministrationService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Models\WorkspaceModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes`

## Deliverables

- Workspace handler for ProjectStructure MCP settings/profile/override records.
- Workspace handler for AI provider profiles and referenced Security secrets.
- AgentFramework handler for agent catalog transfer.
- Processes handler for process definition/configuration records only.
- Module DI registrations.

## Dependency Impact

- The UI checkbox list and actual user value depend on handler descriptors and preview data.
- Process/runtime safety depends on excluding runtime tables.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Implement handler files in their owning modules.
2. Copy encrypted token/secret payloads without decrypting or rendering cleartext.
3. Copy Process definition tables in FK-safe order and exclude runtime data.
4. Register handlers in module service collection extensions.
5. Add targeted tests where practical.

## Scope Exceptions

- Cross-machine encrypted secret migration is out of scope unless the same DataProtection key ring is available.

## Do Not Do

- Do not make Workspace reference AgentFramework or Processes.
- Do not display clear tokens or API keys.
- Do not copy process run history.

## Acceptance Checklist

- Four descriptors are available to the transfer service.
- ProjectStructure token/settings transfer preserves encrypted token records.
- AI providers copy provider rows and referenced secrets.
- AI agents copy file-backed agent catalog data without DB coupling.
- Processes copy definitions/configuration only.

## Proof Required

- Successful build or targeted compile proof.
- Targeted tests for handler behavior where nearby test infrastructure exists.

## Browser Validation Logging

- N/A for handler-only work. UI proof is in subbundle 03.

## Progression Gate

- Proceed only when all required transfer items are registered and previews can drive a UI checkbox list.

## Suggested Agent Prompt

```text
Implement the module-specific transfer handlers and registrations. Keep module boundaries intact and do not edit UI yet.
```
