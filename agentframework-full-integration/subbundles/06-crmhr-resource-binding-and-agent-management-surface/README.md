# 06 — CRM-HR Resource Binding And Agent Management Surface

## Status

- `Ready`

## Objective

- Nechat CRM-HR jako canonical resource pool a zároveň ho propojit s technical agent definitions z AgentFrameworku.
- Zabránit druhému editable source of truth pro agent runtime pole.
- Udělat z `/crm-hr/agents` business-facing resource page s controlled technical delegation.

## Covered Inputs

- `IN-09`, `IN-10`, `RQ-11`, `RQ-12`, `RQ-13`, `US-08`, `US-09`, `US-21`

## Prerequisites

- `05-agent-catalog-persistence-workspace-scoping-and-governance-bridges` closed.
- `04-provider-ownership-bridge-and-legacy-runtime-retirement` closed.

## Exact Source References

- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.CrmHr/Pages/CrmHrAgentsPage.razor
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Workspace/ProjectStructureAgentAdministrationService.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Components/AiAgentsPageTests.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/AiAgentProfileIntegrationTests.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Playwright/AiAgentFlowTests.cs

## Deliverables

- Explicit `AiResourceBinding` model/service between CRM-HR party and AgentFramework technical definition.
- Updated CRM-HR agents page with combined view model and deep link into `/agents`.
- Delegated technical save/load path into AgentFramework facade.
- Resource lookup/query surfaces that keep AI agents in the same pool as people and contractors.

## Dependency Impact

- Process launch recommendation and project assignment selection depend on a clean resource pool model. Pokud CRM-HR ztratí canonical ownership, staffing flow se rozpadne.
- UI recomposition needs deep links and clear business-vs-technical separation here.

## Validation Depth

- `Critical business + UI foundation`
- Vyžaduje component, integration i Playwright proof.

## Implementation Steps

1. Navrhnout a přidat binding entity/service mezi CRM-HR party a AgentFramework agent definition.
2. Rozdělit fields na business-owned a technical-owned a upravit CRM-HR save flow accordingly.
3. Rozšířit CRM-HR agent detail o binding status, technical summary a deep link/open action do Agents modulu.
4. Upravit query služby pro resource listing tak, aby AI agent resources byly first-class citizens společně s humans/contractors.
5. Přidat migration/backfill logiku pro existující `AiAgentProfile` data.

## Scope Exceptions

- Plná launch-plan candidate scoring se řeší až v subbundle 07; tady jde o resource/binding integrity a UI.

## Do Not Do

- Nenechat CRM-HR zapisovat provider/model/runtime technická pole přímo jako canonical technical store.
- Nevytvářet oddělený resource registry uvnitř AgentFrameworku.
- Neházet business a technical metadata do jednoho JSON blobu bez ownershipu.

## Acceptance Checklist

- CRM-HR zůstává canonical resource pool.
- Existuje explicitní binding na technical agent definition.
- CRM-HR UI umí otevřít a spravovat AI resource bez duplikace technical write path.
- Existing AI agent profiles jsou migrovatelné/backfillable.

## Proof Required

- Component tests pro CRM-HR agents page.
- Integration tests pro binding save/load/backfill.
- Playwright proof na `/crm-hr/agents` včetně deep linku do Agents modulu.
- Build proof affected projects.

## Browser Validation Logging

- Route: `/crm-hr/agents` a následný deep link do `/agents`.
- Viewport: `1600x900` a užší pass, pokud detail layout mění split pane.
- Actions: vybrat AI agent resource, ověřit binding summary, otevřít technical definition, vrátit se zpět.
- Screenshot review: business a technical informace jsou rozlišitelné a nezdvojené.

## Progression Gate

- Process launch subbundle nesmí začít, dokud AI resources nejsou čistě reprezentované v CRM-HR s technical bindingem.
- Pokud CRM-HR a Agents page dál oba zapisují stejné technical pole, gate failuje.

## Suggested Agent Prompt

```text
Implement only subbundle 06.

Keep CRM-HR as the canonical resource pool and introduce an explicit binding to the technical AgentFramework definitions. Update `/crm-hr/agents` so it remains the business-facing management surface while delegating technical saves to the AgentFramework module.
```
