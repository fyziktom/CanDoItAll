# 09 — Agent Execution Orchestration Artifact Bridge And Run Observability

## Status

- `Ready`

## Objective

- Napojit schválený process run na AgentFramework execution runtime přes durable boundary.
- Promítat agent artifacts do canonical process evidence.
- Zobrazit run-level observability: assignments, messages, approvals, artifacts a execution events.

## Covered Inputs

- `IN-03`, `IN-08`, `RQ-08`, `RQ-19`, `RQ-20`, `US-07`, `US-19`

## Prerequisites

- `08-manager-approval-human-substitution-and-resource-provisioning` closed.
- `05-agent-catalog-persistence-workspace-scoping-and-governance-bridges` closed.

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessOutbox.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.Operations.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessRuntimeViewModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/ConversationModels.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Maf/MafAgentRuntime.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Deliverables

- Durable orchestration from process run into agent runtime using outbox/worker boundaries.
- Artifact bridge from scoped workspace artifacts into managed storage + process artifact records.
- Run-level event projection into Processes and Collaboration detail views.
- Observable run detail surfaces for assignments, messages, approvals and artifacts.

## Dependency Impact

- UI recomposition and scenario validation depend on this subbundle for truthful runtime evidence.
- Pokud artifacts nebo events nejsou canonical, audit transcript a scenario proof nebudou důvěryhodné.

## Validation Depth

- `Critical runtime closure`
- Vyžaduje integration tests, artifact proof a browser-visible run detail.

## Implementation Steps

1. Navrhnout orchestration contract mezi process outbox a agent runtime execution requestem.
2. Implementovat worker/bridge, který vytvoří agent runy pro schválené assignments a vrátí outcomes zpět do process contextu.
3. Promítnout relevantní artifacts do managed storage a zapsat `ProcessArtifactRecord`.
4. Napojit execution events/messages/approval outcomes do run detail read models.
5. Ujistit se, že run transcript jde rekonstruovat z canonical stores, ne jen z log outputu.

## Scope Exceptions

- Scenario harness surface se doplňuje v subbundle 11; tady jde o actual runtime bridge a observability.

## Do Not Do

- Nepoužívat fake distributed transaction mezi process DB a workspace file write.
- Nenechat canonical evidence jen ve workspace-relative cestě.
- Nelogovat run outcomes jen do Activity streamu bez query modelu.

## Acceptance Checklist

- Approved launch vytváří actual run a dispatchuje agent work přes outbox boundary.
- Artifacts z agent runtime se promítají do canonical process evidence.
- Run detail ukazuje assignments, messages, approvals a artifacts.
- Restart nebo retry nevede k duplicate side effects bez audit trailu.

## Proof Required

- Integration tests pro outbox -> agent runtime -> artifact bridge.
- Integration tests pro run detail projections.
- Playwright proof na run detail route s transcriptem a artifact listem.
- Build proof.

## Browser Validation Logging

- Route: process run detail route under `/processes`.
- Viewport: `1600x900`.
- Actions: otevřít dokončený nebo běžící run, zkontrolovat assignments, transcript, approvals a artifacts, screenshot.
- Screenshot review: evidence hierarchy je čitelná a audit-friendly.

## Progression Gate

- UI recomposition a scenarios smějí pokračovat až když run evidence a artifact bridge jsou canonical a browser-visible.
- Pokud artifacts zůstávají jen ve workspace relative paths bez managed storage mappingu, gate failuje.

## Suggested Agent Prompt

```text
Implement only subbundle 09.

Wire approved process runs into AgentFramework execution through a durable outbox boundary. Bridge artifacts into managed storage and expose full run observability (assignments, messages, approvals, artifacts) in process detail surfaces.
```

