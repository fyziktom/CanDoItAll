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
