# P04 saved-effect runtime adapter

Prepared before adapter edits on 2026-10-19. This continues the existing D1/C4 contract;
schema-five saved-effect support is not schema-six planning-policy activation.
The result-codec prerequisite has passing compatibility proof. Scoped architecture
snapshot `snap-20261019031558-d550e953` is the source baseline for the next extraction.

## Ownership and changes

Core already owns exact displayed revisions, interactive admission, persisted approvals,
one selected original claim, receipt preflight, current read access and terminal result
checkpointing. Add one validated read projection for a bound saved lease and its exact
binding. The returned run is data, not authority: every claim and checkpoint continues
to validate the live owner-issued lease. Do not expose lease construction or accept a
caller-supplied run/governance snapshot.

MAF gets one internal saved-effect dispatcher. It selects exactly one registered runtime
tool provider and receipt owner, reads the current Agent under the existing catalog lease,
restores the exact original arguments/source preparation, and invokes one AIFunction
under the original claim. It does not create/restore an SDK session, ask the provider to
generate another call, fabricate provider output or change the saved operation identity.
Module tool providers remain responsible for capability and current owner authorization.

Extract the existing factual invocation-policy mapping from `MafRuntimeAgentFactory`
into one internal context factory consumed by both normal calls and saved dispatch.
Preserve redaction, path arguments, original governance, audit scope, script inspection,
external targets and module policy contributors. A saved claim uses the existing
application approval path only after Core has verified the exact original approval;
auto-approval stays false. Generic policy denial must still block before function entry.
This is a local cohesive extraction, not a new policy framework, interface or project.
The factual mapper accepts a typed script-inspection delegate, so pure mapping can be
tested without filesystem services; both runtime paths pass the existing real inspector.
This first adapter accepts interactive module providers only. Configured workspace tools
remain refused because saved schema-five data does not retain their complete original
capability-composition context. Module restoration and admission must enforce the saved
capability/operation ceiling and current owner grants; the real task-plan provider must
prove these checks before product activation. No reconstructed default context grants
workspace, browser, process or MCP access.

Composition supplies existing typed catalog, tool-provider and receipt-provider ports.
The continuation adapter delegates saved execution to the dispatcher; the history wrapper
forwards it without synthesizing model-history evidence. Missing composition remains an
explicit unsupported/refused path. Normal execution and provider continuation retain
their current behavior. No module implementation is referenced by MAF/Core.

## Ordering, failure and performance

1. Require this journal's bound saved lease and exact session/binding; read current run.
   A rejected selection can checkpoint without tool creation or effect dispatch.
2. Select unambiguous provider and receipt owner. Read current Agent with a short catalog
   lease, release it before invocation and require the matching storage scope. Restore
   original payload exactly; mismatched provider/tool/source/arguments refuse execution.
3. Observe the original owner receipt before claiming. An already-present receipt for an
   undispatched proposal refuses dispatch without inventing missing claim history.
4. Claim once, bind audit and tool ownership, evaluate the shared policy and current
   module admission, then enter the function at most once. Policy or authorization denial
   before entry is known not committed. A late exception/cancellation is uncertain unless
   the existing typed owner-failure contract proves otherwise. Do not turn an entered
   or committed effect into a fresh denial.
5. Release publication/authorization resources before journal persistence to avoid recursive
   selected-key locking. Use the extracted historical result codec and Core completion;
   preserve uncertainty on failed acknowledgement. Return only terminal binding/revisions,
   not a tool result that bypasses current disclosure. Receipt reads remain separately
   authorized. Do not hold a catalog/workspace lock around provider or owner work.

No network call, global cache/lock or new persistence store is introduced. The dispatcher
uses bounded saved payloads and linear selection over the small registered-provider set.
Root services are disposed once after use. Logging identifies the run/intent and safe
outcome, never original arguments, source payloads or credentials.

## Proof before dependent activation

Exercise the actual file journal and MAF dispatcher through a restored deterministic
tool boundary: one effect/claim, rejected selection, wrong/missing lease/binding,
ambiguous or missing provider/receipt owner, altered restoration, generic policy denial,
current owner denial, entered exception/cancellation, committed acknowledgement and
checkpoint/release failure. Assert origin bytes, approval, effect knowledge and no SDK
continuation/provider call. Test the extracted policy mapper independently of the old
factory and retain normal invocation/host-failure coverage. Validate standard Release
builds, discovered focused tests, CodeAnalytics dependencies and final portability gate.

Product activation still requires the real project task-plan provider, schema-six decision
owner, actual model basis capture, settings and native/API/SPA actions; these tests must
not be described as those end-to-end journeys.
