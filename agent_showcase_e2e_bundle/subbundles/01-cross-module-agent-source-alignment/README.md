# Cross-module agent source alignment

## Status

- `Completed`

## Objective

- Make CRM-HR agent discovery consume the same technical agent inventory used by the dedicated Agents module, while preserving CRM-HR-specific profile editing and binding behavior.

## Covered Inputs

- `U001`
- Functional requirements `1` and `2`
- Foundation dependency for showcase requirement `9`

## Prerequisites

- Prepared bundle validator pass
- No earlier code subbundles

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkCrmHrMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAgentsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\AiTechnicalAgentBridge.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CrmHrCrossModuleIntegrationTests.cs`

## Deliverables

- CRM-HR directory listing resolves from technical agent inventory plus CRM-HR overlay data instead of party rows alone.
- CRM-HR detail and save flows still support owner, validation, capability, and binding edits for selected agents.
- Targeted regression tests cover the converged inventory behavior.

## Dependency Impact

- Subbundles `03` and `04` depend on this because showcase process runtime will source agents through CRM-HR-facing services.
- Weak proof here would invalidate the live showcase because it could succeed in the agent module while failing in CRM-HR resource search.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Trace how dedicated Agents and CRM-HR each build their directory inventories and identify the narrowest service-level composition point.
2. Change CRM-HR discovery so it starts from technical agents and overlays CRM-HR profile data or pending-backfill state instead of requiring a pre-existing AI-agent party row.
3. Keep CRM-HR editing behavior coherent for selected agents, including any necessary backfill or projection path for party-linked profile data.
4. Add or update targeted tests for directory convergence and save-path safety.
5. Run browser proof that `/agents` and `/crm-hr/agents` now show the same population.

## Scope Exceptions

- This phase does not seed the showcase or change process-runtime templates.

## Do Not Do

- Do not create a second persistent agent registry under CRM-HR.
- Do not hardcode a one-time data sync script as the only fix.
- Do not silently drop CRM-HR profile data when an agent has no existing party row.

## Acceptance Checklist

- CRM-HR agent page no longer returns an empty directory when technical agents exist.
- Dedicated Agents and CRM-HR pages show consistent counts for the same dataset.
- Existing CRM-HR profile editing still works for selected agents.
- Tests cover at least one technical-agent-without-party scenario or equivalent bridge case.

## Proof Required

- Targeted test command covering CRM-HR and agent directory behavior.
- Large-screen browser pass on `/agents` and `/crm-hr/agents`.
- Screenshots planned:
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\01-agents-module-directory.png`
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\01-crmhr-agent-directory.png`
- DOM or assertion proof that both routes expose matching non-zero agent counts.

## Closure Evidence

- Code fix projects AgentFramework technical agents into CRM-HR directory discovery through `SynchronizeDirectoryProjectionAsync` before CRM-HR list resolution.
- Targeted regression suite passed, including `CanDoItAll.Tests.Components.AiAgentsPageTests.Existing_technical_agents_are_projected_into_crm_hr_agent_roster`.
- Live browser proof on the requested profile showed:
  - `/agents`: `TECHNICAL AGENTS = 6`, `BOUND RESOURCES = 6`, `CAPABILITIES = 46`
  - `/crm-hr/agents`: `AGENT PARTIES = 6`, `WITHOUT PROFILE = 6`, `Agent roster = 6 visible agent(s)`
- Screenshots captured:
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\01-agents-module-directory.png`
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\01-crmhr-agent-directory.png`

## Browser Validation Logging

- Target routes: `/agents`, `/crm-hr/agents`
- Required viewport: `1600x900`
- Required browser actions: navigate both routes, dismiss startup modal if needed, capture visible counts, inspect at least one row or selection path, and screenshot both pages.
- Review questions:
  - Do both routes show the same visible population size?
  - Is CRM-HR presenting existing agents instead of an empty state?
  - Does selecting an agent still expose CRM-HR profile controls?

## Progression Gate

- Downstream work may continue only when CRM-HR and dedicated Agents converge on the same inventory and targeted tests plus browser proof confirm that the save path still works.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Unify CRM-HR agent discovery with the technical agent source of truth used by the dedicated Agents module, without creating a second registry. Preserve CRM-HR profile editing and bindings. Add targeted regression coverage and browser proof before closure.
```
