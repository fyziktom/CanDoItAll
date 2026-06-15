# SB24 Operator Control Center, Escalations, Approvals, Rework, And Manager Directives

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild the operator control center: escalations, approvals, dead letters, timeline, invariant diagnostics, recovery advice, manager resolution, manager directives, and targeted rework.

## Covered Inputs

- REQ-020 to REQ-025, REQ-026 to REQ-030, REQ-051, REQ-052.
- US-036 through US-039, US-048, and US-054.
- AC-012 to AC-017, AC-018 to AC-021, AC-034, AC-039, AC-040.

## Prerequisites

- SB23 runtime execution view complete.
- SB09 manager incident/recovery/branch/subprocess control complete.
- SB08 outbox/dead-letter projection available.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsOperatorConsoleSection.razor`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessLiveEscalationActionPolicyTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Operator control center projection UI.
- Escalation actions: details, assign, resolve, reopen, request rework.
- Approval actions: approve, reject, changes requested.
- Manager directive command and targeted rework command.
- Dead-letter and recovery advice projections.

## Dependency Impact

- SB25 uses manager/evidence projections after operator workflows are stable.
- SB26 live incident cards reuse escalation action services.

## Validation Depth

- Manager incident lifecycle tests.
- Approval/rework/manager directive command tests.
- Outbox dead-letter projection tests.
- Playwright proof for operator escalation and approval actions.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Bind operator console to incident, approval, dead-letter, timeline, and recovery projections.
2. Implement escalation commands with receipts and refresh behavior.
3. Implement approval commands.
4. Implement manager directive and targeted rework commands.
5. Add tests and Playwright proof.
6. Record story coverage for US-036 through US-039, US-048, and US-054.

## Do Not Do

- Do not let manager control loop become a hidden dispatcher.
- Do not expose raw agent/subprocess diagnostics directly to UI.
- Do not allow unbounded rework or recovery loops.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Operator console renders all required projection areas.
- [ ] Escalation, approval, directive, and rework commands are typed and bounded.
- [ ] Dead-letter and recovery advice are visible and actionable.
- [ ] Browser proof exists.

## Proof Required

- Manager/runtime/outbox/component test output.
- Playwright operator console screenshot evidence.
- Story coverage table for owned stories.

## Browser Validation Logging

- Required. Capture operator tab, escalation/approval/rework action, assertions, screenshot, and console/network summary.

## Progression Gate

- SB25 may start after operator and manager projections are stable.

## Suggested Agent Prompt

Execute SB24 from `codex/bundles/process-module-architecture-v3/subbundles/24-operator-control-center-escalations-approvals-rework-and-manager-directives`. Rebuild operator controls over manager incident projections and bounded commands.

## Handoff Notes For Next Bundle

Record artifact, assignment, messaging, and manager chat projection fields needed by SB25.
