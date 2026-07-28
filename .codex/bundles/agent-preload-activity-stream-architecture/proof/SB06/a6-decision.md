# SB06 A6 UI Decision

## Decision

`GO — three inherited A5 P2 follow-ups remain open`

Date: `2026-07-27`

Architecture gate: `Pass with follow-up`

## Why SB07 may proceed

- A5 authorized UI work with `GO with three P2 follow-ups`.
- Floating send and approval continuation expose their immediate typed operation
  handles before awaiting completion.
- Process Manager send and approval continuation use the same
  `IAgentChatExecutionOrchestrator` route; the orchestrator is injected directly.
- Both surfaces pass the operation stream identity to the shared
  `AgentExecutionActivityStatus` presenter.
- The presenter replays from sequence zero, maps typed phases instead of parsing
  display text, reports gaps/unavailable streams, fences stale operations, and
  disposes its reader with component lifetime.
- The component suite passed 95/95.
- Reviewed `1920x1080` browser evidence covers floating busy, approval, completed,
  and failed states plus Process Manager busy, approval, and completed states.
- Browser review found zero console errors, zero console warnings, no horizontal
  overflow, no visible Blazor error UI, and no stale terminal spinner.
- CodeAnalytics snapshot `snap-20260728014834-63e19a8b` loaded 12 projects and 963
  documents. The affected project graph is acyclic and has no blocking finding.
- No SB06 project reference, `BuildServiceProvider` call, broad helper/manager,
  execution-service partial, or source-of-truth reversal was introduced.

## Inherited P2 follow-ups

1. A blocked synchronous database-switch subscriber can delay the switching thread.
2. WAL proof does not establish physical disk/directory durability under power loss.
3. Final provider revision validation retains an in-memory cross-host race without a
   distributed lease or transaction.

These are unchanged A5 follow-ups. A6 does not convert them into stronger runtime
guarantees.

## Progression and reopen rule

SB06 is complete and SB07 documentation/API/runtime closure is authorized. Reopen
SB06 if a surface bypasses the typed orchestrator/reader path, stale operation or
profile activity becomes visible, approval or terminal state becomes inaccessible,
the reader outlives its component, or the reviewed large-screen layout regresses.
Reopen SB05 under the conditions in `bundle://proof/SB05/a5-decision.md`.
