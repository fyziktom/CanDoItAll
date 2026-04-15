# 08 — Manager Approval Human Substitution And Resource Provisioning

## Status

- `Completed`

## Objective

- Navázat na launch plan explicitní manager approval a možnost nahradit AI managera člověkem.
- Podpořit project-specific Main Manager authority.
- Řídit provisioning nových agentů nebo resources jako součást schváleného launch flow.

## Covered Inputs

- `IN-11`, `IN-12`, `IN-13`, `RQ-17`, část `RQ-19`, `US-14`, `US-16`

## Prerequisites

- `07-process-launch-planning-hr-recommendation-and-default-strategies` closed.
- `02-collaboration-domain-notification-and-conversation-foundation` closed.

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessOutbox.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/AgentModels.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/ConversationModels.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/StaffingAllocationIntegrationTests.cs

## Deliverables

- Approval resolver for project-specific Main Manager or human substitute.
- Launch approval workflow tied into Collaboration inbox / escalation surfaces.
- Provisioning request lifecycle for creating new AI agents before run start.
- Deterministic fallback when no AI manager is configured.

## Dependency Impact

- Execution orchestration and scenario validation depend on the approved assignment set produced here.
- Pokud approval flow není spolehlivý, pozdější run evidence není business-valid.

## Validation Depth

- `Critical process-governance closure`
- Vyžaduje integration tests a browser proof přes approvals.

## Implementation Steps

1. Navrhnout resolver, který určí project-specific Main Manager authority: AI agent binding, project-assigned human manager nebo default fallback.
2. Napojit approval tasky na Collaboration inbox a launch plan status transitions.
3. Implementovat provisioning request flow pro new-agent creation proposals a jejich potvrzení.
4. Přidat rejection / change-request path a vrácení launch plánu do upravitelného stavu.
5. Zajistit, že po schválení vznikne připravený input pro actual run start, ale run se stále spustí až přes jasný orchestration krok.

## Scope Exceptions

- Finální process execution dispatch a artifact bridge se řeší až v subbundle 09.

## Do Not Do

- Nepřenášet approval lifecycle jen do transientního toastu nebo lokálního modal state.
- Nepředpokládat, že Main Manager je vždy AI agent.
- Nevytvářet provisioning side effects uvnitř UI code-behind bez durable boundary.

## Acceptance Checklist

- Launch plan umí čekat na manager/human approval.
- Project-specific manager/human substitution funguje.
- Provisioning request lze schválit a promítnout do resource readiness.
- Rejected plan se vrátí do opravitelného stavu místo tichého failu.

## Proof Required

- Integration tests pro approval resolver a human substitution.
- Integration tests pro provisioning request transition.
- Playwright proof na approval/inbox flow s návratem do launch detailu.
- Build proof.

## Browser Validation Logging

- Route: `/collaboration` approval/inbox surface a launch detail route.
- Viewport: `1600x900`.
- Actions: otevřít pending approval, schválit/vrátit, zkontrolovat změnu launch statusu.
- Screenshot review: schvalovací context je jasný a akce nejsou zaměnitelné.

## Progression Gate

- Execution orchestration nesmí začít, dokud není approval a provisioning flow stabilní.
- Pokud project-specific manager resolution není auditovatelný, gate failuje.

## Suggested Agent Prompt

```text
Implement only subbundle 08.

Add Main Manager approval, human substitution and resource provisioning on top of the launch plan. Use Collaboration for inbox/task visibility, support project-specific manager authority, and keep the flow durable and auditable.
```

