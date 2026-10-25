# Agent policy historical receipt and retained editor recovery

Design reviewed after signed Process foundation `df9f31f8c`. This completes the
unknown-outcome follow-up in [Agent policy access](P07-agent-policy-access.md), in two
connected increments: real file-owner/HTTP historical lookup, then retained native
editor recovery across navigation/restart. Neither increment activates Process policies.

## Source constraints and rejected shortcuts

`AgentDecisionDataPolicyOwner` validates the active profile generation and delegates
the only reserved-policy write to `FileSandboxWorkspaceStore`. Its dedicated catalog
publication already binds the actual next catalog revision, preserves ordinary Agent
fields and retains known acceptance after postcommit failures. Its acknowledgement is
currently only memory; the operation ID is not a persisted receipt.

The workspace identity's generation is a transient activation fence. Treating the old
generation as current after restart, reading matching policy, or silently recapturing a
new expectation would not prove the original save. Do not enable PUT replay or automatic
resubmission. Add a separate read-only lookup of the complete original command. A
matching historical receipt confirms only that original acceptance. An absent, expired
or unreadable receipt leaves the save unresolved; it does not prove that nothing happened.

The file catalog is the atomic boundary. A receipt in a second file would create a
data/receipt crash gap. Store a bounded immutable receipt log in the same atomic catalog
replacement. Normal catalog/full-document/import-shaped writers preserve the existing
log, just as they preserve reserved policy metadata; callers cannot inject or erase it.
Do not reuse external provisioning records or introduce a SQL/file distributed commit.

## Ownership and contract

Models own an immutable bounded receipt value/log and strict codec. A receipt retains
the original operation, profile/scope/generation, Agent, catalog expectation, complete
typed override and server acceptance time. Its exact accepted next snapshot is derived
from the original expected revision, matching the existing acceptance invariant. Preserve
legacy omitted-log serialization; explicit malformed/null log data is an error.

Persistence owns atomic publication. Extract the existing small `AgentPolicyPublication`
state into a cohesive type as it gains receipt preparation; keep lock acquisition,
witness validation and actual durable write with the existing store. No new store
interface, production partial, service locator or project reference is justified.
Pure receipt/protocol tests must not construct the large workspace store.

Use at most 1,024 receipts and 4 KiB per receipt, with 30-day lookup retention. Each
dedicated policy commit may remove expired receipts before appending its own record.
At capacity with no expired entries, refuse before writing; never evict live receipts
or create an unbounded history. Ordinary catalog writes do not prune the log. The same
captured time and original log must drive both publication preparation passes. Measure
maximum log overhead separately; these bounds are not sustained-capacity proof.

Historical receipts are not execution configuration. Exclude their log from the
catalog write set's shared-configuration comparison; dedicated policy publication
already changes and locks its actual Agent. Catalog witness validation and atomic
replacement still serialize conflicting file publications. The first 40-case candidate
exposed an accidental all-Agent lock expansion through the new field; the repair must
prove a policy save completes while an unrelated Agent key is held and the waiting
package/provisioning publication then refuses its stale catalog witness. Never fix that
regression by weakening the existing race tests or increasing their deadline.

Core owns lookup/current-context fencing. Read the original profile and workspace scope
only; a different current generation may read retained historical evidence but cannot
dispatch the original command. Validate the newly captured current generation before
and after the read. A different profile/scope, mismatched operation content, failed read
or missing receipt is explicit. Historical lookup does not invoke catalog mutation,
cache refresh, a provider, a current Agent read or approval continuation.

The existing internal file-store constructor accepts an optional durable writer for
failure injection. Production still constructs the same physical writer; tests inject
its existing committed-stage observer and lose acknowledgement after the actual atomic
replacement. No cross-assembly access to infrastructure internals is added.

Web exposes bounded POST lookup with the same strict original submission wire contract
and existing Agent read permission. Operation IDs alone do not authorize disclosure;
current authenticated read permission and original workspace/route binding remain
required. Return a distinct historical-accepted result, never the current policy. Keep
exceptions/configuration out of the response. PUT remains a one-attempt CAS write.

## Native continuation

The follow-on host persists the original submission before dispatch and retains unknown
submissions across navigation/restart. It restores the original submitted values and
offers receipt lookup without resending. A late acknowledgement cannot delete another
operation's retained ticket or replace later draft edits. Historical acceptance requires
explicit current-state comparison before a new edit. Unresolved retention/expiry remains
visible; a current read cannot clear it. Use a bounded Agent-owned browser contract and
the existing UI component seams, not a dependency on Projects' pending-operation types.

## Proof and rollout

First prove the actual writer commits policy and receipt together, lost durable-write
acknowledgement followed by a fresh store/owner lookup, later edits/deletion with exact
historical results, different generation versus different profile/scope, changed command,
expiration/capacity, corrupt records and generic-writer preservation. Include actual
HTTP permission/bounds/sanitization and existing policy/publication regressions. Native
closure additionally requires retained-before-dispatch failure, navigation/restart,
two tabs, later typing and real browser recovery without a second PUT.

Mixed old/new file writers are not a supported rollout: older builds do not know the
reserved receipt log. Deploy under the catalog owner's normal maintenance exclusion,
then run only updated writers. Dataset restore/import epoch qualification remains the
separate P06 gate; historical lookup never authorizes redispatch against restored data.

CodeAnalytics baseline `snap-20261019144656-d75641cd`: Models/Core/Persistence/Agent module,
2,375 types, 19,419 members, 62 registrations, one entity, six existing cycles, 755
findings and 215 diagnostics. Compare the same scope after implementation. Keep SPA on
`codex/concurrent-mutations`; no merge or push is part of this work.

