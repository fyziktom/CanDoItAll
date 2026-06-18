# SB32 Live Processes Staffing UI And Active Agent Repair

## Status

- Completed on 2026-06-17

## Objective

Repair the remaining project-structure staffing and Live Processes operator experience gaps so technical subprocess roles do not appear staffed by Delivery Manager, starting a reviewed process closes the HR dialog with visible feedback, selected Live Processes time windows are honored, active process/agent cards are understandable, and attention/escalation details are visible from the first Live Processes tab.

## Source Inputs

- User follow-up on 2026-06-17: `bundle://inputs/live-processes-staffing-followup-20260617.md`.
- SB31 readiness repair proof: `bundle://proof/SB31-project-structure-launch-staffing-readiness-and-runtime-sequence-repair/manifest.md`.
- Original Live Processes UI reference branch: `repo://../CanDoItAll-maf-processes-refactor/src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`.

## Exact Source References

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`
- `repo://src/CanDoItAll.Processes.Application/ProcessWorkspaceShellProjectionService.cs`
- `repo://src/CanDoItAll.Processes.Projections/ProcessWorkspaceShellProjectionContracts.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor.css`
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessLaunchExecutorResolverTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs`

## Implementation Steps

- Change the parent software-delivery architecture subprocess step to use `solution-architect` as the visible responsible technical owner and Delivery Manager as reviewer/coordinator.
- Change the parent software-delivery implementation subprocess step to use `lead-engineer` as the visible responsible technical owner and Delivery Manager as reviewer/coordinator.
- Add focused resolver/template assertions proving architecture and implementation launch-plan assignments no longer select Delivery Manager for technical roles.
- Close the project-structure HR dialog before post-launch navigation/linking and surface a route-visible success notification.
- Change live-run queries so selected history windows exclude stale runs, including stale active runs.
- Add active-agent projections from runtime state and step assignments.
- Restore first-tab activity cards for attention/escalation/run status, and make the active agent tab show real cards instead of the manager-context placeholder.
- Expand run detail content with active agents, incidents, recent events, and operator next-action context.
- Make Live Processes tabs stretch to available width through component CSS.

## Do Not Do

- Do not reintroduce old `maf-processes-refactor` runtime services wholesale.
- Do not make generic Process core depend on project-structure or AgentFramework UI concepts.
- Do not hide stale runs in UI only while the API remains wrong.
- Do not silently treat Delivery Manager as a .NET architect or implementation owner.

## Acceptance Checklist

- [x] Architecture and implementation parent subprocess steps resolve to technical roles by default.
- [x] Manual HR overrides still go through SB31 readiness checks.
- [x] Successful Start closes the HR window and leaves visible started feedback after navigation.
- [x] `LiveHour` and `/api/processes/live?windowMinutes=60` exclude runs whose `LastEventAtUtc` is outside the one-hour window.
- [x] Live Processes first tab shows active/attention cards in the selected time window.
- [x] Live Processes Agents tab shows actual working-agent cards when runtime state has claimed/running steps.
- [x] Attention/escalation details include enough operator context to understand block, escalation, stale claim, manager message, and rework needs.
- [x] Live Processes tabs use the available width on desktop.
- [x] Focused unit/component/build validation passes.
- [x] Browser validation captures desktop and narrow Live Processes states plus an open details dialog.

## Proof Required

- `proof/SB32-live-processes-staffing-ui-and-active-agent-repair/manifest.md`
- `proof/SB32-live-processes-staffing-ui-and-active-agent-repair/semantic-invariants.md`
- `proof/SB32-live-processes-staffing-ui-and-active-agent-repair/ui-ux-parity-analysis.md`
- `proof/SB32-live-processes-staffing-ui-and-active-agent-repair/validation.md`
- `proof/SB32-live-processes-staffing-ui-and-active-agent-repair/changed-file-hashes.txt`
- Focused test/build transcripts under `proof/SB32-live-processes-staffing-ui-and-active-agent-repair/transcripts/`.
- Browser screenshots and validation snapshots under `proof/SB32-live-processes-staffing-ui-and-active-agent-repair/`.

## Progression Gate

Direct resolve/rework/approval actions on Live Processes require a current-branch application service over the manager runtime ports. The old `IProcessEscalationService` from `maf-processes-refactor` is not present in this refactor, and the current branch does not register persisted incident/recovery stores. SB32 therefore restores the operator-visible cards, detail context, active-agent evidence, time-window correctness, process-control navigation, and technical role assignment semantics without adding fake UI commands.
