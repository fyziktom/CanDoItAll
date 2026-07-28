## C# Architecture Gate Result

Status: Pass with follow-up

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| P2 — inherited from A5 | A synchronous database-switch subscriber can delay the switching thread | `bundle://proof/SB05/concurrency-invariants.md`; `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs` | Define a bounded notification policy in a separate hardening change |
| P2 — inherited from A5 | WAL recovery proves process interruption, not physical disk and directory durability under power loss | `bundle://proof/SB05/a5-decision.md`; `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs` | Define and test the required physical-durability contract before making a power-loss guarantee |
| P2 — inherited from A5 | Provider revision validation retains an in-memory cross-host race between the final revision probe and external provider use | `bundle://proof/SB05/a5-decision.md`; `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs` | Add a distributed lease/version boundary only if multi-host consistency requires it |

No P0/P1 finding or new SB06 architecture finding was found.

### Dependency direction

CodeAnalytics snapshot `snap-20260728014834-63e19a8b` loaded the 12 affected
projects and 963 documents. The affected project graph is acyclic and retains the
prepared direction: SharedKernel has no product-project dependency; Models and Core
remain inward dependencies; Components consumes typed Core/Models contracts; and the
Agent Framework and Processes modules compose the UI and runtime implementations.
No SB06 `.csproj` change was required.

The snapshot still reports three intra-project module cycles and two nested-type
cycles. They are the disclosed baseline debt outside the affected SB06 path, not
project-reference cycles or evidence of a new dependency reversal.

`ProcessWorkspaceShell` now injects `IAgentChatExecutionOrchestrator` directly for
send and approval continuation. Its workspace lookup no longer falls back through
`ICanDoItAllAgentWorkspaceFactory`, reducing the existing service-locator surface.
No registration-time `BuildServiceProvider` call was introduced.

### Partial-class policy

SB06 adds no execution-service partial and does not spread UI behavior across a
partial-class monolith. `AgentExecutionActivityStatus.razor.cs` is the normal narrow
Razor code-behind partial for one presenter. The narrow
`CurrentProfileAgentExecutionActivityReader` partial keeps only its authorized
profile-bound reader lifecycle in the same source file. Neither use hides unrelated
runtime responsibilities or requires the original execution-service monolith in
tests.

### Testability proof

The component suite passed 95/95. Focused tests prove first typed activity before run
identity, enum-based phase rendering with unrelated message text, bounded gap
display, operation switching with old-reader disposal and late-update fencing,
profile-change/unknown-stream handling, floating send and approval immediate
handles, Process Manager typed send options, snapshot capture, and approval routing.
The presenter tests use a controlled typed reader and do not require filesystem,
database, network, or the original execution runtime.

Reviewed `1920x1080` browser evidence under `bundle://proof/SB06/browser` covers
floating busy, approval, completed, and failed states plus Process Manager busy,
approval, and completed states. The production orchestrator, authorized activity
reader, and persistence path remained active; only the deterministic scenario
provider was substituted. The evidence records no browser console errors or
warnings, no horizontal overflow, no visible Blazor error UI, and no stale spinner
after terminal states.

### Closure decision

SB06 and A6 may close, and SB07 may proceed. The three inherited A5 P2 follow-ups
remain open but do not contradict the typed UI projection or browser proof. Reopen
SB06 if either surface bypasses the orchestrator, derives phase from display text or
selected-run state, leaks a reader across operation/profile changes, hides an
approval or terminal state, regresses layout/accessibility, adds a service-provider
construction shortcut, or expands the execution-service partial monolith.
