# Registered inherited task-planning participant

Status: registered participant and receipt recovery validated; 172 distinct behaviors pass
across the documented input boundaries. This connects the Workbench participant and saved tool. Process producer,
manager continuation and Workflow activation remain separate required integration.

## Ownership and dependency evidence

`ProjectTaskPlanAgentParticipant` currently owns interactive capture and shared prompt,
response preparation and description. `ProjectTaskPlanAgentToolProvider` also constructs
interactive authorization and receipt access. Reusing those constructors for Process
would mix two authority models and grow the shared SDK adapter.

Scoped CodeAnalytics `snap-20261020145253-49194cb4` identifies the exact three owners,
388 Workbench documents, 1,149 types, 10,033 members and 106 registrations. Its 400
findings and 207 diagnostics include existing analyzer/MSBuild/package advisory errors;
it is orientation, not build proof. A one-project scope omits external reference edges.
The actual csproj and the prior five-project graph confirm existing dependencies on
AgentFramework Core/Tooling and Process Application/Runtime, with no new project needed.
Existing module/type cycles are not introduced or repaired by this change.

Keep product interpretation in Workbench and canonical Process reads at existing
Application ports. No reference to the Processes product module, Persistence or Agent
product module is added. Core and MAF remain unaware of the Workbench contract. No
runtime partial, nested business handler, service location or general plugin framework.

## Responsibility split

- A pure planning-content helper owns the unchanged prompt/schema bytes, explicit output
  refusal mapping and description. Both participants call it; interactive capture stays
  with its current owner and shrinks.
- `ProjectTaskPlanProcessParticipant` implements the existing inherited participant
  contract. It reads the immutable original child admission and material through the
  existing two Application reader ports, validates their exact parent binding and passes
  those bytes to the existing codec. It never loads repaired current assignment prompts.
- `ProjectTaskPlanProcessReadAuthority` checks original accepted source and decision
  policy, current profile/fingerprint/generation, selected executor and original launcher,
  source read permission and exact Project lifetimes. It acquires both Agents as one
  ordered catalog set. Executor project grants do not substitute for source permission;
  write revocation does not by itself deny an otherwise authorized receipt read.
- Two concrete publication adapters interpret interactive/inherited origins and construct
  their existing authorities. A small selector returns a typed command, caller, exact
  receipt access and factory bound to the saved session/revisions/proposal. A two-owner
  saved-authority lifetime interface exposes publication plus disposal to the SDK adapter.
- The single existing tool/receipt provider keeps argument restoration, one-use admission,
  business publication, effect evidence, disclosure and reconciliation. No second provider
  claims the same tool, since runtime selection requires exactly one.

A larger conditional provider was rejected because it couples SDK restoration to both
authority constructors. A second provider was rejected because it makes tool ownership
ambiguous. A new project or generic resolver interface is unnecessary for this local
two-origin seam. The small lifetime contract has two real implementations.

## Authority, concurrency and performance

Original read queries compare source evidence, owner fingerprint, root, project reference
and original parent policy. Missing evidence denies; malformed evidence and I/O errors
stay explicit. Profile identity is checked before work and after all awaited checks.
Current read holds one complete selected-Agent lease across Project lifetime checks,
without nested reacquisition. Source SQL occurs before that lease. No full launch decode,
current task recapture, global cache or new background worker is introduced.

Saved publication retains the complete catalog/run snapshot, active work interval and
enlisted Process root/generation fence already proven by the publication authority.
Current read does not grant dispatch. The current Process authority uses Dispatch mode;
manager review cannot borrow it after the dispatch claim is released. The generic
Process executor guard stays closed until a real producer and explicit ManagerReview
authority/atomic outcome path are implemented. A distinct Agent manager will require
one complete three-Agent snapshot, not nested two-Agent leases.

## Test and progression contract

Unit tests exercise prompt/refusal/description and immutable original child/material
interpretation through fake Application ports, with no old runtime construction.
Negatives include missing/mismatched child, material substitution, unsupported contract
and malformed provider output. Existing interactive behavior characterizes unchanged
prompt and publication semantics.

Integration uses the existing isolated SQL fixture and actual MAF wire adapter, registered
participant, saved tool and receipt provider. Bind the real runtime contract before new
admission. Prove exact original material, one provider request, real publication/receipt,
source and executor revocation, profile change, stale target, invalid origin, rollback
and lost acknowledgement without provider replay. Manually prepared Process admission
is supporting adapter proof; it does not claim an executable Process producer.

Build affected production/test projects serially in Release, discover the exact focused
selection, run bounded groups, enforce full portability without baseline writes and
compare scoped architecture. Retain failures and verify source/runtime identities before
repair. Reuse the existing isolated PostgreSQL server and reconcile owned resources.
The old participant/provider must shrink or lose concrete authority dependencies.

Next: real selected Process producer, allocation/material lifecycle, atomic child outcome
and budget settlement, manager authorization and restart/lost-ack handling. Then qualify
Workflow and UI/settings through their actual producers before lifting their guards.

## Recovery correction discovered by the registered path

The actual lost-acknowledgement case exposes an older Core schema whitelist in
`AcquireInterruptedReconciliationAsync`: schema 7 is omitted although the existing
receipt-only machinery already validates inherited identities and current source reads.
Admit schema 7 at that entry, retaining the same exclusive run lease, pinned original
session, current profile/source checks, receipt-only lease mode and exact checkpoint CAS.
This does not grant planning work, parent dispatch authority, refreshed material or a new
budget. Unknown work remains charged/pending with its original allocation. No new partial,
project reference, public contract or product knowledge enters Core.

Ten focused filesystem integration cases cover unchanged decision/work/origin, refusal
of dispatch under a recovery lease, caller/source revocation before and during lookup,
changed original profile/source identity and exclusion of a concurrent recovery lease.
Five existing effects-only cases and the interactive lost-ack case cover older schemas.
The registered Process case proves the real owner receipt after dispatch claim expiry.
The separate expiry-after-flush case must expect current-parent access denial at the
acknowledgement checkpoint and inspect the retained explicit no-effect refusal; it must
not weaken parent authority just to return a convenient result.

Repeat 124 selected current cases on fresh Release outputs. Reuse the preceding 48
authority/history passes only across the explicit single-method invalidation boundary:
those tests do not enter interrupted reconciliation, and their owners/fixtures are
unchanged. No full-suite trigger is introduced by this local whitelist correction.

Closure: [validation and architecture gate](../validation/2026-10-20-inherited-task-participant.md). No production Process producer or manager activation is claimed.
