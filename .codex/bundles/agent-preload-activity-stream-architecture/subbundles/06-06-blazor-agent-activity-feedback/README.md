# 06 Blazor Agent Activity Feedback

## Status

- `Completed`
- Gate: `A6 GO with inherited A5 P2 follow-ups`

## Closure Evidence

- Component suite: `95/95` passed.
- Browser proof: `bundle://proof/SB06/browser/README.md`.
- Governed proof manifest: `bundle://proof/SB06/manifest.md`.
- A6 decision: `bundle://proof/SB06/a6-decision.md`.
- Final C# architecture gate:
  `bundle://reviews/csharp-architecture-gate.md`.
- CodeAnalytics: `snap-20260728014834-63e19a8b`; 12 affected projects,
  963 documents, acyclic affected project graph, and no blocking finding.

## Objective

- Render the typed operation lifecycle consistently in floating and Process Manager chats from submit through preparation, runtime, tool/approval, completion, cancellation, and failure.

## Success Criteria

- Both surfaces show the first backend activity without waiting for run selection/creation.
- Status is driven by phase/state enums and operation identity, not parsed strings or spinner inference.
- Process Manager uses the common orchestrator/activity route and live progress.
- Current activity stays visible and compact while transcript remains the only scroll owner.
- Component and large-screen browser tests cover normal, busy, failure, cancellation, and approval where supported.

## Covered Inputs

- R10 and UI portion of the main goal.
- Floating agent and process manager parity.

## Prerequisites

- SB05 A5 decision is `Go`.
- Read `candoitall-components-mcp` and compact UI composition guidance before editing.

## Exact Source References

- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Components\AgentExecutionActivityStatus.razor`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Components\AgentExecutionActivityStatus.razor.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Services\CurrentProfileAgentExecutionActivityReader.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceShell.razor`
- `C:\repositories\CanDoItAll\tests\Components\CanDoItAll.Tests.Components\AgentExecutionActivityStatusTests.cs`
- `C:\repositories\CanDoItAll\tests\Components\CanDoItAll.Tests.Components\AgentChatPanelResponsivenessTests.cs`
- `C:\repositories\CanDoItAll\tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs`

## UI Composition Contract

- Primary surface: existing compact run-state region inside floating chat and existing Manager Chat `SurfaceCard`.
- Supporting detail: current phase/message and optional elapsed/reuse hint; durable history remains in existing execution log/transcript.
- Stats: no new cards; transient timing is supporting text, not a dashboard.
- Organization: inline in the existing chat surface; no modal/tab/split change.
- Textarea/dialog: preserve existing sizes unless screenshot evidence proves collision.
- First viewport: current activity and prompt action remain visible at `1920x1080`; chat transcript remains the single scroll owner.

## Deliverables

- Reusable typed activity projection/presenter in the existing component library boundary where justified.
- Floating panel reader lifecycle and operation correlation.
- Process Manager common orchestrator/read path.
- Accessible live-region status, terminal/error/approval rendering.
- Component and Playwright proof.

## Dependency Impact

- Final docs/runtime closure depends on actual UI parity. A selected-run or local-string shortcut would undermine the event architecture and SSE documentation.

## Validation Depth

- Proof tier: `Behavioral`.
- Browser-visible surfaces: floating agent chat and Process Workspace Manager tab.

## Implementation Steps

1. [x] Add failing component tests for pre-run activity and Process Manager live phases.
2. [x] Build a small typed projection/presenter using existing BaseLib/Radzen wrappers.
3. [x] Subscribe/dispose scoped authorized readers with component lifetime and filter by stable profile/workspace operation partition.
4. [x] Route Process Manager send/approval through the common orchestrator.
5. [x] Verify terminal/error/cancel/approval state and stale-operation suppression.
6. [x] Run maximized `1920x1080` browser actions and inspect screenshots/overlays.
7. [x] Record the A6 decision, governed manifest, and browser analytics.

## Scope Exceptions

- No mobile/tablet tuning, activity dashboard, SSE client, or redesign of chat layout.

## Do Not Do

- Do not add raw ad-hoc HTML where an existing wrapper fits.
- Do not parse `Message`/legacy `Phase` strings for state.
- Do not subscribe to unrestricted global partitions.
- Do not create nested scrolling or move transcript/prompt ownership.

## Acceptance Checklist

- [x] First typed activity appears before run ID.
- [x] Correct operation remains visible during concurrent/stale updates.
- [x] Floating and Manager chat phase parity exists.
- [x] Failure/cancel/approval is accessible and actionable.
- [x] Reader is disposed and profile/source switch removes old state.
- [x] Screenshots show no clipping, overlap, stale spinner, or hidden action.

## Proof Recorded

- Component suite passed `95/95`, including the typed presenter, floating immediate
  handles, Process Manager orchestrator/snapshot path, approval continuation, stale
  operation fencing, profile-change handling, cancellation mapping, and disposal.
- Reviewed floating busy/approval/completed/failed and Process Manager
  busy/approval/completed evidence is under `proof/SB06/browser`.
- Browser observations record the `1920x1080` viewport, transcript scroll ownership,
  semantic live status, visible approval actions, zero console errors/warnings, no
  horizontal overflow, and no stale terminal spinner.

## Browser Validation Logging

- Routes: the production page exposing floating chat and `/processes` Manager Chat,
  using the deterministic scenario provider while retaining the production
  orchestrator, authorized reader, and persistence path.
- Viewport: `1920x1080` maximized large desktop.
- Floating captures: busy, approval, completed, and failed.
- Process Manager captures: busy, approval, and completed.
- Result: semantic live status remained visible, approval actions remained in the
  first viewport, the transcript remained the only scroll owner, terminal states
  cleared the spinner, `#blazor-error-ui` stayed hidden, and the console recorded
  zero errors and zero warnings.

## Proof Required

- `proof/SB06/manifest.md`, `proof/SB06/a6-decision.md`, the focused component
  results, and reviewed browser artifacts under `proof/SB06/browser`.

## Progression Gate

- `Passed`. A6 is `GO`; SB07 may proceed with component and reviewed browser proof
  for both surfaces and no string/selected-run state inference.

## Reopen Triggers

- Real-agent phases missing in UI, stale cross-profile activity, undisposed reader, layout regression, or inaccessible status reopens SB06.

## C# Architecture Contract

- Components consume a typed read projection and dispatch commands.
- Activity state reducer/presenter is deterministic and unit-testable.
- Subscription cancellation/disposal is tied to component lifecycle.
- Process-specific prompt/snapshot construction remains in services/adapters, not markup.

## Re-entry Agent Prompt

```text
Reopen SB06 only if an A6 reopen trigger is observed.
Preserve the typed orchestrator/reader path, add focused component proof for the regression, refresh the reviewed large-screen evidence, and do not weaken the inherited A5 follow-ups.
```
