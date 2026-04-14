# 07 — Process Launch Planning HR Recommendation And Default Strategies

## Status

- `Ready`

## Objective

- Změnit start procesu z okamžitého `Active` runu na staged launch/staffing flow.
- Vytvořit HR recommendation pipeline pro existing resources i new-agent proposals.
- Zajistit, že systém funguje i bez AI provideru přes defaultní rule-based HR a Main Manager strategie.

## Covered Inputs

- `IN-11`, `IN-12`, `RQ-14`, `RQ-15`, `RQ-16`, `US-10`, `US-11`, `US-12`, `US-15`, `US-17`

## Prerequisites

- `03-process-messaging-policy-canvas-and-runtime-enforcement` closed.
- `06-crmhr-resource-binding-and-agent-management-surface` closed.

## Exact Source References

- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.CrmHr/Components/StaffingRequestEditor.razor
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/StaffingAllocationIntegrationTests.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Playwright/StaffingFlowTests.cs

## Deliverables

- New launch/staffing aggregate under Processes.
- HR recommendation service/bridge with scoring of existing resources and creation proposals.
- Rule-based fallback strategy that works with no AI configured.
- Process start UI updated to request and display candidate resources before actual run creation.

## Dependency Impact

- Main Manager approval, run orchestration, scenario validation i final business acceptance závisí na této subbundle. Je to jádro požadovaného flow ze zadání.
- Pokud se tady zvolí špatný aggregate nebo state model, pozdější proof bude křehký.

## Validation Depth

- `Critical foundation`
- Vyžaduje integration tests, browser flow proof a negative-path proof.

## Implementation Steps

1. Navrhnout `ProcessLaunchPlan` a navázané role/candidate/provisioning entity.
2. Rozšířit process start API/UI tak, aby nejprve vytvořil launch plan místo `Active` runu.
3. Implementovat HR recommendation bridge, který využívá CRM-HR resource pool, project assignments a AgentFramework templates/definitions.
4. Implementovat default rule-based fallback strategii pro případ, že není AI provider nebo není HR AI agent configured.
5. Zobrazit candidate list, scoring a creation proposals v launch UI.

## Scope Exceptions

- Finální approval decision logic se dotahuje v subbundle 08; tady musí být připravené data a status transitions.

## Do Not Do

- Nerozšiřovat starý `StartRunAsync` jen pomocí několika if-else vět bez samostatného launch aggregate.
- Neskrývat unresolved role gap tím, že se run stejně spustí potichu.
- Nedělat recommendation flow závislý výhradně na AI; fallback je mandatory.

## Acceptance Checklist

- Start procesu nejprve vytvoří launch plan.
- Launch plan ukazuje kandidáty pro role a případné new-agent proposals.
- Rule-based fallback doporučení funguje bez AI provideru.
- Actual `ProcessRun` nevznikne před schválením launch plánu.

## Proof Required

- Integration tests pro launch plan lifecycle a candidate generation.
- Negative-path test, že run nevznikne před approval/ready state.
- Playwright proof na process launch flow s candidate matrix.
- Build proof affected projects.

## Browser Validation Logging

- Route: process start / launch wizard under `/processes`.
- Viewport: `1600x900`.
- Actions: z definice spustit launch, zobrazit kandidáty, ověřit blokovaný start bez approval.
- Screenshot review: role list, candidate ranking a readiness state jsou čitelné.

## Progression Gate

- Approval subbundle smí pokračovat až když launch plan existuje a run není možné spustit předčasně.
- Pokud se stále vytváří `Active` run přímo ze start akce, gate failuje.

## Suggested Agent Prompt

```text
Implement only subbundle 07.

Introduce a staged process launch plan with HR recommendations and a mandatory rule-based fallback strategy. Starting a process must no longer create an active run immediately. Prove candidate selection, creation proposals and blocked early-run behavior.
```
