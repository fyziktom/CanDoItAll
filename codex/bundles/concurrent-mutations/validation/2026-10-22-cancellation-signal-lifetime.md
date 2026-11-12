# Agent cancellation signal lifetime — 2026-10-22

Behavioral gate: **Pass** for P14. All 60 selected cases pass, including 13 new cases.
No failed candidate or source repair occurred in this selection. This does not close
the overall concurrent-mutations bundle.

## Behavior and boundaries

Exact and by-process stop, as well as upstream cancellation, synchronously mark the
selected token and return without running consumer callbacks inline. One observed
CancelAsync completion belongs to each registration. Repeated signals create no extra
notification. Callback failures are logged once with execution ID, count and type,
without arbitrary callback messages, exception objects or request data.

Dispose is nonblocking; DisposeAsync drains the retained notification. The source and
exact registry identity remain until callbacks finish, so re-registration cannot overlap
cleanup. Old disposal cannot remove a replacement. No Process schema/reference change,
provider coupling, task per retry or new global lock was introduced. Both production DI
paths resolve the required logger; the compatibility execution-service constructor uses
its existing logger.

## Executed selection

| Lane | Discovered and passed |
| --- | ---: |
| Unit-AgentExecutionCancellationRegistryTests | 15 |
| Unit-AgentInheritedParentStopTests | 20 |
| Unit-AgentFrameworkProcessRuntimeCancellationObserverTests | 6 |
| Integration-AgentInheritedParentStopTests | 9 |
| Integration-ProjectTaskPlanProcessChildStopperTests | 10 |

Tests include held and throwing callbacks, upstream/explicit/group races, repeated
disposal, callback self-disposal, duplicate identity, both DI registrations, actual
file-backed stop without journal changes while callbacks are held, and registered
Workbench/Process stop with exact retained accounting and accepted effects.

Six standard Release builds pass. Existing NuGet package-advisory warnings remain in
their logs; this is not a warning-free dependency claim. Full portability-static passes
with 15665 reviewed findings unchanged; rendering register and documentation checks pass.
Source was fixed before/during builds; Git HEAD remained `94a4d1ac9` during compilation.
Source/runtime verification and five-command resource coverage pass with zero new
process/database/temp delta. Ordinary 5032 and the separate SPA branch are preserved.

## Architecture and proof

Core snapshots: `snap-20261022044934-ee223b1b` → `snap-20261022053019-ee223b1b`.
Types 795 → 794; members 5887 → 5901; edges 1060 → 1058; findings 226 → 228.
The same three pre-existing type cycles and zero diagnostics remain. Two informational
size findings were reviewed: the notification owner and registry have distinct cohesive
responsibilities, and further forwarding/splitting would add boilerplate. Architecture
gate passes with those existing findings recorded.

Source: 9947 files, SHA256 `9C2D7D14A1F06255B089B2494DE3760519E48C87358010C69423BDBB3FC2C876`.
Runtime: 8078 files in 11 standard Release roots, SHA256 `BC69F87101D5D51D838602793E390F9712A5E0401CD9A2D9A85C489CFC2C75A6`.
[Compact proof](../proof/2026-10-19-planning-authority/c4-cancellation-signal.json):
71587 bytes, SHA256 `2844805BEFF1A0AD1E63DD125A1433811CE065798CD77383CA414DD925CC0E49`; UTF-8/LF bytes and private-sentinel secret scan pass.
Manifests, TRX, command records and resource reconciliation remain in
`.artifacts/concurrent-mutations/validation/c4-cancellation-signal`.

## Limits and continuation

DisposeAsync must not be synchronously awaited by the callback it is draining. As with
.NET cancellation tokens, callbacks registered after cancellation can run synchronously
on the registering thread and are outside the earlier notification task; owners must
finish token use before disposal. Arbitrarily blocked callbacks keep their identity
reserved. This is not global cancellation-storm, throughput or sustained-capacity proof.
Known sibling source consumers were audited, but external binary consumers of the new
IAsyncDisposable interface and required logger constructor need release compatibility
review. Terminal empty-decision closure and durable fair recovery remain next, followed
by snapshot refresh, actual producers, Workflow/settings, other writers and integrated
Stable/restore/capacity gates. Historical cleanup denials remain unresolved.
