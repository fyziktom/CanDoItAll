# SB06 Governed Proof Manifest

## Identity

- Subbundle: `SB06 Blazor Agent Activity Feedback`
- Status: `Complete — A6 GO with inherited A5 P2 follow-ups`
- Date: `2026-07-27`
- Owned requirement: R10 and the SB06 UI-validation portion of R11.
- Upstream authorization: `bundle://proof/SB05/a5-decision.md`
- Architecture gate: `bundle://reviews/csharp-architecture-gate.md`
- Downstream decision: `bundle://proof/SB06/a6-decision.md`

## Required evidence status

| Evidence | Status | Artifact |
| --- | --- | --- |
| Shared typed activity presenter | Pass — enum-driven phase/tone, sequence-zero replay, gap/unavailable handling, accessible live region | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentExecutionActivityStatus.razor`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentExecutionActivityStatus.razor.cs` |
| Floating activity correlation | Pass — immediate send/approval handles feed the exact stream identity and reset on agent/session changes | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor`, `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs` |
| Process Manager parity | Pass — typed orchestrator send/approval route, published process snapshot, no locally fabricated selected-run prompt | `repo://src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` |
| Reader lifecycle and stale-state fencing | Pass — operation switch disposes the old reader; profile change and unknown/evicted streams fail visibly | `repo://tests/Components/CanDoItAll.Tests.Components/AgentExecutionActivityStatusTests.cs` |
| Component validation | Pass — 95/95 | `repo://tests/Components/CanDoItAll.Tests.Components/AgentExecutionActivityStatusTests.cs`, `repo://tests/Components/CanDoItAll.Tests.Components/AgentChatPanelResponsivenessTests.cs`, `repo://tests/Components/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` |
| Browser validation | Pass — seven reviewed surface/state captures at `1920x1080` | `bundle://proof/SB06/browser/README.md` |
| C# architecture | Pass with inherited A5 follow-up; no SB06 blocker | `bundle://reviews/csharp-architecture-gate.md` |
| CodeAnalytics | Pass — `snap-20260728014834-63e19a8b`, 12 projects, 963 documents, affected project graph acyclic | CodeAnalytics snapshot |

## Browser evidence

### Floating chat

| State | Screenshot | Supporting evidence | Result |
| --- | --- | --- | --- |
| Busy | `bundle://proof/SB06/browser/final-floating-busy.png` | `bundle://proof/SB06/browser/final-floating-busy.yml` | Pass — backend-controlled activity is visible while execution remains active |
| Approval | `bundle://proof/SB06/browser/final-floating-approval.png` | `bundle://proof/SB06/browser/final-floating-approval.yml`, `bundle://proof/SB06/browser/final-floating-approval-geometry.json` | Pass — approval state and action remain visible |
| Completed | `bundle://proof/SB06/browser/final-floating-completed.png` | `bundle://proof/SB06/browser/final-floating-completed.yml`, `bundle://proof/SB06/browser/final-floating-completed-geometry.json` | Pass — continuation uses a new operation and clears the spinner |
| Failed | `bundle://proof/SB06/browser/final-floating-failed.png` | `bundle://proof/SB06/browser/final-floating-failure.yml`, `bundle://proof/SB06/browser/final-floating-failed-geometry.json` | Pass — terminal failure is correlated and visible |

### Process Manager chat

| State | Screenshot | Supporting evidence | Result |
| --- | --- | --- | --- |
| Busy | `bundle://proof/SB06/browser/final-manager-busy.png` | `bundle://proof/SB06/browser/manager-activity-observations.json` | Pass — first captured loading phase is visible before completion |
| Approval | `bundle://proof/SB06/browser/final-manager-approval.png` | `bundle://proof/SB06/browser/manager-activity-observations.json` | Pass — suspended operation stays correlated and all three actions are visible |
| Completed | `bundle://proof/SB06/browser/final-manager-completed.png` | `bundle://proof/SB06/browser/final-manager-completed.yml`, `bundle://proof/SB06/browser/manager-activity-observations.json` | Pass — continuation changes operation identity and persists the response |

## Layout and runtime observations

- Both surfaces use the production orchestrator, current-profile authorized reader,
  and workspace persistence path. Only the deterministic scenario provider is
  substituted.
- The activity region uses `role=status`, `aria-live=polite`, and `aria-atomic=true`.
- The transcript remains the only scroll owner; no activity-region scroll container
  was added.
- Status, composer, and approval actions remain in the first `1920x1080` viewport.
- Recorded Manager geometry has `scrollWidth == clientWidth`; the reviewed floating
  captures also record no horizontal overflow.
- Browser console: 0 errors and 0 warnings. `#blazor-error-ui` remains hidden.

## Architecture evidence

- Snapshot `snap-20260728014834-63e19a8b` contains no project-level cycle in the 12
  affected projects.
- Three module cycles and two nested-type cycles remain disclosed pre-existing debt
  outside the affected SB06 path.
- New presenter/reader types are narrow and independently testable.
- No SB06 `.csproj` change or registration-time `BuildServiceProvider` was added.
- No new execution-service partial or broad partial-class monolith was added.
- Direct `IAgentChatExecutionOrchestrator` injection in `ProcessWorkspaceShell`
  reduces its previous service-locator use; the workspace-factory fallback was
  removed.

## Residual follow-ups

The three P2 items in `bundle://proof/SB05/a5-decision.md` remain open: synchronous
database-switch subscriber delay, physical WAL power-loss durability, and the
in-memory cross-host provider revision race. They do not block A6 and are not
reclassified as UI guarantees.

## Closure

A6 is `GO`; SB06 is complete and SB07 may proceed. The reopen triggers are recorded
in `bundle://proof/SB06/a6-decision.md`.
