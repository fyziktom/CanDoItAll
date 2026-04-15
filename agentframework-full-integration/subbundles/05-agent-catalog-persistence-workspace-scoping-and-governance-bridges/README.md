# 05 — Agent Catalog Persistence Workspace Scoping And Governance Bridges

## Status

- `Completed`

## Objective

- Udělát z integrovaného AgentFramework modulu canonical ownera technical agent definitions, chat/execution persistence a workspace scoping logiky.
- Odstranit globální sandbox workspace root jako aktivní integrated mode assumption.
- Napojit approvals/checkpoints/governance bridge do CanDoItAll durable modelu.

## Covered Inputs

- `IN-03`, `IN-16`, `RQ-18`, `RQ-19`, část `RQ-12`, `US-18`, `US-20`

## Prerequisites

- `04-provider-ownership-bridge-and-legacy-runtime-retirement` closed.
- Module skeleton from subbundle 01 is stable.

## Exact Source References

- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/AgentModels.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/ConversationModels.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Core/Contracts.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Core/AgentFrameworkWorkspaceCatalogService.Agents.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Persistence/FileSandboxWorkspaceStore.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Maf/MafAgentRuntime.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/AiAgentProfileIntegrationTests.cs

## Deliverables

- Integrated persistence/store model pro agent definitions, chat sessions, execution runs, approvals a checkpoints.
- Workspace locator/context factory scoped minimálně by project/process/run.
- Governance bridge that can round-trip approvals between agent runtime and main app.
- Effective permission handling for escalation/ask-other-agents semantics in integrated mode.

## Dependency Impact

- CRM-HR binding, process launch, scenario harness i chat UI závisejí na stabilním technical agent catalogu a scoped workspaces.
- Pokud tu zůstane globální sandbox root nebo neintegrovaná checkpoint persistence, restart/resume a scenario proof budou nedůvěryhodné.

## Validation Depth

- `Critical foundation`
- Vyžaduje deep integration tests včetně restart/resume / approval round-trip.

## Implementation Steps

1. Převzít a upravit agent, chat a execution modely do integrovaného persistence designu.
2. Implementovat `IAgentWorkspaceLocator` / context factory a odpojit globální sandbox root jako jediný integrated mode model.
3. Napojit execution governance bridge na Collaboration/Processes approval surfaces.
4. Implementovat persistent catalog service pro agent definitions a templates.
5. Pokud je potřeba workspace file storage zachovat pro working files, omezit ji jen na scoped workspace folders a oddělit od canonical metadata.

## Scope Exceptions

- Final scenario harness wiring a UI exposure se řeší až později; tady jde o canonical stores a governance.

## Do Not Do

- Nedržet v integrated mode jedinou globální workspace root bez contextu.
- Nenechávat approvals jen uvnitř sandbox-compatible compatibility blobu bez main-app bridge.
- Nevytvářet technical agent store uvnitř CRM-HR.

## Acceptance Checklist

- Agent definitions mají canonical persistence v AgentFramework modulu.
- Workspaces jsou scoped by context a nekolidují.
- Pending approvals a checkpoints se dají obnovit po restartu a jsou napojené na main app governance.
- Chat/execution data nejsou jen sandbox-local JSON bez integrated store story.

## Proof Required

- Integration tests pro scoped workspace creation and lookup.
- Integration tests pro approval round-trip a checkpoint resume.
- Targeted build.
- Optional admin/browser proof na Governance/Diagnostics tab foundation, pokud je už dostupný.

## Browser Validation Logging

- Route: `/agents?tab=Governance` nebo admin diagnostics surface, pokud existuje; jinak `N/A` dočasně zdokumentované.
- Viewport: `1600x900`.
- Actions: načíst list runů nebo pending approvals, screenshotnout diagnostic summary.
- Screenshot review: context je srozumitelný a nezobrazuje sandbox-only pojmy.

## Progression Gate

- CRM-HR binding a process launch smějí pokračovat až když existuje canonical technical agent catalog a scoped workspace story.
- Pokud po restartu mizí approvals nebo runs, gate failuje.

## Suggested Agent Prompt

```text
Implement only subbundle 05.

Turn the imported AgentFramework into an integrated technical agent catalog with scoped workspaces, durable execution state and governance bridges. Remove the global sandbox-root assumption for integrated mode. Prove restart/resume and approval round-trip behavior.
```

