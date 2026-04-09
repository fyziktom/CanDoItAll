# PRM-F22 — Process-native work briefs, baton handoffs, and governed triage routing

## Objective

Make the modeled process the canonical collaboration and handoff graph by issuing normalized work briefs, persisting baton handoffs, and governing triage or routing inside process semantics.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Wave 2**
- Depends on: **PRM-F04, PRM-F05, PRM-F07, PRM-F16, PRM-F17**

## Why this feature exists

The latest review clarified that future agents should not simply be wired to each other directly. The process model itself needs to own the topology and the handoff packets.

## In scope

- Normalized work brief templates derived from step contracts and actor-template snapshots
- Immutable work brief snapshots attached to activation and handoff events
- Governed triage or dispatcher decisions recorded as process artifacts
- Break-glass override journaling for exceptional out-of-process routing

## Non-goals

- Do not let triage or baton routing live only inside opaque runtime prompts.
- Do not bypass modeled process boundaries with direct production agent-to-agent shortcuts.
- Do not require AgentFramework packages to implement process-native baton semantics.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessWorkBriefModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessContextReferenceModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessWorkBriefService.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessTriageService.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeServices.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTransitionServices.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessRunPage.razor (new)`
- `tests/CanDoItAll.Tests.Integration/ProcessWorkBriefIntegrationTests.cs (new)`
- `tests/CanDoItAll.Tests.Integration/ProcessTriageRoutingIntegrationTests.cs (new)`
- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
