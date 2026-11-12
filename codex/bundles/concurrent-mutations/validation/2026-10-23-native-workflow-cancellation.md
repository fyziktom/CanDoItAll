# Native cancellation and Workflow recovery

The native Process action now admits root cancellation through the finite owner and
observes cleanup separately. Recovery includes both Agent and Workflow children. The
global and Project browser scenarios pass on the final unchanged candidate, including
a held Workflow boundary, pending observation, release and verified completion.
Legacy transport, default worker activation and the remaining engine-wide work stay open.

## Ownership and behavior

The Processes module defines a focused native cancellation client. Web supplies current
interactive authority and a fresh operation scope. Read requires Process read permission;
request requires read and execute, rechecked before admission. Only the explicitly trusted
local operator gains these two UI permissions. Restricted authenticated users remain
restricted. Root admission and selected-child cancellation retain different receipts.

The host component owns the selected-run lifetime, cancelled/retired reads, one mutation,
uncertain acknowledgements and manual refresh. It never retries an uncertain write. An
explicit status refresh updates the parent's existing projection through a separate
callback; initial reads do not. A projection failure preserves known acceptance/status.
The existing renderer uses shared Alert and Button, with no new assets or polling loop.

Workflow recovery is a small owner port in the existing Workflow abstractions. Its native
implementation selects one exact Process child by indexed keyset and validates bounded
original origin evidence, profile, Process scope and policy. Active runs use the existing
signal. Waiting runs require exactly one pending request and its original durable boundary;
the existing resume owner arbitrates cancellation against a live response lease. Missing,
ambiguous or incompatible evidence requires reconciliation. No replacement Workflow or
second cancellation transaction is invented. The Processes adapter merges one Agent and
one Workflow candidate, preserves exact root membership and requires an empty scan before
claiming exhaustion. Existing mapped Workflow admission keeps the root publication fence.

Coverage version 2 prevents old Agent-only completion from claiming current completion.
Unfinished version-1 work resets under its lease only after any original intent reaches a
definite outcome. Unknown outcomes retain that intent. The bounded seeder reopens valid
completed version-1 rows while preserving their original barrier. Corrupt evidence rolls
back the page and logs its root identity; it is never skipped or overwritten.

The guarded migration creates two concurrent indexes and resets only the singleton legacy
seed cursor. Conflicting index definitions stop the migration before that reset. Old
workers must stop before the version-2 worker runs. Downgrade requires workers disabled;
there is no lossy conversion of retained version-2 evidence. Recovery remains disabled by
default until the supported capacity/deployment envelope is qualified.

## Validation and retained failures

All builds use the existing Release outputs and isolated PostgreSQL 18 endpoint. Final
candidate verification covers 494 source pins and 100 owning/copied outputs. The latest
passing selection by topic is 244 cases across explicit source-repair boundaries; this is
not a claim that one 244-case run passed. The compact proof preserves each command,
discovery count, result and candidate identity.

| Run | Executed | Passed | Failed | Disposition |
|---|---:|---:|---:|---|
| Initial Workflow integration | 231 | 213 | 18 | Corrected empty typed test ID, fixture JSON/revision/historical admission, and actual Process-scope validation. |
| Corrected owner and held boundary | 58 | 57 | 1 | Both browser cases pass; query assertion incorrectly read PostgreSQL numeric `1.00` as an integer. |
| Final projection/diagnostics/query | 44 | 43 | 1 | Component 27, worker 7, upgrade 7 and query 1 pass. Global browser passes; Project manager-chat revision conflict occurs before cancellation. |
| Unchanged native browser rerun | 2 | 2 | 0 | Both complete, with no source or binary change. The manager-chat race remains open. |

Real persistence tests cover native and retained legacy Workflow origins, active response
leases, lost commit acknowledgement, original request identity, sibling preservation,
invalid scope/profile/origin, migration conflict refusal and bounded version-1 reopening.
HTTP/native tests cover permissions, unknown acknowledgement and read-only recovery.
The 27 component cases include selection retirement, manual parent refresh and preservation
of known status after refresh failure. Discovery is explicit for every lane.

With 10,000 unrelated Workflow rows and 10,000 unrelated external requests, both actual
selection plans use their intended indexes, return one row and have no sequential scan
or sort. The captured server execution samples are 0.043/0.041 ms; these are query-shape
observations, not a sustained capacity or end-to-end latency guarantee.

At 1920 by 1080, the inspected pending/completed native overlays keep their footer actions
visible, text readable and scrolling inside the existing content owner. The pending state
does not claim completion. After explicit refresh the summary and footer both show the
confirmed cancelled state. Earlier screenshots exposed a stale Active summary; the separate
parent refresh callback and browser assertion repair it. Existing file and manager-chat
interactions remain in both browser scenarios.

The failed Project browser captured expected workspace revision 14 versus current 15 while
opening manager chat. The existing create/rename-session path has a Reload action, but
its concurrency and partial-create semantics require a separate owner repair. The passing
unchanged rerun does not repair or erase that failure. It remains in R1/R6 follow-up scope.

## Architecture and static gates

No project reference, reverse dependency, new business partial or dependency cycle was
introduced. The AgentFramework module's two pre-existing scoped cycle findings remain
unchanged. New contracts remain neutral; native Workflow persistence owns SQL/boundary
work, Processes owns cross-owner recovery and Web owns current interactive identity.
Generated EF migration partials follow the existing migration convention. CodeAnalytics
adds no findings; existing design-time diagnostics remain and direct builds pass.

Full portability enforcement passes with 15,691 reviewed findings. Twelve added matches
are migration/model index names and generated metadata; no OS-path behavior was added.
The reviewed baseline diff and regenerated scan are retained. Rendering review passes
for 1,212 inputs after review of the four changed native/rendering entries. The original
6/4/7 portability, secret-scanner and rendering-tool tests pass. Documentation and artifact
checks are recorded in the compact proof.

All four Workflow validation runs have zero new observed processes, database-name deltas
or temporary fixture paths after disposal. The shared PG18 server remains for subsequent
lanes; ordinary 5032 and installed tooling are preserved. Historical automatic cleanup
denials remain unresolved, including the two compatibility handoff data folders and
baseline junction. This is not whole-bundle cleanup closure.

The [compact proof](../proof/2026-10-23-native-workflow-cancellation.json) binds the tested
candidates, explicit repair bridges, result histories, architecture review, plans and
resource observations. Continue the legacy cancellation transport transition, manager-chat
owner repair, capacity qualification and remaining owner/policy waves. SPA stays on
`codex/concurrent-mutations` for colleague review; no SPA main merge is authorized.

The six local run scans have zero private credential-sentinel matches and no oversized or unreadable text exclusions. Their three findings each are reviewed PowerShell variable expressions. The exact delivery proof scans with zero findings. Six new bulk scan files (151,710,561 bytes total) were removed after preserving their hashes, summaries, baseline review and enforcement logs; absence was verified. Historical denied targets were not retried. Documentation and whitespace checks pass.
