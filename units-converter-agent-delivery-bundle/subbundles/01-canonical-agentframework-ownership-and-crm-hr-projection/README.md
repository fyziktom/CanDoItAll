# Canonical AgentFramework Ownership And CRM-HR Projection

## Status

- `Completed`

## Objective

- Repair the canonical-source split so AgentFramework becomes the sole editable AI-agent registry for the target profile and CRM-HR renders that same catalog through the existing projection bridge.

## Covered Inputs

- `N001`
- `N002`

## Prerequisites

- Prepared-stage validator pass for the root bundle.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkWorkspaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkAiTechnicalAgentBridge.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAgentsPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AiAgentProfileIntegrationTests.cs`

## Deliverables

- Canonicalization or migration logic that brings legacy organization-scope agents into the active AgentFramework catalog for the target profile.
- CRM-HR directory projection that reads the same technical-agent catalog surfaced on the Agents page.
- Regression tests covering the repaired ownership model.

## Dependency Impact

- This is the hardest foundation. If it is weak, later capability hardening, provisioning, and live-run proof are all invalid because they would operate against the wrong agent catalog.

## Validation Depth

- `Critical foundation`
- `Critical UI foundation`

## Implementation Steps

1. Confirm how the active profile resolves organization scope and where legacy agents still live.
2. Implement the minimal safe canonicalization path so the active AgentFramework scope owns all editable agents needed by the target profile.
3. Keep CRM-HR as a projection and edit bridge over AgentFramework instead of introducing dual reads from multiple scopes.
4. Add or update tests that prove CRM-HR and AgentFramework now surface the same editable agents.
5. Browser-verify both pages against the target profile and record evidence before progressing.

## Scope Exceptions

- This phase does not yet provision the serious units-converter project.

## Do Not Do

- Do not keep a long-term two-scope merge view as the product behavior.
- Do not patch CRM-HR to special-case legacy showcase names without fixing ownership.

## Acceptance Checklist

- Agents such as `Showcase Lead Engineer` appear on the AgentFramework Agents page for the target profile.
- CRM-HR and AgentFramework agent counts align for the same profile after canonicalization.
- Editing routes still operate through AgentFramework-owned technical-agent data.

## Proof Required

- Targeted integration or component tests for AgentFramework and CRM-HR agent catalog alignment.
- DOM proof that the Agents page and CRM-HR page both render the repaired catalog.
- Screenshots of both pages after the fix.

## Browser Validation Logging

- Target routes: `/agents?tab=agents` and `/crm-hr/agents`
- Required viewports: `1600x900` primary and `1280x900` secondary
- Required Playwright MCP actions: navigate to both pages, wait for data load, extract rendered agent counts and key names, capture screenshots
- Expected evidence paths: execution-report entries for AgentFramework and CRM-HR catalog screenshots
- Screenshot review questions: do both pages show the same serious agents, are edit affordances still reachable, and is there any leftover evidence of split ownership

## Progression Gate

- Do not start subbundle `02` until the same target-profile agent list is visible on both pages and test coverage proves CRM-HR remains a projection over AgentFramework.

## Suggested Agent Prompt

```text
Implement only subbundle 01. Repair the AgentFramework and CRM-HR source-of-truth split so the active AgentFramework catalog becomes the only editable AI-agent registry for the target profile, and CRM-HR renders that same catalog through the existing bridge. Add tests and browser proof on both pages before closing the phase.
```

## Closure Notes

- The active AgentFramework organization catalog now imports CRM-HR-bound legacy organization-scope agents into the canonical editable catalog before AgentFramework or CRM-HR surfaces load.
- Targeted integration and component tests passed after the repair:
  - `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~AiAgentProfileIntegrationTests"`
  - `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~AiAgentsPageTests"`
- Browser proof on the target profile confirmed that `/agents?tab=agents` and `/crm-hr/agents` both surface `14` agents and include `Showcase Lead Engineer`.
- Evidence copied into the bundle:
  - `C:\repositories\CanDoItAll\units-converter-agent-delivery-bundle\reviews\evidence\subbundle-01-agents-page-1600.png`
  - `C:\repositories\CanDoItAll\units-converter-agent-delivery-bundle\reviews\evidence\subbundle-01-crmhr-page-1600.png`