## Backend receipt entry gate

Governed proof is bounded to the receipt protocol and its existing policy writers.
Entry: pass against signed policy owner `890e21eb4`, external target repair `623686202`
and current Process foundation `df9f31f8c`. No Process runtime trust is borrowed.
Expected discovery: 16 Unit cases under `FullyQualifiedName~AgentDecisionDataPolicy`
(metadata, owner, receipt codec/publication) and 24 Integration cases under
`FullyQualifiedName~AgentDecisionDataPolicyOwnerIntegrationTests|FullyQualifiedName~AgentDecisionPolicyHttpTests`.
The HTTP real-owner case is the dependent producer check. Invalidation keys are the
policy metadata, receipt codec/log, catalog preparation/preservation, current-context
lookup and HTTP request/outcome contract. Legacy absence is explicitly round-tripped.
No unfiltered gate is selected: the optional new field and reserved writer are bounded
by this perimeter; full release/merge validation remains separately open. Native ticket
lifetime changes will require their own editor/host/browser proof before activation.

## Native retained submission design

Backend prerequisite is signed `a2dd219b0`, with 40 passing cases. This increment owns
the existing native Agent policy dialog and its browser adapter; SPA remains untouched.
Keep the renderer in AgentFramework.UI, move nontrivial orchestration from the dialog
to one module-owned editor session, and define the browser-storage port beside the
typed editor contracts. Web implements the port with IndexedDB. No Projects pending
types, generic mutation coordinator, new project reference or production partial.

Persist one original submission per native caller/profile/scope/Agent. Caller binding
comes from the current interactive authenticated identity, with its stable issuer/subject
hashed before storage; profile generation is retained inside the original command but
is not a browser address component, so restart can recover it. Recheck the caller around
browser awaits. The browser record never supplies backend permission. Use the existing
versioned Web submission wire contract, preserving decimal revision strings; JavaScript
stores the bounded payload as opaque text. C# strictly decodes and verifies every key,
operation, stage and outcome before exposing it to the editor.

Use strict-durability IndexedDB transactions, with at most 128 records, an 8 KiB command
and 4 KiB terminal result per record. No eviction or timer cleanup of unresolved work.
The finite record/byte bounds avoid a scan or serialization of all records on each save.
Same-key prepare is atomic across tabs. Stages are Prepared, Dispatching, Accepted and
Rejected. Prepare stores the original command; a separate compare-and-transition to
Dispatching grants exactly one live dispatch. A lost preparation/dispatch acknowledgement
cannot authorize another write. Prepared records can be explicitly discarded atomically
only while they remain Prepared. Dispatching records require read-only server lookup.

Persist a known terminal outcome before clearing it. Both operations compare the complete
original command, so a late acknowledgement cannot clear another tab's newer operation.
A failure to record/clear acceptance preserves the known result and blocks another save
until recovery; a rejected result cannot disappear into an unresolved placeholder.
Browser storage failure before dispatch means no owner write. Never fall back to memory
or silently start an unretained request. A crashed circuit may leave Dispatching even
when no request arrived; NotObserved remains unresolved, as the server contract requires.

Restore on the first interactive render, before enabling Save. Fresh editor restoration
loads the submitted values; existing draft edits survive all later results and reads.
Navigation may cancel reads, but never interprets cancellation as rejection of an
already dispatched command. Profile changes retire the old editor. Historical acceptance
clears uncertainty only for the original operation, retains its exact result and requires
an explicit current read plus Use current/Keep draft before a new submission. It does
not replace later typed text or perform a second PUT.

Composition: retain the existing wide, dense BaseLib Dialog and six-field editor as
the primary surface. Put pending/history feedback and its action in the existing Alert
area, use shared Button/Cluster, keep footer actions visible and let the dialog body
own overflow. No metrics, new nested overlay, custom CSS or small-screen redesign.
Components MCP libraries, recommendation, Alert/Button contracts, real usages and proof
routes were read through the existing installed server; queries and its clean exit are
retained under `.artifacts/concurrent-mutations/ui-planning-components/agent-policy-recovery.*`.

Entry proof scope: existing UI policy state and native dialog tests, focused browser
adapter/protocol tests, the existing native conflict journey, and a new real-browser
retention/recovery journey including two tabs, navigation/reload and later edits.
Expected discovery will be frozen after the concrete test cases are written and before
execution. Reuse the isolated PostgreSQL server and standard Release outputs. A browser
store script alone cannot close this increment: prove the native producer retains before
dispatch and invokes only historical lookup after restoration. Production-host restart
with the same browser origin/data needs explicit evidence before claiming that variant.

The native candidate selects 12 UI-state cases, 12 dialog/session cases, 11 Web adapter
and strict-codec cases, and four browser cases (the existing native conflict and sandbox
journeys, the native two-tab/reload recovery journey, and IndexedDB protocol/bounds).
Expected total: 39, with discovery required before execution. The native fault is loss
of terminal browser persistence after the actual owner accepts; reloading then requires
the original server receipt, while a later writer's current state remains unchanged.
The backend's separately frozen fault proof covers acknowledgement loss at atomic file
replacement. The candidate does not claim production-host restart or recovery entry
after the Agent has disappeared from the native catalog; the lookup API covers deletion,
but native orphaned-ticket discovery remains open. Neither limitation permits resending.

The bounded native increment now passes its [closure gate](../validation/2026-10-19-agent-policy-native-recovery.md).
All 39 selected behaviors have passing evidence, with exact unchanged-production
bridges over two repairs to the new browser test. Keep the two native lifecycle/entry
variants above and wider Process/Workflow work open.
